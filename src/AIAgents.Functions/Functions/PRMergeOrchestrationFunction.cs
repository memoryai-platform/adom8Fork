using System.Net;
using System.Text.Json;
using AIAgents.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using AIAgents.Functions.Services;

namespace AIAgents.Functions.Functions;

public sealed class PRMergeOrchestrationFunction
{
    private readonly IAzureDevOpsClient _adoClient;
    private readonly IMergeEventDeduplicationStore _dedupeStore;
    private readonly ILogger<PRMergeOrchestrationFunction> _logger;

    public PRMergeOrchestrationFunction(
        IAzureDevOpsClient adoClient,
        IMergeEventDeduplicationStore dedupeStore,
        ILogger<PRMergeOrchestrationFunction> logger)
    {
        _adoClient = adoClient;
        _dedupeStore = dedupeStore;
        _logger = logger;
    }

    [Function("PRMergeOrchestrationFunction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "pr-merge-webhook")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        using var doc = JsonDocument.Parse(body);
        var processed = await ProcessMergedPullRequestAsync(doc.RootElement, cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(processed ? "Processed" : "Ignored", cancellationToken);
        return response;
    }

    internal async Task<bool> ProcessMergedPullRequestAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var action = root.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : null;
        if (!string.Equals(action, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!root.TryGetProperty("pull_request", out var prNode) ||
            !prNode.TryGetProperty("merged", out var mergedProp) ||
            mergedProp.ValueKind != JsonValueKind.True)
        {
            return false;
        }

        var prNumber = prNode.GetProperty("number").GetInt32();
        var mergeSha = prNode.TryGetProperty("merge_commit_sha", out var shaProp) ? shaProp.GetString() ?? string.Empty : string.Empty;
        var headBranch = prNode.GetProperty("head").GetProperty("ref").GetString();

        var storyId = CopilotBridgeWebhook.ExtractWorkItemIdFromBranch(headBranch);
        if (storyId is null)
        {
            _logger.LogInformation("Merged PR #{PrNumber} branch {Branch} did not map to a story", prNumber, headBranch);
            return false;
        }

        var dedupeKey = $"pr:{prNumber}:story:{storyId}:sha:{mergeSha}";
        if (!await _dedupeStore.TryProcessAsync(dedupeKey, cancellationToken))
        {
            return false;
        }

        var successors = await _adoClient.GetSuccessorIdsAsync(storyId.Value, cancellationToken);
        foreach (var successorId in successors)
        {
            var predecessors = await _adoClient.GetPredecessorIdsAsync(successorId, cancellationToken);
            var allDone = true;

            foreach (var predecessorId in predecessors)
            {
                var predecessor = await _adoClient.GetWorkItemAsync(predecessorId, cancellationToken);
                if (!string.Equals(predecessor.State, "Done", StringComparison.OrdinalIgnoreCase))
                {
                    allDone = false;
                    break;
                }
            }

            if (allDone)
            {
                await _adoClient.UpdateWorkItemStateAsync(successorId, "AI Agent", cancellationToken);
                _logger.LogInformation("Unlocked successor US-{SuccessorId} after merge for US-{StoryId}", successorId, storyId.Value);
            }
        }

        return true;
    }
}
