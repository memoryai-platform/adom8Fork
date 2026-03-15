# Phase 1 Research - Infrastructure Foundation

**Gathered:** 2026-03-15
**Phase:** 1 - Infrastructure Foundation
**Goal:** Build the Dataverse monitor plumbing so the app can authenticate, read PluginTraceLog errors, normalize/fingerprint them, and persist tracking state behind a feature flag that defaults to off.
**Requirements:** CONN-01, CONN-02, CONN-03, CONN-04, DETECT-01, DETECT-02, DETECT-03, DETECT-06

## Repo-Grounded Findings

### 1. Startup and DI patterns already fit this feature

- `src/AIAgents.Functions/Program.cs` binds options with `services.Configure<T>(configuration.GetSection(...))`.
- Named `HttpClient` registrations already exist for `"AIClient"`, `"GitHub"`, and `"SaasCallback"`.
- Core services such as `AIClient` already take `IHttpClientFactory`, `IOptions<T>`, and `ILogger<T>` and are registered from the Functions app.
- Conclusion: a Dataverse client fits best as a Core service plus Functions-layer registration.

### 2. Table Storage usage is already established in the Functions layer

- `src/AIAgents.Functions/Services/TableStorageActivityLogger.cs` and `src/AIAgents.Functions/Services/CopilotDelegationService.cs` both construct `TableClient` from `AzureWebJobsStorage`.
- Existing table services create their table on startup and use `TableEntity` directly.
- `TableStorageCopilotDelegationService` already demonstrates `ETag` handling and `UpdateEntityAsync(...)`.
- Conclusion: `ErrorTracking` should live in the Functions project beside other storage-backed operational services.

### 3. Feature-flagged dormant behavior already has good examples

- `src/AIAgents.Functions/Functions/CopilotTimeoutChecker.cs` returns early when `CopilotOptions.Enabled` is false.
- `src/AIAgents.Functions/Services/SaasCallbackService.cs` turns itself into a no-op when config is incomplete.
- Conclusion: the safest Phase 1 behavior is registration-time branching that resolves real services only when Dataverse config is present, and otherwise resolves no-op implementations.

### 4. Health and timer patterns are already in the codebase

- `src/AIAgents.Functions/Functions/HealthCheck.cs` shows the current cache/check style for operational diagnostics.
- Timer-trigger functions already exist (`CopilotTimeoutChecker`, `DeadLetterQueueHandler`), and cron strings are stored inline today.
- Conclusion: Phase 1 should introduce `DataverseOptions.MonitorSchedule` now so Phase 3 can reuse it without another config redesign.

### 5. Current tests favor xUnit + Moq with targeted helper constructors

- `src/AIAgents.Functions.Tests/Functions/HealthCheckTests.cs` and existing agent tests show a strong pattern of constructor-based dependency injection, Moq doubles, and targeted `dotnet test --filter ...` runs.
- Conclusion: plan for unit tests that do not require a live Dataverse org or live Azure Table Storage account.

## Recommended Implementation Shape

### 1. Dataverse client belongs in `AIAgents.Core`

Add these files:

- `src/AIAgents.Core/Configuration/DataverseOptions.cs`
- `src/AIAgents.Core/Interfaces/IDataverseClient.cs`
- `src/AIAgents.Core/Models/PluginTraceLogEntry.cs`
- `src/AIAgents.Core/Services/DataverseClient.cs`

Recommended design:

- `DataverseOptions` should expose:
  - `BaseUrl`
  - `TenantId`
  - `ClientId`
  - `ClientSecret`
  - `MonitorSchedule = "0 */15 * * * *"`
  - `PluginTraceLogPageSize = 250`
  - `bool IsConfigured` computed from the four required connection values
- `DataverseClient` should inject:
  - `IConfidentialClientApplication`
  - `IHttpClientFactory`
  - `IOptions<DataverseOptions>`
  - `ILogger<DataverseClient>`
- Use the named client `"Dataverse"` and acquire tokens with client credentials.
- Scope should be based on the org root URL plus `/.default`, not the `/api/data/v9.2` path.
- Query shape should be concrete and stable for Phase 1:
  - entity set: `plugintracelogs`
  - select: `plugintracelogid,typename,messagename,primaryentity,mode,depth,createdon,exceptiondetails,messageblock`
  - filter: `exceptiondetails ne null`
  - phase-ready hook: optional `createdon gt {watermark}` parameter once storage is wired
  - order: `createdon asc`
- Pagination should follow `@odata.nextLink` until exhausted.
- Retry behavior should explicitly handle:
  - `429 Too Many Requests`
  - `5xx`
  - `Retry-After` headers when present

### 2. Error tracking storage belongs in `AIAgents.Functions`

Add these files:

- `src/AIAgents.Functions/Models/ErrorTrackingRecord.cs`
- `src/AIAgents.Functions/Services/IErrorTrackingService.cs`
- `src/AIAgents.Functions/Services/TableStorageErrorTrackingService.cs`

Recommended table contract:

- Table name: `ErrorTracking`
- Error rows:
  - `PartitionKey = plugin typename normalized to lower-invariant`
  - `RowKey = SHA-256 fingerprint`
- Watermark row:
  - `PartitionKey = "meta"`
  - `RowKey = "plugintracelog-watermark"`

Recommended record shape for forward compatibility:

- `PluginType`
- `MessageName`
- `PrimaryEntity`
- `Fingerprint`
- `NormalizedMessage`
- `OccurrenceCount`
- `FirstSeenUtc`
- `LastSeenUtc`
- `Status`
- `Classification`
- `WorkItemId`
- `SuppressedUntilUtc`
- `ResolvedAtUtc`
- `ConsecutiveMissedWindows`
- `ETag`

Behavior to lock in during Phase 1:

- create row when fingerprint first appears
- update occurrence counts and `LastSeenUtc`
- persist watermark independently of error rows
- handle optimistic concurrency with an explicit retry loop on HTTP 412

### 3. Feature gating should be registration-time, not scattered null checks

Add a Functions-layer extension:

- `src/AIAgents.Functions/Extensions/DataverseServiceCollectionExtensions.cs`

Recommended behavior:

- Always bind `DataverseOptions`.
- When `DataverseOptions.IsConfigured` is false:
  - register `IDataverseClient` as `NoOpDataverseClient`
  - register `IErrorTrackingService` as `NoOpErrorTrackingService`
  - do not register `IConfidentialClientApplication`
  - do not create a Dataverse `HttpClient` with a potentially invalid base URL
- When configured:
  - register `IConfidentialClientApplication` as a singleton
  - register named client `"Dataverse"`
  - register the real `DataverseClient`
  - register the real `TableStorageErrorTrackingService`

This is the cleanest way to satisfy the dormant-mode success criterion:

- no token acquisition path exists when config is absent
- later timer and HTTP endpoint phases can inject the same interfaces safely

### 4. Fingerprinting should include context, not only raw text

Add these files:

- `src/AIAgents.Core/Interfaces/IErrorFingerprintService.cs`
- `src/AIAgents.Core/Services/ErrorFingerprintService.cs`

Recommended normalization rules:

- replace GUIDs with `[guid]`
- replace ISO-8601 timestamps and common datetime strings with `[timestamp]`
- replace long numeric identifiers with `[id]`
- replace hex memory addresses like `0x7ffde123` with `[addr]`
- collapse repeated whitespace to single spaces
- trim and lowercase the final normalized message

Recommended fingerprint input:

- `string.Join("|", pluginType, messageName, primaryEntity, normalizedMessage)`

Reason:

- the same generic exception text can occur in multiple plugins
- including plugin/message/entity context reduces false-positive collisions

## File-Level Placement Decisions

### Core project additions

- Keep Dataverse HTTP/auth code in Core because the existing HTTP client pattern already lives there.
- Add `Microsoft.Identity.Client` to `src/AIAgents.Core/AIAgents.Core.csproj`.
- Keep the fingerprint service in Core so future triage services can reuse it without a Functions-specific dependency.

### Functions project additions

- Keep Azure Table Storage code in Functions because the project already references `Azure.Data.Tables` there.
- Keep registration/no-op wiring in Functions because that is where `Program.cs` and environment binding already live.

## Validation Architecture

### Automated tests to add

- `src/AIAgents.Core.Tests/Services/DataverseClientTests.cs`
  - token acquisition uses client credentials
  - query builder includes `exceptiondetails ne null`
  - pagination follows `@odata.nextLink`
  - retry logic honors `Retry-After`
- `src/AIAgents.Core.Tests/Services/ErrorFingerprintServiceTests.cs`
  - normalization strips GUIDs, timestamps, IDs, and addresses
  - equivalent errors produce the same hash
  - different plugin/message/entity context produces different hashes
- `src/AIAgents.Functions.Tests/Services/TableStorageErrorTrackingServiceTests.cs`
  - create/update path persists counts and timestamps
  - watermark round-trips
  - optimistic concurrency retries on 412
- `src/AIAgents.Functions.Tests/Services/DataverseServiceCollectionExtensionsTests.cs`
  - missing config registers no-op services
  - complete config registers real services and the MSAL singleton

### Testability note

`TableClient` interactions should be isolated behind either:

- an internal constructor that accepts a `TableClient`, or
- a tiny internal factory abstraction used only by the table service

Without one of those seams, the table service becomes awkward to unit test.

### Commands

- Quick loop:
  - `dotnet test src/AIAgents.Core.Tests/AIAgents.Core.Tests.csproj --filter "FullyQualifiedName~DataverseClientTests|FullyQualifiedName~ErrorFingerprintServiceTests"`
  - `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTracking|FullyQualifiedName~DataverseServiceCollectionExtensionsTests"`
- Full suite:
  - `dotnet test src/AIAgents.sln`

## Execution Notes

- Do not touch Phase 2 AI classification or Bug creation logic in this phase.
- Do not add a timer trigger yet; only introduce `MonitorSchedule` config now.
- Favor additive changes that preserve all current startup paths when Dataverse config is absent.
- Prefer `Program.cs` extension methods over large inline registration blocks so the new wiring is unit-testable.

## Open Questions To Resolve During Execution

- Whether the Dataverse org root is provided with or without `/api/data/v9.2`; the options class should normalize either input.
- Whether `messageblock` or `exceptiondetails` is the better source text for normalization when one is missing; the implementation should prefer `exceptiondetails` and fall back to `messageblock`.
- Whether the table service should expose a generic `UpsertAsync(...)` or a more intention-revealing `RecordOccurrenceAsync(...)`; either is acceptable as long as concurrency and watermark behavior stay explicit.
