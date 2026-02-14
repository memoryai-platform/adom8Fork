# Phase 11: HTTP Trigger Functions

**Goal:** OrchestratorWebhook and GetCurrentStatus HTTP functions

## Files to Create

1. `src/AIAgents.Functions/Functions/OrchestratorWebhook.cs`
   - HTTP POST, AuthorizationLevel.Function
   - Parse ServiceHookPayload (handle both ADO field locations)
   - Map state → AgentType: "Story Planning"→Planning, "AI Code"→Coding, etc.
   - Queue AgentTask to "agent-tasks"
   - Log activity to table
   - Return 200 OK immediately (idempotent)

2. `src/AIAgents.Functions/Functions/GetCurrentStatus.cs`
   - HTTP GET, AuthorizationLevel.Anonymous
   - Query activitylog table for recent entries
   - Calculate stats (stories processed, avg time, success rate, time saved)
   - Return DashboardStatus JSON
