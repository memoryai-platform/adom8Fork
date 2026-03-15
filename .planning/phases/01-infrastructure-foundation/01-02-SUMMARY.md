---
phase: 01-infrastructure-foundation
plan: 02
subsystem: database
tags: [azure-table-storage, watermark, etag, concurrency]
requires: []
provides:
  - "Tracked error persistence for fingerprints, counts, and status metadata"
  - "Durable watermark storage for last successful PluginTraceLog scan"
  - "Optimistic concurrency retry behavior for tracked error updates"
affects: [phase-02-triage, phase-03-monitoring, error-lifecycle]
tech-stack:
  added: [Azure.Data.Tables]
  patterns: ["Table storage wrappers for testable optimistic concurrency"]
key-files:
  created:
    - src/AIAgents.Functions/Models/ErrorTrackingRecord.cs
    - src/AIAgents.Functions/Services/IErrorTrackingService.cs
    - src/AIAgents.Functions/Services/TableStorageErrorTrackingService.cs
    - src/AIAgents.Functions.Tests/Services/TableStorageErrorTrackingServiceTests.cs
  modified:
    - src/AIAgents.Functions.Tests/Agents/DocumentationAgentServiceTests.cs
key-decisions:
  - "Stored the watermark in the same ErrorTracking table under a stable metadata row to avoid introducing another persistence surface."
  - "Resolved 412 conflicts by reloading and merging tracked-error state instead of dropping updates."
patterns-established:
  - "Functions services that wrap Azure SDK clients expose an internal test abstraction for deterministic unit tests."
  - "Tracked error persistence always creates the table lazily before reads or writes."
requirements-completed: [DETECT-03, DETECT-06]
duration: 30min
completed: 2026-03-15
---

# Phase 1 Plan 2: Error Tracking Persistence Summary

**Azure Table Storage persistence for tracked errors and scan watermarks with optimistic concurrency recovery**

## Performance

- **Duration:** 30 min
- **Started:** 2026-03-15T17:15:00-06:00
- **Completed:** 2026-03-15T17:45:00-06:00
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments
- Added the persistent tracked-error model and service contract for later triage and lifecycle work.
- Implemented Azure Table Storage reads, upserts, and watermark storage with merge-on-conflict retry behavior.
- Added Functions-layer tests for create, update, watermark round-tripping, and 412 retry handling.

## Task Commits

Plan execution was consolidated into one commit in this resumed session:

1. **Plan implementation** - `b6b5c20` (`feat(01-02): add error tracking storage service`)

## Files Created/Modified
- `src/AIAgents.Functions/Models/ErrorTrackingRecord.cs` - Durable tracked-error state model.
- `src/AIAgents.Functions/Services/IErrorTrackingService.cs` - Contract for tracked errors and watermark operations.
- `src/AIAgents.Functions/Services/TableStorageErrorTrackingService.cs` - Azure Table-backed implementation with optimistic concurrency retries.
- `src/AIAgents.Functions.Tests/Services/TableStorageErrorTrackingServiceTests.cs` - Storage service verification coverage.
- `src/AIAgents.Functions.Tests/Agents/DocumentationAgentServiceTests.cs` - Test harness fix so the Functions test project still builds after constructor changes elsewhere in the codebase.

## Decisions Made
- Reused the `ErrorTracking` table for the scan watermark to keep persistence local to the monitor subsystem.
- Introduced a thin internal table-client interface so concurrency behavior can be tested without emulator dependencies.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Repaired a failing Functions test harness constructor**
- **Found during:** Verification
- **Issue:** `DocumentationAgentServiceTests` no longer compiled because `DocumentationAgentService` now requires `IOptions<GitHubOptions>`.
- **Fix:** Added `Microsoft.Extensions.Options` and supplied a minimal `GitHubOptions` test instance in the helper factory.
- **Files modified:** `src/AIAgents.Functions.Tests/Agents/DocumentationAgentServiceTests.cs`
- **Verification:** Functions tests compiled and the solution build succeeded.
- **Committed in:** `b6b5c20`

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The fix was required to keep the test project buildable and did not expand scope beyond verification support.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 2 can cache classifications, thresholds, and bug references in `IErrorTrackingService`.
- Phase 3 can move the monitor watermark forward between scheduled scans without depending on in-memory state.

## Self-Check: PASSED

- Key files exist on disk.
- Commit `b6b5c20` matches `01-02`.

---
*Phase: 01-infrastructure-foundation*
*Completed: 2026-03-15*
