---
phase: 02
slug: triage-orchestrator
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-15
---

# Phase 02 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit on .NET 8 |
| **Config file** | `src/AIAgents.sln` |
| **Quick run command** | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTriageServiceTests|FullyQualifiedName~AIErrorClassificationServiceTests"` |
| **Full suite command** | `dotnet build src/AIAgents.sln` |
| **Estimated runtime** | ~45 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTriageServiceTests|FullyQualifiedName~AIErrorClassificationServiceTests"`
- **After every plan wave:** Run `dotnet build src/AIAgents.sln`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | TRIAGE-01, DETECT-05, DETECT-07, DETECT-08, DETECT-09 | unit | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTriageServiceTests"` | yes | pending |
| 02-02-01 | 02 | 2 | TRIAGE-02, TRIAGE-03, TRIAGE-04, TRIAGE-05 | unit | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~AIErrorClassificationServiceTests|FullyQualifiedName~ErrorTriageServiceTests"` | yes | pending |
| 02-03-01 | 03 | 3 | DETECT-04, BUG-01, BUG-02 | unit + build | `dotnet test src/AIAgents.Functions.Tests/AIAgents.Functions.Tests.csproj --filter "FullyQualifiedName~ErrorTriageServiceTests" && dotnet build src/AIAgents.sln` | yes | pending |

*Status: pending = not yet run, green = passing, red = failing, flaky = unstable*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

---

## Manual-Only Verifications

All phase behaviors have automated verification.

---

## Validation Sign-Off

- [x] All tasks have automated verify or existing infrastructure coverage
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
