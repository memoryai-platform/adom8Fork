# Codebase Structure

**Analysis Date:** 2026-03-14

## Directory Layout

```
C:\ADO-Agent\ADO-Agent/
├── src/                          # Primary source code (.NET C# projects)
│   ├── AIAgents.sln             # Visual Studio solution file
│   ├── AIAgents.Core/           # Shared abstractions and services
│   ├── AIAgents.Core.Tests/     # Core layer unit tests
│   ├── AIAgents.Functions/      # Azure Functions and agents
│   └── AIAgents.Functions.Tests/ # Function and agent tests
├── dashboard/                    # React-based monitoring UI
│   ├── src/                     # React components and hooks
│   ├── dist/                    # Built/compiled output
│   └── package.json             # Node dependencies
├── docs/                        # User-facing documentation
├── scripts/                     # Automation and build scripts
├── infrastructure/              # Azure deployment templates
├── setup/                       # Initial setup automation
├── .ado/                        # ADO work item tracking
│   └── stories/                # Per-story artifacts (US-NNN directories)
├── .adom8/                      # SaaS integration config
├── .agent/                      # Codebase documentation for AI prompts
└── .planning/                   # GSD orchestrator artifacts
```

## Directory Purposes

**src/**
- Purpose: All C# source code for the AI agent pipeline
- Contains: Solution file, four projects (Core, Core.Tests, Functions, Functions.Tests)
- Key files: `AIAgents.sln`, `Program.cs` (Functions entrypoint)

**src/AIAgents.Core/**
- Purpose: Shared abstractions, models, and reusable services
- Contains: Interfaces, implementations, configuration, models, telemetry
- Does not take Azure Functions dependency — pure library
- Consumed by: AIAgents.Functions, tests

**src/AIAgents.Core/Interfaces/**
- Purpose: Service contracts that decouple implementations
- Contains: 15+ interface definitions
- Key interfaces: `IAIClient`, `IRepositoryProvider`, `IStoryContext`, `IAgentService`, `ICodebaseContextProvider`, `IInputValidator`

**src/AIAgents.Core/Services/**
- Purpose: Implementations of core interfaces
- Contains: 14 service implementations
- Examples:
  - `AIClient.cs` - Claude API communication
  - `GitHubApiContextService.cs` - Repository file access
  - `StoryContext.cs` - Per-story state persistence
  - `InputValidator.cs` - Security validation

**src/AIAgents.Core/Models/**
- Purpose: Data transfer objects (DTOs) and domain models
- Contains: 20+ model classes
- Key models:
  - `StoryState.cs` - Story workflow state (persisted to Table Storage)
  - `AgentStatus.cs` - Status of individual agents
  - `PlanningResult.cs`, `CodingModels.cs` - Agent-specific output
  - `CodebaseAnalysis.cs` - Codebase intelligence results

**src/AIAgents.Core/Configuration/**
- Purpose: Options patterns for environment-specific settings
- Contains: 12 configuration classes (AIOptions, GitHubOptions, etc)
- Bound in `Program.cs` via `services.Configure<T>(configuration.GetSection(...))`

**src/AIAgents.Core/Constants/**
- Purpose: Application-level constants
- Contains: `AIPipelineNames.cs` (agent names), `CustomFieldNames.cs` (ADO field mappings)

**src/AIAgents.Core/Telemetry/**
- Purpose: Observability infrastructure
- Contains: Application Insights configuration, structured logging helpers

**src/AIAgents.Core/Templates/**
- Purpose: Scriban template files for artifact generation
- Files:
  - `PLAN.template.md` - Planning agent output format
  - `CODE_REVIEW.template.md` - Review agent output
  - `TEST_PLAN.template.md` - Testing agent output
  - `TASKS.template.md` - Subtask list
  - `DOCUMENTATION.template.md` - Doc generation
  - `state.schema.json` - JSON schema for StoryState validation

**src/AIAgents.Functions/**
- Purpose: Azure Function entry points and agent implementations
- Contains: Function triggers, agent services, models, services
- Single `Program.cs` for all function triggers (uses HostBuilder pattern)

**src/AIAgents.Functions/Functions/**
- Purpose: Azure Function triggers responding to specific events
- Contains: 15+ function classes, each with [Function("name")] attribute
- Key functions:
  - `OrchestratorWebhook.cs` - ADO service hook entry point
  - `AgentTaskDispatcher.cs` - Queue trigger for agent execution
  - `CopilotBridgeWebhook.cs` - Copilot completion callback
  - `HealthCheck.cs`, `GetCurrentStatus.cs` - Query endpoints

**src/AIAgents.Functions/Agents/**
- Purpose: Specialized AI agents for each workflow stage
- Contains: 7 agent service implementations
- Each implements `IAgentService` interface
- Examples:
  - `PlanningAgentService.cs` - Creates implementation plan (Stage 1)
  - `CodingAgentService.cs` - Generates code via agentic tool use (Stage 2)
  - `TestingAgentService.cs` - Creates test cases (Stage 3)
  - `ReviewAgentService.cs` - Code review (Stage 4)
  - `DocumentationAgentService.cs` - Generates docs (Stage 5)
  - `DeploymentAgentService.cs` - Triggers deployment (Stage 6)
  - `CodebaseDocumentationAgentService.cs` - Updates .agent/ docs

**src/AIAgents.Functions/Services/**
- Purpose: Functions-specific services
- Contains:
  - `IAgentService.cs` - Agent execution contract
  - `IAgentTaskQueue.cs` - Queue abstraction
  - `IActivityLogger.cs` - Activity feed logging
  - `TableStorageActivityLogger.cs` - Implementation
  - `CopilotDelegationService.cs` - Copilot tracking
  - `SaasCallbackService.cs` - adom8.dev integration

**src/AIAgents.Functions/Models/**
- Purpose: Functions-specific DTOs
- Contains:
  - `AgentTask.cs` - Queue message format
  - `AgentResult.cs` - Agent result with error categorization
  - `AgentType.cs` - Enum of agent types (Planning, Coding, etc)
  - `ErrorCategory.cs` - Error classification for retry logic

**src/AIAgents.Core.Tests/**
- Purpose: Unit tests for Core layer
- Contains: Test classes matching Core services
- Subdirectories: `Models/`, `Services/`

**src/AIAgents.Functions.Tests/**
- Purpose: Unit tests for Functions layer
- Contains: Agent tests, function tests, service tests
- Subdirectories: `Agents/`, `Functions/`, `Services/`, `Models/`, `Helpers/`

**dashboard/**
- Purpose: React-based real-time monitoring UI
- Contains: React components, styling, API hooks
- Key files:
  - `src/App.jsx` - Root component with layout
  - `src/api.js` - API client for backend calls
  - `src/components/` - UI components (AgentActivityFeed, StoryQueueTable, etc)
  - `src/hooks/` - Custom hooks (useAgentStatus, useSystemHealth, etc)
  - `src/config.js` - Environment-specific backend URL
- Build: Vite-based (package.json scripts: dev, build, preview)
- Output: Deployed to Azure Static Web Apps

**.ado/stories/**
- Purpose: Per-story work item artifacts (created at runtime)
- Structure: `.ado/stories/US-{workItemId}/`
- Contains:
  - `state.json` - Story state (persisted from StoryState model)
  - `documents/` - Attachment directory
  - PLAN.md, CODE.md, TESTS.md, REVIEW.md, DOCS.md - Generated artifacts (also in Table Storage)

**.agent/**
- Purpose: Codebase documentation for AI prompts
- Contains: Markdown files describing codebase structure, conventions, architecture
- Loaded by: `CodebaseContextLoader.cs`
- Used in: Agent system prompts for context

**docs/user-stories/**
- Purpose: Feature specifications and user stories
- Contains: Markdown documents describing planned work

**infrastructure/**
- Purpose: Azure infrastructure-as-code (ARM templates, Bicep, Terraform, etc)
- Contains: Templates for Functions, Static Web Apps, Storage, etc

**scripts/**
- Purpose: Automation scripts for setup, deployment, debugging
- Contains: PowerShell, bash scripts

## Key File Locations

**Entry Points:**
- `./src/AIAgents.Functions/Program.cs` - Function app startup (builds host, configures DI, registers services)
- `./src/AIAgents.Functions/Functions/OrchestratorWebhook.cs` - HTTP entry point from ADO
- `./dashboard/src/main.jsx` - React app entry point

**Configuration:**
- `./src/AIAgents.Functions/Program.cs` - Service registration and Options binding
- `./src/AIAgents.Core/Configuration/*.cs` - Configuration classes
- `./dashboard/src/config.js` - Backend URL configuration

**Core Logic:**
- `./src/AIAgents.Core/Services/AIClient.cs` - AI provider communication
- `./src/AIAgents.Functions/Agents/PlanningAgentService.cs` - Example agent implementation
- `./src/AIAgents.Core/Services/GitHubApiContextService.cs` - Repository access

**Data Models:**
- `./src/AIAgents.Core/Models/StoryState.cs` - Central state model
- `./src/AIAgents.Functions/Models/AgentTask.cs` - Queue message format
- `./src/AIAgents.Functions/Models/AgentResult.cs` - Agent result format

**Testing:**
- `./src/AIAgents.Core.Tests/` - Core layer tests
- `./src/AIAgents.Functions.Tests/` - Functions and agent tests

## Naming Conventions

**Files:**
- C# source files: PascalCase matching class name (e.g., `AIClient.cs`)
- Interfaces: Prefix with I (e.g., `IAIClient.cs`)
- Test files: `[ClassName]Tests.cs` (e.g., `AIClientTests.cs`)
- Templates: UPPERCASE with `.template.md` extension (e.g., `PLAN.template.md`)

**Directories:**
- C# namespaces: PascalCase matching directory structure (e.g., `AIAgents.Core.Services`)
- Feature directories: PascalCase (e.g., `Configuration`, `Interfaces`, `Services`)
- Test directories: Match source structure with "Tests" suffix

**Classes & Interfaces:**
- Service implementations: `[ServiceName]Service` (e.g., `PlanningAgentService`)
- Interfaces: `I[ServiceName]` (e.g., `IAgentService`)
- Models: Descriptive nouns (e.g., `StoryState`, `AgentResult`, `CodebaseAnalysis`)
- Enums: PascalCase singular (e.g., `AgentType`, `ErrorCategory`)

**Methods:**
- Async methods: `*Async` suffix (e.g., `ExecuteAsync`, `CompleteAsync`)
- Query methods: `Get*` prefix (e.g., `GetClientForAgent`)
- Factory methods: `Create*` prefix (e.g., `CreateClient`)
- Converters: `To*` or `From*` (e.g., `ToAgentResult`)

**Variables & Parameters:**
- Private fields: `_camelCase` prefix (e.g., `_logger`, `_aiClient`)
- Local variables: `camelCase` (e.g., `workItem`, `storyState`)
- Constants: `UPPER_SNAKE_CASE` (in Constants/*.cs files)

## Where to Add New Code

**New Agent:**
1. Create interface inheriting `IAgentService` in `./src/AIAgents.Functions/Services/I[AgentName]AgentService.cs` (if custom interface needed)
2. Implement agent class in `./src/AIAgents.Functions/Agents/[AgentName]AgentService.cs`
3. Register in `Program.cs`: `services.AddKeyedScoped<IAgentService, [AgentName]AgentService>("[AgentName]");`
4. Add enum value to `./src/AIAgents.Functions/Models/AgentType.cs`
5. Add agent to dispatcher logic in `AgentTaskDispatcher.cs` (if needed)
6. Create tests in `./src/AIAgents.Functions.Tests/Agents/[AgentName]AgentServiceTests.cs`

**New Function Trigger:**
1. Create function class in `./src/AIAgents.Functions/Functions/[TriggerName].cs`
2. Decorate with appropriate trigger attribute (`[Function(...)]` with `HttpTrigger`, `QueueTrigger`, or `TimerTrigger`)
3. Inject required services via constructor
4. Implementation calls service layer methods
5. Add tests in `./src/AIAgents.Functions.Tests/Functions/[TriggerName]Tests.cs`

**New Service:**
1. Interface in `./src/AIAgents.Core/Interfaces/I[ServiceName].cs`
2. Implementation in `./src/AIAgents.Core/Services/[ServiceName].cs`
3. Register in `Program.cs` using appropriate lifetime (`AddSingleton` for stateless, `AddScoped` for per-request)
4. Add unit tests in `./src/AIAgents.Core.Tests/Services/[ServiceName]Tests.cs`

**New Model/DTO:**
1. Add to `./src/AIAgents.Core/Models/` or `./src/AIAgents.Functions/Models/`
2. Use `[JsonPropertyName(...)]` attributes for JSON serialization
3. Implement equality members if needed for testing

**Dashboard Component:**
1. New component in `./dashboard/src/components/[ComponentName].jsx`
2. Custom hook in `./dashboard/src/hooks/use[HookName].js` if fetching data
3. Styling via Tailwind classes (configured in `tailwind.config.js`)
4. Import and use in parent component or `App.jsx`

**Configuration:**
1. Create class in `./src/AIAgents.Core/Configuration/[OptionName].cs`
2. Define public properties and `public const string SectionName`
3. Register in `Program.cs`: `services.Configure<[OptionName]>(configuration.GetSection([OptionName].SectionName));`
4. Access via `IOptions<[OptionName]>` in dependent services

**Utilities:**
- Shared extension methods: `./src/AIAgents.Core/` (no specific subdirectory for extensions yet)
- Template files: `./src/AIAgents.Core/Templates/`

## Special Directories

**`.ado/stories/`**
- Purpose: Per-work-item storage of story state and artifacts
- Generated: Yes (created at runtime by StoryContext)
- Committed: No (local artifacts only, state persisted to Table Storage)
- Structure: One directory per story (e.g., `.ado/stories/US-123/state.json`)

**`.agent/`**
- Purpose: Codebase documentation for AI prompts
- Generated: No (manually created and maintained)
- Committed: Yes (part of repository, loaded by CodebaseContextLoader)
- Contents: Architecture docs, conventions, patterns (enable AI to follow project patterns)

**`./src/bin/` and `./src/obj/`**
- Purpose: Build artifacts (compiled assemblies, temp files)
- Generated: Yes (by .NET build system)
- Committed: No (in .gitignore)

**`./dashboard/node_modules/`**
- Purpose: NPM dependencies
- Generated: Yes (by `npm install`)
- Committed: No (in .gitignore)

**`./dashboard/dist/`**
- Purpose: Built React application
- Generated: Yes (by `npm run build`)
- Committed: No (built on deployment)

**`.planning/`**
- Purpose: GSD orchestrator planning artifacts
- Generated: Yes (by GSD commands)
- Committed: No (local planning workspace)
- Contains: Phase plans, codebase analysis documents

---

*Structure analysis: 2026-03-14*
