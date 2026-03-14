using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIAgents.Core.Configuration;
using AIAgents.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIAgents.Functions.Functions;

/// <summary>
/// HTTP-triggered Azure Function that handles GitHub webhook events for Copilot coding agent handoff.
///
/// Completion is detected when either the delegated GitHub issue title or the Copilot PR title
/// drops the <c>[WIP]</c> marker. PRs still resume even if they remain draft.
/// </summary>
public sealed class CopilotBridgeWebhook
{
    private readonly CopilotOptions _copilotOptions;
    private readonly ICopilotDelegationService _delegationService;
    private readonly IActivityLogger _activityLogger;
    private readonly ICopilotCompletionService _completionService;
    private readonly ILogger<CopilotBridgeWebhook> _logger;

    public CopilotBridgeWebhook(
        IOptions<CopilotOptions> copilotOptions,
        ICopilotDelegationService delegationService,
        IActivityLogger activityLogger,
        ICopilotCompletionService completionService,
        ILogger<CopilotBridgeWebhook> logger)
    {
        _copilotOptions = copilotOptions.Value;
        _delegationService = delegationService;
        _activityLogger = activityLogger;
        _completionService = completionService;
        _logger = logger;
    }

    [Function("CopilotBridgeWebhook")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "copilot-webhook")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Copilot bridge webhook triggered");

        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrEmpty(body))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (!ValidateSignature(req, body))
        {
            _logger.LogWarning("Invalid webhook signature - rejecting request");
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        if (!req.Headers.TryGetValues("X-GitHub-Event", out var eventTypes))
        {
            _logger.LogDebug("No X-GitHub-Event header");
            return req.CreateResponse(HttpStatusCode.OK);
        }

        using var doc = JsonDocument.Parse(body);
        return eventTypes.First() switch
        {
            "pull_request" => await HandlePullRequestEventAsync(req, doc.RootElement, cancellationToken),
            "issues" => await HandleIssueEventAsync(req, doc.RootElement, cancellationToken),
            _ => req.CreateResponse(HttpStatusCode.OK)
        };
    }

    private async Task<HttpResponseData> HandlePullRequestEventAsync(
        HttpRequestData req,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var action = root.GetProperty("action").GetString() ?? string.Empty;
        if (action is not ("opened" or "ready_for_review" or "edited" or "review_requested" or "reopened" or "synchronize"))
        {
            _logger.LogDebug("Ignoring pull_request action: {Action}", action);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        var pr = root.GetProperty("pull_request");
        var prNumber = pr.GetProperty("number").GetInt32();
        var prTitle = pr.GetProperty("title").GetString() ?? string.Empty;
        var prBody = pr.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
        var copilotBranch = pr.GetProperty("head").GetProperty("ref").GetString() ?? string.Empty;
        var isDraft = pr.TryGetProperty("draft", out var draftProp) && draftProp.ValueKind == JsonValueKind.True;
        var baseBranch = pr.TryGetProperty("base", out var baseProp)
            ? baseProp.TryGetProperty("ref", out var baseRef) ? baseRef.GetString() ?? string.Empty : string.Empty
            : string.Empty;

        var isReady = IsReadyToReconcile(action, prTitle, isDraft);
        _logger.LogInformation(
            "PR #{PrNumber} action={Action}: wip={HasWip}, draft={IsDraft}, ready={IsReady}",
            prNumber,
            action,
            CopilotCompletionService.HasWipMarker(prTitle),
            isDraft,
            isReady);

        if (!isReady)
        {
            var waitResponse = req.CreateResponse(HttpStatusCode.OK);
            await waitResponse.WriteStringAsync($"PR #{prNumber} not ready. Waiting.", cancellationToken);
            return waitResponse;
        }

        var workItemId = ExtractWorkItemId(prTitle, prBody);
        CopilotDelegation? delegation = null;

        if (workItemId is not null)
        {
            delegation = await _delegationService.GetByWorkItemIdAsync(workItemId.Value, cancellationToken);
        }

        if (delegation is null || !string.Equals(delegation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(baseBranch))
            {
                var pending = await _delegationService.GetPendingAsync(cancellationToken);
                delegation = pending.FirstOrDefault(d =>
                    string.Equals(d.BranchName, baseBranch, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (delegation is null || !string.Equals(delegation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "PR #{PrNumber} does not match any pending Copilot delegation (workItemId={WorkItemId})",
                prNumber,
                workItemId);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        try
        {
            await _activityLogger.LogAsync(
                "Coding",
                delegation.WorkItemId,
                $"Copilot PR #{prNumber} ready (action={action}) - reconciling",
                "info",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Non-critical activity log failed before reconciliation for PR #{PrNumber} WI-{WorkItemId}",
                prNumber,
                delegation.WorkItemId);
        }

        try
        {
            var completed = await _completionService.TryCompleteFromPullRequestAsync(
                delegation,
                prNumber,
                copilotBranch,
                $"Copilot PR #{prNumber} ready (action={action})",
                cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(
                completed
                    ? $"Reconciled Copilot PR #{prNumber} for WI-{delegation.WorkItemId}"
                    : $"PR #{prNumber} matched WI-{delegation.WorkItemId}, but completion is still waiting on reconciliable code.",
                cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            var errorDetails = FormatExceptionDetails(ex);
            _logger.LogError(
                ex,
                "Failed to reconcile Copilot PR #{PrNumber} for WI-{WorkItemId}. Details: {ErrorDetails}",
                prNumber,
                delegation.WorkItemId,
                errorDetails);

            try
            {
                await _activityLogger.LogAsync(
                    "Coding",
                    delegation.WorkItemId,
                    $"Error reconciling Copilot PR #{prNumber}: {errorDetails}",
                    "error",
                    cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(
                    logEx,
                    "Non-critical activity log failed while reporting reconciliation error for PR #{PrNumber} WI-{WorkItemId}",
                    prNumber,
                    delegation.WorkItemId);
            }

            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    private async Task<HttpResponseData> HandleIssueEventAsync(
        HttpRequestData req,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var action = root.GetProperty("action").GetString() ?? string.Empty;
        if (!CopilotCompletionService.ShouldHandleIssueAction(action))
        {
            _logger.LogDebug("Ignoring issues action: {Action}", action);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        var issue = root.GetProperty("issue");
        var issueNumber = issue.GetProperty("number").GetInt32();
        var issueTitle = issue.GetProperty("title").GetString() ?? string.Empty;

        if (CopilotCompletionService.HasWipMarker(issueTitle))
        {
            var waitResponse = req.CreateResponse(HttpStatusCode.OK);
            await waitResponse.WriteStringAsync($"Issue #{issueNumber} still has [WIP]. Waiting.", cancellationToken);
            return waitResponse;
        }

        var delegation = await _delegationService.GetByIssueNumberAsync(issueNumber, cancellationToken);
        if (delegation is null || !string.Equals(delegation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Issue #{IssueNumber} does not match any pending Copilot delegation", issueNumber);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        try
        {
            var completed = await _completionService.TryCompleteFromIssueAsync(
                delegation,
                $"GitHub issue #{issueNumber} ready (action={action})",
                cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(
                completed
                    ? $"Reconciled GitHub coding completion for WI-{delegation.WorkItemId} from issue #{issueNumber}."
                    : $"Issue #{issueNumber} is ready, but no reconciliable PR was found yet. Waiting.",
                cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            var errorDetails = FormatExceptionDetails(ex);
            _logger.LogError(
                ex,
                "Failed to process GitHub issue #{IssueNumber} for WI-{WorkItemId}. Details: {ErrorDetails}",
                issueNumber,
                delegation.WorkItemId,
                errorDetails);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    internal static int? ExtractWorkItemId(string title, string body)
    {
        var combined = $"{title}\n{body}";
        var match = System.Text.RegularExpressions.Regex.Match(
            combined,
            @"US-(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    internal static bool IsReadyToReconcile(string action, string prTitle, bool isDraft)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        if (string.Equals(action, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !CopilotCompletionService.HasWipMarker(prTitle);
    }

    private static string FormatExceptionDetails(Exception ex)
    {
        var message = string.IsNullOrWhiteSpace(ex.Message)
            ? "No exception message provided"
            : ex.Message.Trim();

        if (ex.InnerException is null)
        {
            return $"{ex.GetType().Name}: {message}";
        }

        var innerMessage = string.IsNullOrWhiteSpace(ex.InnerException.Message)
            ? "No inner exception message provided"
            : ex.InnerException.Message.Trim();

        return $"{ex.GetType().Name}: {message} | Inner {ex.InnerException.GetType().Name}: {innerMessage}";
    }

    private bool ValidateSignature(HttpRequestData req, string body)
    {
        if (string.IsNullOrEmpty(_copilotOptions.WebhookSecret))
        {
            _logger.LogWarning("Copilot:WebhookSecret not configured - skipping signature validation");
            return true;
        }

        if (!req.Headers.TryGetValues("X-Hub-Signature-256", out var signatures))
        {
            return false;
        }

        var signature = signatures.First();
        if (!signature.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }

        var expectedHash = signature["sha256=".Length..];
        var keyBytes = Encoding.UTF8.GetBytes(_copilotOptions.WebhookSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = BitConverter.ToString(hmac.ComputeHash(bodyBytes)).Replace("-", string.Empty).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(expectedHash));
    }
}
