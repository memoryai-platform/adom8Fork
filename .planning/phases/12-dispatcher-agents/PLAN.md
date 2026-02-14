# Phase 12: Dispatcher & Agent Services

**Goal:** Queue dispatcher and all 5 agent service implementations

## Files to Create

1. `src/AIAgents.Functions/Functions/AgentTaskDispatcher.cs`
   - Queue trigger on "agent-tasks"
   - Resolve IAgentService via GetRequiredKeyedService
   - Call ProcessAsync, handle errors (log + rethrow for retry)

2. `src/AIAgents.Functions/Services/PlanningAgentService.cs`
   - Clone repo, create story context, build planning prompts
   - Call IAIClient.CompleteAsync with JSON response format
   - Parse PlanningAnalysis, render PLAN/TASKS templates
   - Commit to .ado/stories/US-{id}/, update ADO → "AI Code"

3. `src/AIAgents.Functions/Services/CodingAgentService.cs`
   - Load context from PLAN.md, build coding prompt
   - Generate code, write artifacts, commit
   - Update ADO → "AI Test"

4. `src/AIAgents.Functions/Services/TestingAgentService.cs`
   - Read code artifacts, build testing prompt
   - Generate tests, write test artifacts, commit
   - Update ADO → "AI Review"

5. `src/AIAgents.Functions/Services/ReviewAgentService.cs`
   - Read code, build review prompt, parse CodeReview JSON
   - Render CODE_REVIEW template
   - Score routing: ≥90 → "AI Docs", ≥70 → "Ready for QA", <70 → "Needs Revision"

6. `src/AIAgents.Functions/Services/DocumentationAgentService.cs`
   - Read code + context, build docs prompt
   - Write DOCUMENTATION.md, commit
   - Update ADO → "Ready for QA"

## Common Pattern (all agents)

```
1. Get work item from IAzureDevOpsClient
2. await using var repo = await _gitOps.CloneOrPullAsync()
3. Create IStoryContext via factory
4. Update agent status → "in_progress"
5. [Agent-specific work]
6. Commit and push
7. Update ADO state
8. Update agent status → "completed"
9. Log activity

Error: catch → status "failed" → ADO comment → rethrow
```
