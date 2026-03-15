# Domain Pitfalls

**Domain:** Dataverse plugin trace log monitoring and self-healing systems
**Researched:** 2026-03-15
**Confidence:** HIGH (codebase inspection) / MEDIUM (Dataverse-specific, verified against known platform behavior)

---

## Critical Pitfalls

Mistakes that cause rewrites, production incidents, or runaway costs.

---

### Pitfall 1: MSAL Token Cache Not Shared Across Azure Functions Invocations

**What goes wrong:** `ConfidentialClientApplicationBuilder.Build()` is called inside the timer function body or in a scoped service. Each invocation creates a new in-memory token cache, acquires a fresh token, and discards the cache. In high-frequency scanning or burst scenarios this hammers the Azure AD token endpoint and can result in throttling (HTTP 429 from login.microsoftonline.com) or hitting the per-App-Registration token rate limit.

**Why it happens:** Developers familiar with web app patterns treat MSAL as a request-scoped object. Azure Functions isolates invocation state but DI singletons persist across invocations. The fix is to register `IConfidentialClientApplication` as a singleton so the built-in in-memory token cache is reused.

**Consequences:**
- Unnecessary AAD round trips on every timer tick
- Token throttling in high-frequency environments
- Each invocation pays ~200-500ms latency for token acquisition instead of cache hit
- If AAD is degraded during a scan window, the function fails even though a valid cached token could have been used

**Warning signs:**
- Application Insights shows `AcquireTokenForClient` duration consistently 200ms+ on every invocation (no cache hits)
- AAD sign-in logs show a new token issued every 5 minutes (matching timer cadence)
- Occasional HTTP 429 responses logged from login.microsoftonline.com

**Prevention:**
- Register `IConfidentialClientApplication` as `Singleton` in `Program.cs`, mirroring how `VssConnection` is registered as `Lazy<VssConnection>` in `AzureDevOpsClient`
- The Dataverse client service should inject the singleton MSAL app, call `AcquireTokenForClient`, and let MSAL's built-in cache return the token silently for its lifetime (default 1 hour)
- Do not use `WithForceRefresh(true)` except during explicit token refresh debugging

**Phase:** Phase 1 (Dataverse client implementation) — must be correct from day one

---

### Pitfall 2: Watermark Drift Causes Duplicate Bug Creation

**What goes wrong:** The scan watermark (last-scanned timestamp stored in Table Storage) is written AFTER bug creation. If the function crashes, is restarted, or the host is recycled between "query Dataverse" and "write watermark," the next run re-queries the same time window and creates duplicate Bug work items for errors that were already processed.

**Why it happens:** Teams write the watermark at the end of a "successful run" without understanding that Azure Functions can be interrupted at any point. The queue-based pipeline downstream has no duplicate-suppression logic for Bug creation.

**Consequences:**
- Multiple identical Bug work items for the same plugin error
- AI agent pipeline triggered multiple times for the same fix, multiplying AI costs
- Confusion in ADO boards, potentially conflicting code changes from parallel pipeline runs

**Warning signs:**
- Multiple Bug work items with identical titles in ADO (same plugin name, same error signature)
- Inverted RowKey pattern in ErrorTracking table showing same ErrorHash with multiple entries
- AI cost spike after a Function App restart or host recycle

**Prevention:**
- Write the watermark to Table Storage BEFORE dispatching bug creation, not after. If a bug then fails to create, the ErrorTracking dedup table (keyed on ErrorHash) prevents re-creation on the next scan cycle
- The ErrorTracking table's dedup record must be the source of truth for "have we already actioned this error" — not the watermark alone
- Use Table Storage ETag-based optimistic concurrency when writing the watermark to detect concurrent writes from overlapping timer invocations (possible if a run takes longer than the timer interval)
- Mirror the existing `TableStorageActivityLogger` pattern: the dedup insert should use `AddEntityAsync` (which fails on duplicate RowKey) rather than `UpsertEntityAsync`

**Phase:** Phase 1 (watermark design) and Phase 2 (dedup implementation)

---

### Pitfall 3: Dataverse API Throttling Breaks the Entire Monitoring Window

**What goes wrong:** The PluginTraceLog query is issued without respecting Dataverse service protection limits. Dataverse enforces per-user (App Registration), per-org, and per-endpoint rate limits. A single large `$select` query that returns thousands of rows, or a burst of queries issued during startup, will receive HTTP 429 with a `Retry-After` header. An unhandled 429 throws an exception, marks the timer run as failed, and the function retries immediately — making the throttling worse.

**Why it happens:** Teams copy the AIClient resilience pattern (Polly `AddStandardResilienceHandler`) but do not configure it correctly for Dataverse's `Retry-After` header semantics. The default Polly retry uses exponential backoff starting at 2 seconds, but Dataverse can return `Retry-After: 60` (seconds). Polly retries at 2s/4s, both fail, and the circuit breaker opens.

**Consequences:**
- Entire monitoring window skipped with no errors processed
- Circuit breaker opens and suppresses monitoring for 30+ seconds
- If the App Registration's 5-minute token is acquired during throttle, it also fails

**Warning signs:**
- HTTP 429 responses logged from `{org}.api.crm.dynamics.com`
- `Retry-After` values of 60+ seconds in response headers
- Application Insights showing "circuit breaker open" for the Dataverse HTTP client
- Consecutive failed timer runs in the Function App monitor

**Prevention:**
- Register a dedicated named `HttpClient("Dataverse")` in `Program.cs` separate from `HttpClient("AIClient")` — do not share circuit breaker state between Dataverse and AI calls
- Configure the Dataverse client's resilience handler to honor `Retry-After`: use `options.Retry.UseJitter = false` and set `options.Retry.DelayGenerator` to read the `Retry-After` response header
- Apply `$top=250` pagination on PluginTraceLog queries — never request unbounded result sets
- The `$filter` must always include `createdon ge {watermark}` to constrain the query window — never scan the full table
- Target the minimum required fields via `$select=plugintracelogid,typename,messageblock,exceptiondetails,createdon` — do not `$select=*`

**Phase:** Phase 1 (Dataverse client) — throttle-safe design is non-negotiable for production

---

### Pitfall 4: Noisy PluginTraceLog Causes Runaway AI Classification Costs

**What goes wrong:** Every PluginTraceLog entry — including verbose INFO traces, expected validation errors, and known-good transient failures — is passed to the AI classifier. A busy Dataverse org can generate thousands of trace entries per scan window. With even a 5-minute timer and a conservative estimate of 100 entries per window, that is 28,800 AI calls per day. At Claude Sonnet pricing (~$0.003/1K input tokens) and a 500-token prompt per error, that is ~$43/day for classification alone before any code changes.

**Why it happens:** The triage funnel is designed code-first but implemented AI-first in practice. The rule-based filter is either too permissive or skipped for "edge cases." Teams defer deduplication to the AI classifier ("let AI decide if it's new") rather than enforcing it in code.

**Consequences:**
- AI costs spiral to hundreds of dollars per day
- Rate limiting from AI provider (Claude/OpenAI) blocks the entire agent pipeline
- Latency per scan window increases as AI calls queue up
- Cache becomes irrelevant when every entry has a unique message (GUIDs not normalized)

**Warning signs:**
- AI cost telemetry (`Cost` column in AgentActivity table) shows spike after enabling the monitor
- IAIClient.CompleteAsync call count in Application Insights exceeds 100 per timer run
- Log entries show AI being called for errors with `typename = null` or `messageblock` containing "successfully"
- ErrorTracking table shows no `AIClassification` cache hits (cache miss ratio near 100%)

**Prevention:**
- The 5-layer funnel is mandatory, not optional. Layer ordering must be enforced by code, not convention:
  1. Config gate (Dataverse:Enabled check — fast path exit if disabled)
  2. Rule-based noise filter (discard entries where `messageblock` matches known-good patterns: "successfully", "Validation", "not found", severity INFO)
  3. Dedup lookup (check ErrorTracking table by ErrorHash — if exists and not regression, skip AI entirely)
  4. AI classification (only for genuinely novel, non-noise entries)
  5. Threshold gate (AI result must be CRITICAL or BUG to proceed; MONITOR and NOISE do not create work items)
- Error message normalization (strip GUIDs, entity IDs, timestamps, correlation IDs) MUST happen before hashing and before AI prompt construction — otherwise every unique ID creates a unique hash defeating the cache
- Cache AI classifications permanently in ErrorTracking table: an error classified as NOISE remains NOISE until a human unsuppresses it via the suppression endpoint

**Phase:** Phase 2 (triage funnel) — the ordering of layers is the single most important implementation decision

---

### Pitfall 5: False Positive Bug Creation from Transient Dataverse Errors

**What goes wrong:** The system creates a Bug work item for errors that are transient (network timeouts, Dataverse maintenance windows, plugin execution timeouts from infrastructure blips) or expected (data validation errors that are user-facing, not code bugs). The AI agent pipeline then attempts to "fix" infrastructure timeouts by modifying plugin code — generating wrong code changes that waste reviewer time.

**Why it happens:** The AI classifier is given the raw error message and exception details but not context about error frequency, time-of-day patterns, or whether the error only appeared during a known Dataverse outage window. A single occurrence of "System.TimeoutException" looks identical to a recurring timeout from a slow plugin regardless of root cause.

**Consequences:**
- Human reviewers lose trust in the system ("another false alarm")
- AI pipeline tokens wasted on unfixable or already-correct code
- Bug work items accumulate without resolution, clogging ADO boards
- If auto-merge were ever enabled (it's not in this design), transient errors could trigger bad code merges

**Warning signs:**
- Bug work items with titles like "Fix System.TimeoutException in DataverseMaintenance window"
- Bug creation spikes correlate with known Dataverse maintenance windows in tenant logs
- Per-plugin occurrence count is 1 for bugs that were auto-created
- Reviewer feedback consistently "not a code issue"

**Prevention:**
- Enforce per-plugin occurrence threshold BEFORE AI classification and bug creation. The threshold is configurable per plugin (PROJECT.md requires this); the default should be 3+ occurrences before a Bug is created
- ErrorCategory mapping: `System.TimeoutException`, `System.Net.Http.HttpRequestException`, and Dataverse error codes 0x80040217 (throttled), 0x80044005 (server busy) must be pre-classified as `Transient` by the rule-based filter — never passed to AI for classification
- The AI classification prompt must include occurrence count and first/last seen timestamps to give the model frequency context
- Occurrence window matters: 3 occurrences in 5 minutes (burst) is different from 3 occurrences over 3 days (recurring). The threshold logic should require minimum time spread (e.g., at least 2 different scan windows)

**Phase:** Phase 2 (triage funnel, threshold logic) and Phase 3 (AI prompt engineering)

---

## Moderate Pitfalls

Mistakes that cause delays, rework, or technical debt.

---

### Pitfall 6: CreateWorkItemAsync Hardcoded to "User Story" Breaks Existing Callers on Parameterization

**What goes wrong:** The current `CreateWorkItemAsync` signature (in `IAzureDevOpsClient`) creates work items as "User Story." Adding a `workItemType` parameter with a default is safe, but any change to the underlying ADO process template patch document (field names differ between Bug and User Story in ADO) can break existing callers if the abstraction leaks type-specific fields.

**Why it happens:** ADO Bug work items use `Microsoft.VSTS.TCM.ReproSteps` for description rather than `System.Description`. Bug items also have `Microsoft.VSTS.Common.Priority` and `Microsoft.VSTS.Common.Severity` fields that don't exist on User Stories. A naive parameterization that only changes the `$type` parameter but uses the same patch document for all types will silently produce malformed Bug work items.

**Warning signs:**
- Bug work items created with empty "Repro Steps" fields even though description was provided
- ADO rejects patch document with 400 Bad Request for Bug type
- Bug items not appearing in board sprints because mandatory fields are missing

**Prevention:**
- Add `string workItemType = "User Story"` as an optional parameter — existing call sites are unaffected (PROJECT.md already identifies this)
- Create a `BuildPatchDocument(string workItemType, ...)` internal method that builds the correct field set per type. Bug type should map description to `Microsoft.VSTS.TCM.ReproSteps`; fall back to `System.Description` for all other types
- Bug work items need their custom AI states provisioned separately from User Story states — `ProvisionAzureDevOps` function must be extended or a separate provisioning path created

**Phase:** Phase 1 (work item type parameterization)

---

### Pitfall 7: Table Storage Partition Key Design Causes Hot Partitions Under Load

**What goes wrong:** All ErrorTracking rows written with the same PartitionKey (e.g., "errors" — mirroring the `AgentActivity` table's single "activity" partition). Table Storage throttles at the partition level. A scan window that writes 50+ new error tracking records simultaneously to one partition hits throughput limits (10,000 operations/second per partition, but in practice much lower for small storage accounts).

**Why it happens:** The `TableStorageActivityLogger` uses a single "activity" partition because it's append-only log with time-ordered RowKey. ErrorTracking has different access patterns: random reads by ErrorHash for dedup, time-range reads for resolved detection. Copying the logger pattern without considering access patterns creates a hot partition.

**Warning signs:**
- `StorageErrorCode: ServerBusy` in Table Storage exceptions during bulk write phases
- Watermark writes failing when ErrorTracking writes are concurrent
- Dedup lookups timing out during large scan windows

**Prevention:**
- Use the plugin's TypeName as the PartitionKey for ErrorTracking rows. This distributes writes across partitions (one per plugin class), aligns with the primary query pattern (dedup lookup by plugin + error hash), and bounds partition size naturally
- RowKey should be the normalized ErrorHash (deterministic, stable across scans)
- Keep the watermark in a separate table (or a dedicated "meta" partition in ErrorTracking, mirroring the `MetaPartitionKey = "meta"` pattern in `TableStorageActivityLogger`)

**Phase:** Phase 2 (ErrorTracking table design)

---

### Pitfall 8: PluginTraceLog `exceptiondetails` Field Contains Sensitive Data

**What goes wrong:** Dataverse plugin exception details frequently contain connection strings, API keys, user email addresses, entity GUIDs linked to PII, or internal infrastructure details that were logged for debugging. These are passed verbatim to the AI classifier prompt and, worse, written into Bug work item descriptions in Azure DevOps — which may have broader team access than the Dataverse environment.

**Why it happens:** The assumption is that plugin trace logs are internal infrastructure data. But plugins often catch and re-throw exceptions that include request payloads, which may contain user data.

**Warning signs:**
- Bug work item descriptions containing email addresses, GUIDs formatted as entity references, or JSON payloads with field names like `emailaddress1` or `telephone1`
- AI provider logs (if accessible) showing PII in prompts
- Security review flags on ADO work items

**Prevention:**
- Normalize `exceptiondetails` before any external use: strip email addresses (regex), GUIDs (replace with `[ID]`), connection string patterns, and any field containing `emailaddress`, `phonenumber`, `mobilephone` as substrings
- The same normalization used for ErrorHash generation should be used for AI prompt construction and work item description — single normalization function, applied once
- Document the normalization approach in code comments so future maintainers understand why it exists

**Phase:** Phase 2 (normalization implementation)

---

### Pitfall 9: Feature Flag Disabled Check Not Propagating to All Entry Points

**What goes wrong:** The `DataverseOptions.Enabled` check is added to the timer function body (following `CopilotTimeoutChecker` pattern) but not to the HTTP endpoints (suppression endpoint, dashboard endpoint). When the feature is disabled, the HTTP endpoints still attempt to read from ErrorTracking table and return 500 errors because the table was never created.

**Why it happens:** The timer function is the obvious entry point for the feature flag check. HTTP endpoints are secondary and get added later, often by a different developer who doesn't see the feature flag pattern in the timer function.

**Warning signs:**
- HTTP 500 from `/api/errors` endpoint when `Dataverse:Enabled` is false
- `RequestFailedException: Table 'ErrorTracking' was not found` in logs
- Support requests from users trying to access dashboard before feature is enabled

**Prevention:**
- The `DataverseOptions.Enabled` check must be in a shared service layer, not duplicated in each function. Return a `FeatureDisabledException` or use the existing pattern of returning early with a 400/404 response when the feature is not configured
- HTTP endpoints should return HTTP 404 with body `{"error": "Dataverse monitoring is not enabled"}` when disabled — same pattern as other optional integrations

**Phase:** Phase 1 (feature flag scaffolding)

---

### Pitfall 10: Regression Detection Window Too Short Misses Recurring Errors

**What goes wrong:** An error is marked "resolved" when it doesn't appear in N consecutive scan windows. The resolved window is set too short (e.g., 2 consecutive 5-minute windows = 10 minutes). Transient Dataverse issues that clear themselves within minutes trigger false "resolved" then false "regression detected" cycles, creating new Bug work items repeatedly for the same underlying problem.

**Why it happens:** The resolved detection threshold is chosen arbitrarily without considering Dataverse's maintenance patterns (which can cause 15-30 minute quiet windows during rolling restarts).

**Warning signs:**
- Same plugin error appearing as new Bug repeatedly (3+ Bugs with identical titles)
- ErrorTracking table shows rapid Resolved/Active/Resolved state cycling
- ADO board cluttered with resolved-then-reopened Bugs for the same component

**Prevention:**
- Resolved threshold should be configurable and default to at minimum 6 consecutive scan windows (30 minutes at 5-minute cadence) before marking resolved
- Regression detection should also require a minimum quiet period (e.g., error absent for 2 hours) to distinguish from normal error variability
- Consider a "cooling off" state between Active and Resolved to prevent rapid cycling

**Phase:** Phase 3 (resolved/regression detection logic)

---

## Minor Pitfalls

Mistakes that cause annoyance but are fixable without rework.

---

### Pitfall 11: OData `$filter` Datetime Format Requires UTC with 'Z' Suffix

**What goes wrong:** The watermark timestamp is stored as a `DateTime` and formatted as `yyyy-MM-ddTHH:mm:ss` without timezone indicator. Dataverse OData API interprets ambiguous datetime values as local time in some versions, returning records outside the intended window or rejecting the filter with 400 Bad Request.

**Prevention:** Always format datetime watermarks as `yyyy-MM-ddTHH:mm:ssZ` (UTC with explicit Z suffix) or use `DateTimeOffset.UtcNow.ToString("o")` which produces ISO 8601 with offset. Store watermarks in Table Storage as UTC strings.

**Phase:** Phase 1

---

### Pitfall 12: PluginTraceLog `typename` Is the Full Assembly-Qualified Name

**What goes wrong:** The `typename` field in PluginTraceLog contains the full assembly-qualified name (e.g., `Company.Plugins.AccountPreValidation, Company.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`). Using this as a display name in Bug titles or Table Storage PartitionKey creates unreadably long strings and PartitionKey values that exceed the 1KB Table Storage limit.

**Prevention:** Extract the simple class name from `typename` using `typename.Split(',')[0].Split('.').Last()` before using it in display contexts or as PartitionKey. Store the full typename separately for diagnostic purposes.

**Phase:** Phase 2

---

### Pitfall 13: Timer Function Overlap When Scan Takes Longer Than Interval

**What goes wrong:** Default Azure Functions timer behavior allows concurrent runs. If a scan window takes longer than the 5-minute timer interval (possible when Dataverse is throttled and the function waits on `Retry-After: 60`), two overlapping instances run simultaneously. Both query the same time window, potentially creating duplicate dedup records and duplicate Bug work items.

**Prevention:** Set `RunOnStartup = false` and configure `%PluginMonitor:UseMonitor%` = true in the `TimerTrigger` attribute to enable singleton execution (only one instance runs at a time). The Azure Functions runtime's blob lease prevents concurrent timer invocations when `UseMonitor = true`.

**Phase:** Phase 1

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Dataverse client / MSAL auth | Token cache not shared (Pitfall 1) | Singleton MSAL registration in DI |
| Dataverse client / HTTP | Throttle handling (Pitfall 3) | Dedicated named HttpClient with Retry-After support |
| Watermark / persistence | Duplicate bugs from crash-restart (Pitfall 2) | Write dedup record before bug creation; watermark before dispatch |
| Triage funnel ordering | AI cost spiral (Pitfall 4) | Strict layer enforcement: rule-based before AI |
| Triage funnel / thresholds | False positive bugs (Pitfall 5) | Occurrence threshold + transient error pre-classification |
| Work item type parameterization | Patch document field mismatch (Pitfall 6) | Type-specific patch builder; provision Bug states |
| ErrorTracking table design | Hot partition (Pitfall 7) | TypeName as PartitionKey; separate watermark meta row |
| Error normalization | PII in AI prompts and ADO (Pitfall 8) | Single normalization function applied before all external use |
| Feature flag scaffolding | HTTP endpoints bypass disabled check (Pitfall 9) | Enabled check in service layer, not just timer function |
| Resolved/regression logic | Rapid cycling creates duplicate Bugs (Pitfall 10) | Configurable resolved threshold, minimum 30-minute quiet period |
| OData query construction | Datetime timezone errors (Pitfall 11) | Always format watermarks as UTC with Z suffix |
| Plugin typename handling | Oversized PartitionKey (Pitfall 12) | Extract simple class name before storage/display use |
| Timer function concurrency | Concurrent runs on slow scans (Pitfall 13) | UseMonitor = true on TimerTrigger |

---

## Sources

- Codebase inspection: `/c/ADO-Agent/ADO-Agent/src/` — existing patterns for timer functions, Table Storage, circuit breakers, DI registration, feature flags (HIGH confidence)
- PROJECT.md (`.planning/PROJECT.md`) — explicitly identified risks: Dataverse API throttling, noisy logs, duplicate bugs, AI cost spiraling, false positives, MSAL token caching in serverless (HIGH confidence)
- Existing `TableStorageActivityLogger` partition/RowKey patterns — inverted tick RowKey, single partition anti-pattern identified by contrast (HIGH confidence)
- Existing `AzureDevOpsClient` — `CreateWorkItemAsync` hardcoded to User Story, patch document structure (HIGH confidence)
- Existing `Program.cs` — `AddStandardResilienceHandler` configuration, named HttpClient registration patterns (HIGH confidence)
- Dataverse Web API platform behavior: OData datetime formatting, PluginTraceLog entity field names, throttling response headers — verified against known platform behavior (MEDIUM confidence; recommend official docs verification during Phase 1 implementation)
- MSAL token caching in serverless/Functions: singleton registration requirement — MEDIUM confidence based on known MSAL design; verify against Microsoft.Identity.Client current documentation during Phase 1
