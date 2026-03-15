# Codebase Concerns

**Analysis Date:** 2026-03-14

## Tech Debt

**Legacy Fallback Path in CodebaseDocumentationAgent:**
- Issue: `CodebaseDocumentationAgentService` (lines 103-107) contains a fallback code path that performs a full local git clone when `ApiOnlyInitializationEnabled = false`. This path is marked as legacy and should be removed once API-only mode is validated in production.
- Files: `src/AIAgents.Functions/Agents/CodebaseDocumentationAgentService.cs` (line 106 TODO)
- Impact: Adds complexity, doubles maintenance burden, increases Azure Functions timeout risk for large repositories, unused unless explicitly disabled
- Fix approach: Remove legacy fallback after API-only mode runs in production for 2+ weeks without issues. Flag: `ApiOnlyInitializationEnabled` already defaults to `true` but needs production validation before cleanup.

**Blocking Field Creation During Provisioning:**
- Issue: `ProvisionAzureDevOps.cs` attempts field/state/hook creation serially with catch-all error handlers that only warn/log. If Azure DevOps API rate-limits or denies permissions, provision operation appears partially successful but critical ADO schema may be missing.
- Files: `src/AIAgents.Functions/Functions/ProvisionAzureDevOps.cs` (lines 195-222, 224-250)
- Impact: Stories may proceed through planning with missing custom fields, causing downstream agent failures when trying to read/write ADO custom fields
- Fix approach: Add validation sweep after provisioning to verify all required fields exist; fail hard if critical fields missing; separate optional features (metrics, model overrides) from required ones.

**Bare Catch-All Exception Handlers:**
- Issue: Multiple locations use empty `catch { }` blocks to suppress errors:
  - `CodingAgentService.cs` line 170: `catch { }` when updating ADO field
  - `PlanningAgentService.cs` line 104: `catch { /* field may not exist yet */ }` when setting current agent field
  - `AgentTaskDispatcherTests.cs` line 236: `catch { }` in test
- Files: `src/AIAgents.Functions/Agents/CodingAgentService.cs` (line 170), `src/AIAgents.Functions/Agents/PlanningAgentService.cs` (line 104)
- Impact: Silently swallows failures, making debugging difficult; masks permission issues or infrastructure problems
- Fix approach: Log at warn level before suppressing; use typed exception catches instead of catch-all.

---

## Known Bugs

**Task.Result Usage in HealthCheck:**
- Symptoms: Potential deadlock if `HealthCheck.CheckConfiguration()` (line 78 in `src/AIAgents.Functions/Functions/HealthCheck.cs`) synchronously waits on async result
- Files: `src/AIAgents.Functions/Functions/HealthCheck.cs` (line 78)
- Trigger: Health endpoint called while other code holds task context
- Workaround: Currently appears safe because `CheckConfiguration()` returns `Task.FromResult()` (synchronous), but pattern is fragile
- Fix approach: Refactor to `await configTask` instead of `.Result`; ensure all health checks are fully async.

---

## Security Considerations

**Secrets in Plain App Settings (Pre-Production Warning):**
- Risk: Default setup stores secrets (AI API key, ADO PAT, GitHub token, Copilot webhook secret) in Azure Function App settings rather than Key Vault. If Function App dashboard is accessed, secrets are visible.
- Files: All integration points read from `IOptions<{Feature}Options>` which come from app settings. See `Program.cs` configuration binding (lines 20-28).
- Current mitigation: Documentation `SECURITY_HARDENING.md` provides Key Vault migration steps; secrets are not logged; environment isolation exists
- Recommendations:
  1. For production: Enforce Key Vault references (documented in `SECURITY_HARDENING.md`)
  2. Add startup check to warn if secrets remain in app settings
  3. Rotate PATs/API keys quarterly; GitHub fine-grained tokens preferred over classic PATs

**Webhook Secret Validation:**
- Risk: GitHub webhook signature validation exists (X-Hub-Signature-256) but depends on `Copilot:WebhookSecret` being set. If not configured, webhook acceptance is loose.
- Files: `src/AIAgents.Functions/Functions/CopilotBridgeWebhook.cs`
- Current mitigation: Function-level auth (`AuthorizationLevel.Function`) protects against arbitrary callers
- Recommendations: Require webhook secret at startup if Copilot is enabled; fail early rather than silently accepting unsigned webhooks.

**Service Hook Security:**
- Risk: ADO Service Hook posts to function URL; payload is not encrypted in transit (HTTPS is required but not enforced in code). Function key is optional but recommended.
- Files: `src/AIAgents.Functions/Functions/OrchestratorWebhook.cs`
- Current mitigation: HTTPS endpoint; function key in URL; validation of work item payload format
- Recommendations: Document requirement to always include function key; consider adding ADO service hook signature validation.

**GitHub Token Scope:**
- Risk: GitHub PAT stored in config may have overly broad scope (repo, workflow, admin). Single leaked token compromises multiple systems.
- Files: `src/AIAgents.Core/Configuration/GitHubOptions.cs`; `Program.cs` lines 67-68
- Current mitigation: Documentation recommends fine-grained tokens; PAT scope not enforced in code
- Recommendations: Enforce fine-grained token type at startup; add warning if classic PAT detected; separate tokens per scope if possible.

---

## Performance Bottlenecks

**Concurrent API Calls Without Adaptive Throttling:**
- Problem: `GitHubApiContextService.GetFileContentsAsync()` (line 93 in `src/AIAgents.Core/Services/GitHubApiContextService.cs`) uses fixed `SemaphoreSlim(5, 5)` for concurrency. Large file fetches may be throttled unnecessarily; GitHub API rate limits (60 req/min for unauthenticated, 5000 for authenticated) are not dynamically respected.
- Files: `src/AIAgents.Core/Services/GitHubApiContextService.cs` (lines 90-105)
- Cause: No circuit breaker or backoff for GitHub rate-limit responses (HTTP 429)
- Improvement path:
  1. Add detection for `X-RateLimit-Remaining` header
  2. Implement exponential backoff on 429 responses
  3. Consider burst throttling based on remaining quota

**Large Repository Analysis Timeout Risk:**
- Problem: `CodebaseDocumentationAgentService` fetches full tree recursively (`?recursive=1` in `GitHubCodebaseOnboardingService` line 50). For repos >100k files, single API call times out.
- Files: `src/AIAgents.Core/Services/GitHubCodebaseOnboardingService.cs` (line 50), `Program.cs` (line 50: `AttemptTimeout.Timeout = TimeSpan.FromSeconds(300)`)
- Cause: GitHub API returns full tree in single response; paginated fetch not implemented
- Improvement path:
  1. Add pagination for tree endpoint (GitHub supports `?recursive=1&page=X`)
  2. Implement incremental scanning for repositories >50k files
  3. Add progress reporting for long-running analysis

**Synchronous Prompt Tokenization:**
- Problem: `AIClient.CompleteAsync()` builds entire request body in memory before sending. Large context (codebase documentation) is serialized all at once.
- Files: `src/AIAgents.Core/Services/AIClient.cs` (lines 76-100)
- Cause: No streaming or chunked request support
- Improvement path: For multi-agent context with >100k tokens, implement request streaming or split into multiple calls with state management.

**Planning Agent Prompt Engineering Overhead:**
- Problem: `PlanningAgentService` builds extremely detailed system prompt (lines 115-150+) with 7 triage checks. For simple stories, this is overkill and increases token usage.
- Files: `src/AIAgents.Functions/Agents/PlanningAgentService.cs` (lines 115-150)
- Cause: One-size-fits-all prompt regardless of story complexity
- Improvement path: Implement adaptive prompts based on story point estimate and description length.

---

## Fragile Areas

**Copilot Delegation State Machine:**
- Files: `src/AIAgents.Functions/Agents/CodingAgentService.cs`, `src/AIAgents.Functions/Functions/CopilotBridgeWebhook.cs`, `src/AIAgents.Functions/Functions/CopilotTimeoutChecker.cs`
- Why fragile: Copilot completion handoff depends on:
  1. GitHub webhook delivery (can be delayed or lost)
  2. Issue WIP marker removal by Copilot agent (external behavior)
  3. Timer-based timeout recovery polling every 2 minutes
  4. Checkpoint enforcement validating ADO field updates

  Missing webhook delivery leaves delegation stuck indefinitely until timeout. Race condition possible if webhook and timeout checker both detect completion simultaneously.

- Safe modification:
  1. Add idempotency key to completion tracking
  2. Log all state transitions with correlation IDs
  3. Add test for webhook delivery failure recovery
  4. Document timeout checker behavior in comments

- Test coverage: `CopilotCodingStrategyTests.cs`, `CopilotTimeoutCheckerTests.cs` exist but gaps:
  - No test for webhook delivery failure followed by timeout recovery
  - No test for simultaneous webhook + timeout completion detection

**Large File Agents (>700 lines):**
- Files:
  - `src/AIAgents.Functions/Functions/ProvisionAzureDevOps.cs` (1497 lines)
  - `src/AIAgents.Core/Services/GitHubCodebaseOnboardingService.cs` (1186 lines)
  - `src/AIAgents.Core/Services/AIClient.cs` (1139 lines)
  - `src/AIAgents.Functions/Agents/PlanningAgentService.cs` (978 lines)
  - `src/AIAgents.Functions/Agents/CodebaseDocumentationAgentService.cs` (916 lines)
  - `src/AIAgents.Functions/Agents/CopilotCodingStrategy.cs` (760 lines)
- Why fragile: Long files have multiple responsibilities mixed together, making single-change edits likely to break something unrelated
- Safe modification: Refactor by extracting helper classes (e.g., ProvisionAzureDevOps should split field/state/hook provisioning into separate private classes)
- Test coverage: Most have tests but coverage gaps on private helper methods

**Bare HttpClient Instances:**
- Files: `src/AIAgents.Core/Services/GitHubCodebaseOnboardingService.cs` (lines 32-40), `src/AIAgents.Core/Services/GitHubApiContextService.cs` (lines 43-54)
- Why fragile: Both services create raw `new HttpClient()` in constructors when factory not provided. This bypasses connection pooling and can lead to socket exhaustion.
- Safe modification: Always use `IHttpClientFactory`; remove fallback bare HttpClient constructors
- Test coverage: Tested with mocks, but connection pooling behavior not tested in integration scenarios

---

## Scaling Limits

**Azure Queue Storage Message Size (50 KB):**
- Current capacity: Agent tasks serialized as JSON fit within queue message limit
- Limit: If task includes large supporting artifacts (binary attachments >40 KB), queue message exceeds 50 KB and is rejected
- Scaling path:
  1. Store large artifacts in Blob Storage
  2. Pass Blob Storage URI in task instead of inline payload
  3. Modify `AgentTask` to support optional artifact references

**GitHub API Rate Limits (5000 req/min authenticated):**
- Current capacity: Single codebase analysis uses ~50-100 requests; safe for 50+ concurrent analyses per minute
- Limit: If >50 concurrent analyses or if rate limit headers ignored, requests start failing with 429
- Scaling path:
  1. Implement rate limit awareness (read `X-RateLimit-Remaining` header)
  2. Add request queue with dynamic throttling
  3. Consider GitHub enterprise account for higher limits

**Story Context Table Storage (1 MB entity size):**
- Current capacity: Story state (planning results, code artifacts, test results) stored per story in Table Storage; typical story state ~500 KB
- Limit: Table Storage enforces 1 MB entity size; very complex stories with many files may exceed this
- Scaling path:
  1. Split context into multiple table entities (one per stage: planning, coding, testing)
  2. Or move to Blob Storage for large states and reference via URI

**AI Token Budget (Per-Agent):**
- Current capacity: Planning agent ~4k tokens input, Coding agent ~8k input, Testing/Review agents ~6k input
- Limit: Large codebases (>1M LOC) + codebase documentation artifact may exceed token budget for single agent
- Scaling path:
  1. Implement context window estimation before calling AI
  2. Split large analyses across multiple agent tasks
  3. Add token usage tracking per story (already tracked in `StoryTokenUsage` model; not persisted)

---

## Dependencies at Risk

**Anthropic Claude API Dependency:**
- Risk: All planning/coding uses Claude API. If Anthropic service unavailable or API changes, entire pipeline blocks.
- Impact: Coding agent fails; stories stuck in "AI Code" state
- Migration plan: Code supports multiple providers via `AIOptions.Provider` ("Claude"/"OpenAI"). OpenAI models available but not tested at scale. Recommend maintaining dual-provider configuration in production.
- Files: `src/AIAgents.Core/Services/AIClient.cs` (lines 50-74 provider logic); `src/AIAgents.Core/Configuration/AIOptions.cs`

**GitHub Copilot Coding Strategy:**
- Risk: Copilot integration adds new dependency on GitHub's external coding agent. If Copilot API changes or is disabled, alternate path (agentic strategy) required.
- Impact: Stories routed to Copilot fail to complete; manual fallback needed
- Migration plan: `CodingAgentService` already has dual-strategy support. If Copilot path fails, timeout checker falls back to agentic strategy. Recommended: set `Copilot:Enabled=false` if not using, reduce operational complexity.
- Files: `src/AIAgents.Functions/Agents/CodingAgentService.cs`; `src/AIAgents.Core/Configuration/CopilotOptions.cs`

**Azure DevOps Custom Fields (Hard Schema Dependency):**
- Risk: All stories expect custom fields `Custom.AIMinimumReviewScore`, `Custom.AILastAgent`, etc. If ADO admin removes fields or changes references, agents fail to update work items.
- Impact: Agent status cannot be tracked; review scores lost
- Migration plan: Add startup validation to verify all required fields exist; fail early rather than during story processing
- Files: `src/AIAgents.Functions/Functions/ProvisionAzureDevOps.cs` (lines 75-97 defines required fields)

---

## Missing Critical Features

**No Persistent Token Usage Tracking:**
- Problem: `StoryTokenUsage` model (in memory) tracks tokens per story, but is never persisted to durable storage. If function app restarts, token usage data is lost.
- Blocks: Can't report on cost per story or cumulative AI spend; can't alert on token budget overrun
- Files: `src/AIAgents.Functions/Agents/CodebaseDocumentationAgentService.cs` (line 31), but never saved to persistent storage
- Fix approach: Persist `StoryTokenUsage` to Table Storage after each agent completes; add cost calculation based on token counts

**No Automated Rollback on Agent Failure:**
- Problem: If Coding agent pushes commits and then fails, commits remain on pipeline branch. Downstream agents may reference broken code.
- Blocks: Can't safely retry failed stories; human intervention required to revert bad commits
- Fix approach:
  1. On agent failure, revert all commits pushed during that agent's run
  2. Or: implement staging/rollback commits (one per agent phase)
  3. Document manual revert procedure for emergency cases

**No Rate-Limit-Aware Queue Processing:**
- Problem: Agent task dispatcher (queue trigger) doesn't respect GitHub/AI API rate limits. Multiple concurrent agents hitting limits simultaneously causes cascade failures.
- Blocks: Can't gracefully handle peak load; no predictable scaling
- Fix approach:
  1. Implement circuit breaker pattern for GitHub/AI APIs (already done for AI API, not for GitHub)
  2. Add adaptive concurrency based on rate-limit remaining
  3. Document queue scaling recommendations

**No Multi-Language Support in Codebase Analysis:**
- Problem: Codebase documentation agent assumes JavaScript/TypeScript tech stack. If analyzing C#/.NET or Python repos, tech stack detection is incomplete.
- Blocks: Mixed-language repos (e.g., C# backend + React frontend) have incomplete documentation
- Files: `src/AIAgents.Core/Services/GitHubCodebaseOnboardingService.cs` (tech detection logic lines ~67-68)
- Fix approach: Expand tech stack detection to include language-specific file patterns; test with polyglot repos

---

## Test Coverage Gaps

**Untested Scenario: Webhook Delivery Failure + Timeout Recovery:**
- What's not tested: Service hook webhook fails to deliver (network error), but story remains in "AI Agent" state. Timeout checker should eventually detect and recover.
- Files: `src/AIAgents.Functions/Functions/CopilotTimeoutChecker.cs`; tests in `src/AIAgents.Functions.Tests/Functions/CopilotTimeoutCheckerTests.cs`
- Risk: Timeout checker may not correctly resume stories if webhook never fires
- Priority: High (blocks Copilot integration reliability)

**Untested Scenario: Large Repository Analysis (>100k files):**
- What's not tested: Codebase analysis on repos exceeding 100k files; current tests use small fixture repos
- Files: `src/AIAgents.Core/Services/GitHubCodebaseOnboardingService.cs` (recursive tree fetch line 50)
- Risk: GitHub API timeout, or payload truncation
- Priority: Medium (impacts large enterprise repos)

**Untested Scenario: ADO Field Permission Errors During Provisioning:**
- What's not tested: Provisioning when user doesn't have permission to create custom fields
- Files: `src/AIAgents.Functions/Functions/ProvisionAzureDevOps.cs` (lines 195-222, field creation)
- Risk: Provisioning appears successful but required fields missing; stories fail downstream
- Priority: Medium (impacts setup reliability)

**Untested Scenario: Rate-Limit Recovery (GitHub API 429):**
- What's not tested: GitHub rate limit exhaustion (HTTP 429) and automatic backoff/recovery
- Files: `src/AIAgents.Core/Services/GitHubApiContextService.cs` (file fetching lines 90-105)
- Risk: Codebase analysis fails silently on rate limit; no retry
- Priority: High (impacts parallel analysis scaling)

**Untested Scenario: Concurrent Planning + Coding on Same Story:**
- What's not tested: Race condition if planning agent and coding agent run simultaneously (should not happen, but not prevented)
- Files: `src/AIAgents.Functions/Agents/PlanningAgentService.cs`, `src/AIAgents.Functions/Agents/CodingAgentService.cs`
- Risk: Conflicting ADO updates; story state corruption
- Priority: Medium (unlikely but possible under high concurrency)

**No Integration Tests for Full E2E Pipeline:**
- What's not tested: Complete story flow from creation → planning → coding → testing → review → documentation → deployment
- Files: Multiple agent services; no single test orchestrates full flow
- Risk: Breaking changes in handoff between agents; integration points missed
- Priority: High (critical for overall reliability)

---

*Concerns audit: 2026-03-14*
