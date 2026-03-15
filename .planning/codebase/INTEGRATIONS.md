# External Integrations

**Analysis Date:** 2026-03-14

## APIs & External Services

**AI Completion Providers:**
- Claude (Anthropic) - Primary AI provider for code generation, planning, testing, review, documentation
  - SDK/Client: Custom HTTP client in `src/AIAgents.Core/Services/AIClient.cs`
  - Auth: Bearer token from `AI:ApiKey` environment variable
  - Models: claude-sonnet-4-20250514 (primary), claude-opus-4-20250514, claude-3-5-haiku-latest
  - Endpoint: https://api.anthropic.com (inferred, used via AIClient)

- OpenAI - Alternative AI provider (GPT-4o, GPT-4o-mini)
  - SDK/Client: Custom HTTP client (AIClient handles both OpenAI and Azure OpenAI)
  - Auth: Bearer token from `AI:ProviderKeys:OpenAI:ApiKey`
  - Models: gpt-4o, gpt-4o-mini
  - Endpoint: https://api.openai.com/v1 (configurable via `AI:Endpoint`)

- Google Generative AI - Supported provider
  - SDK/Client: Custom HTTP client
  - Auth: API key from `AI:ProviderKeys:Google:ApiKey`
  - Endpoint: https://generativelanguage.googleapis.com/v1beta/openai

- Azure OpenAI - Supported provider
  - SDK/Client: Custom HTTP client
  - Auth: API key from `AI:Endpoint` and `AI:ApiKey`
  - Models: Deployment-specific (gpt-4o, etc.)

**Azure DevOps Services:**
- Azure DevOps REST API v5.0+ - Work item CRUD, state transitions, field updates
  - SDK/Client: Microsoft.TeamFoundationServer.Client 19.225.1 + Microsoft.VisualStudio.Services.Client
  - Implementation: `src/AIAgents.Core/Services/AzureDevOpsClient.cs`
  - Auth: Personal Access Token (PAT) from `AzureDevOps:Pat`
  - Config: Organization URL, project name from `AzureDevOps:OrganizationUrl`, `AzureDevOps:Project`
  - Operations: GetWorkItem, UpdateWorkItemState, AddComment, CreateWorkItem
  - Service Hook Integration: Webhook endpoint `/api/OrchestratorWebhook` receives work item state changes

**GitHub REST API v3:**
- Repository operations (create PR, push commits, get file content)
  - SDK/Client: Custom HTTP client in `src/AIAgents.Core/Services/GitHubRepositoryProvider.cs` and `GitHubApiContextService.cs`
  - Auth: Bearer token (Personal Access Token or GitHub App) from `GitHub:Token`
  - Config: Repository owner, repo from `GitHub:Owner`, `GitHub:Repo`
  - Operations: CreatePullRequestAsync, FindExistingPullRequestAsync, GetRepositoryContentAsync, PushContentAsync
  - Named HTTP client: "GitHub" (configured in `Program.cs` with 90s timeout)
  - GitHub App support: Detected in settings for advanced scenarios

- GitHub GraphQL API v4 - Used for advanced repository queries
  - Auth: Bearer token same as REST API

- GitHub Webhooks (optional) - Copilot integration
  - Inbound: GitHub Copilot Chat webhook (`Copilot:WebhookSecret` verification)
  - Processing: `IGitHubOrchestrationLauncherService` handles copilot:submit events
  - Configuration: Copilot enabled/disabled via `Copilot:Enabled`, `Copilot:Mode`

## Data Storage

**Databases:**
- Azure Table Storage - Primary database for activity logs, delegation tracking, metadata
  - Connection: `AzureWebJobsStorage` connection string
  - Client: Azure.Data.Tables 12.9.1
  - Tables:
    - `AgentActivity` - Activity log entries with inverted timestamp RowKey for reverse-chronological queries
    - `CopilotDelegations` - Tracks GitHub Copilot delegation requests (partition: org/repo, row: workflow_id)
  - ORM/Client: Table SDK (non-ORM, simple entity operations via `TableClient`)
  - Implementation: `src/AIAgents.Functions/Services/TableStorageActivityLogger.cs`, `TableStorageCopilotDelegationService.cs`

**File Storage:**
- Azure Blob Storage - Package deployment storage
  - Connection: `AzureWebJobsStorage` (same storage account as tables)
  - Use: Azure Functions zip-based deployment (`WEBSITE_RUN_FROM_PACKAGE=1`)
  - Implicit access via Azure Functions platform

- Local filesystem - Temporary git clones, codebase analysis
  - Path: Configured via `Git:LocalBasePath`, defaults to OS temp directory
  - Cleanup: Automatic sweep of stale directories (30+ minutes old)
  - Disk pressure: Emergency sweep if <300 MB free space
  - Implementation: `src/AIAgents.Core/Services/GitOperations.cs` (EnsureBranchAsync method)

**Caching:**
- In-memory singletons - Service state caching (no explicit cache framework)
  - `IStoryContextFactory` - Caches story context per work item
  - `IAIClientFactory` - Caches per-agent AI client instances with merged configuration

## Authentication & Identity

**Azure Services:**
- Azure Managed Identity - Used in production for Azure Table Storage, Application Insights
  - Implicit RBAC on Azure Function's system-assigned identity
  - No credentials in code (uses DefaultAzureCredential pattern)

**Azure DevOps:**
- Personal Access Token (PAT)
  - Env var: `AzureDevOps:Pat`
  - Token format: `[A-Za-z0-9]{52}` (Alternate credentials)
  - Scopes required: Code read/write, Build read/write, Work Item read/write

**GitHub:**
- Personal Access Token (PAT) or GitHub App Token
  - Env var: `GitHub:Token` and `Git:Token`
  - OAuth scopes: `repo` (full control), `workflow` (optional for deployment)
  - Alternative: GitHub App with RSA key (not currently configured)

**Clerk (Authentication):**
- Third-party auth provider for dashboard
  - Env vars: `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY`
  - Implementation: Frontend uses Clerk SDK for user authentication
  - Not integrated with backend (backend is API-only, no user sessions)

**AI Provider Keys:**
- Multi-provider key management
  - Env vars: `AI:ProviderKeys:{Provider}:ApiKey`, `AI:ProviderKeys:{Provider}:Endpoint`
  - Factory auto-resolves: When agent overrides use a different provider (e.g., user selects "gpt-4" for a story), factory looks up key in ProviderKeys config
  - Supported: Claude, OpenAI, Google, Azure OpenAI

## Monitoring & Observability

**Error Tracking & Logging:**
- Application Insights
  - SDK: Microsoft.ApplicationInsights.WorkerService 2.22.0
  - Connection string: `APPLICATIONINSIGHTS_CONNECTION_STRING` or `APPINSIGHTS_INSTRUMENTATIONKEY`
  - Instrumentation key: `dcb12b0e-ecbf-486e-87e1-fd71e770a6a0` (test deployment)
  - Scope: Automatic telemetry for function execution, dependencies, exceptions
  - Custom logging: ILogger integrated (Microsoft.Extensions.Logging.Abstractions)

**Logs:**
- Host.json configuration (`src/AIAgents.Functions/host.json`):
  - Application Insights sampling enabled (excludes Requests)
  - Live metrics filters enabled
  - Default log level: Information
  - Function log level: Information
  - Host.Results: Error (failures only)
  - Host.Aggregator: Trace

- Activity logging to Table Storage:
  - Implementation: `TableStorageActivityLogger` writes to `AgentActivity` table
  - Logged info: Agent name, work item ID, message, token usage, cost, severity level
  - Access: Dashboard queries via Activity Feed endpoint

## CI/CD & Deployment

**Hosting:**
- Azure Functions (Consumption or App Service plan)
  - Runtime: .NET Isolated Worker Process (dotnet-isolated)
  - Version: Azure Functions Worker SDK v2.0.0
  - Timeout: 10 minutes (configurable via `functionTimeout` in host.json)
  - Storage: Queue-based dispatch (agent-tasks, agent-tasks-poison dead-letter queue)

- Azure Static Web Apps (Frontend)
  - Hosts `dashboard/` (built via Vite)
  - Deployment: Automatic via Azure DevOps Pipeline or GitHub Actions

**CI Pipeline:**
- Azure DevOps Pipeline (`adom8-onboarding-pipeline.yml`)
  - Trigger: Manual (bootstrap setup) or schedule-based
  - Stages: Validate, Provision, Deploy Functions, Deploy Dashboard
  - Build: `dotnet publish` for Functions, `npm build` for dashboard
  - Deploy: Zip-based Azure Functions deployment (`WEBSITE_RUN_FROM_PACKAGE=1`)

- GitHub Actions (optional):
  - Deploy workflow: `GitHub:DeployWorkflow` (configurable, default: `deploy.yml`)
  - Triggered by: PR merges, manual dispatch
  - Actions: Build, test, deploy to Azure

**Deployment Configuration:**
- App settings (Azure Portal or environment variables):
  - `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
  - `FUNCTIONS_EXTENSION_VERSION=~4`
  - `WEBSITE_RUN_FROM_PACKAGE=1` (zip deployment)
  - `WEBSITE_CONTENTSHARE=ai-agents-func-{unique-id}`
  - `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING={connection-string}`

## Environment Configuration

**Required Environment Variables (Backend):**

*AI Configuration:*
- `AI__Provider` - "Claude", "OpenAI", "AzureOpenAI", or "Google"
- `AI__Model` - Model name (e.g., "claude-sonnet-4-20250514")
- `AI__ApiKey` - API key for the default provider
- `AI__Endpoint` - API endpoint (optional for OpenAI, required for Azure OpenAI)
- `AI__MaxTokens` - Max output tokens (default: 4096)
- `AI__Temperature` - Sampling temperature (default: 0.3)
- `AI__AgentModels__{Agent}__Model` - Per-agent model override (e.g., `AI__AgentModels__Coding__Model`)
- `AI__AgentModels__{Agent}__Provider` - Per-agent provider override
- `AI__ProviderKeys__{Provider}__ApiKey` - API key for alternate providers

*Azure DevOps:*
- `AzureDevOps__OrganizationUrl` - ADO organization URL (e.g., "https://dev.azure.com/org-name")
- `AzureDevOps__Pat` - Personal Access Token
- `AzureDevOps__Project` - Project name

*GitHub:*
- `GitHub__Owner` - Repository owner
- `GitHub__Repo` - Repository name
- `GitHub__Token` - GitHub PAT or App token
- `Git__RepositoryUrl` - Full clone URL (e.g., "https://github.com/owner/repo.git")
- `Git__Username` - Git username (e.g., "x-token-auth")
- `Git__Token` - Git authentication token
- `Git__Email` - Committer email
- `Git__Name` - Committer name
- `Git__Provider` - "GitHub" or "AzureDevOps"

*Deployment:*
- `Deployment__PipelineName` - Azure DevOps pipeline name
- `Deployment__PipelineId` - Pipeline ID (optional, auto-discovered)
- `Deployment__DefaultAutonomyLevel` - 0-5 (default: 3)
- `Deployment__DefaultMinimumReviewScore` - Minimum code review quality score (0-100, default: 85)
- `Deployment__RequireHealthCheck` - true/false

*Codebase Documentation:*
- `CodebaseDocumentation__MaxFilesToAnalyze` - Max files to scan (default: 10000)
- `CodebaseDocumentation__MaxUserStories` - Max stories to extract (default: 500)
- `CodebaseDocumentation__MaxCommits` - Max commits to analyze (default: 1000)
- `CodebaseDocumentation__DefaultTimeframe` - Time period for analysis (default: "6months")
- `CodebaseDocumentation__OutputFolder` - Output folder for docs (default: ".agent")

*Copilot Integration:*
- `Copilot__Enabled` - true/false (default: false)
- `Copilot__Mode` - "Always" or "OnDemand"
- `Copilot__TimeoutMinutes` - Copilot interaction timeout (default: 60)
- `Copilot__WebhookSecret` - Webhook signature secret for GitHub

*Frontend:*
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` - Clerk publishable key
- `CLERK_SECRET_KEY` - Clerk secret key

*Infrastructure:*
- `AzureWebJobsStorage` - Azure Storage connection string
- `AzureWebJobsDashboard` - Dashboard storage (same as above)
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - Application Insights connection string
- `APPINSIGHTS_INSTRUMENTATIONKEY` - Application Insights instrumentation key

**Secrets Location:**
- Development: `src/AIAgents.Functions/local.settings.json` (git-ignored)
- Production: Azure Key Vault (accessed via Managed Identity) or App Configuration
- Settings override: Environment variables > Key Vault > appsettings.json

## Webhooks & Callbacks

**Incoming Webhooks:**
- Azure DevOps Service Hook (work item state change)
  - Endpoint: `POST /api/OrchestratorWebhook` (Azure Function HTTP trigger)
  - Payload: Work item state change event
  - Validation: Azure DevOps signature verification (hardcoded in function)
  - Queue enqueue: Places agent task in `agent-tasks` queue

- GitHub Webhook (Copilot integration, optional)
  - Endpoint: `POST /api/CopilotWebhook` (if enabled)
  - Payload: GitHub Copilot Chat submit event
  - Validation: HMAC-SHA256 signature verification using `Copilot:WebhookSecret`
  - Processing: Launches orchestration workflow in GitHub Actions

**Outgoing Webhooks/Callbacks:**
- Azure DevOps:
  - Update work item state (via REST API, not webhook)
  - Add comments (via REST API)
  - Create new work items (via REST API)

- GitHub:
  - Create/update pull requests (via REST API)
  - Commit and push to branches (via Git protocol)

- adom8.dev Dashboard (optional SaaS integration):
  - Endpoint: Configurable via `SaaS:CallbackUrl` (if enabled)
  - Payload: Agent execution events (state, tokens, cost, status)
  - Service: `ISaasCallbackService`, configured in `Program.cs`
  - HTTP client: Named "SaasCallback" with 10s timeout
  - Purpose: Real-time dashboard updates for hosted deployments

---

*Integration audit: 2026-03-14*
