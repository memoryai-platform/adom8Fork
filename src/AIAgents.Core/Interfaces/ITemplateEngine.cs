namespace AIAgents.Core.Interfaces;

/// <summary>
/// Renders Scriban templates from embedded resources.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Renders a template by name using the provided model data.
    /// Template names correspond to embedded resource file names (e.g., "PLAN.template.md").
    /// </summary>
    /// <param name="templateName">The template file name (e.g., "PLAN.template.md").</param>
    /// <param name="model">A dictionary of UPPERCASE key → value pairs for template variables.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered template content.</returns>
    Task<string> RenderAsync(
        string templateName,
        IDictionary<string, object?> model,
        CancellationToken cancellationToken = default);
}
