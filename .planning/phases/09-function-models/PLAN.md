# Phase 9: Function Models

**Goal:** Queue message and HTTP payload/response models

## Files to Create

1. `src/AIAgents.Functions/Models/AgentType.cs` — Enum: Planning, Coding, Testing, Review, Documentation
2. `src/AIAgents.Functions/Models/AgentTask.cs` — WorkItemId (int), AgentType, State (string), QueuedAt (DateTime)
3. `src/AIAgents.Functions/Models/ServiceHookPayload.cs` — Flexible model for ADO webhook payloads (handles both field locations)
4. `src/AIAgents.Functions/Models/DashboardStatus.cs` — Response model for GetCurrentStatus API
