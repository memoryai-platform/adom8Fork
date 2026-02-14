# Phase 6: Core Models

**Goal:** All data models, records, and enums

## Files to Create

1. `src/AIAgents.Core/Models/StoryWorkItem.cs` — Record: Id, Title, Description, AcceptanceCriteria, State, AssignedTo, WorkItemType
2. `src/AIAgents.Core/Models/PlanningAnalysis.cs` — Record: ProblemAnalysis, AffectedFiles, Approach, Subtasks, Complexity, Dependencies, Architecture, Risks, TestingStrategy, Assumptions
3. `src/AIAgents.Core/Models/CodeReview.cs` — Record: Score, Summary, Critical/High/Medium/Low lists, PositiveFindings
4. `src/AIAgents.Core/Models/ReviewIssue.cs` — Record: Line (int?), Issue, Fix, Code (nullable)
5. `src/AIAgents.Core/Models/StoryState.cs` — WorkItemId, CurrentState, timestamps, Agents dict, Artifacts, Decisions, Questions
6. `src/AIAgents.Core/Models/AgentStatus.cs` — Status, StartedAt, CompletedAt, AdditionalData
7. `src/AIAgents.Core/Models/Decision.cs` — Timestamp, Agent, DecisionText, Rationale
8. `src/AIAgents.Core/Models/Question.cs` — Timestamp, Agent, QuestionText, AskedTo
9. `src/AIAgents.Core/Models/ArtifactType.cs` — Enum: SourceCode, Test, Documentation, Configuration
10. `src/AIAgents.Core/Models/ClonedRepository.cs` — IAsyncDisposable, Path property, deletes temp dir on dispose
