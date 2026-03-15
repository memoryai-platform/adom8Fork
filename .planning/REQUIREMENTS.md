# Requirements: Self-Healing Dataverse Monitor

**Defined:** 2026-03-15
**Core Value:** Production bugs in Dataverse plugins are automatically detected, triaged, and fixed with zero human intervention until Code Review.

## v1 Requirements

### Dataverse Connectivity

- [x] **CONN-01**: System connects to Dataverse Web API using OAuth2 client credentials (MSAL) with Azure AD App Registration
- [x] **CONN-02**: System queries PluginTraceLog via OData filtering for entries with non-null `exceptiondetails`
- [x] **CONN-03**: System is feature-flagged - disabled by default, enabled when Dataverse config keys are present
- [x] **CONN-04**: Timer interval is configurable via cron expression in app settings (default: every 15 minutes)

### Error Detection

- [x] **DETECT-01**: System extracts error entries from PluginTraceLog (`exceptiondetails`, `typename`, `messagename`, `primaryentity`, `mode`, `depth`, `createdon`)
- [x] **DETECT-02**: System normalizes error messages by stripping GUIDs, timestamps, record IDs, and memory addresses for consistent fingerprinting
- [x] **DETECT-03**: System stores error fingerprints in Azure Table Storage `ErrorTracking` with occurrence counts, classification, status, and work item references
- [ ] **DETECT-04**: System deduplicates errors - does not create a Bug for an error that already has an open Bug work item
- [ ] **DETECT-05**: System supports configurable per-plugin occurrence thresholds (different plugins have different noise levels)
- [x] **DETECT-06**: System persists the scan watermark in Table Storage to track last-scanned timestamps across restarts
- [ ] **DETECT-07**: System filters cascade errors by skipping PluginTraceLog entries with `depth > 0` (only root-cause errors create Bugs)
- [ ] **DETECT-08**: System elevates severity for synchronous plugin errors (`mode=0`) vs asynchronous (`mode=1`) since sync errors are user-blocking
- [ ] **DETECT-09**: System tracks occurrence frequency over time (stores rolling-window data in ErrorTracking for future trend visualization)

### AI Triage

- [ ] **TRIAGE-01**: System applies a code-first triage funnel - config gate, Dataverse query, dedup lookup, rule-based filter - before any AI call
- [ ] **TRIAGE-02**: System sends only novel, unclassified errors (that passed all code-based filters) to AI for classification
- [ ] **TRIAGE-03**: AI classifies errors as CRITICAL, BUG, MONITOR, or NOISE with confidence score (0-100), suggested title, and root cause hypothesis
- [ ] **TRIAGE-04**: AI classification results are permanently cached in ErrorTracking - each unique error hash is sent to AI at most once
- [ ] **TRIAGE-05**: System applies the decision matrix: CRITICAL + 70% confidence -> Bug immediately; BUG + 80% + threshold -> Bug; MONITOR -> track 3 windows; NOISE -> cache permanently

### Bug Creation & Pipeline

- [ ] **BUG-01**: System creates Bug work items in Azure DevOps (parameterize existing `CreateWorkItemAsync` to support Bug type alongside User Story)
- [ ] **BUG-02**: Bug work items include actionable context: typename, messagename, entity, exception details, stack trace, occurrence count, first seen, last seen, and AI hypothesis
- [ ] **BUG-03**: Bug work item states are provisioned in ADO (same AI pipeline states as User Story: AI Agent, Code Review, etc.)
- [ ] **BUG-04**: Bugs are created with state `AI Agent` to automatically trigger the existing agent pipeline (Planning -> Coding -> Testing -> Review)
- [ ] **BUG-05**: System detects regressions - when a resolved error reappears, creates a new Bug referencing the previous work item

### Management

- [ ] **MGMT-01**: HTTP endpoint `POST /api/suppress-error` allows manual suppression of specific error signatures for a configurable number of days
- [ ] **MGMT-02**: HTTP endpoint `POST /api/unsuppress-error` allows removing a suppression
- [ ] **MGMT-03**: HTTP endpoint `GET /api/tracked-errors` returns all tracked errors with current status, counts, and classification
- [ ] **MGMT-04**: System extends the existing health check to report Dataverse monitor status (connected, last scan time, errors tracked)
- [ ] **MGMT-05**: System detects resolved errors - errors with `BugCreated` status that have not appeared in the last 3 scan windows are marked `Resolved`

## v2 Requirements

### Dashboard & Reporting

- **DASH-01**: Dashboard UI for viewing tracked errors, suppression management, and trend visualization
- **DASH-02**: Error frequency trend charts showing occurrence patterns over time

### Multi-Environment

- **ENV-01**: Support monitoring multiple Dataverse environments from a single Function App
- **ENV-02**: Per-environment configuration and threshold management

### Advanced Detection

- **ADV-01**: Correlation-group analysis using `correlationid` for complex cascade detection beyond depth filtering
- **ADV-02**: Auto-reopen closed Bug work items when an associated error recurs (instead of creating a new Bug)
- **ADV-03**: Performance anomaly detection using `performanceexecutionduration`

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real-time streaming / webhooks from Dataverse | Dataverse does not support real-time exception streaming; timer polling is the correct pattern |
| One Bug per error occurrence | Creates ticket flooding - aggregate occurrences into a single Bug |
| Alerting / notifications (email, Teams, PagerDuty) | ADO Bug creation is the notification; teams use their existing board workflows |
| Dataverse plugin registration management | Too risky - stay read-only on Dataverse; monitor and fix code, do not modify plugin registrations |
| Auto-close Bugs when errors stop | Complex state management; let developers close after verifying a fix |
| Configuration UI | Milestone unto itself; app settings are sufficient for v1 |
| Auto-merge without human review | Self-healing must stop at Code Review for safety |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CONN-01 | Phase 1 | Complete |
| CONN-02 | Phase 1 | Complete |
| CONN-03 | Phase 1 | Complete |
| CONN-04 | Phase 1 | Complete |
| DETECT-01 | Phase 1 | Complete |
| DETECT-02 | Phase 1 | Complete |
| DETECT-03 | Phase 1 | Complete |
| DETECT-06 | Phase 1 | Complete |
| DETECT-04 | Phase 2 | Pending |
| DETECT-05 | Phase 2 | Pending |
| DETECT-07 | Phase 2 | Pending |
| DETECT-08 | Phase 2 | Pending |
| DETECT-09 | Phase 2 | Pending |
| TRIAGE-01 | Phase 2 | Pending |
| TRIAGE-02 | Phase 2 | Pending |
| TRIAGE-03 | Phase 2 | Pending |
| TRIAGE-04 | Phase 2 | Pending |
| TRIAGE-05 | Phase 2 | Pending |
| BUG-01 | Phase 2 | Pending |
| BUG-02 | Phase 2 | Pending |
| BUG-03 | Phase 3 | Pending |
| BUG-04 | Phase 3 | Pending |
| BUG-05 | Phase 3 | Pending |
| MGMT-05 | Phase 3 | Pending |
| MGMT-01 | Phase 4 | Pending |
| MGMT-02 | Phase 4 | Pending |
| MGMT-03 | Phase 4 | Pending |
| MGMT-04 | Phase 4 | Pending |

**Coverage:**
- v1 requirements: 28 total
- Mapped to phases: 28
- Unmapped: 0

---
*Requirements defined: 2026-03-15*
*Last updated: 2026-03-15 - Phase 1 requirements completed and traceability updated*
