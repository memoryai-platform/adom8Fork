# Architecture Patterns: Dataverse Plugin Trace Log Monitoring

**Domain:** Dataverse error monitoring integrated into an existing Azure Functions AI agent pipeline
**Researched:** 2026-03-15
**Confidence:** HIGH — based on direct codebase inspection of the existing AIAgents.Functions app

---

## Recommended Architecture

The feature adds five new components to the existing app. None require new Azure resources. All
follow patterns already established in the codebase and slot into the existing DI container in
`Program.cs` with a feature-flag gate identical to the Copilot integration.

```
┌────────────────────────────────────────────────────────────────────────┐
│  AIAgents.Functions (existing Azure Functions app)                     │
│                                                                        │
│  NEW TRIGGER                                                           │
│  ┌─────────────────────────────────┐                                  │
│  │ PluginTraceLogMonitor           │  TimerTrigger (cron, configurable)│
│  │ (AIAgents.Functions/Functions/) │                                  │
│  └────────────┬────────────────────┘                                  │
│               │ calls                                                  │
│               ▼                                                        │
│  NEW ORCHESTRATOR SERVICE                                              │
│  ┌─────────────────────────────────┐                                  │
│  │ ErrorTriageService              │  IErrorTriageService              │
│  │ (AIAgents.Functions/Services/)  │                                  │
│  └──┬──────────┬────────────┬──────┘                                  │
│     │          │            │                                          │
│     │ reads    │ reads/     │ calls                                    │
│     ▼          │ writes     ▼                                          │
│  NEW CLIENT    │     EXISTING SERVICE                                  │
│  ┌─────────────────┐  │  ┌──────────────────────────────┐             │
│  │ DataverseClient │  │  │ IAIClient (existing)         │             │
│  │ (Core/Services/)│  │  │ AI classification            │             │
│  └────────┬────────┘  │  └──────────────────────────────┘             │
│           │ OData      │                                               │
│           │ HTTP       ▼                                               │
│           │    NEW TABLE STORAGE SERVICE                               │
│           │    ┌─────────────────────────────────┐                    │
│           │    │ ErrorTrackingService             │  IErrorTrackingService│
│           │    │ (AIAgents.Functions/Services/)   │                    │
│           │    │ Table: PluginTraceErrors         │                    │
│           │    └──────────────────────────────────┘                   │
│           │                                                            │
│           ▼                                                            │
│    ┌──────────────────────┐                                            │
│    │ Dataverse Web API    │  external                                  │
│    │ (MSAL OAuth2 creds)  │                                            │
│    └──────────────────────┘                                            │
│                                                                        │
│  ON BUG CREATION: calls existing pipeline                             │
│  ErrorTriageService ──► IAzureDevOpsClient.CreateWorkItemAsync("Bug") │
│                    ──► IAgentTaskQueue.EnqueueAsync(Planning agent)   │
│                                                                        │
│  NEW HTTP ENDPOINTS                                                    │
│  ┌───────────────────────────────────────┐                            │
│  │ ErrorSuppressionEndpoint              │  HTTP POST/DELETE/GET      │
│  │ (AIAgents.Functions/Functions/)       │                            │
│  └──────────────────────────┬────────────┘                            │
│                             │ calls                                    │
│                             ▼                                          │
│                    IErrorTrackingService                               │
└────────────────────────────────────────────────────────────────────────┘
```

---

## Component Boundaries

| Component | Project | Responsibility | Communicates With |
|-----------|---------|----------------|-------------------|
| `DataverseOptions` | `AIAgents.Core/Configuration/` | Holds Enabled flag, OrgUrl, ClientId, ClientSecret, TenantId, cron schedule, per-plugin thresholds | Read by `DataverseClient` and `PluginTraceLogMonitor` |
| `DataverseClient` | `AIAgents.Core/Services/` | MSAL OAuth2 client credentials token acquisition; Dataverse OData HTTP calls; PluginTraceLog entity queries with `$filter` by `createdon`; returns `PluginTraceLogEntry[]` | `IHttpClientFactory` (named "Dataverse"), MSAL `IConfidentialClientApplication`, Dataverse Web API |
| `IDataverseClient` | `AIAgents.Core/Interfaces/` | Contract for Dataverse queries | Consumed by `ErrorTriageService` |
| `ErrorTrackingService` | `AIAgents.Functions/Services/` | Table Storage CRUD for `PluginTraceErrors` table; watermark reads/writes; occurrence counting; suppression flag; resolved/regression state transitions | `Azure.Data.Tables.TableClient` (same `AzureWebJobsStorage` connection) |
| `IErrorTrackingService` | `AIAgents.Functions/Services/` | Contract for error state persistence | Consumed by `ErrorTriageService`, `ErrorSuppressionEndpoint` |
| `ErrorTriageService` | `AIAgents.Functions/Services/` | Orchestrates the 5-layer triage funnel (see Data Flow below); dedup; rule-based filter; AI classification; Bug creation; queue enqueue | `IDataverseClient`, `IErrorTrackingService`, `IAIClient`, `IAzureDevOpsClient`, `IAgentTaskQueue`, `IActivityLogger` |
| `IErrorTriageService` | `AIAgents.Functions/Services/` | Contract for triage orchestration | Consumed by `PluginTraceLogMonitor` |
| `PluginTraceLogMonitor` | `AIAgents.Functions/Functions/` | Timer-triggered Azure Function; feature-flag early exit; invokes `IErrorTriageService.RunAsync()`; logs result to `IActivityLogger` | `IErrorTriageService`, `IActivityLogger`, `DataverseOptions` |
| `ErrorSuppressionEndpoint` | `AIAgents.Functions/Functions/` | HTTP POST/DELETE for suppress/unsuppress; HTTP GET to list tracked errors | `IErrorTrackingService` |

**Placement rationale:** `DataverseClient` and `DataverseOptions` go in `AIAgents.Core` (reusable, framework-neutral, testable). All four other services go in `AIAgents.Functions` — they depend on Azure Functions–specific infrastructure (Table Storage via `AzureWebJobsStorage`, queue dispatch, activity logger) that already lives there.

---

## Data Flow

### Full Triage Cycle (per timer invocation)

```
1. TIMER FIRES
   PluginTraceLogMonitor.Run()
   └─ Check DataverseOptions.Enabled → if false, return immediately (no-op)

2. QUERY DATAVERSE
   ErrorTriageService.RunAsync()
   └─ ErrorTrackingService.GetWatermarkAsync()        → last processed createdon timestamp
   └─ DataverseClient.QueryPluginTraceLogsAsync(since: watermark)
      → GET {orgUrl}/api/data/v9.2/plugintracelog
        ?$filter=createdon gt {watermark}
        &$select=plugintracelogid,typename,messageblock,exceptiondetails,createdon
        &$orderby=createdon asc
      → Returns: PluginTraceLogEntry[]

3. DEDUPLICATION (per entry)
   ErrorTrackingService.GetByNormalizedHashAsync(hash)
   └─ Normalize error message (strip GUIDs, timestamps, numeric IDs, stack frames)
   └─ SHA-256 hash of (typename + normalizedMessage)
   └─ Lookup in PluginTraceErrors table by PartitionKey=typename, RowKey=hash

4. RULE-BASED FILTER (code-first, no AI cost)
   For each unseen error:
   └─ Check per-plugin occurrence threshold (DataverseOptions.Thresholds[typename])
   └─ Check suppression flag (ErrorTrackingService.IsSuppressedAsync)
   └─ Check known-noise patterns (configurable regex list)
   → Skip if below threshold, suppressed, or matches noise pattern

5. AI CLASSIFICATION (last resort — only for errors passing rule filter)
   ErrorTrackingService.GetCachedClassificationAsync(hash)
   └─ If cached: use cached CRITICAL/BUG/MONITOR/NOISE result (no AI call)
   └─ If uncached:
      IAIClient.CompleteAsync(systemPrompt, classificationPrompt)
      → Returns: "CRITICAL" | "BUG" | "MONITOR" | "NOISE"
      ErrorTrackingService.CacheClassificationAsync(hash, classification)

6. BUG CREATION (only for CRITICAL or BUG classifications)
   IAzureDevOpsClient.CreateWorkItemAsync(
       title:       "[Plugin Error] {typename}: {errorSummary}",
       description: formatted markdown with stack trace, occurrence count, first/last seen,
       state:       "Story Planning",          ← triggers Planning agent via existing service hook
       workItemType: "Bug"                     ← requires parameterization of existing method
   )
   IAgentTaskQueue.EnqueueAsync(new AgentTask {
       WorkItemId = bugId,
       AgentType  = AgentType.Planning,
       TriggerSource = "PluginTraceLogMonitor"
   })
   IActivityLogger.LogAsync("DataverseMonitor", 0, $"Created Bug WI-{bugId} for {typename}")

7. STATE PERSISTENCE
   ErrorTrackingService.UpsertErrorRecordAsync(...)
   └─ Stores: typename, normalizedHash, classification, firstSeen, lastSeen,
              occurrenceCount, bugWorkItemId, status (Active/Resolved/Suppressed)
   ErrorTrackingService.SetWatermarkAsync(latestCreatedon)
   └─ Persists scan watermark so next run picks up from here

8. RESOLVED DETECTION (end of each cycle)
   ErrorTrackingService.GetActiveErrorsAsync()
   └─ For each active error not seen this cycle: increment missedScanCount
   └─ If missedScanCount >= DataverseOptions.ResolvedAfterMissedScans (default: 3):
      mark as Resolved in Table Storage
      IActivityLogger.LogAsync("DataverseMonitor", bugId, "Error appears resolved")

9. REGRESSION DETECTION
   On each new error lookup:
   └─ If found with status = Resolved: mark as Regressed, create new Bug, link to original
```

### Bug → Agent Pipeline Flow

```
Bug WI created (state: "Story Planning")
        │
        ▼
ADO Service Hook fires to existing OrchestratorWebhook
        │
        ▼  (OR: direct enqueue from ErrorTriageService skips webhook round-trip)
AgentTaskQueue.EnqueueAsync(Planning)
        │
        ▼
AgentTaskDispatcher (existing queue trigger)
        │
        ▼
PlanningAgentService → CodingAgentService → TestingAgentService → ReviewAgentService
        │                                                              │
        └─────────────────── Stops here ──────────────────────────────┘
                             Human approves PR in Code Review
```

**Note on Bug pipeline stop:** The existing autonomy level mechanism already handles this. Bug work items created by ErrorTriageService should default to AutonomyLevel 3 (or lower), which runs through Review. The pipeline stops at Review — `DocumentationAgentService` and `DeploymentAgentService` will be skipped until a human updates the Bug state to trigger deployment. No new logic required.

---

## How New Components Integrate with Existing Services

### Feature Flag (matches Copilot pattern exactly)

```csharp
// In Program.cs — same pattern as CopilotOptions
services.Configure<DataverseOptions>(configuration.GetSection(DataverseOptions.SectionName));

// Conditional HTTP client registration
var dataverseOpts = configuration.GetSection(DataverseOptions.SectionName).Get<DataverseOptions>();
if (dataverseOpts?.Enabled == true)
{
    services.AddHttpClient("Dataverse", client =>
    {
        client.BaseAddress = new Uri(dataverseOpts.OrgUrl + "/api/data/v9.2/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        client.DefaultRequestHeaders.Add("OData-Version", "4.0");
    });
    services.AddSingleton<IDataverseClient, DataverseClient>();
    services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();
    services.AddSingleton<IErrorTriageService, ErrorTriageService>();
}
else
{
    services.AddSingleton<IDataverseClient, NoOpDataverseClient>();
    services.AddSingleton<IErrorTrackingService, NoOpErrorTrackingService>();
    services.AddSingleton<IErrorTriageService, NoOpErrorTriageService>();
}
```

The `PluginTraceLogMonitor` timer function performs a secondary `DataverseOptions.Enabled` early exit check (same as `CopilotTimeoutChecker` checks `CopilotOptions.Enabled`) so the function body is effectively dormant with zero overhead when the feature is off.

### Table Storage (matches TableStorageActivityLogger pattern)

`ErrorTrackingService` uses the same `AzureWebJobsStorage` connection string and `Azure.Data.Tables.TableClient` pattern already established in `TableStorageActivityLogger`. Two tables are needed:

| Table | PartitionKey | RowKey | Purpose |
|-------|-------------|--------|---------|
| `PluginTraceErrors` | `typename` (plugin class name) | `SHA256(typename+normalizedMsg)` | One row per unique error signature |
| `PluginTraceErrors` | `_meta` | `watermark` | Scan watermark (last processed `createdon`) |

Using the same table for both data and meta (with a reserved `_meta` partition) follows the existing `AgentActivity` table pattern where meta rows use `MetaPartitionKey = "meta"`.

### IAzureDevOpsClient.CreateWorkItemAsync (requires one change)

The current signature hardcodes `"User Story"` as the work item type:

```csharp
// Current — line 239 in AzureDevOpsClient.cs
var workItem = await client.CreateWorkItemAsync(patchDocument, _options.Project, "User Story", ...);
```

Required change: add an optional `workItemType` parameter defaulting to `"User Story"` to preserve backward compatibility with all existing callers:

```csharp
Task<int> CreateWorkItemAsync(
    string title,
    string description,
    string state,
    string workItemType = "User Story",   // new optional parameter
    CancellationToken cancellationToken = default);
```

This is the only change to existing code required by this feature. All existing callers continue to work without modification.

### MSAL Token Acquisition

`DataverseClient` acquires tokens using `Microsoft.Identity.Client.ConfidentialClientApplicationBuilder`:

```csharp
var app = ConfidentialClientApplicationBuilder
    .Create(options.ClientId)
    .WithClientSecret(options.ClientSecret)
    .WithAuthority($"https://login.microsoftonline.com/{options.TenantId}")
    .Build();

var result = await app.AcquireTokenForClient(
    new[] { $"{options.OrgUrl}/.default" }
).ExecuteAsync(cancellationToken);
```

Token is cached by MSAL in-memory automatically. No additional caching layer needed. The `DataverseClient` is registered as `Singleton` so the MSAL app instance (and its token cache) persist across function invocations.

### IAgentTaskQueue (no change)

`ErrorTriageService` enqueues Planning tasks using the existing `IAgentTaskQueue.EnqueueAsync(AgentTask)` method. No interface or implementation changes required.

### IActivityLogger (no change)

`ErrorTriageService` and `PluginTraceLogMonitor` log to the existing activity feed via `IActivityLogger.LogAsync()`. Uses agent name `"DataverseMonitor"` and `workItemId = 0` for monitor-level events, or the actual Bug work item ID once created.

---

## Suggested Build Order

Dependencies drive the order. Each phase can be built and tested independently.

### Phase 1: Infrastructure Foundation

Build first because everything else depends on it.

1. `DataverseOptions` (Configuration class, no dependencies)
2. `IDataverseClient` + `DataverseClient` (Core — standalone HTTP client, verifiable with a Postman/test call)
3. `IErrorTrackingService` + `ErrorTrackingService` (Table Storage, no external dependencies beyond storage)
4. `IAzureDevOpsClient.CreateWorkItemAsync` parameterization (one-line change, backward compatible)

**Validation gate:** Unit test `DataverseClient` against a real Dataverse sandbox. Manually verify Table Storage reads/writes in `ErrorTrackingService`. Confirm existing callers compile unchanged after `CreateWorkItemAsync` signature update.

### Phase 2: Triage Orchestrator

Build after Phase 1 completes.

5. `IErrorTriageService` + `ErrorTriageService` (depends on all Phase 1 components + existing `IAIClient`, `IAzureDevOpsClient`, `IAgentTaskQueue`, `IActivityLogger`)
6. Wire feature-flag conditional registration in `Program.cs`

**Validation gate:** Integration test with real Dataverse — verify full triage cycle creates Bug work items and enqueues Planning tasks. Verify AI classification is called only when cache misses.

### Phase 3: Timer Trigger

Build after Phase 2 completes.

7. `PluginTraceLogMonitor` (TimerTrigger Azure Function — depends on `IErrorTriageService`)

**Validation gate:** Deploy to staging. Trigger manually via Azure portal. Confirm end-to-end flow.

### Phase 4: HTTP Endpoints

Can be built in parallel with Phase 3 (no dependency on timer function).

8. `ErrorSuppressionEndpoint` (HTTP function — depends on `IErrorTrackingService` only)

**Validation gate:** HTTP requests to suppress/unsuppress errors. Verify next timer cycle respects suppression flags.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Direct Table Storage Access in Timer Function

**What:** `PluginTraceLogMonitor` directly instantiates `TableClient` and queries storage.
**Why bad:** Bypasses `IErrorTrackingService` abstraction; untestable; duplicates connection management.
**Instead:** All Table Storage access goes through `IErrorTrackingService`. The timer function only calls `IErrorTriageService.RunAsync()`.

### Anti-Pattern 2: Token Acquisition per Request

**What:** Acquiring a new MSAL token on every Dataverse API call.
**Why bad:** Unnecessary latency (AAD round-trip ~200ms); potential rate-limit exposure.
**Instead:** Register `DataverseClient` as `Singleton`. MSAL's in-memory token cache handles refresh automatically. The MSAL `ConfidentialClientApplication` is instantiated once in the constructor.

### Anti-Pattern 3: AI Classification on Every Error on Every Scan

**What:** Calling `IAIClient.CompleteAsync()` for every new log entry every timer cycle.
**Why bad:** Expensive. A busy plugin generating 1000 errors/hour would cost $X per cycle.
**Instead:** `ErrorTrackingService` caches classification permanently per error hash. Once classified, the result is never re-classified. AI is only called for the first occurrence of a unique error signature.

### Anti-Pattern 4: Hardcoded Scan Window Lookback

**What:** Always querying `createdon > (now - 1 hour)`.
**Why bad:** If the timer function is down for 2 hours, those errors are missed permanently. If the function restarts, the first run duplicates the last hour.
**Instead:** Watermark pattern. Last processed `createdon` is persisted in Table Storage. Each run queries `createdon > watermark`. Survives restarts with no gaps or duplicates.

### Anti-Pattern 5: Creating Duplicate Bug Work Items

**What:** Checking for duplicates only by plugin name, not error signature.
**Why bad:** Same error generates a new Bug on every scan cycle (hundreds of duplicate tickets).
**Instead:** Dedup by `SHA-256(typename + normalizedMessage)`. Normalization strips GUIDs, timestamps, and numeric IDs so the same logical error produces the same hash across occurrences. One hash = one Bug, forever. Regressions create a new Bug with a link to the original.

### Anti-Pattern 6: New Function App for This Feature

**What:** Deploying a separate Azure Functions app to host the Dataverse monitor.
**Why bad:** New Azure resources (App Service Plan or Consumption plan, storage account, App Insights workspace); separate deployment pipeline; no shared DI container with existing services.
**Instead:** Feature-flag in existing Function App following the Copilot/SaaS/MCP precedent. Zero new Azure resources. Controlled by config key `Dataverse:Enabled = true`.

---

## Scalability Considerations

| Concern | At current scale (~100 errors/day) | At high volume (~10K errors/day) |
|---------|-------------------------------------|----------------------------------|
| Dataverse query latency | Single OData query per timer cycle, <1s | Add `$top=500` paging; multiple requests per cycle |
| Table Storage writes | One upsert per unique error hash (low volume) | Batch upserts using `TableTransactionAction`; partition per plugin |
| AI classification | Rare (cache hit rate approaches 100% after initial seeding) | Consider AI classification queue as separate concern |
| Bug creation | Max one Bug per unique error per regression | ADO work item creation is not a bottleneck |
| Timer frequency | 5–15 minute interval appropriate | Reduce interval if SLA requires faster detection |

---

## Configuration Reference

`DataverseOptions` (bound to `"Dataverse"` section, all new):

```json
{
  "Dataverse": {
    "Enabled": false,
    "OrgUrl": "https://yourorg.crm.dynamics.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-app-registration-client-id",
    "ClientSecret": "your-client-secret",
    "CronSchedule": "0 */5 * * * *",
    "ResolvedAfterMissedScans": 3,
    "DefaultOccurrenceThreshold": 5,
    "Thresholds": {
      "YourPlugin.ClassName": 10,
      "NoisyPlugin.ClassName": 50
    }
  }
}
```

Feature is dormant until `Dataverse:Enabled = true`. No other config changes are required in the existing app.

---

## Sources

- Direct codebase inspection: `AIAgents.Functions/Program.cs` (DI registration patterns)
- Direct codebase inspection: `AIAgents.Functions/Services/TableStorageActivityLogger.cs` (Table Storage pattern)
- Direct codebase inspection: `AIAgents.Functions/Functions/AgentTaskDispatcher.cs` (keyed DI dispatch, queue pattern)
- Direct codebase inspection: `AIAgents.Functions/Functions/CopilotTimeoutChecker.cs` (timer trigger + feature-flag early exit pattern)
- Direct codebase inspection: `AIAgents.Functions/Functions/DeadLetterQueueHandler.cs` (timer trigger pattern)
- Direct codebase inspection: `AIAgents.Core/Configuration/CopilotOptions.cs` (feature-flag Options pattern)
- Direct codebase inspection: `AIAgents.Core/Configuration/SaasOptions.cs` (optional integration Options pattern)
- Direct codebase inspection: `AIAgents.Core/Services/AzureDevOpsClient.cs` (CreateWorkItemAsync signature)
- Direct codebase inspection: `.planning/codebase/ARCHITECTURE.md` (existing architecture documentation)
- Direct codebase inspection: `.planning/PROJECT.md` (validated requirements and decisions)

---

*Architecture research: 2026-03-15*
