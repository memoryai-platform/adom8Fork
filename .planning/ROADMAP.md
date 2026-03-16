# Roadmap: Self-Healing Dataverse Monitor

## Overview

This milestone adds automated Dataverse plugin error monitoring to the existing ADOm8 Azure Functions app. The system polls PluginTraceLog on a timer, deduplicates and triages errors through a code-first funnel, optionally classifies novel errors with AI, and creates Bug work items that enter the existing ADOm8 coding pipeline. The entire feature is delivered as a feature-flagged addition with no new Azure resources. Phases execute sequentially (1 -> 2 -> 3), with Phase 4 parallelizable after Phase 1 completes.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Infrastructure Foundation** - DataverseClient, ErrorTracking table, feature flag scaffolding, watermark, error normalization (completed 2026-03-15)
- [ ] **Phase 2: Triage Orchestrator** - 5-layer triage funnel, AI classification with caching, deduplication, Bug creation via existing ADO client
- [ ] **Phase 3: Timer Trigger and Lifecycle Detection** - PluginTraceLogMonitor Azure Function, ADO Bug state provisioning, resolved and regression detection
- [ ] **Phase 4: Management Endpoints** - HTTP endpoints for suppression, unsuppression, tracked error listing, and health check extension

## Phase Details

### Phase 1: Infrastructure Foundation

**Goal**: The plumbing exists - the system can authenticate to Dataverse, query PluginTraceLog, normalize error messages, and persist state to Table Storage, all behind a feature flag that defaults to off.

**Depends on**: Nothing (first phase)

**Requirements**: CONN-01, CONN-02, CONN-03, CONN-04, DETECT-01, DETECT-02, DETECT-03, DETECT-06

**Success Criteria** (what must be TRUE):
1. When Dataverse config keys are absent, the feature is completely dormant - no connections attempted, no errors thrown, existing functionality unchanged
2. When Dataverse config keys are present, the system can authenticate to Dataverse using OAuth2 client credentials and return PluginTraceLog entries filtered to entries with non-null `exceptiondetails`
3. The system can normalize an error message (stripping GUIDs, timestamps, entity IDs) and produce a consistent SHA-256 fingerprint for the same logical error across multiple occurrences
4. The system can read and write ErrorTracking records to Azure Table Storage, including creating new entries and updating occurrence counts with optimistic concurrency
5. The scan watermark persists across function restarts - after any restart, the next scan begins from the last recorded timestamp, not from a fixed lookback window

**Plans**: 3 complete

Plans:
- [x] 01-01: DataverseClient - MSAL singleton, named HttpClient registration, OData query with pagination and throttle-safe retry
- [x] 01-02: ErrorTracking Table Storage service - CRUD operations, watermark persistence, optimistic concurrency
- [x] 01-03: Feature flag scaffolding - DataverseOptions, Program.cs registration, NoOp implementations, error normalization utilities

---

### Phase 2: Triage Orchestrator

**Goal**: The full triage cycle operates correctly - new errors pass through all five layers in the correct order, only novel errors reach AI classification, AI results are permanently cached, and qualifying errors produce Bug work items with full context in Azure DevOps.

**Depends on**: Phase 1

**Requirements**: TRIAGE-01, TRIAGE-02, TRIAGE-03, TRIAGE-04, TRIAGE-05, DETECT-04, DETECT-05, DETECT-07, DETECT-08, DETECT-09, BUG-01, BUG-02

**Success Criteria** (what must be TRUE):
1. An error that already has an open Bug work item in ADO does not produce a second Bug - the dedup check stops processing before any AI call is made
2. A known noise pattern (transient timeout, Dataverse throttle error) is rejected by the rule-based filter layer without any AI call, and is permanently recorded as NOISE in the ErrorTracking table
3. A novel, actionable error that meets the per-plugin occurrence threshold triggers exactly one AI classification call, and subsequent occurrences of the same error hash use the cached classification - no second AI call
4. A Bug work item created by the system contains: plugin typename, message name, entity type, full exception details, stack trace, occurrence count, first seen timestamp, last seen timestamp, and the AI-generated root cause hypothesis
5. Cascade errors (PluginTraceLog entries with depth > 0) are skipped - only root-cause entries (depth = 0) progress through the triage funnel
6. Existing callers of `CreateWorkItemAsync` continue to compile and behave identically - the Bug type parameter defaults to User Story

**Plans**: 3 planned

Plans:
- [ ] 02-01: ErrorTriageService skeleton - 5-layer funnel structure, rule-based noise filter, per-plugin threshold config, cascade depth filter
- [ ] 02-02: AI classification integration - prompt design, CRITICAL/BUG/MONITOR/NOISE output parsing, permanent result caching in ErrorTracking
- [ ] 02-03: Bug creation wiring - `CreateWorkItemAsync` parameterization (backward-compatible), Bug context assembly, ADO client integration, dedup write-before-dispatch ordering

---

### Phase 3: Timer Trigger and Lifecycle Detection

**Goal**: The system runs autonomously on a configurable schedule, automatically transitions errors through their lifecycle (Active -> Resolved -> Regression), and creates appropriately linked Bug work items for regressions.

**Depends on**: Phase 2

**Requirements**: BUG-03, BUG-04, BUG-05, MGMT-05

**Success Criteria** (what must be TRUE):
1. The `PluginTraceLogMonitor` timer function fires on the configured cron schedule (default: every 15 minutes), invokes the triage cycle, and exits cleanly - a misconfigured or disabled feature flag causes an early exit with no error
2. Bug work items created by the system are provisioned with the same AI pipeline states as User Stories (AI Agent, Code Review, etc.) and are created with state `AI Agent` to automatically trigger the planning pipeline
3. An error that had a `BugCreated` status and has not appeared in the last 3 consecutive scan windows is automatically marked `Resolved` in the ErrorTracking table
4. When a `Resolved` error reappears, the system creates a new Bug work item that references the original Bug's ADO work item ID in its description

**Plans**: TBD

Plans:
- [ ] 03-01: PluginTraceLogMonitor Azure Function - timer trigger wiring, feature-flag early exit, configurable cron, `UseMonitor=true` guard
- [ ] 03-02: ADO Bug state provisioning and pipeline trigger - verify/provision AI pipeline states on Bug work item type, confirm `AI Agent` state triggers planning pipeline
- [ ] 03-03: Resolved and regression detection - scan window tracking, resolved transition logic, regression detection with linked Bug creation

---

### Phase 4: Management Endpoints

**Goal**: Operators can suppress known-acceptable errors, remove suppressions, and inspect the current state of all tracked errors - and the existing health check reports Dataverse monitor status.

**Depends on**: Phase 1 (no dependency on Phase 3 - parallelizable)

**Requirements**: MGMT-01, MGMT-02, MGMT-03, MGMT-04

**Success Criteria** (what must be TRUE):
1. `POST /api/suppress-error` with a valid error signature sets that error's status to `Suppressed` in ErrorTracking for the configured number of days - the next timer cycle skips that error without creating a Bug
2. `POST /api/unsuppress-error` removes a suppression and restores the error to its previous status - it is eligible for Bug creation on the next timer cycle if thresholds are met
3. `GET /api/tracked-errors` returns all tracked errors with current status (`Active`, `Suppressed`, `Resolved`, `BugCreated`), occurrence counts, classification, and work item reference where applicable
4. When Dataverse monitoring is disabled (feature flag off), all three HTTP endpoints return 404 - not 500
5. The existing `/api/health` endpoint (or equivalent) includes a Dataverse monitor section showing: enabled status, last successful scan timestamp, and count of currently tracked errors

**Plans**: TBD

Plans:
- [ ] 04-01: ErrorSuppressionEndpoint - POST suppress, POST unsuppress, GET tracked-errors HTTP function implementation
- [ ] 04-02: Health check extension - add Dataverse monitor status section to existing health check response

## Progress

**Execution Order:**
Phases execute in numeric order. Phase 4 can begin after Phase 1 completes (no dependency on Phase 3).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Infrastructure Foundation | 3/3 | Complete | 2026-03-15 |
| 2. Triage Orchestrator | 0/3 | Planned | - |
| 3. Timer Trigger and Lifecycle Detection | 0/3 | Not started | - |
| 4. Management Endpoints | 0/2 | Not started | - |
