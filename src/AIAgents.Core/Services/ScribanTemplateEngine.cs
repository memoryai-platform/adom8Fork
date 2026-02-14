using System.Reflection;
using AIAgents.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace AIAgents.Core.Services;

/// <summary>
/// Renders Scriban templates from embedded resources.
/// Templates are embedded in the AIAgents.Core assembly under the Templates folder.
/// </summary>
public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private readonly ILogger<ScribanTemplateEngine> _logger;
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;

    public ScribanTemplateEngine(ILogger<ScribanTemplateEngine> logger)
    {
        _logger = logger;
        _assembly = typeof(ScribanTemplateEngine).Assembly;
        _resourcePrefix = $"{_assembly.GetName().Name}.Templates.";
    }

    public async Task<string> RenderAsync(
        string templateName,
        IDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        var templateContent = await LoadTemplateAsync(templateName, cancellationToken);

        var template = Template.Parse(templateContent, templateName);

        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(m => m.ToString()));
            _logger.LogError("Template '{TemplateName}' has parse errors: {Errors}", templateName, errors);
            throw new InvalidOperationException($"Template '{templateName}' has parse errors: {errors}");
        }

        var scriptObject = new ScriptObject();
        foreach (var kvp in model)
        {
            scriptObject.Add(kvp.Key, kvp.Value);
        }

        var context = new TemplateContext
        {
            MemberRenamer = member => member.Name,
            StrictVariables = false
        };
        context.PushGlobal(scriptObject);

        var result = await template.RenderAsync(context);

        _logger.LogDebug("Rendered template '{TemplateName}' ({Length} chars)", templateName, result.Length);

        return result;
    }

    private async Task<string> LoadTemplateAsync(string templateName, CancellationToken cancellationToken)
    {
        // Embedded resource names use dots instead of path separators
        var resourceName = $"{_resourcePrefix}{templateName.Replace('/', '.').Replace('\\', '.')}";

        var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var available = string.Join(", ", _assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(_resourcePrefix)));
            _logger.LogError(
                "Template '{TemplateName}' not found as embedded resource '{ResourceName}'. Available: {Available}",
                templateName, resourceName, available);
            throw new FileNotFoundException(
                $"Template '{templateName}' not found as embedded resource. Available: {available}");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
