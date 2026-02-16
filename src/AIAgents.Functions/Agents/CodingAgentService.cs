using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIAgents.Core.Constants;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Agents;

/// <summary>
/// Coding agent: evaluates complexity and either auto-generates code or hands off to a human.
/// Simple tasks (complexity 1-2): AI generates search/replace edits and new files directly.
/// Complex tasks (3+): Sets "Awaiting Code" for human developer with Codex/Copilot.
/// Transitions: AI Code → AI Test (auto) or AI Code → Awaiting Code → AI Test (via /api/resume).
/// </summary>
public sealed class CodingAgentService : IAgentService
{
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAzureDevOpsClient _adoClient;
    private readonly IGitOperations _gitOps;
    private readonly IStoryContextFactory _contextFactory;
    private readonly ICodebaseContextProvider _codebaseContext;
    private readonly ILogger<CodingAgentService> _logger;
    private readonly IAgentTaskQueue _taskQueue;
    private readonly IActivityLogger _activityLogger;

    /// <summary>
    /// Complexity threshold: 1-2 = auto, 3+ = handoff to human.
    /// </summary>
    private const int AutoComplexityThreshold = 3;

    public CodingAgentService(
        IAIClientFactory aiClientFactory,
        IAzureDevOpsClient adoClient,
        IGitOperations gitOps,
        IStoryContextFactory contextFactory,
        ICodebaseContextProvider codebaseContext,
        ILogger<CodingAgentService> logger,
        IAgentTaskQueue taskQueue,
        IActivityLogger activityLogger)
    {
        _aiClientFactory = aiClientFactory;
        _adoClient = adoClient;
        _gitOps = gitOps;
        _contextFactory = contextFactory;
        _codebaseContext = codebaseContext;
        _logger = logger;
        _taskQueue = taskQueue;
        _activityLogger = activityLogger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        try
        {
        _logger.LogInformation("Coding agent starting for WI-{WorkItemId}", task.WorkItemId);

        var workItem = await _adoClient.GetWorkItemAsync(task.WorkItemId, cancellationToken);
        var aiClient = _aiClientFactory.GetClientForAgent("Coding", workItem.GetModelOverrides());
        var branchName = $"feature/US-{task.WorkItemId}";
        var repoPath = await _gitOps.EnsureBranchAsync(branchName, cancellationToken);

        await using var context = _contextFactory.Create(task.WorkItemId, repoPath);
        var state = await context.LoadStateAsync(cancellationToken);
        state.CurrentState = "AI Code";
        state.Agents["Coding"] = AgentStatus.InProgress();
        await context.SaveStateAsync(state, cancellationToken);

        // Read the plan
        var plan = await context.ReadArtifactAsync("PLAN.md", cancellationToken)
            ?? "No plan found. Generate code based on the story description.";

        // Get existing file structure
        var existingFiles = await _gitOps.ListFilesAsync(repoPath, cancellationToken);
        var fileListSummary = string.Join("\n", existingFiles.Take(100));

        // Extract file paths mentioned in the plan and read their content
        var referencedFiles = ExtractReferencedFiles(plan, existingFiles);
        var fileContents = new List<string>();
        foreach (var filePath in referencedFiles.Take(10)) // Limit to 10 files to stay within token budget
        {
            var content = await _gitOps.ReadFileAsync(repoPath, filePath, cancellationToken);
            if (content is not null)
            {
                // Truncate very large files to avoid blowing token limits
                var truncated = content.Length > 8000 ? content[..8000] + "\n// ... truncated ..." : content;
                fileContents.Add($"### File: {filePath}\n```\n{truncated}\n```");
            }
        }
        var existingFileContents = fileContents.Count > 0
            ? "## Existing File Contents (files mentioned in plan)\n" + string.Join("\n\n", fileContents)
            : "";

        // AI call: assess complexity AND generate code if simple
        var systemPrompt = @"You are a senior software developer. Analyze the task complexity and generate code if it's simple enough.

Respond ONLY with valid JSON in this exact format:
{
  ""complexity"": <number 1-5>,
  ""reason"": ""<brief explanation of complexity assessment>"",
  ""confidence"": <number 0-100>,
  ""edits"": [
    {
      ""file"": ""relative/path/to/file"",
      ""operation"": ""edit"",
      ""search"": ""exact text to find in the existing file"",
      ""replace"": ""exact replacement text""
    }
  ],
  ""newFiles"": [
    {
      ""relativePath"": ""relative/path/to/new/file"",
      ""content"": ""full file content""
    }
  ]
}

COMPLEXITY SCALE:
1 = Trivial (fix a typo, change a constant, update a CSS value)
2 = Simple (small bug fix, change a few lines in 1-2 files, add a simple method)
3 = Moderate (modify multiple files, add a new feature with several components)
4 = Complex (architectural changes, refactoring, cross-cutting concerns)
5 = Major (large-scale redesign, new subsystem, significant infrastructure changes)

RULES:
- If complexity is 1 or 2: provide ALL edits needed in the ""edits"" array and any new files in ""newFiles""
- If complexity is 3+: set edits and newFiles to empty arrays [] — a human will do the coding
- For edits: ""search"" must be the EXACT text currently in the file (include enough context to be unique — at least 3 lines)
- For edits: ""replace"" is what replaces the search text
- Do NOT generate test files (the testing agent handles that)";

        var userPrompt = $@"## Story
**ID:** {workItem.Id}
**Title:** {workItem.Title}
**Description:** {workItem.Description ?? "N/A"}

## Implementation Plan
{plan}

## Repository File Listing
{fileListSummary}

{existingFileContents}

{await _codebaseContext.LoadRelevantContextAsync(repoPath, workItem.Title, workItem.Description, cancellationToken)}

Assess complexity and generate code edits if this is simple enough (complexity 1-2).";

        var aiResult = await aiClient.CompleteAsync(systemPrompt, userPrompt,
            new AICompletionOptions { MaxTokens = 8192, Temperature = 0.2 }, cancellationToken);
        state.TokenUsage.RecordUsage("Coding", aiResult.Usage);

        // Parse the AI response
        var codingDecision = ParseCodingDecision(aiResult.Content);

        _logger.LogInformation(
            "Coding agent for WI-{WorkItemId}: complexity={Complexity}, confidence={Confidence}, reason={Reason}",
            task.WorkItemId, codingDecision.Complexity, codingDecision.Confidence, codingDecision.Reason);

        // Decision: auto-code or handoff?
        if (codingDecision.Complexity < AutoComplexityThreshold && codingDecision.Confidence >= 70)
        {
            // === AUTO MODE: Apply edits and continue pipeline ===
            return await AutoCodeAsync(task, workItem, repoPath, context, state, codingDecision, cancellationToken);
        }
        else
        {
            // === HANDOFF MODE: Pause for human developer ===
            return await HandoffAsync(task, workItem, context, state, plan, codingDecision, cancellationToken);
        }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return AgentResult.Fail(ErrorCategory.Transient, $"Rate limit hit for Coding agent on WI-{task.WorkItemId}", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return AgentResult.Fail(ErrorCategory.Configuration, $"Authentication failed for Coding agent on WI-{task.WorkItemId}. Check API key.", ex);
        }
        catch (HttpRequestException ex)
        {
            return AgentResult.Fail(ErrorCategory.Code, $"HTTP error in Coding agent for WI-{task.WorkItemId}: {ex.Message} [StatusCode={ex.StatusCode}]", ex);
        }
        catch (Exception ex)
        {
            return AgentResult.Fail(ErrorCategory.Code, $"Unexpected error in Coding agent for WI-{task.WorkItemId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Auto mode: AI generates edits, apply them, commit, and continue pipeline.
    /// </summary>
    private async Task<AgentResult> AutoCodeAsync(
        AgentTask task, StoryWorkItem workItem, string repoPath,
        IStoryContext context, StoryState state, CodingDecision decision,
        CancellationToken cancellationToken)
    {
        var filesModified = 0;

        // Apply search/replace edits to existing files
        foreach (var edit in decision.Edits)
        {
            var currentContent = await _gitOps.ReadFileAsync(repoPath, edit.File, cancellationToken);
            if (currentContent is null)
            {
                _logger.LogWarning("Edit target file not found: {File}", edit.File);
                continue;
            }

            if (!currentContent.Contains(edit.Search))
            {
                _logger.LogWarning("Search text not found in {File}, skipping edit", edit.File);
                continue;
            }

            var newContent = currentContent.Replace(edit.Search, edit.Replace);
            await _gitOps.WriteFileAsync(repoPath, edit.File, newContent, cancellationToken);
            state.Artifacts.Code.Add(edit.File);
            filesModified++;
            _logger.LogInformation("Edited: {FilePath}", edit.File);
        }

        // Create new files
        foreach (var newFile in decision.NewFiles)
        {
            await _gitOps.WriteFileAsync(repoPath, newFile.RelativePath, newFile.Content, cancellationToken);
            state.Artifacts.Code.Add(newFile.RelativePath);
            filesModified++;
            _logger.LogInformation("Created: {FilePath}", newFile.RelativePath);
        }

        if (filesModified == 0)
        {
            _logger.LogWarning("AI said complexity was low but produced no valid edits for WI-{WorkItemId}, falling back to handoff", task.WorkItemId);
            // Fall back to handoff if no edits were applied
            var plan = await context.ReadArtifactAsync("PLAN.md", cancellationToken) ?? "";
            return await HandoffAsync(task, workItem, context, state, plan, decision, cancellationToken);
        }

        // Commit
        await _gitOps.CommitAndPushAsync(repoPath,
            $"[AI Coding] US-{workItem.Id}: Auto-generated {filesModified} edit(s) (complexity {decision.Complexity}/5)",
            cancellationToken);

        // Update ADO
        await _adoClient.AddWorkItemCommentAsync(workItem.Id,
            $"<b>🤖 AI Coding Agent Complete (Auto)</b><br/>Complexity: {decision.Complexity}/5 — {decision.Reason}<br/>Files modified: {filesModified}<br/>" +
            string.Join("<br/>", state.Artifacts.Code.Select(f => $"• {f}")),
            cancellationToken);

        // Update state and enqueue next
        state.Agents["Coding"] = AgentStatus.Completed();
        state.Agents["Coding"].AdditionalData = new Dictionary<string, object>
        {
            ["mode"] = "auto",
            ["complexity"] = decision.Complexity,
            ["filesModified"] = filesModified
        };
        state.CurrentState = "AI Test";
        await context.SaveStateAsync(state, cancellationToken);

        try { await _adoClient.UpdateWorkItemFieldAsync(workItem.Id, CustomFieldNames.Paths.LastAgent, "Coding", cancellationToken); }
        catch { /* field may not exist yet */ }

        await _adoClient.UpdateWorkItemStateAsync(workItem.Id, "AI Test", cancellationToken);

        var nextTask = new AgentTask
        {
            WorkItemId = task.WorkItemId,
            AgentType = AgentType.Testing,
            CorrelationId = task.CorrelationId
        };
        await _taskQueue.EnqueueAsync(nextTask, cancellationToken);

        _logger.LogInformation("Coding agent (auto) completed for WI-{WorkItemId}, enqueued Testing agent", task.WorkItemId);

        return AgentResult.Ok();
    }

    /// <summary>
    /// Handoff mode: Set "Awaiting Code" and wait for human to call /api/resume.
    /// </summary>
    private async Task<AgentResult> HandoffAsync(
        AgentTask task, StoryWorkItem workItem,
        IStoryContext context, StoryState state, string plan, CodingDecision decision,
        CancellationToken cancellationToken)
    {
        // Post detailed comment to ADO with the plan
        var comment = $@"<b>🤖 AI Coding Agent — Awaiting Human Code</b><br/>
<b>Complexity:</b> {decision.Complexity}/5 — {decision.Reason}<br/>
<b>Branch:</b> <code>feature/US-{workItem.Id}</code><br/>
<b>Plan:</b> See PLAN.md on the feature branch<br/><br/>
<b>To continue the pipeline after coding:</b><br/>
<code>POST /api/resume</code> with body <code>{{""workItemId"": {workItem.Id}}}</code><br/>
Or use the Resume button on the dashboard.";

        await _adoClient.AddWorkItemCommentAsync(workItem.Id, comment, cancellationToken);

        // Update state — mark Coding as completed with handoff mode
        state.Agents["Coding"] = AgentStatus.Completed();
        state.Agents["Coding"].AdditionalData = new Dictionary<string, object>
        {
            ["mode"] = "handoff",
            ["complexity"] = decision.Complexity,
            ["reason"] = decision.Reason
        };
        state.CurrentState = "Awaiting Code";
        await context.SaveStateAsync(state, cancellationToken);

        try { await _adoClient.UpdateWorkItemFieldAsync(workItem.Id, CustomFieldNames.Paths.LastAgent, "Coding", cancellationToken); }
        catch { /* field may not exist yet */ }

        // Update ADO state to Awaiting Code
        try { await _adoClient.UpdateWorkItemStateAsync(workItem.Id, "Awaiting Code", cancellationToken); }
        catch
        {
            // If "Awaiting Code" state doesn't exist in ADO, keep it at AI Code
            _logger.LogWarning("Could not set 'Awaiting Code' state in ADO for WI-{WorkItemId}", task.WorkItemId);
        }

        _logger.LogInformation(
            "Coding agent (handoff) completed for WI-{WorkItemId}: complexity {Complexity}/5, awaiting human code",
            task.WorkItemId, decision.Complexity);

        // Log activity for dashboard detection — "Awaiting human" is the key phrase
        await _activityLogger.LogAsync(
            "Coding", task.WorkItemId,
            $"Awaiting human developer — complexity {decision.Complexity}/5: {decision.Reason}",
            "info", cancellationToken);

        // Do NOT enqueue Testing — the /api/resume endpoint will do that
        return AgentResult.Ok();
    }

    /// <summary>
    /// Extract file paths referenced in the plan by matching against actual repository files.
    /// </summary>
    internal static List<string> ExtractReferencedFiles(string plan, IReadOnlyList<string> existingFiles)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in existingFiles)
        {
            // Check if the file path or filename appears in the plan
            var fileName = Path.GetFileName(file);
            if (plan.Contains(file, StringComparison.OrdinalIgnoreCase) ||
                (fileName.Length > 5 && plan.Contains(fileName, StringComparison.OrdinalIgnoreCase)))
            {
                referenced.Add(file);
            }
        }

        // Also look for backtick-quoted paths like `src/foo/bar.cs`
        var backtickPaths = Regex.Matches(plan, @"`([^`]+\.[a-zA-Z]{1,10})`");
        foreach (Match match in backtickPaths)
        {
            var path = match.Groups[1].Value.Replace('\\', '/');
            var matchingFile = existingFiles.FirstOrDefault(f =>
                f.Replace('\\', '/').EndsWith(path, StringComparison.OrdinalIgnoreCase) ||
                f.Replace('\\', '/').Equals(path, StringComparison.OrdinalIgnoreCase));
            if (matchingFile is not null)
                referenced.Add(matchingFile);
        }

        return referenced.ToList();
    }

    /// <summary>
    /// Parse the AI response into a structured coding decision.
    /// </summary>
    internal static CodingDecision ParseCodingDecision(string aiResponse)
    {
        var json = aiResponse.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                json = json[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var decision = new CodingDecision
            {
                Complexity = root.TryGetProperty("complexity", out var c) ? c.GetInt32() : 5,
                Reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "Unknown" : "Unknown",
                Confidence = root.TryGetProperty("confidence", out var conf) ? conf.GetInt32() : 0
            };

            // Parse edits
            if (root.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
            {
                foreach (var edit in edits.EnumerateArray())
                {
                    if (edit.ValueKind != JsonValueKind.Object) continue;
                    var file = edit.TryGetProperty("file", out var f) ? f.GetString() : null;
                    var search = edit.TryGetProperty("search", out var s) ? s.GetString() : null;
                    var replace = edit.TryGetProperty("replace", out var rp) ? rp.GetString() : null;

                    if (file is not null && search is not null && replace is not null)
                    {
                        decision.Edits.Add(new CodeEdit { File = file, Search = search, Replace = replace });
                    }
                }
            }

            // Parse new files
            if (root.TryGetProperty("newFiles", out var newFiles) && newFiles.ValueKind == JsonValueKind.Array)
            {
                foreach (var nf in newFiles.EnumerateArray())
                {
                    if (nf.ValueKind != JsonValueKind.Object) continue;
                    var path = nf.TryGetProperty("relativePath", out var p) ? p.GetString() : null;
                    var content = nf.TryGetProperty("content", out var ct) ? ct.GetString() : null;

                    if (path is not null && content is not null)
                    {
                        decision.NewFiles.Add(new CodeFile { RelativePath = path, Content = content, IsNew = true });
                    }
                }
            }

            return decision;
        }
        catch (Exception)
        {
            // If we can't parse, assume complex → handoff
            return new CodingDecision
            {
                Complexity = 5,
                Reason = "AI response could not be parsed — defaulting to human handoff",
                Confidence = 0
            };
        }
    }

    internal sealed class CodingDecision
    {
        public int Complexity { get; set; } = 5;
        public string Reason { get; set; } = "";
        public int Confidence { get; set; }
        public List<CodeEdit> Edits { get; set; } = [];
        public List<CodeFile> NewFiles { get; set; } = [];
    }

    internal sealed class CodeEdit
    {
        public string File { get; set; } = "";
        public string Search { get; set; } = "";
        public string Replace { get; set; } = "";
    }

}
