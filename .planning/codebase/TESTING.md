# Testing Patterns

**Analysis Date:** 2026-03-14

## Test Framework

**Runner:**
- xUnit 2.9.2 (C#/.NET)
- Config: Project reference via csproj `IsTestProject=true`
- No JavaScript test framework configured

**Assertion Library:**
- xUnit `Assert.*` static methods (C#)

**Run Commands:**
```bash
dotnet test                    # Run all C# tests
dotnet test --filter "ClassName"  # Run specific test class
dotnet test --collect:"XPlat Code Coverage"  # With coverage
```

## Test File Organization

**Location:**
- C# tests: `src/AIAgents.Core.Tests/` and `src/AIAgents.Functions.Tests/`
- Structure mirrors main project: `Services/`, `Agents/`, `Models/`, `Functions/` subdirectories
- Test classes placed in matching namespace to source under `.Tests` suffix
- No JavaScript tests found

**Naming:**
- C# test class: `[SourceClassName]Tests` (e.g., `AIClientFactoryTests`, `ModelSerializationTests`, `PlanningAgentServiceTests`)
- C# test method: `[Scenario]_[Condition]_[ExpectedResult]` or `[Method]_[Scenario]`
  - Examples: `Resolve_Uses_Global_Defaults_When_No_Overrides()`, `StoryState_RoundTrips()`, `DetectProvider_Claude_Models()`

**Structure:**
```
src/
├── AIAgents.Core.Tests/
│   ├── GlobalUsings.cs          # xUnit using
│   ├── Models/
│   │   ├── ModelSerializationTests.cs
│   │   └── TokenUsageTests.cs
│   └── Services/
│       └── AIClientFactoryTests.cs
└── AIAgents.Functions.Tests/
    ├── Agents/
    │   ├── PlanningAgentServiceTests.cs
    │   ├── CodingAgentServiceTests.cs
    │   └── [other agent tests]
    ├── Functions/
    │   └── [function tests]
    └── Helpers/
        └── [mock data builders]
```

## Test Structure

**Suite Organization:**
```csharp
namespace AIAgents.Core.Tests.Models;

public sealed class ModelSerializationTests
{
    // Logical grouping with region markers
    #region StoryState
    [Fact]
    public void StoryState_RoundTrips() { ... }
    #endregion

    #region AgentStatus
    [Fact]
    public void AgentStatus_Pending_HasCorrectStatus() { ... }
    #endregion
}
```

**Patterns:**
- **Setup (in constructor or helper method):**
  - Mock dependencies created in constructor
  - Setup methods like `CreateFactory()`, `CreateService()` return fully wired instances
  - `SetupHappyPath()` method configures mocks for typical success scenario

```csharp
public sealed class PlanningAgentServiceTests
{
    private readonly Mock<IAIClientFactory> _aiFactoryMock;
    private readonly Mock<IAIClient> _aiClientMock;

    public PlanningAgentServiceTests()
    {
        _aiFactoryMock = new Mock<IAIClientFactory>();
        _aiClientMock = new Mock<IAIClient>();
    }

    private PlanningAgentService CreateService() => new(
        _aiFactoryMock.Object,
        _adoMock.Object,
        ...);

    private void SetupHappyPath(StoryWorkItem? workItem = null)
    {
        var wi = workItem ?? MockAIResponses.SampleWorkItem();
        _adoMock.Setup(a => a.GetWorkItemAsync(wi.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wi);
    }
}
```

- **Teardown:** Not explicitly shown; xUnit disposes mocks automatically
- **Assertion pattern:** Arrange → Act → Assert with clear phase separation

```csharp
[Fact]
public void Resolve_Uses_Global_Defaults_When_No_Overrides()
{
    // Arrange
    var factory = CreateFactory();

    // Act
    var effective = factory.ResolveEffectiveOptions("Planning");

    // Assert
    Assert.Equal("Claude", effective.Provider);
    Assert.Equal("claude-sonnet-4-20250514", effective.Model);
}
```

## Mocking

**Framework:**
- Moq 4.20.72

**Patterns:**
```csharp
var mock = new Mock<IInterface>();
mock.Setup(m => m.Method(It.IsAny<string>()))
    .ReturnsAsync(expectedValue);

// Or for property setup:
mock.Setup(m => m.Property).Returns(value);

// Capture arguments:
StoryState capturedState = null;
_contextMock.Setup(c => c.SaveStateAsync(It.IsAny<StoryState>(), ...))
    .Callback<StoryState, CancellationToken>((state, ct) => capturedState = state)
    .Returns(Task.CompletedTask);
```

**What to Mock:**
- External dependencies (AI clients, Azure DevOps, GitHub APIs)
- Database/repository patterns (but not in this codebase — in-memory state used)
- HTTP clients
- Configuration providers

**What NOT to Mock:**
- Models (use real instances)
- Value objects (use real instances)
- Pure functions (call directly)
- Test helpers and builders (use real builders)

## Fixtures and Factories

**Test Data:**
- Helper class `MockAIResponses` provides factory methods:
  ```csharp
  var workItem = MockAIResponses.SampleWorkItem();
  var state = MockAIResponses.SampleState(workItemId);
  ```
- Models constructed inline for assertions:
  ```csharp
  var state = new StoryState
  {
      WorkItemId = 12345,
      CurrentState = "AI Code",
      HandoffRef = new StoryHandoffReference { ... }
  };
  ```

**Location:**
- Helper builders in `src/AIAgents.Functions.Tests/Helpers/` directory
- JSON test data embedded in test methods or loaded from files
- No external fixture files; builders preferred

## Coverage

**Requirements:**
- No explicit coverage target enforced in CI/CD (not detected)
- Coverage collector included: `coverlet.collector` 6.0.2

**View Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
# Generates .coverage XML in TestResults/ folder
# Can be imported into SonarQube or other tools
```

## Test Types

**Unit Tests:**
- **Scope:** Individual service/model behavior
- **Approach:** Mock all external dependencies, test in isolation
- **Example:** `AIClientFactoryTests.Resolve_Uses_Global_Defaults_When_No_Overrides()`
  - Tests single method in isolation
  - Verifies configuration resolution without network calls
  - Uses real models, mocked dependencies

**Integration Tests:**
- **Scope:** Multiple components working together
- **Approach:** In-memory test doubles, no external services
- **Example:** `ModelSerializationTests.StoryState_RoundTrips()`
  - Tests JSON serialization round-trip (serialize → deserialize)
  - Verifies all model properties survive serialization
  - Tests integration between models and System.Text.Json

**E2E Tests:**
- **Framework:** Not used in current codebase
- **Note:** Azure Functions could be integration-tested via local function runtime, but no E2E test suite found

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task Method_Scenario_Result()
{
    var service = CreateService();
    var result = await service.ProcessAsync(input, CancellationToken.None);
    Assert.NotNull(result);
}
```

**Error Testing:**
```csharp
[Fact]
public void AgentStatus_Failed_IncludesReason()
{
    var status = AgentStatus.Failed("Network timeout");

    Assert.Equal("failed", status.Status);
    Assert.NotNull(status.AdditionalData);
    Assert.Equal("Network timeout", status.AdditionalData!["error"]);
}
```

**Theory Testing (Parameterized):**
```csharp
[Theory]
[InlineData("claude-sonnet-4-20250514", "Claude")]
[InlineData("gpt-4o", "OpenAI")]
[InlineData("gemini-2.5-pro", "Google")]
public void DetectProvider_Models(string model, string expected)
    => Assert.Equal(expected, AIClientFactory.DetectProviderFromModel(model));
```

**Test-Specific Setup Methods:**
```csharp
private void SetupHappyPath(StoryWorkItem? workItem = null, string? aiResponse = null)
{
    var wi = workItem ?? MockAIResponses.SampleWorkItem();
    _adoMock.Setup(a => a.GetWorkItemAsync(wi.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(wi);
    // ... configure remaining mocks
}
```

**Captured State Verification:**
```csharp
[Fact]
public async Task Service_Saves_State_With_Updated_Values()
{
    var capturedState = null as StoryState;
    _contextMock.Setup(c => c.SaveStateAsync(It.IsAny<StoryState>(), ...))
        .Callback<StoryState, CancellationToken>((state, _) => capturedState = state)
        .Returns(Task.CompletedTask);

    await service.ProcessAsync(...);

    Assert.NotNull(capturedState);
    Assert.Equal("NewState", capturedState.CurrentState);
}
```

---

*Testing analysis: 2026-03-14*
