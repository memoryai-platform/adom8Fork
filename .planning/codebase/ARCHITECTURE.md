# Architecture

**Analysis Date:** 2026-03-14

## Pattern Overview

**Overall:** Distributed AI Agent Pipeline with layered service abstraction

**Key Characteristics:**
- Serverless Azure Functions entry points triggering specialized AI agents
- Queue-based task distribution for asynchronous processing
- Provider-agnostic repository abstractions (GitHub/Azure DevOps)
- Keyed dependency injection for agent service routing
- Template-driven artifact generation with Scriban
- Resilient HTTP clients with circuit breaker patterns
- State persisted in Azure Table Storage (per-story state.json)
- Web-based dashboard for real-time monitoring

## Layers

**Presentation Layer:**
- Purpose: Real-time dashboard UI showing agent status, activity feed, and deployment metrics
- Location: `./dashboard/src`
- Contains: React/JSX components, hooks for API communication, styling
- Depends on: Backend API functions (via HTTP)
- Used by: End users monitoring the pipeline

**API/Function Trigger Layer:**
- Purpose: HTTP/Queue entry points that initiate workflows
- Location: `./src/AIAgents.Functions/Functions`
- Contains: Azure Function triggers (HTTP, Queue, Timer-based)
- Depends on: Service layer (agents, orchestration)
- Used by: External services (GitHub webhooks, ADO service hooks), queue-based triggering
- Key functions:
  - `OrchestratorWebhook.cs` - Receives service hook payloads from ADO
  - `AgentTaskDispatcher.cs` - Processes queued agent tasks
  - `CopilotBridgeWebhook.cs` - Handles Copilot completions from adom8.dev
  - `HealthCheck.cs` - System diagnostics endpoint
  - `GetCurrentStatus.cs` - Query current story state
  - `ResumePipeline.cs` - Resume interrupted workflows

**Agent Service Layer:**
- Purpose: Specialized AI agents that execute distinct workflow stages
- Location: `./src/AIAgents.Functions/Agents`
- Contains: Agent implementations (PlanningAgentService, CodingAgentService, TestingAgentService, ReviewAgentService, DocumentationAgentService, DeploymentAgentService, CodebaseDocumentationAgentService)
- Depends on: Core services (IAIClient, IRepositoryProvider, IStoryContextFactory)
- Used by: AgentTaskDispatcher via keyed DI
- Pattern: Each agent implements IAgentService, returns AgentResult with success/error categorization for intelligent retry logic

**Core Service Layer:**
- Purpose: Reusable services for common operations
- Location: `./src/AIAgents.Core/Services`
- Contains:
  - `AIClient.cs` - Low-level AI provider communication (Claude, etc)
  - `AIClientFactory.cs` - Creates AI clients with per-story model overrides
  - `AzureDevOpsClient.cs` - Work item and artifact operations
  - `GitHubApiContextService.cs` - File tree and content access via REST API (no local clone)
  - `GitHubRepositoryProvider.cs` - PR creation and merge via GitHub API
  - `AzureDevOpsRepositoryProvider.cs` - ADO Repos operations
  - `StoryContextFactory.cs` & `StoryContext.cs` - Per-story state management (Table Storage)
  - `ScribanTemplateEngine.cs` - Template rendering for artifacts
  - `CodebaseContextLoader.cs` - Loads .agent/ documentation for prompts
  - `InputValidator.cs` - Security and length validation
  - `TableStorageActivityLogger.cs` - Activity feed persistence
  - `TableStorageCopilotDelegationService.cs` - Copilot completion tracking

**Abstraction Layer (Interfaces):**
- Purpose: Decouple implementations from consumers
- Location: `./src/AIAgents.Core/Interfaces`
- Key abstractions:
  - `IAIClient` - AI completion and agentic tool-use
  - `IRepositoryProvider` - PR/deployment abstraction (GitHub vs ADO)
  - `IStoryContext` - Story state and artifact access
  - `ICodebaseContextProvider` - Codebase documentation loading
  - `IInputValidator` - Input validation
  - `IActivityLogger` - Activity feed logging
  - `IAgentService` - Agent execution contract
  - `IAgentTaskQueue` - Task queueing (Azure Storage Queue)

**Configuration Layer:**
- Purpose: Options patterns for environment-specific settings
- Location: `./src/AIAgents.Core/Configuration`
- Contains:
  - `AIOptions` - AI provider keys, endpoints, model mappings
  - `GitHubOptions` - GitHub token, repo owner/name
  - `AzureDevOpsOptions` - ADO organization, project, PAT
  - `GitOptions` - Provider selection (GitHub/ADO)
  - `DeploymentOptions` - Deployment pipeline triggers
  - `CopilotOptions` - adom8.dev integration settings
  - `SaasOptions` - Real-time callback dashboard settings
  - `InputValidationOptions` - Security constraints

## Data Flow

**Story Processing Pipeline:**

1. **Initiation**
   - User creates/updates ADO work item
   - ADO service hook fires to OrchestratorWebhook
   - Function enqueues AgentTask (work item ID, agent type)

2. **Task Dispatch**
   - AgentTaskDispatcher dequeues message
   - Deserializes AgentTask JSON
   - Uses keyed DI to resolve correct IAgentService

3. **Agent Execution** (Example: Planning Agent)
   - Load work item from ADO API
   - Resolve AI client with per-story model overrides
   - Fetch repository context via GitHub REST API (file tree, targeted file content)
   - Load codebase documentation from `.agent/` directory
   - Call IAIClient.CompleteAsync() with system/user prompts
   - Render PLAN artifact using ScribanTemplateEngine
   - Save story state to Table Storage
   - Return AgentResult (success or categorized error)

4. **Handoff & Continuation**
   - On success: Enqueue next agent task (Coding → Testing → Review → Deployment)
   - On transient error: Dispatcher retries (max 1 attempt, 2-second delay)
   - On permanent error: Enqueue DeadLetterQueueHandler, notify via activity feed

5. **State Persistence**
   - StoryContext persists state.json to Table Storage
   - Includes: current stage, agent statuses, artifacts, decisions, blockers
   - Enables resume from interruption

**Agentic Tool Use (For Coding Agent):**

1. Agent calls `IAIClient.CompleteWithToolsAsync()`
2. AI provider responds with ToolCall requests
3. CodingToolExecutor executes tool (read file, write code, etc)
4. Result fed back to AI in next turn
5. Loop continues until AI ends conversation
6. AgenticResult aggregates all rounds and token usage

**State Management:**

- **Per-Story State:** `./StoryState` in Table Storage (serialized from `StoryState.cs`)
  - CurrentState: Describes workflow stage (e.g., "Story Planning", "AI Code")
  - CurrentStage: Agent stage name (e.g., "Planning", "Coding")
  - Agents: Dictionary of agent statuses (InProgress, Success, Failed)
  - Artifacts: Artifact paths (PLAN.md, CODE.md, TESTS.md, etc)
  - Decisions, Blockers, Questions: User-facing notes
  - TokenUsage: Accumulated tokens across all agents

- **Activity Feed:** Table Storage table tracking all events
  - Logged via IActivityLogger implementations
  - Real-time query for dashboard activity panel

## Key Abstractions

**AgentTask:**
- Purpose: Serializable message representing work to be done
- Example: `{ WorkItemId: 123, AgentType: "Coding", CorrelationId: "abc...", TriggerSource: "OrchestratorWebhook" }`
- Placed on Azure Storage Queue by initiating functions

**AgentResult:**
- Purpose: Structured result enabling intelligent retry logic
- Fields: Success (bool), Category (ErrorCategory), ErrorMessage, Exception, TokensUsed, CostIncurred
- Categories: Transient (retry), ConfigError (fail), DataError (fail), ProviderError (evaluate)
- Patterns:
  ```csharp
  return AgentResult.Ok(tokensUsed: 5000, cost: 0.15m);
  return AgentResult.Fail(ErrorCategory.Transient, "Timeout", ex);
  return AgentResult.Fail(ErrorCategory.ConfigError, "Missing GitHub token");
  ```

**IStoryContext:**
- Purpose: Encapsulates per-story file and state operations
- Implementations: Table Storage-backed in production
- Methods: LoadStateAsync, SaveStateAsync, WriteArtifactAsync, ReadArtifactAsync
- Ensures atomic writes (temp file + move semantics)

**IRepositoryProvider:**
- Purpose: Abstract repository operations
- Implementations: GitHubRepositoryProvider, AzureDevOpsRepositoryProvider
- Methods: CreatePullRequestAsync, MergePullRequestAsync, TriggerDeploymentAsync
- Enables provider switching via configuration

**ToolDefinition & ToolCall:**
- Purpose: Agentic tool use abstraction for AI
- ToolDefinition: Schema describing available function to AI
- ToolCall: AI's request to invoke a tool
- CodingToolExecutor implements actual tool operations (file read/write)

## Entry Points

**OrchestratorWebhook:**
- Location: `./src/AIAgents.Functions/Functions/OrchestratorWebhook.cs`
- Triggers: HTTP POST from ADO service hook
- Responsibilities: Deserialize work item payload, enqueue AgentTask for Planning agent
- Returns: HTTP 200 if queued, 400/500 for errors

**AgentTaskDispatcher:**
- Location: `./src/AIAgents.Functions/Functions/AgentTaskDispatcher.cs`
- Triggers: Azure Storage Queue (agent-tasks)
- Responsibilities: Dequeue AgentTask, resolve and execute correct IAgentService, handle AgentResult, enqueue downstream tasks or DeadLetterQueueHandler
- Returns: Task completion (async)

**CopilotBridgeWebhook:**
- Location: `./src/AIAgents.Functions/Functions/CopilotBridgeWebhook.cs`
- Triggers: HTTP POST from adom8.dev (Copilot completion callback)
- Responsibilities: Receive completion result, record in CopilotDelegationService, resume pipeline
- Returns: HTTP 200

**CopilotTimeoutChecker (Timer):**
- Location: `./src/AIAgents.Functions/Functions/CopilotTimeoutChecker.cs`
- Triggers: Timer (configurable interval, e.g., every 5 minutes)
- Responsibilities: Check for stalled Copilot delegations, timeout/fail if exceeded
- Returns: Task completion

**Health/Status Endpoints (HTTP GET):**
- `HealthCheck.cs` - System diagnostics
- `GetCurrentStatus.cs` - Query story state by work item ID
- `GetPendingCode.cs` - Retrieve generated code artifact

## Error Handling

**Strategy:** Categorized failures with retry logic in dispatcher

**Patterns:**
- **Transient:** Network timeout, rate limit — dispatcher retries
- **ConfigError:** Missing environment variable, invalid AI key — fail permanently, alert operator
- **DataError:** Missing work item, invalid artifact — fail permanently, log for investigation
- **ProviderError:** GitHub API 500, ADO service outage — retry with exponential backoff

**Implementation:**
```csharp
// Agent detects transient error
if (ex is HttpRequestException && ex.InnerException is TimeoutException)
{
    return AgentResult.Fail(ErrorCategory.Transient, "Request timeout", ex);
}

// Dispatcher receives result
if (!result.Success && result.Category == ErrorCategory.Transient)
{
    // Requeue task
    await _agentTaskQueue.EnqueueAsync(task);
}
else if (!result.Success)
{
    // Route to DeadLetterQueueHandler
    await _agentTaskQueue.EnqueueDeadLetterAsync(task, result.ErrorMessage);
}
```

## Cross-Cutting Concerns

**Logging:** Structured logging via ILogger<T>
- Leverages Application Insights for centralized telemetry
- Each agent logs entry/exit with work item ID and correlation ID
- Service Fabric auto-enriches with request context

**Validation:** Multi-layer input validation via IInputValidator
- Length limits (prompt max 4000 chars, code max 10000 lines)
- Prompt injection detection (regex patterns for common injection attempts)
- Schema validation for JSON inputs (AgentTask, ServiceHookPayload)
- Called in OrchestratorWebhook before queueing

**Authentication:**
- AI providers: Per-AgentType API key from AIOptions configuration
- GitHub: Bearer token in HTTP client headers (set in Program.cs)
- ADO: PAT in HTTP Authorization header (set in AzureDevOpsClient)
- SaaS Dashboard: API key validation before callback acceptance

**Rate Limiting:** Resilience pipeline in HTTP client
- Circuit breaker: Opens after 5 consecutive failures, 30-second recovery
- Retry: 1 attempt with 2-second delay
- Timeout: 300 seconds per request, 9 minutes total
- Applied to AIClient and GitHub API clients

---

*Architecture analysis: 2026-03-14*
