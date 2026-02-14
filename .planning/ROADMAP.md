# AI Development Agents — Roadmap

## Overview

Build a complete AI-powered development workflow automation system for Azure DevOps. 16 phases from infrastructure to validation. Queue-based C# Azure Functions, multi-provider AI, Git automation, live monitoring dashboard.

## Phases

- [x] Phase 1: Project Scaffold
- [x] Phase 2: Terraform Infrastructure
- [x] Phase 3: Solution & Project Files
- [x] Phase 4: Configuration Classes
- [x] Phase 5: Core Interfaces
- [x] Phase 6: Core Models
- [x] Phase 7: Core Service Implementations
- [x] Phase 8: Scriban Templates
- [x] Phase 9: Function Models
- [x] Phase 10: DI & Function Config
- [x] Phase 11: HTTP Trigger Functions
- [x] Phase 12: Dispatcher & Agent Services
- [x] Phase 13: Activity Logging Service
- [x] Phase 14: Dashboard
- [x] Phase 15: CI/CD Workflows
- [x] Phase 16: Build Validation

---

### Phase 1: Project Scaffold
**Goal:** Create full directory tree plus root config files (.gitignore, README.md, DEMO_GUIDE.md, SETUP.md)
**Depends on:** Nothing
**Files:** .gitignore, README.md, DEMO_GUIDE.md, SETUP.md, all directories

### Phase 2: Terraform Infrastructure
**Goal:** All 7 Terraform files for Azure resources (Functions, Storage, Queues, Static Web App)
**Depends on:** Phase 1
**Files:** infrastructure/main.tf, variables.tf, storage.tf, functions.tf, static-web-app.tf, outputs.tf, terraform.tfvars.example

### Phase 3: Solution & Project Files
**Goal:** .sln file and both .csproj files with all package references
**Depends on:** Phase 1
**Files:** src/AIAgents.sln, src/AIAgents.Core/AIAgents.Core.csproj, src/AIAgents.Functions/AIAgents.Functions.csproj

### Phase 4: Configuration Classes
**Goal:** Strongly-typed IOptions<T> configuration classes
**Depends on:** Phase 3
**Files:** src/AIAgents.Core/Configuration/AIOptions.cs, AzureDevOpsOptions.cs, GitOptions.cs

### Phase 5: Core Interfaces
**Goal:** Clean interface contracts for all services
**Depends on:** Phase 3
**Files:** IAIClient.cs, IAzureDevOpsClient.cs, IGitOperations.cs, IStoryContext.cs, IStoryContextFactory.cs, ITemplateEngine.cs

### Phase 6: Core Models
**Goal:** All data models, records, and enums
**Depends on:** Phase 3
**Files:** StoryWorkItem.cs, PlanningAnalysis.cs, CodeReview.cs, ReviewIssue.cs, StoryState.cs, AgentStatus.cs, Decision.cs, Question.cs, ArtifactType.cs, ClonedRepository.cs

### Phase 7: Core Service Implementations
**Goal:** All service classes implementing core interfaces
**Depends on:** Phase 4, 5, 6
**Files:** ClaudeClient.cs, OpenAIClient.cs, AIResponseParser.cs, AzureDevOpsClient.cs, GitOperations.cs, StoryContext.cs, StoryContextFactory.cs, TemplateEngine.cs

### Phase 8: Scriban Templates
**Goal:** All markdown templates in Scriban syntax, plus state schema
**Depends on:** Phase 1
**Files:** 8 template files in src/AIAgents.Core/Templates/ and .ado/templates/

### Phase 9: Function Models
**Goal:** Queue message models and HTTP payload/response models
**Depends on:** Phase 3
**Files:** AgentTask.cs, AgentType.cs, ServiceHookPayload.cs, DashboardStatus.cs

### Phase 10: DI & Function Config
**Goal:** Program.cs with full DI registration, host.json, local.settings.json, IAgentService interface
**Depends on:** Phase 7, 9, 13
**Files:** Program.cs, host.json, local.settings.json, IAgentService.cs

### Phase 11: HTTP Trigger Functions
**Goal:** OrchestratorWebhook and GetCurrentStatus HTTP functions
**Depends on:** Phase 10
**Files:** OrchestratorWebhook.cs, GetCurrentStatus.cs

### Phase 12: Dispatcher & Agent Services
**Goal:** Queue dispatcher and all 5 agent service implementations
**Depends on:** Phase 10
**Files:** AgentTaskDispatcher.cs, PlanningAgentService.cs, CodingAgentService.cs, TestingAgentService.cs, ReviewAgentService.cs, DocumentationAgentService.cs

### Phase 13: Activity Logging Service
**Goal:** Azure Table Storage activity log service for dashboard API
**Depends on:** Phase 4, 5, 6
**Files:** ActivityLogService.cs, ActivityLogEntity.cs

### Phase 14: Dashboard
**Goal:** HTML/CSS/JS dashboard with TWO intentional bugs
**Depends on:** Phase 1
**Files:** dashboard/index.html, dashboard/staticwebapp.config.json

### Phase 15: CI/CD Workflows
**Goal:** GitHub Actions workflows for Functions and Dashboard deployment
**Depends on:** Phase 1
**Files:** .github/workflows/deploy-functions.yml, .github/workflows/deploy-dashboard.yml

### Phase 16: Build Validation
**Goal:** Verify dotnet build succeeds, terraform validate passes, all contracts fulfilled
**Depends on:** All phases
**Success criteria:** Zero build errors, zero warnings, terraform validate passes
