# Phase 10: DI & Function Config

**Goal:** Program.cs with full DI, host.json, local.settings.json, IAgentService interface

## Files to Create

1. `src/AIAgents.Functions/Program.cs` — FunctionsApplication.CreateBuilder, all DI registrations
2. `src/AIAgents.Functions/host.json` — Queue config (batchSize:1, maxDequeueCount:5, visibilityTimeout:10min)
3. `src/AIAgents.Functions/local.settings.json` — All config sections with placeholders
4. `src/AIAgents.Functions/Interfaces/IAgentService.cs` — ProcessAsync(AgentTask, CancellationToken)

## Program.cs DI Registration Order

1. ConfigureFunctionsWebApplication()
2. App Insights telemetry (BEFORE resilience)
3. IOptions<T> bindings (AI, AzureDevOps, Git sections)
4. IHttpClientFactory named clients ("Claude", "OpenAI") with AddStandardResilienceHandler (60s timeout)
5. IAIClient conditional registration based on AIOptions.Provider
6. Singletons: IAzureDevOpsClient, IGitOperations, IStoryContextFactory, ITemplateEngine
7. Keyed scoped: IAgentService for each agent type
8. TableServiceClient for activity log
