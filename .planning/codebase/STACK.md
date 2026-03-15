# Technology Stack

**Analysis Date:** 2026-03-14

## Languages

**Primary:**
- C# 12 (net8.0) - Backend services, Azure Functions, core logic (`src/AIAgents.Core/`, `src/AIAgents.Functions/`)
- JavaScript/JSX (ES2022) - Frontend dashboard (`dashboard/src/`)

**Secondary:**
- JSON - Configuration and manifest files
- YAML - GitHub Actions, Azure Pipelines
- Markdown - Templates, documentation

## Runtime

**Environment:**
- .NET 8.0 (net8.0) - Primary runtime for all backend services
- Node.js (v18+) - Frontend build tooling only, not runtime deployment

**Package Manager:**
- NuGet (.NET packages)
  - Lockfile: Implicit in `.csproj` files, restored via NuGet
- npm (JavaScript packages)
  - Lockfile: `package-lock.json` present in `dashboard/`

## Frameworks

**Core (.NET):**
- Azure Functions Worker SDK v2.0.0 - Serverless compute runtime (`src/AIAgents.Functions/`)
- ASP.NET Core 8.0 - HTTP handling via `Microsoft.AspNetCore.App` framework reference
- Microsoft Extensions (Configuration, DependencyInjection, Logging, Http, Options) v8.0+ - Standard .NET ecosystem

**Frontend:**
- React 18.3.1 - UI framework (`dashboard/package.json`)
- React Router 6.30.1 - Client-side routing
- Recharts 2.15.4 - Data visualization (dashboard charts)

**Testing:**
- xUnit 2.9.2 - .NET test runner (`src/AIAgents.Core.Tests/`, `src/AIAgents.Functions.Tests/`)
- Moq 4.20.72 - .NET mocking framework
- coverlet.collector 6.0.2 - Code coverage collection

**Build/Dev:**
- Vite 6.2.2 - Frontend bundler and dev server (`dashboard/`)
- Vitejs/plugin-react 4.4.1 - React plugin for Vite
- Tailwind CSS 4.1.1 - CSS utility framework with @tailwindcss/forms 0.5.10 and @tailwindcss/postcss 4.1.1
- PostCSS 4.1.1 - CSS transformation (via Tailwind)

## Key Dependencies

**Critical (.NET):**
- Microsoft.TeamFoundationServer.Client 19.225.1 - Azure DevOps REST API client (`src/AIAgents.Core/Services/AzureDevOpsClient.cs`)
- Microsoft.VisualStudio.Services.Client 19.225.1 - Azure DevOps SDK
- LibGit2Sharp 0.30.0 - Git operations (clone, commit, push) (`src/AIAgents.Core/Services/GitOperations.cs`)
- System.Text.Json 8.0.5 - JSON serialization (built-in to .NET 8.0)
- Azure.Data.Tables 12.9.1 - Azure Table Storage for activity logging and delegation tracking (`src/AIAgents.Functions/Services/TableStorageActivityLogger.cs`)

**Infrastructure (.NET):**
- Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore 2.0.0 - HTTP trigger support
- Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues 5.5.0 - Azure Storage Queue triggers
- Microsoft.Azure.Functions.Worker.Extensions.Timer 4.3.1 - Timer triggers (dead-letter queue handler)
- Microsoft.Extensions.Http.Resilience 8.10.0 - Resilience patterns (circuit breaker, retry) for HTTP calls

**Observability (.NET):**
- Microsoft.ApplicationInsights.WorkerService 2.22.0 - Application Insights integration
- Microsoft.Azure.Functions.Worker.ApplicationInsights 2.0.0 - Azure Functions AI instrumentation

**Templating:**
- Scriban 5.12.0 - Template engine for code generation (`src/AIAgents.Core/Services/ScribanTemplateEngine.cs`, embedded templates in `src/AIAgents.Core/Templates/`)

**Frontend:**
- date-fns 4.1.0 - Date formatting and manipulation
- react-dom 18.3.1 - React DOM renderer

## Configuration

**Environment (.NET):**
- Configuration sources (in order of precedence):
  1. `local.settings.json` - Local development settings (not committed, git-ignored)
  2. `host.json` - Azure Functions host configuration (`src/AIAgents.Functions/host.json`)
  3. Azure Key Vault - Production secrets (via Azure Functions managed identity)
  4. Environment variables - Configuration values

- Key configuration sections bound to `IOptions<T>`:
  - `AI` → `AIOptions` - AI provider, model, API key, per-agent overrides, model tiers
  - `AzureDevOps` → `AzureDevOpsOptions` - ADO organization URL, PAT token, project name
  - `Git` → `GitOptions` - Repository URL, username, token, email, name
  - `GitHub` → `GitHubOptions` - Repository owner, repo name, token
  - `Deployment` → `DeploymentOptions` - Pipeline name/ID, autonomy level, review thresholds
  - `CodebaseDocumentation` → `CodebaseDocumentationOptions` - File/story/commit limits, timeframe, output folder
  - `RepositoryCapacity` → `RepositoryCapacityOptions` - Sizing constraints
  - `Copilot` → `CopilotOptions` - Enabled flag, mode, timeout, webhook secret
  - `SaaS` → `SaasOptions` - adom8.dev dashboard callback integration
  - `InputValidation` → `InputValidationOptions` - Length limits, prompt injection detection

**Build (.NET):**
- Solution file: `src/AIAgents.sln` - Defines 4 projects: Core, Functions, Core.Tests, Functions.Tests
- Project files: `.csproj` with target framework, package references, embedded resources
- MSBuild: Standard .NET build system (implicit)

**Build (Frontend):**
- Vite config: `dashboard/vite.config.js` - Simple React plugin setup, `dist/` output
- Tailwind config: `dashboard/tailwind.config.js` - Purge HTML/JSX files
- PostCSS implicit (via Tailwind `@tailwindcss/postcss` v4)

## Platform Requirements

**Development:**
- .NET 8.0 SDK (targeting net8.0)
- Node.js 18+ (for frontend npm install)
- Git 2.30+ (for cloning, committing, pushing)
- Azure CLI (for local Azure Functions runtime testing)
- Azure Functions Core Tools v4 (for `func` CLI)
- Visual Studio 2022 or VS Code with C# Dev Kit
- PowerShell 7+ (for setup scripts)

**Production:**
- **Backend Deployment:** Azure Functions (Consumption or Dedicated plan)
  - Requires Azure subscription
  - Requires Azure Storage account (for Storage Queues, Table Storage, blob storage for package)
  - Requires Application Insights resource (monitoring/telemetry)
  - Deployed via `WEBSITE_RUN_FROM_PACKAGE=1` (zip-based deployment)

- **Frontend Deployment:** Azure Static Web Apps (SPA) or blob storage with CDN
  - `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` environment variable for auth UI

- **Version Control:**
  - GitHub (adom8 repository) or Azure DevOps Repos
  - Git clone/push via token-based auth

- **Integrations:**
  - Azure DevOps Services (REST API v5.0+)
  - GitHub REST API v3 (or GraphQL v4)
  - Claude API (Anthropic) or OpenAI API
  - Clerk (authentication/identity)

## Runtime Configuration at Startup

**Azure Functions (`src/AIAgents.Functions/Program.cs`):**
1. Load configuration from `host.json` and environment variables
2. Register Application Insights telemetry (with error handling for local dev)
3. Configure named HTTP clients:
   - `AIClient` - AI provider calls (Claude/OpenAI/Google) with resilience policy
   - `GitHub` - GitHub REST API calls (90s timeout, bearer token auth)
   - `SaasCallback` - Optional adom8.dev dashboard integration (10s timeout)
4. Register core services (singletons):
   - `IAIClient`, `IAIClientFactory` - AI completion
   - `IAzureDevOpsClient` - ADO work item CRUD
   - `IGitOperations` - Local git operations
   - `IStoryContextFactory`, `ITemplateEngine` - Story context and templating
5. Register repository provider based on `Git:Provider` config:
   - GitHub path: `GitHubRepositoryProvider`, `GitHubRepositorySizingService`, `GitHubCodebaseOnboardingService`
   - Azure DevOps path: `AzureDevOpsRepositoryProvider`, no-op sizing/onboarding
6. Register infrastructure services:
   - `IActivityLogger` → `TableStorageActivityLogger` - Azure Table Storage logging
   - `IAgentTaskQueue` → `AgentTaskQueue` - Azure Storage Queue abstraction
   - `IGitHubApiContextService` - Stateless GitHub API file tree/content operations
   - `ICopilotDelegationService` → `TableStorageCopilotDelegationService` - GitHub Copilot delegation tracking
7. Register agent services via keyed DI (resolved by dispatcher based on task type)
8. Start Azure Functions worker host

---

*Stack analysis: 2026-03-14*
