# Phase 7: Core Service Implementations

**Goal:** All service classes implementing core interfaces

## Files to Create

1. `src/AIAgents.Core/Services/ClaudeClient.cs` — Thin IAIClient, pre-configured HttpClient, IOptions<AIOptions>, CompleteAsync → /v1/messages
2. `src/AIAgents.Core/Services/OpenAIClient.cs` — Thin IAIClient, handles OpenAI + Azure OpenAI, dictionary request body
3. `src/AIAgents.Core/Services/AIResponseParser.cs` — Static utility: ParseJson<T>, CleanMarkdownFences
4. `src/AIAgents.Core/Services/AzureDevOpsClient.cs` — IOptions<AzureDevOpsOptions>, IDisposable, returns StoryWorkItem
5. `src/AIAgents.Core/Services/GitOperations.cs` — IOptions<GitOptions>, ClonedRepository return, Task.Run wrapping documented
6. `src/AIAgents.Core/Services/StoryContext.cs` — File-based state.json, atomic writes, explicit ArtifactType
7. `src/AIAgents.Core/Services/StoryContextFactory.cs` — Creates StoryContext with runtime params
8. `src/AIAgents.Core/Services/TemplateEngine.cs` — Scriban wrapper, embedded resources, ConcurrentDictionary cache, ScriptObject with UPPERCASE keys
