using System.Text.Json;

namespace AIAgents.Functions.Tests.Functions;

/// <summary>
/// Contract tests for inbound webhook payload shapes used by Azure DevOps and GitHub.
/// These guard the minimum JSON schema expected by webhook-triggered functions.
/// </summary>
public sealed class WebhookContractTests
{
    [Fact]
    public void AdoWebhookContract_ValidStateChangePayload_Passes()
    {
        var json = """
        {
          "eventType": "workitem.updated",
          "resource": {
            "id": 123,
            "workItemId": 456,
            "fields": {
              "System.State": {
                "oldValue": "New",
                "newValue": "AI Agent"
              }
            }
          }
        }
        """;

        using var document = JsonDocument.Parse(json);
        var isValid = TryReadAdoStateChange(document.RootElement, out var workItemId, out var newState);

        Assert.True(isValid);
        Assert.Equal(456, workItemId);
        Assert.Equal("AI Agent", newState);
    }

    [Fact]
    public void AdoWebhookContract_MissingStateChange_Fails()
    {
        var json = """
        {
          "eventType": "workitem.updated",
          "resource": {
            "id": 123,
            "workItemId": 456,
            "fields": {}
          }
        }
        """;

        using var document = JsonDocument.Parse(json);
        var isValid = TryReadAdoStateChange(document.RootElement, out _, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void GithubWebhookContract_ValidPullRequestPayload_Passes()
    {
        var json = """
        {
          "action": "ready_for_review",
          "pull_request": {
            "number": 88,
            "title": "[US-12345] Implement feature",
            "body": "Details",
            "draft": false,
            "requested_reviewers": [ { "login": "octocat" } ],
            "head": { "ref": "copilot/us-12345" },
            "base": { "ref": "feature/US-12345" }
          }
        }
        """;

        using var document = JsonDocument.Parse(json);
        var isValid = TryReadGitHubPullRequestEvent(document.RootElement, out var action, out var number, out var branchName);

        Assert.True(isValid);
        Assert.Equal("ready_for_review", action);
        Assert.Equal(88, number);
        Assert.Equal("copilot/us-12345", branchName);
    }

    [Fact]
    public void GithubWebhookContract_MissingPullRequestNode_Fails()
    {
        var json = """
        {
          "action": "ready_for_review"
        }
        """;

        using var document = JsonDocument.Parse(json);
        var isValid = TryReadGitHubPullRequestEvent(document.RootElement, out _, out _, out _);

        Assert.False(isValid);
    }

    private static bool TryReadAdoStateChange(JsonElement root, out int workItemId, out string newState)
    {
        workItemId = 0;
        newState = string.Empty;

        if (!root.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (resource.TryGetProperty("workItemId", out var workItemIdNode) && workItemIdNode.TryGetInt32(out var parsedWorkItemId))
        {
            workItemId = parsedWorkItemId;
        }
        else if (resource.TryGetProperty("id", out var idNode) && idNode.TryGetInt32(out var parsedId))
        {
            workItemId = parsedId;
        }

        if (!resource.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!fields.TryGetProperty("System.State", out var stateNode) || stateNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!stateNode.TryGetProperty("newValue", out var newValueNode) || newValueNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        newState = newValueNode.GetString() ?? string.Empty;
        return workItemId > 0 && !string.IsNullOrWhiteSpace(newState);
    }

    private static bool TryReadGitHubPullRequestEvent(JsonElement root, out string action, out int prNumber, out string headRef)
    {
        action = string.Empty;
        prNumber = 0;
        headRef = string.Empty;

        if (!root.TryGetProperty("action", out var actionNode) || actionNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        action = actionNode.GetString() ?? string.Empty;

        if (!root.TryGetProperty("pull_request", out var prNode) || prNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!prNode.TryGetProperty("number", out var numberNode) || !numberNode.TryGetInt32(out prNumber))
        {
            return false;
        }

        if (!prNode.TryGetProperty("head", out var headNode) || headNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!headNode.TryGetProperty("ref", out var headRefNode) || headRefNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        headRef = headRefNode.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(action) && prNumber > 0 && !string.IsNullOrWhiteSpace(headRef);
    }
}
