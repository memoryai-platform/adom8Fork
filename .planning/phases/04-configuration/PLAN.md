# Phase 4: Configuration Classes

**Goal:** Strongly-typed IOptions<T> configuration classes

## Files to Create

1. `src/AIAgents.Core/Configuration/AIOptions.cs` — Provider enum, Model, ApiKey, Endpoint, MaxTokens
2. `src/AIAgents.Core/Configuration/AzureDevOpsOptions.cs` — OrganizationUrl, Pat, Project
3. `src/AIAgents.Core/Configuration/GitOptions.cs` — RepositoryUrl, Username, Token, Email, Name

## Details

- AIOptions.Provider is an enum: Claude, OpenAI, AzureOpenAI
- AIOptions.Endpoint is nullable (only used for Azure OpenAI)
- GitOptions has sensible defaults for Username, Email, Name
- All classes use "AI", "AzureDevOps", "Git" config section names
