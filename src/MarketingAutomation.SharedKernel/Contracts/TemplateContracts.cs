namespace MarketingAutomation.SharedKernel.Contracts;

/// <summary>Channel-agnostic rendered output of a template for one recipient.</summary>
public sealed record RenderedContent(string? Subject, string Body, string? Text);

/// <summary>
/// Renders a stored template for a recipient (implemented by Templates). Lets Campaigns and
/// Journeys send template-backed content without referencing the Templates module.
/// </summary>
public interface ITemplateRenderer
{
    Task<RenderedContent> RenderAsync(
        Guid templateId, IReadOnlyDictionary<string, object?> data, CancellationToken ct);
}
