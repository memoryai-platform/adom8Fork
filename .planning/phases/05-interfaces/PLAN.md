# Phase 5: Core Interfaces

**Goal:** Clean interface contracts for all services

## Files to Create

1. `src/AIAgents.Core/Interfaces/IAIClient.cs` — Single CompleteAsync method + AICompletionOptions record
2. `src/AIAgents.Core/Interfaces/IAzureDevOpsClient.cs` — GetWorkItem, UpdateState, AddComment, CreateTask, UpdateField
3. `src/AIAgents.Core/Interfaces/IGitOperations.cs` — CloneOrPull (returns ClonedRepository), CommitAndPush, CreateBranch
4. `src/AIAgents.Core/Interfaces/IStoryContext.cs` — LoadState, SaveState, UpdateAgentStatus, WriteMarkdown, ReadMarkdown, GetFullContext, WriteArtifact, AddDecision, AskQuestion
5. `src/AIAgents.Core/Interfaces/IStoryContextFactory.cs` — Create(repoPath, workItemId)
6. `src/AIAgents.Core/Interfaces/ITemplateEngine.cs` — Render(templateName, model), Render(templateName, dictionary)

## Key Design Notes

- IAIClient is thin — single CompleteAsync(systemPrompt, userPrompt, options?, ct)
- IGitOperations.CloneOrPullAsync returns ClonedRepository : IAsyncDisposable
- IStoryContext.WriteArtifactAsync takes explicit ArtifactType enum
- No stub methods — everything in the interface is implemented
