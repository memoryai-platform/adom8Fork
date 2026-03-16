# Phase 2 Research - Triage Orchestrator

**Gathered:** 2026-03-15
**Phase:** 2 - Triage Orchestrator
**Goal:** Build the code-first triage funnel that deduplicates Dataverse plugin errors, calls AI only for novel actionable failures, caches classification results, and creates Bug work items with complete context.
**Requirements:** TRIAGE-01, TRIAGE-02, TRIAGE-03, TRIAGE-04, TRIAGE-05, DETECT-04, DETECT-05, DETECT-07, DETECT-08, DETECT-09, BUG-01, BUG-02
**Context note:** No `CONTEXT.md` exists for Phase 2, so this research uses the roadmap, requirements, Phase 1 artifacts, and current codebase patterns.

## Repo-Grounded Findings

### 1. Phase 1 already provides the exact runtime seams Phase 2 needs

- `src/AIAgents.Core/Services/DataverseClient.cs` returns typed `PluginTraceLogEntry` rows with `TypeName`, `MessageName`, `PrimaryEntity`, `Mode`, `Depth`, `CreatedOnUtc`, `ExceptionDetails`, and `MessageBlock`.
- `src/AIAgents.Core/Services/ErrorFingerprintService.cs` already produces a deterministic fingerprint from plugin context plus normalized error text.
- `src/AIAgents.Functions/Services/TableStorageErrorTrackingService.cs` already persists `Status`, `Classification`, `WorkItemId`, `OccurrenceCount`, `FirstSeenUtc`, `LastSeenUtc`, and watermark data with optimistic concurrency.
- Conclusion: Phase 2 should be additive. The triage service can compose these existing services instead of redesigning storage or Dataverse access.

### 2. Azure DevOps Bug creation support is not present yet, but the extension point is narrow

- `src/AIAgents.Core/Interfaces/IAzureDevOpsClient.cs` exposes `CreateWorkItemAsync(string title, string description, string state, CancellationToken cancellationToken = default)`.
- `src/AIAgents.Core/Services/AzureDevOpsClient.cs` hardcodes `"User Story"` in the SDK call to `client.CreateWorkItemAsync(...)`.
- `src/AIAgents.Functions/Functions/CodebaseIntelligence.cs` is the only current call site for `CreateWorkItemAsync(...)`.
- Conclusion: Phase 2 can parameterize the work item type with a default value of `"User Story"` and keep current callers unchanged while enabling `"Bug"` creation.

### 3. The codebase already has a strong JSON-prompt-and-parse pattern for AI calls

- `src/AIAgents.Functions/Agents/PlanningAgentService.cs` sends a strict JSON-only instruction set to `IAIClient.CompleteAsync(...)`.
- The same service strips markdown fences and parses the JSON result through a dedicated parser method.
- `src/AIAgents.Core/Services/AIClient.cs` already supports deterministic options like low temperature and bounded token counts.
- Conclusion: Phase 2 AI classification should follow the Planning agent pattern: strict JSON response contract, fence stripping, explicit enum validation, and targeted parser tests.

### 4. The triage funnel belongs in the Functions layer, not Core

- Runtime orchestration services such as `TableStorageActivityLogger`, `CopilotCompletionService`, and the agent services all live in `src/AIAgents.Functions`.
- `src/AIAgents.Functions/Program.cs` is already the composition root for the Dataverse services added in Phase 1.
- The triage flow needs both Core services (`IDataverseClient`, `IErrorFingerprintService`, `IAzureDevOpsClient`, `IAIClient`) and Functions services (`IErrorTrackingService`).
- Conclusion: `ErrorTriageService` and `IErrorClassificationService` should live in the Functions project and be registered from `Program.cs`.

### 5. Existing tracked-error storage supports Phase 2, but it needs a few fields for richer caching

- `ErrorTrackingRecord` already has `Classification`, `Status`, and `WorkItemId`, which are enough for a minimal triage loop.
- It does not yet have fields for cached AI confidence, suggested Bug title, root-cause hypothesis, or rolling occurrence history.
- The table service already serializes strings, ints, and nullable timestamps directly through `TableEntity`.
- Conclusion: Phase 2 can extend `ErrorTrackingRecord` with a small set of triage-specific fields without changing the storage abstraction.

### 6. There is no existing triage service or Bug description builder

- No `ErrorTriageService`, `IErrorClassificationService`, or Bug payload formatter exists under `src/AIAgents.Functions/Services`.
- No tests currently cover Bug creation through the triage path.
- Conclusion: the plans need to introduce both the orchestration service and a focused Bug description formatter so downstream execution stays testable.

### 7. Requirement placement suggests a three-step plan split

- Code-first funnel requirements (`TRIAGE-01`, `DETECT-05`, `DETECT-07`, `DETECT-08`, `DETECT-09`) cluster around a triage skeleton and threshold/noise handling.
- AI-specific requirements (`TRIAGE-02` through `TRIAGE-05`) cluster around classification prompts, parser behavior, and permanent caching.
- Bug-specific requirements (`DETECT-04`, `BUG-01`, `BUG-02`) cluster around ADO work item creation, open-Bug deduplication, and rich bug descriptions.
- Conclusion: the roadmap's `02-01`, `02-02`, `02-03` split is repo-aligned and should remain sequential to avoid multiple plans editing the same triage service in parallel.

## Recommended Implementation Shape

### 1. Introduce a single triage orchestrator service in Functions

Add:

- `src/AIAgents.Functions/Models/ErrorTriageDecision.cs`
- `src/AIAgents.Functions/Services/IErrorTriageService.cs`
- `src/AIAgents.Functions/Services/ErrorTriageService.cs`

Recommended contract:

- `Task<ErrorTriageDecision> EvaluateAsync(PluginTraceLogEntry entry, CancellationToken cancellationToken = default);`

Recommended decision payload fields:

- `Outcome` - exact strings such as `SkippedDisabled`, `SkippedCascade`, `SkippedNoise`, `AwaitingThreshold`, `ReadyForClassification`, `TrackOnly`, `CreateBug`, `OpenBugExists`
- `Fingerprint`
- `NormalizedMessage`
- `OccurrenceCount`
- `RequiredOccurrences`
- `Severity`
- `ErrorTrackingRecord? TrackingRecord`

This keeps Phase 3 free to call the triage service from a timer without embedding orchestration logic in the function itself.

### 2. Extend Dataverse configuration rather than creating a second triage options type

Recommended additions to `src/AIAgents.Core/Configuration/DataverseOptions.cs`:

- `DefaultBugOccurrenceThreshold = 3`
- `OccurrenceHistoryLimit = 20`
- `Dictionary<string, int> PluginOccurrenceThresholds`

Reason:

- Phase 1 already centralized Dataverse monitor configuration under one options object.
- Thresholds belong to the same monitor feature boundary and do not need a second options section yet.

### 3. Extend tracked-error persistence for triage caching

Recommended additions to `ErrorTrackingRecord` and `TableStorageErrorTrackingService`:

- `ClassificationConfidence`
- `SuggestedTitle`
- `RootCauseHypothesis`
- `OccurrenceHistoryJson`

Recommended `OccurrenceHistoryJson` behavior:

- Store a JSON array of ISO-8601 timestamps.
- Append the newest occurrence time during triage.
- Trim the array to `OccurrenceHistoryLimit`.

This satisfies the rolling-history requirement without inventing a second table or analytics store.

### 4. Use a strict AI classification contract with deterministic parsing

Add:

- `src/AIAgents.Functions/Models/ErrorClassificationResult.cs`
- `src/AIAgents.Functions/Services/IErrorClassificationService.cs`
- `src/AIAgents.Functions/Services/AIErrorClassificationService.cs`

Recommended JSON response shape:

```json
{
  "classification": "CRITICAL|BUG|MONITOR|NOISE",
  "confidence": 0,
  "suggestedTitle": "",
  "rootCauseHypothesis": "",
  "reasoning": ""
}
```

Recommended AI call options:

- `Temperature = 0.1`
- `MaxTokens = 600`

Recommended decision matrix in `ErrorTriageService`:

- `CRITICAL` with confidence >= 70 -> `CreateBug`
- `BUG` with confidence >= 80 and occurrence count >= threshold -> `CreateBug`
- `MONITOR` -> `TrackOnly`
- `NOISE` -> `Ignore`

### 5. Parameterize Bug creation at the ADO client boundary

Update:

- `IAzureDevOpsClient.CreateWorkItemAsync(...)`
- `AzureDevOpsClient.CreateWorkItemAsync(...)`

Recommended signature:

- `Task<int> CreateWorkItemAsync(string title, string description, string state, string workItemType = "User Story", CancellationToken cancellationToken = default);`

Reason:

- Keeps all current callers compiling.
- Lets triage create `Bug` work items now without forcing the rest of the platform to understand Bug-specific behavior yet.

### 6. Add a dedicated Bug description builder instead of building HTML inline inside the triage service

Add:

- `src/AIAgents.Functions/Services/DataverseBugWorkItemBuilder.cs`

Recommended content sections:

- Plugin type, message name, primary entity
- Fingerprint and classification metadata
- Occurrence count, first seen, last seen
- Root-cause hypothesis
- Full `ExceptionDetails` body
- Fallback to `MessageBlock` when `ExceptionDetails` is empty

This keeps the triage service readable and makes the Bug payload independently testable.

## Risks and Planning Notes

### 1. Open-Bug deduplication needs an explicit state rule

The requirement says "open Bug work item", but the repo currently has no Bug-specific lifecycle helper.

Recommended Phase 2 rule:

- Treat Bug states `Resolved`, `Closed`, `Done`, and `Removed` as non-open.
- Everything else is considered open for deduplication.

### 2. Phase 2 should create Bugs in a neutral state

Phase 3 owns Bug-state provisioning and automatic `AI Agent` startup.

Recommended Phase 2 Bug creation state:

- `"New"`

This avoids coupling Phase 2 to process-template work that the roadmap explicitly deferred to Phase 3.

### 3. Write-before-dispatch needs a concrete pre-Bug status

To satisfy the "write dedup record before dispatch" decision in `STATE.md`, use a pre-create status such as:

- `BugPending`

Flow:

1. Persist/update the tracked row as `BugPending`
2. Create the ADO Bug
3. Persist the returned `WorkItemId` and final `BugCreated` status

### 4. The triage service will be heavily edited across all three plans

To avoid wave conflicts:

- `02-01` should establish the service contract, threshold lookup, history updates, and code-first skip paths
- `02-02` should modify that service only to add AI classification and cache reuse
- `02-03` should modify it only to add open-Bug lookup and Bug creation dispatch

This is a strong argument for sequential waves.

## Validation Architecture

### Automated tests to add

- `src/AIAgents.Functions.Tests/Services/ErrorTriageServiceTests.cs`
  - disabled config returns `SkippedDisabled`
  - cascade errors (`Depth > 0`) return `SkippedCascade`
  - known noise patterns short-circuit to `NOISE`
  - plugin-specific thresholds gate `ReadyForClassification`
  - cached classifications skip new AI calls
  - open Bugs block duplicate Bug creation
- `src/AIAgents.Functions.Tests/Services/AIErrorClassificationServiceTests.cs`
  - parser handles fenced JSON
  - invalid enum values are rejected
  - low-temperature completion call uses the strict JSON contract
- `src/AIAgents.Functions.Tests/Services/TableStorageErrorTrackingServiceTests.cs`
  - extend coverage for new triage cache fields and `OccurrenceHistoryJson`

### Commands

- Quick loop:
  - `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTriageServiceTests|FullyQualifiedName~AIErrorClassificationServiceTests"`
- Storage-focused loop:
  - `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~TableStorageErrorTrackingServiceTests"`
- Full validation:
  - `dotnet build src/AIAgents.sln`

## Proposed Plan Split

### 02-01 - Code-first triage skeleton

- Add threshold config
- Create the triage service and decision model
- Implement disabled, cascade, noise, severity, threshold, and occurrence-history behavior

### 02-02 - AI classification and permanent cache

- Add strict classification prompt/response handling
- Persist classification metadata to `ErrorTracking`
- Reuse cached results and apply the decision matrix

### 02-03 - Bug creation wiring

- Parameterize ADO work item creation for `Bug`
- Build Bug title/description content
- Prevent duplicate Bugs when an open work item already exists
