---
phase: 1
slug: infrastructure-foundation
status: ready
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-15
---

# Phase 1 - Validation Strategy

> Per-phase validation contract for Infrastructure Foundation execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Moq |
| **Config file** | Existing .NET test projects in `src/AIAgents.Core.Tests` and `src/AIAgents.Functions.Tests` |
| **Quick run command** | `dotnet test src/AIAgents.sln --filter "FullyQualifiedName~DataverseClientTests|FullyQualifiedName~ErrorFingerprintServiceTests|FullyQualifiedName~ErrorTracking|FullyQualifiedName~DataverseServiceCollectionExtensionsTests"` |
| **Full suite command** | `dotnet test src/AIAgents.sln` |
| **Estimated runtime** | ~90 seconds |

---

## Sampling Rate

- After every task commit: run `dotnet test src/AIAgents.sln --filter "FullyQualifiedName~DataverseClientTests|FullyQualifiedName~ErrorFingerprintServiceTests|FullyQualifiedName~ErrorTracking|FullyQualifiedName~DataverseServiceCollectionExtensionsTests"`
- After every plan wave: run `dotnet test src/AIAgents.sln`
- Before `$gsd-verify-work`: full suite must be green
- Max feedback latency: 45 seconds for the filtered loop

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-01-T1 | 01 | 1 | CONN-01 | unit | `dotnet test src/AIAgents.Core.Tests/AIAgents.Core.Tests.csproj --filter "FullyQualifiedName~DataverseClientTests"` | `src/AIAgents.Core.Tests/Services/DataverseClientTests.cs` | pending |
| 01-01-T2 | 01 | 1 | CONN-02, DETECT-01 | unit | `dotnet test src/AIAgents.Core.Tests/AIAgents.Core.Tests.csproj --filter "FullyQualifiedName~DataverseClientTests"` | `src/AIAgents.Core.Tests/Services/DataverseClientTests.cs` | pending |
| 01-02-T1 | 02 | 1 | DETECT-03 | unit | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTracking"` | `src/AIAgents.Functions.Tests/Services/TableStorageErrorTrackingServiceTests.cs` | pending |
| 01-02-T2 | 02 | 1 | DETECT-06 | unit | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTracking"` | `src/AIAgents.Functions.Tests/Services/TableStorageErrorTrackingServiceTests.cs` | pending |
| 01-03-T1 | 03 | 2 | CONN-03, CONN-04 | unit | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~DataverseServiceCollectionExtensionsTests"` | `src/AIAgents.Functions.Tests/Services/DataverseServiceCollectionExtensionsTests.cs` | pending |
| 01-03-T2 | 03 | 2 | DETECT-02 | unit | `dotnet test src/AIAgents.Core.Tests/AIAgents.Core.Tests.csproj --filter "FullyQualifiedName~ErrorFingerprintServiceTests"` | `src/AIAgents.Core.Tests/Services/ErrorFingerprintServiceTests.cs` | pending |

---

## Wave 0 Requirements

- Existing infrastructure covers all phase requirements.
- No new framework install is required before Phase 1 execution begins.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Dataverse authentication and PluginTraceLog query against a real org | CONN-01, CONN-02 | The repo does not contain a Dataverse environment or safe credentials for CI | Set `Dataverse__BaseUrl`, `Dataverse__TenantId`, `Dataverse__ClientId`, and `Dataverse__ClientSecret`, then run the targeted Dataverse client tests or a one-off smoke harness during execution. |

---

## Validation Sign-Off

- [x] All planned tasks have automated verification or an explicit manual exception
- [x] Sampling continuity avoids long unverified stretches
- [x] Existing test infrastructure removes the need for a separate Wave 0
- [x] No watch-mode commands are required
- [x] Feedback latency stays under 45 seconds for the filtered loop
- [x] `nyquist_compliant: true` is set in frontmatter

**Approval:** pending
