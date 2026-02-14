# Phase 3: Solution & Project Files

**Goal:** .sln and both .csproj files with all NuGet package references

## Files to Create

1. `src/AIAgents.sln` — Solution referencing Core and Functions projects
2. `src/AIAgents.Core/AIAgents.Core.csproj` — .NET 8 class library with packages
3. `src/AIAgents.Functions/AIAgents.Functions.csproj` — .NET 8 isolated worker with packages

## Key Packages (Core)
- Microsoft.TeamFoundationServer.Client 19.225.1
- Microsoft.VisualStudio.Services.Client 19.225.1
- LibGit2Sharp 0.30.0
- Scriban (latest stable)
- System.Text.Json 8.0.x
- Microsoft.Extensions.* 8.0.x

## Key Packages (Functions)
- Microsoft.Azure.Functions.Worker 2.x
- Microsoft.Azure.Functions.Worker.Sdk 2.x
- Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore 1.x
- Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues 5.x
- Azure.Data.Tables 12.x
- Microsoft.Extensions.Http.Resilience
- Microsoft.ApplicationInsights.WorkerService
- Project reference to AIAgents.Core

## Acceptance Criteria

- [ ] dotnet restore succeeds
- [ ] Both projects compile (after all phases complete)
- [ ] Embedded resources configured for Templates/**/*.md
