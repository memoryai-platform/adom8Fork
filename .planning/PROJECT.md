# AI Development Agents — Project Context

## What This Is

An AI-powered development workflow automation system for Azure DevOps. When a work item state changes (e.g., "Story Planning"), Azure Functions agents autonomously analyze, code, test, review, and document the work — then push changes to Git and advance the work item through the pipeline.

**The killer demo:** Create a vague, typo-filled user story like "Progres bars on dashbord dont work" → watch 5 AI agents fix the dashboard bugs in under 3 minutes → auto-deploy the fix.

## Architecture

```
Azure DevOps Service Hook
  → OrchestratorWebhook (HTTP Function)
    → "agent-tasks" Storage Queue
      → AgentTaskDispatcher (Queue Function)
        → Keyed IAgentService (Planning/Coding/Testing/Review/Docs)
          → IAIClient.CompleteAsync() (Claude/OpenAI/Azure OpenAI)
          → IGitOperations (LibGit2Sharp clone/commit/push)
          → IAzureDevOpsClient (update work items)
          → IStoryContext (.ado/stories/US-{id}/ state tracking)
```

**State Machine:**
Story Planning → AI Code → AI Test → AI Review → AI Docs (if score ≥90) → Ready for QA

## Tech Stack

- **Runtime:** .NET 8 isolated worker Azure Functions
- **AI:** Multi-provider (Claude, OpenAI, Azure OpenAI) via thin IAIClient
- **Git:** LibGit2Sharp for clone/commit/push
- **ADO:** Microsoft.TeamFoundationServer.Client SDK
- **Templates:** Scriban (mature .NET template engine)
- **State:** File-based state.json with atomic writes
- **Activity Log:** Azure Table Storage (Azure.Data.Tables SDK)
- **Dashboard:** Static HTML/CSS/JS on Azure Static Web App
- **Infrastructure:** Terraform (AzureRM ~>3.0)
- **CI/CD:** GitHub Actions
- **Resilience:** Microsoft.Extensions.Http.Resilience (built-in Polly v8)

## Requirements

### Validated (must have)
- Queue-based architecture — no HTTP timeouts, infinite scalability
- Multi-provider AI support — configurable via IOptions<AIOptions>
- Git automation — clone, branch, commit, push per work item
- Story workspace — .ado/stories/US-{id}/ folder with state.json
- ADO integration — read work items, update state, post comments
- Template engine — Scriban for markdown generation
- Dashboard — real-time polling of agent status API
- TWO intentional dashboard bugs — progress bars (return 0), completed color (blue not green)

### Active (building now)
- 5 agent services: Planning, Coding, Testing, Review, Documentation
- Single dispatcher function with keyed DI resolution
- Activity logging to Azure Table Storage
- GetCurrentStatus API for dashboard
- GitHub Actions CI/CD

### Out of Scope
- PR creation (interface omitted until properly implemented)
- Azure Service Bus (using Storage Queues for simplicity)
- Multi-repo support
- Authentication on dashboard (anonymous for demo)
- Unit tests for the agent system itself

## Key Decisions

| # | Decision | Rationale | Date |
|---|----------|-----------|------|
| 1 | Thin IAIClient (single CompleteAsync) | SRP — agents own prompt engineering, easy to add providers | 2025-02-13 |
| 2 | Single dispatcher + keyed DI | Storage Queues have no message filtering — avoids wasted dequeues | 2025-02-13 |
| 3 | Scriban over custom regex engine | Mature, handles escaping/caching/conditionals/loops | 2025-02-13 |
| 4 | File-based state.json with atomic writes | Sequential pipeline guarantees no concurrent writes | 2025-02-13 |
| 5 | Activity log in Table Storage | Dashboard needs fast reads without cloning repos | 2025-02-13 |
| 6 | IOptions<T> over raw IConfiguration | Type-safe, validated at startup, standard .NET 8 | 2025-02-13 |
| 7 | StoryWorkItem over WorkItem | Avoids namespace collision with ADO SDK | 2025-02-13 |
| 8 | ClonedRepository : IAsyncDisposable | Auto cleanup prevents disk exhaustion on consumption plan | 2025-02-13 |
| 9 | Omit stub methods from interfaces | No silent no-ops — interfaces only promise what's implemented | 2025-02-13 |
| 10 | ScriptObject with UPPERCASE keys in Scriban | Template variables stay {{ WORK_ITEM_ID }} matching spec intent | 2025-02-13 |
| 11 | FunctionsApplication.CreateBuilder pattern | .NET 8 isolated worker recommended approach | 2025-02-13 |
| 12 | AddStandardResilienceHandler with 60s timeout | AI API calls take 30-60s, need custom attempt timeout | 2025-02-13 |
| 13 | App Insights registered before resilience handlers | Known SDK ordering requirement to prevent telemetry loss | 2025-02-13 |

## Repository

- **Remote:** https://github.com/toddpick/ADO-Agent.git
- **Branch:** main
- **Workspace:** c:\ADO-Agent\ADO-Agent
