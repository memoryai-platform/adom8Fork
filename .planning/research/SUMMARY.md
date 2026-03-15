# Project Research Summary

**Project:** ADOm8 — Dataverse Plugin Trace Log Monitor & Self-Healing Bug Detection
**Domain:** Production error monitoring with automated remediation integrated into an existing Azure Functions AI agent pipeline
**Researched:** 2026-03-15
**Confidence:** HIGH

## Executive Summary

This milestone adds Dataverse plugin trace log monitoring to the existing ADOm8 Azure Functions app. The system polls Dataverse's `plugintracelog` entity on a timer, aggregates and deduplicates plugin exceptions, applies a code-first triage funnel, optionally classifies novel errors with AI, and creates Bug work items in Azure DevOps that automatically enter the existing ADOm8 coding pipeline. The entire feature is delivered as a feature-flagged addition to the existing Function App — no new Azure resources are required. The recommended implementation uses raw `HttpClient` + MSAL for Dataverse communication (not the `Microsoft.PowerPlatform.Dataverse.Client` SDK, which has known .NET 8 isolated worker incompatibilities and pull in conflicting dependencies).

The architecture is dominated by one key design decision: a 5-layer triage funnel where code-based filtering and deduplication gate access to AI classification. This is not optional — skipping or reordering the layers causes runaway AI costs (up to $43+/day in busy environments). Every new component follows an established pattern already in the codebase: the timer trigger follows `CopilotTimeoutChecker`, the Table Storage service follows `TableStorageActivityLogger`, the named `HttpClient` registration follows the `AIClient`/`GitHub`/`SaaS` pattern, and the feature flag follows `CopilotOptions`. The only change to existing code is adding an optional `workItemType` parameter to `IAzureDevOpsClient.CreateWorkItemAsync`.

The top risks are: AI cost spiral from a poorly ordered triage funnel (Pitfall 4), duplicate Bug work items from watermark/crash interaction (Pitfall 2), Dataverse API throttling breaking entire monitoring windows (Pitfall 3), and false-positive Bugs from transient infrastructure errors (Pitfall 5). All four have clear, well-understood prevention strategies that must be built into Phase 1 and Phase 2 — they cannot be retrofitted later without rework.

---

## Key Findings

### Recommended Stack

The feature adds exactly one new NuGet package: `Microsoft.Identity.Client` (MSAL, 4.61.x line) added to `AIAgents.Core.csproj`. Everything else uses infrastructure already present in the solution. Dataverse communication uses a named `HttpClient("Dataverse")` registered in `Program.cs` alongside the existing `AIClient`, `GitHub`, and `SaasCallback` clients. `System.Text.Json` (already present) handles OData response deserialization. The `Microsoft.PowerPlatform.Dataverse.Client` SDK is explicitly rejected — it was designed for in-process .NET Framework/Core 3.1 and carries ~15 transitive dependencies including ADAL shims that conflict with the .NET 8 isolated worker model.

**Core technologies:**
- `Microsoft.Identity.Client` (MSAL 4.61.x): OAuth2 client credentials token acquisition for Dataverse — only correct way to authenticate a server-to-server Dataverse connection with explicit token cache control
- Built-in `HttpClient` via `IHttpClientFactory`: Dataverse Web API OData calls — consistent with all other external integrations in the codebase, zero new dependencies
- `System.Text.Json` (existing): Typed DTO deserialization of `plugintracelog` OData responses — already present, no new package needed
- `IConfidentialClientApplication` singleton: MSAL token cache persisted across Azure Functions invocations — must be Singleton to avoid per-invocation AAD round trips

### Expected Features

The full feature breakdown is in `.planning/research/FEATURES.md`. Summary below.

**Must have (table stakes):**
- Timer polling function with configurable interval (5–15 min) — PluginTraceLog records are deleted after 24 hours; polling must be faster than that
- Error extraction from `plugintracelog` via OData `$filter=exceptiondetails ne null`
- Error deduplication using SHA-256 fingerprint of normalized (typename + exception type + first line) — prevents noise storm of duplicate Bugs
- Per-plugin configurable occurrence threshold before Bug creation — prevents false-positive Bugs from single-occurrence transient errors
- Bug work item creation in Azure DevOps via existing `IAzureDevOpsClient`
- Manual suppression endpoint — safety valve for known/acceptable errors
- Idempotent processing via ETag-based optimistic concurrency in Table Storage

**Should have (differentiators):**
- Code-first triage funnel (rule-based filter before AI) — eliminates non-actionable errors cheaply
- AI error classification (severity: CRITICAL/BUG/MONITOR/NOISE, fix hypothesis) — value-add over a basic alert-to-ticket system
- Automatic pipeline trigger on Bug creation (set state to "Story Planning") — closes the detection-to-remediation loop
- Cascade/plugin-chain detection via `depth` field filter — prevents duplicate root-cause Bugs
- Re-open closed Bug on regression detection — prevents silently ignoring "fixed" errors that recur
- Resolved detection (mark error resolved after N missed scan windows)

**Defer (v2+):**
- Trend dashboards and reporting UI — requires separate frontend investment
- Multi-environment fan-out — design config for it, implement for one env in v1
- Auto-closing Bugs when errors stop — complex state management with low payoff
- Configurable alert thresholds via UI — app settings are sufficient for v1
- Real-time streaming/webhooks — Dataverse does not support reliable exception webhooks

### Architecture Approach

The feature adds five new components to the existing `AIAgents.Functions` app, all following established patterns. `DataverseClient` and `DataverseOptions` live in `AIAgents.Core` (reusable, framework-neutral, testable in isolation). The remaining services (`ErrorTrackingService`, `ErrorTriageService`) live in `AIAgents.Functions` alongside the two new Azure Function entry points (`PluginTraceLogMonitor` timer function, `ErrorSuppressionEndpoint` HTTP function). All components are wired behind a `DataverseOptions.Enabled` feature flag in `Program.cs` with NoOp implementations for when the flag is off. The full data flow has nine defined steps from timer fire through watermark persistence. See `.planning/research/ARCHITECTURE.md` for the component diagram and complete data flow.

**Major components:**
1. `DataverseClient` (`AIAgents.Core/Services/`) — MSAL token acquisition and Dataverse OData HTTP queries; returns `PluginTraceLogEntry[]`
2. `ErrorTrackingService` (`AIAgents.Functions/Services/`) — Table Storage CRUD for `PluginTraceErrors` table; watermark reads/writes; occurrence counting; suppression state; resolved/regression transitions
3. `ErrorTriageService` (`AIAgents.Functions/Services/`) — orchestrates the 5-layer triage funnel; dedup; rule-based filter; AI classification; Bug creation via `IAzureDevOpsClient`; queue enqueue via `IAgentTaskQueue`
4. `PluginTraceLogMonitor` (`AIAgents.Functions/Functions/`) — Azure Functions `TimerTrigger`; feature-flag early exit; invokes `IErrorTriageService.RunAsync()`
5. `ErrorSuppressionEndpoint` (`AIAgents.Functions/Functions/`) — HTTP POST/DELETE/GET for suppress/unsuppress/list tracked errors

### Critical Pitfalls

The full list of 13 pitfalls is in `.planning/research/PITFALLS.md`. The five that cause rewrites or production incidents:

1. **MSAL token cache not shared across invocations** — register `IConfidentialClientApplication` as Singleton in `Program.cs`; never instantiate it inside a function body or scoped service
2. **Watermark drift causes duplicate Bug creation** — write dedup record to `ErrorTrackingService` BEFORE dispatching Bug creation; use ETag-based optimistic concurrency on watermark writes; dedup table is the authoritative "already actioned" check, not the watermark alone
3. **Dataverse API throttling breaks entire monitoring window** — configure dedicated `HttpClient("Dataverse")` resilience handler to honor `Retry-After` header from 429 responses; always use `$top=250` pagination; always include `createdon ge {watermark}` in filter; never use `$select=*`
4. **Noisy PluginTraceLog causes runaway AI classification costs** — the 5-layer funnel ordering is mandatory: (1) config gate, (2) rule-based noise filter, (3) dedup lookup, (4) AI classification, (5) threshold gate; normalize error messages (strip GUIDs, entity IDs, timestamps) before hashing to maximize cache hit rate
5. **False positive Bug creation from transient errors** — pre-classify known transient error types (`System.TimeoutException`, `HttpRequestException`, Dataverse throttle codes) in the rule-based layer; enforce occurrence threshold across minimum 2 scan windows before Bug creation

---

## Implications for Roadmap

Based on research, the dependency graph drives a four-phase build order. Phases 1 and 2 are the critical path for MVP. Phases 3 and 4 can be built in parallel.

### Phase 1: Infrastructure Foundation

**Rationale:** All other phases depend on this. The Dataverse client, Table Storage service, and `CreateWorkItemAsync` parameterization must be built and validated first because every subsequent component calls them. Getting the MSAL singleton registration and watermark write order correct here prevents the most severe production pitfalls (Pitfalls 1, 2, 3).

**Delivers:** Working Dataverse query capability, ErrorTracking Table Storage CRUD, feature-flag scaffolding, one backward-compatible change to `IAzureDevOpsClient`

**Addresses features from FEATURES.md:** Timer polling infrastructure, error extraction from PluginTraceLog, Table Storage-backed deduplication state

**Avoids pitfalls:** Pitfall 1 (MSAL singleton), Pitfall 3 (throttle-safe HttpClient), Pitfall 9 (feature flag in service layer), Pitfall 11 (UTC datetime format in OData), Pitfall 12 (typename truncation), Pitfall 13 (UseMonitor=true on TimerTrigger)

**Validation gate:** Unit test `DataverseClient` against real Dataverse sandbox; manually verify Table Storage reads/writes; confirm existing callers compile unchanged

### Phase 2: Triage Orchestrator

**Rationale:** This is the highest-value and highest-risk component. The 5-layer funnel ordering must be correct from the start — reordering layers later requires significant rework and risks AI cost spikes in production. Built after Phase 1 because it depends on all four Phase 1 components plus the existing `IAIClient`, `IAzureDevOpsClient`, and `IAgentTaskQueue`.

**Delivers:** Full triage cycle from Dataverse query through Bug creation and pipeline trigger; AI classification with permanent caching; deduplication; rule-based noise filtering; PII normalization

**Uses from STACK.md:** MSAL singleton (Phase 1), named Dataverse HttpClient (Phase 1), existing `IAIClient`

**Implements:** `ErrorTriageService` + `IErrorTriageService`; feature-flag conditional registration in `Program.cs`

**Avoids pitfalls:** Pitfall 2 (watermark/dedup write order), Pitfall 4 (AI cost spiral via funnel ordering), Pitfall 5 (false positives via threshold + transient error pre-classification), Pitfall 6 (Bug work item type parameterization), Pitfall 7 (TypeName as PartitionKey), Pitfall 8 (PII normalization)

**Validation gate:** Integration test with real Dataverse — verify full triage cycle creates Bug work items and enqueues Planning tasks; verify AI classification called only on cache misses

### Phase 3: Timer Trigger + Resolved/Regression Detection

**Rationale:** The timer function itself is thin (it calls `IErrorTriageService.RunAsync()` and exits), but resolved and regression detection logic depends on a working full triage cycle from Phase 2. Both features require observing multiple scan windows, so they can only be validated after the timer trigger is running.

**Delivers:** Fully operational `PluginTraceLogMonitor` Azure Function; automated resolved detection; regression detection with linked Bug creation

**Avoids pitfalls:** Pitfall 10 (resolved threshold minimum 30-minute quiet period; cooling-off state between Active and Resolved)

**Validation gate:** Deploy to staging; trigger manually via Azure portal; confirm end-to-end flow including resolved detection across multiple timer invocations

### Phase 4: HTTP Endpoints

**Rationale:** Can be built in parallel with Phase 3 — `ErrorSuppressionEndpoint` depends only on `IErrorTrackingService` from Phase 1. No dependency on the timer function.

**Delivers:** `POST /suppress`, `DELETE /suppress`, `GET /errors` HTTP endpoints; operator control over suppressed plugins; audit trail visibility

**Avoids pitfalls:** Pitfall 9 (HTTP endpoints return 404 when `Dataverse:Enabled = false`, not 500)

**Validation gate:** HTTP requests to suppress/unsuppress errors; verify next timer cycle respects suppression flags

### Phase Ordering Rationale

- Phase 1 before all others because `DataverseClient`, `ErrorTrackingService`, and the `CreateWorkItemAsync` change have no upstream dependencies and are required by every other component
- Phase 2 before Phase 3 because `PluginTraceLogMonitor` is a thin shell that calls `IErrorTriageService.RunAsync()` — building the timer before the orchestrator would mean testing with a no-op implementation
- Phase 3 and Phase 4 can run in parallel because they have no dependency on each other — if team has capacity, build simultaneously
- AI classification (differentiator) is wired into Phase 2 rather than deferred because the permanent AI result caching (per error hash) must be built at the same time as the dedup table — retrofitting caching into a working dedup system is more disruptive than building it together

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2:** Triage funnel is the most complex component with the highest cost/quality risk; AI prompt engineering for the classification prompt (severity levels, error categories, fix hypothesis format) warrants a dedicated planning session; also requires deciding on transient error type pre-classification list
- **Phase 3:** Resolved/regression detection thresholds need calibration against actual Dataverse maintenance patterns for the target org; recommend capturing 1-2 days of real PluginTraceLog data before finalizing thresholds

Phases with standard patterns (skip research-phase):
- **Phase 1:** All patterns are directly verified against existing codebase; MSAL singleton registration, named HttpClient, `IOptions<T>` config, Table Storage CRUD are all established and documented
- **Phase 4:** `ErrorSuppressionEndpoint` follows the same HTTP trigger pattern as existing endpoints; depends only on `IErrorTrackingService` which is built in Phase 1

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | One new package (MSAL); all other infrastructure verified by direct codebase inspection; only version verification needed before first build |
| Features | HIGH | PluginTraceLog schema from official Microsoft docs (updated 2025-10-31); ADO Bug work item fields from official docs (updated 2026-02-28); feature patterns synthesized from codebase and established monitoring system knowledge |
| Architecture | HIGH | Entirely based on direct codebase inspection of existing patterns; no assumptions about new infrastructure required |
| Pitfalls | HIGH (codebase) / MEDIUM (Dataverse platform) | Codebase-derived pitfalls (MSAL singleton, watermark order, HttpClient isolation) are HIGH confidence; Dataverse-specific platform behavior (throttle limits, OData datetime semantics, typename format) is MEDIUM — verify against official docs during Phase 1 |

**Overall confidence:** HIGH

### Gaps to Address

- **MSAL version:** `Microsoft.Identity.Client` version must be verified on NuGet before pinning — training data indicates `4.61.x` line but patch version should be confirmed
- **Dataverse API throttle limits:** Specific per-App-Registration and per-org throttle thresholds are not in training data; verify during Phase 1 against Microsoft's Service Protection API documentation to set `$top` and retry backoff values correctly
- **`Microsoft.PowerPlatform.Dataverse.Client` .NET 8 status:** If there is any desire to revisit the SDK decision, current NuGet page should be checked — the incompatibility was known at August 2025 training cutoff but may have been resolved
- **AI classification prompt format:** The research recommends a CRITICAL/BUG/MONITOR/NOISE output schema but the exact prompt, few-shot examples, and output parsing are not specified — needs design during Phase 2 planning
- **Bug work item ADO state for pipeline trigger:** Research assumes "Story Planning" as the trigger state; confirm with ADO process template that this state exists on Bug work items (not just User Stories) before wiring the pipeline trigger in Phase 2

---

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection: `AIAgents.Functions/Program.cs`, `AIAgents.Core/`, `AIAgents.Functions/Services/`, `AIAgents.Functions/Functions/` — all architectural patterns, DI registration, existing service interfaces
- PluginTraceLog entity schema: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/plugintracelog (updated 2025-10-31)
- Dataverse trace logging: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/logging-tracing
- Azure DevOps Bug work item fields: https://learn.microsoft.com/en-us/azure/devops/boards/backlogs/manage-bugs (updated 2026-02-28)

### Secondary (MEDIUM confidence)
- MSAL .NET token caching patterns in serverless: training knowledge of `Microsoft.Identity.Client` design, aligned with Microsoft's Dataverse + client credentials guidance
- Dataverse Web API OData throttling behavior and `Retry-After` semantics: training knowledge of Dataverse service protection limits
- Error monitoring system feature patterns (Sentry, Datadog, PagerDuty): synthesized from training knowledge; no single official specification

### Tertiary (LOW confidence)
- `Microsoft.PowerPlatform.Dataverse.Client` .NET 8 isolated worker incompatibility: known community issue at August 2025 training cutoff; should be verified before any decision to revisit the SDK recommendation

---

*Research completed: 2026-03-15*
*Ready for roadmap: yes*
