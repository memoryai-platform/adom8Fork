# Phase 13: Activity Logging Service

**Goal:** Azure Table Storage service for activity log and dashboard stats

## Files to Create

1. `src/AIAgents.Functions/Services/ActivityLogService.cs`
   - Uses TableClient from Azure.Data.Tables
   - ActivityLogEntity : ITableEntity (PartitionKey=date, RowKey=timestamp+guid)
   - Methods: LogActivityAsync, GetRecentActivityAsync, GetStatsAsync
   - Injected into dispatcher and agent services

2. `src/AIAgents.Functions/Models/ActivityLogEntity.cs`
   - ITableEntity implementation
   - Properties: WorkItemId, AgentType, Status, Message, Emoji
