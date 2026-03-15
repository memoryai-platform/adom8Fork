# Feature Landscape: Dataverse Plugin Error Monitoring & Self-Healing

**Domain:** Production error monitoring with automated remediation (Dataverse plugin trace logs → AI triage → Bug work items → agent pipeline)
**Researched:** 2026-03-15
**Milestone context:** Subsequent — adding to existing ADOm8 AI agent pipeline

---

## Source Notes

- PluginTraceLog schema: HIGH confidence — from official Microsoft Dataverse documentation (updated 2025-10-31)
- Azure DevOps Bug work item fields: HIGH confidence — from official Microsoft ADO documentation (updated 2026-02-28)
- Error monitoring feature patterns: MEDIUM confidence — synthesized from Azure Monitor patterns (official docs) and knowledge of established monitoring systems (Sentry, Datadog, PagerDuty)
- AI-powered triage patterns: MEDIUM confidence — synthesized from training knowledge of production monitoring systems; no single definitive official spec exists
- Self-healing system patterns: MEDIUM confidence — training knowledge of incident automation platforms

---

## PluginTraceLog Data Available (Source of Truth)

These are the actual fields the monitor can read from Dataverse. This constrains every detection feature.

| Column | Type | Monitoring Value |
|--------|------|-----------------|
| `typename` | string(1024) | Plugin class name — primary grouping key for deduplication |
| `messagename` | string(1024) | Triggering CRM message (Create, Update, Delete, etc.) — secondary grouping key |
| `primaryentity` | string(1000) | Entity the plugin ran against — tertiary grouping key |
| `exceptiondetails` | memo | Full exception text — fingerprinting, AI classification |
| `messageblock` | memo (10KB max, truncated) | Plugin trace output — context for AI triage |
| `createdon` | datetime | When the error occurred — windowed aggregation, trend detection |
| `mode` | picklist | Synchronous (0) or Asynchronous (1) — severity hint (sync = user-blocking) |
| `operationtype` | picklist | Plugin (1) or Workflow Activity (2) — routing |
| `correlationid` | uniqueidentifier | Tracks one plugin execution — correlation across related errors |
| `depth` | int | Execution depth — cascade/loop detection |
| `performanceexecutionduration` | int (ms) | Execution time — performance anomaly detection |
| `pluginstepid` | uniqueidentifier | Registration step ID — links back to plugin registration |
| `requestid` | uniqueidentifier | CRM request ID — cross-system correlation |
| `issystemcreated` | bool | System trace vs. developer trace — filter noise |

**Key constraint:** `messageblock` is capped at 10KB with oldest lines dropped. Long-running plugins may have truncated traces. Exception details are in a separate `exceptiondetails` field — always check both.

**Key constraint:** PluginTraceLog records are deleted after 24 hours by a background job. The monitoring timer must run frequently enough (every 5–15 minutes) to catch errors before deletion.

---

## Table Stakes

Features every production monitoring system has. Missing = monitoring system feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Polling timer with configurable interval** | Monitoring is useless if it doesn't run regularly | Low | Azure Functions timer trigger; must run faster than 24h TTL of PluginTraceLog records. Recommend 5–15 min. |
| **Error extraction from PluginTraceLog** | Core data source — must query `exceptiondetails` IS NOT NULL | Low | OData query on Dataverse Web API; filter `issystemcreated eq false` to skip system noise |
| **Error deduplication (don't re-create the same bug twice)** | Without this, the same recurring error creates hundreds of Bug work items | Medium | Fingerprint = `typename` + `messagename` + exception type/first line. Store in Azure Table Storage `ErrorTracking` table. Check before creating Bug. |
| **Deduplication window / TTL reset on recurrence** | A suppressed error that reappears should re-open or increment | Medium | Update `LastSeen`, `OccurrenceCount` in `ErrorTracking`. Re-open if Bug was closed and error recurs. |
| **Per-plugin configurable error threshold** | A plugin that always throws 1 error/day should not create a Bug; one that throws 50 in 5 minutes should | Medium | Config per `typename` — min occurrences in window before Bug creation. Defaults: 3 occurrences in 15 min. |
| **Bug work item creation in Azure DevOps** | The whole point of the pipeline integration | Medium | Use existing `IAzureDevOpsClient.CreateWorkItemAsync`. Set Title, Steps to Reproduce, System Info, Severity, Priority. |
| **Bug body contains actionable error context** | Developers need to understand what went wrong without digging into Dataverse | Low | Include: typename, messagename, entity, exception, sample trace snippet, occurrence count, first/last seen, environment. |
| **Manual suppression endpoint** | Some errors are known/acceptable; must be silenceable | Low | HTTP trigger `POST /suppress` with `typename` (optionally `messagename`). Writes suppression record to `ErrorTracking`. |
| **Basic health check / observability** | Operators need to know if the monitor itself is working | Low | Extend existing `HealthCheck` function or add `/health/monitor` endpoint. Log each timer run. |
| **Idempotent processing** | Timer may double-fire or overlap; must not double-create Bugs | Medium | Lock via `ErrorTracking` table row with ETag-based optimistic concurrency. |

---

## Differentiators

What separates this from a basic alert → ticket system. These are the "self-healing" features.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Code-first triage funnel** | Eliminates clearly non-actionable errors cheaply before spending AI tokens | Medium | Rule-based pre-filter before AI: filter `depth > 1` (cascades), filter known transient system errors, filter suppressed plugins. Only survivors go to AI. |
| **AI error classification** | Determines severity and provides fix hints without human triage | High | Prompt with: typename, messagename, exception type + message, entity, mode (sync/async), occurrence frequency. Output: severity (P1/P2/P3), error category (data/config/code/external), 2–3 sentence fix hypothesis. |
| **Automatic ADO Bug creation with pipeline trigger** | Closes the loop from detection to remediation — work items automatically enter the AI coding pipeline | Medium | After Bug creation, set state to "AI Agent" (or equivalent pipeline trigger state). Bug title includes typename and exception type. Uses existing `AgentType.Planning` entry point. |
| **Aggregation before Bug creation** | Groups multiple occurrences in the observation window into one Bug, not one-per-occurrence | Medium | Aggregate over the timer window: count occurrences, collect earliest/latest, sample 1–3 representative trace snippets. One Bug represents N occurrences. |
| **Synchronous vs. async severity elevation** | Sync plugins are user-blocking — same exception in sync context is higher severity than async | Low | `mode = 0` (Synchronous) → bump severity by one level. Include in Bug body and AI classification prompt. |
| **Cascade / plugin-chain detection** | Plugin errors triggered by other plugin errors should not create separate Bugs | Medium | Filter `depth > 1` or correlate by `correlationid`. Only root-cause errors (depth=0 or lowest depth in correlation group) create Bugs. |
| **Occurrence frequency trending** | A plugin throwing 1 error/hour for 3 days is different from one that just spiked | Low | Store `OccurrenceCount` and rolling window data in `ErrorTracking`. Include trend in Bug body. |
| **Existing-open-bug guard** | If a Bug for this error already exists and is open, don't create a duplicate | Medium | Query ADO for open Bugs with matching title prefix or tag. Skip creation; update occurrence count as comment instead. |
| **Re-open closed Bug on recurrence** | A "fixed" error that reappears should reactivate the Bug, not silently be ignored | Medium | If `ErrorTracking` shows a closed/resolved Bug ID, and error reappears after N hours: add comment to existing Bug, optionally reactivate it. |
| **ErrorTracking table as audit trail** | Complete history of what was detected, deduplicated, suppressed, and acted on | Low | Every detection event written to `ErrorTracking` in Azure Table Storage. Foundation for future reporting. |

---

## Feature Dependencies

```
Azure Timer Trigger
    │
    ├── PluginTraceLog query (Dataverse Web API)
    │       └── requires: Dataverse service account, trace logging enabled on environment
    │
    ├── Aggregation (group by typename + messagename + exception type)
    │       └── requires: PluginTraceLog query
    │
    ├── Code-first triage filter
    │       └── requires: Aggregation
    │       └── includes: depth filter, suppression lookup
    │
    ├── ErrorTracking lookup (Azure Table Storage)
    │       └── requires: Code-first triage filter
    │       └── gates: deduplication, threshold check, open-bug guard
    │
    ├── AI classification (optional — runs on survivors only)
    │       └── requires: ErrorTracking lookup (confirmed new/re-occurring)
    │       └── requires: existing IAIClient / CopilotCompletionService
    │
    ├── Bug work item creation (IAzureDevOpsClient)
    │       └── requires: ErrorTracking lookup, AI classification (for severity)
    │       └── must write BugWorkItemId back to ErrorTracking
    │
    └── Pipeline trigger (set Bug state to trigger ADOm8 pipeline)
            └── requires: Bug work item creation
            └── requires: existing OrchestratorWebhook / AgentTaskDispatcher
```

**Critical path for MVP:**
Timer → PluginTraceLog query → Aggregate → Dedup check (ErrorTracking) → Threshold check → Bug creation → ErrorTracking update

AI classification and pipeline trigger are the differentiators and can be layered on after the core loop works.

---

## MVP Recommendation

For MVP, prioritize (in implementation order):

1. **Timer function with Dataverse query** — polling and extraction
2. **Fingerprint + ErrorTracking deduplication** — prevents noise storm
3. **Per-plugin threshold configuration** — prevents false-positive Bugs
4. **Bug work item creation** — delivers the core value proposition
5. **Manual suppression endpoint** — safety valve for known errors

Defer to post-MVP:

- **AI classification** — useful but adds cost and latency per detection cycle; rule-based severity heuristics (sync = high, frequency = escalating) are sufficient for v1
- **Re-open closed Bug on recurrence** — complex state management; add comment to closed Bug instead for v1
- **Cascade / plugin-chain detection** — depth filter alone (filter `depth > 1`) handles the common case; full correlation-group analysis is post-MVP
- **Occurrence frequency trending** — store the data now; visualize it later
- **Pipeline trigger** — wire up after Bug creation is stable; the ADOm8 pipeline already exists, this is a 1-line state transition

---

## Anti-Features

Things to deliberately NOT build for v1. Common mistakes in monitoring system implementations.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Real-time streaming / webhook-based detection** | Dataverse does not provide a reliable real-time exception webhook. Plugin execution events don't push to external systems. | Timer polling at 5–15 min is the correct pattern for PluginTraceLog. Accept the detection latency. |
| **One Bug per occurrence** | Creates ticket flooding — 100 errors in an hour = 100 Bugs. Teams immediately disable the integration. | Aggregate occurrences in the timer window. One Bug represents N occurrences. Occurrence count in Bug body. |
| **Alerting / notification system (email, Teams, PagerDuty)** | ADOm8 is a work item creation and coding pipeline, not a NOC alerting tool. Adding notification infrastructure bloats scope. | Bug creation in ADO is the notification. Teams get alerted through their normal ADO board workflows. |
| **Dataverse Plugin Registration management** | Tempting to auto-disable flapping plugins — requires elevated Dataverse permissions and is extremely risky in production. | Stay read-only on Dataverse. Monitor, detect, create Bugs. Let humans/pipeline fix the code. |
| **Trend dashboards and reporting UI** | Out of scope for v1 — requires a separate frontend investment. | Write structured data to `ErrorTracking` now. Dashboard can be built in a future milestone. |
| **Multi-environment fan-out** | Monitoring multiple Dataverse environments in v1 multiplies configuration complexity. | Design the config model to support multiple environments (per-environment connection strings), but implement for one environment in v1. |
| **Auto-closing Bugs when errors stop** | Requires polling ADO work item state and correlating with absence of errors — complex and error-prone. | Let developers close Bugs manually after verifying the fix. The pipeline already handles state transitions once coding is done. |
| **Configurable alert thresholds via UI** | Building a configuration UI is a milestone unto itself. | Configuration via `local.settings.json` / app settings per environment. Document the config format. |

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Dataverse query | PluginTraceLog records are deleted after 24h. Timer interval longer than this will miss errors. | Set timer interval to 5–15 minutes. Track `LastPolledAt` in `ErrorTracking` meta row to query only new records. |
| Deduplication fingerprint | Exception message text varies with data (e.g., "Entity 12345 not found" vs "Entity 67890 not found"). Full-message fingerprinting creates unique errors for every entity ID. | Fingerprint on exception *type* + first *line* + `typename` + `messagename`. Strip GUIDs and integers from the fingerprint. |
| Bug creation idempotency | Timer double-fires under Azure Functions consumption plan. Two concurrent timer invocations both pass the dedup check before either writes the Bug ID back. | Use Azure Table Storage optimistic concurrency (ETag) or a distributed mutex when writing new `ErrorTracking` records. |
| AI classification cost | Every error in every timer window gets AI classification — high token cost for noisy environments. | AI runs only on errors that pass the code-first triage funnel AND are confirmed new (not deduplicated). Cap AI calls per timer run (e.g., max 10). |
| Plugin trace log disabled | If Dataverse trace logging is set to "Off", `PluginTraceLog` will have zero exception records. The monitor will appear to work but detect nothing. | Health check should verify that at least one exception record exists within the last 24h. Alert (log warning) if the table is empty and logging may be disabled. |
| Existing work item type mismatch | ADOm8 currently creates User Stories. Bugs use a different work item type with different required fields (Repro Steps, System Info, Severity). | Use `CreateWorkItemAsync` with `type: "Bug"` parameter. Map exception details to "Steps to Reproduce" field. |
| Depth-1 cascade noise | Plugin A triggers Plugin B which throws. Both create separate `PluginTraceLog` entries. Without depth filtering, two Bugs are created for one root cause. | Filter `depth > 0` in the aggregation phase. Only the root execution (depth=0) generates a Bug. Verify this assumption against real log data early. |

---

## Sources

- PluginTraceLog schema: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/plugintracelog (updated 2025-10-31) — HIGH confidence
- Trace logging overview: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/logging-tracing — HIGH confidence
- Azure DevOps Bug work item fields: https://learn.microsoft.com/en-us/azure/devops/boards/backlogs/manage-bugs (updated 2026-02-28) — HIGH confidence
- Azure Monitor alert patterns: https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-overview — HIGH confidence (for stateful/stateless patterns, dedup concepts)
- Error monitoring feature patterns (Sentry, Datadog, PagerDuty): MEDIUM confidence — synthesized from training knowledge; no single official source
- ADOm8 codebase analysis: HIGH confidence — read directly from source (IAzureDevOpsClient, AgentTask, ErrorCategory, AgentTaskDispatcher, TableStorageActivityLogger)
