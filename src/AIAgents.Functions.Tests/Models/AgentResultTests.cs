using AIAgents.Functions.Models;

namespace AIAgents.Functions.Tests.Models;

/// <summary>
/// Tests for AgentResult factory methods and state invariants.
/// </summary>
public sealed class AgentResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessTrue()
    {
        var result = AgentResult.Ok();

        Assert.True(result.Success);
    }

    [Fact]
    public void Ok_HasNullErrorFields()
    {
        var result = AgentResult.Ok();

        Assert.Null(result.Category);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Fail_ReturnsSuccessFalse()
    {
        var result = AgentResult.Fail(ErrorCategory.Transient, "timeout");

        Assert.False(result.Success);
    }

    [Fact]
    public void Fail_SetsCategory()
    {
        var result = AgentResult.Fail(ErrorCategory.Configuration, "bad key");

        Assert.Equal(ErrorCategory.Configuration, result.Category);
    }

    [Fact]
    public void Fail_SetsErrorMessage()
    {
        var result = AgentResult.Fail(ErrorCategory.Data, "invalid input");

        Assert.Equal("invalid input", result.ErrorMessage);
    }

    [Fact]
    public void Fail_WithException_SetsException()
    {
        var ex = new InvalidOperationException("boom");
        var result = AgentResult.Fail(ErrorCategory.Code, "crash", ex);

        Assert.Same(ex, result.Exception);
    }

    [Fact]
    public void Fail_WithoutException_ExceptionIsNull()
    {
        var result = AgentResult.Fail(ErrorCategory.Transient, "retry");

        Assert.Null(result.Exception);
    }

    [Theory]
    [InlineData(ErrorCategory.Transient)]
    [InlineData(ErrorCategory.Configuration)]
    [InlineData(ErrorCategory.Data)]
    [InlineData(ErrorCategory.Code)]
    public void Fail_AllCategories_AreSettable(ErrorCategory category)
    {
        var result = AgentResult.Fail(category, "test");

        Assert.Equal(category, result.Category);
        Assert.False(result.Success);
    }
}
