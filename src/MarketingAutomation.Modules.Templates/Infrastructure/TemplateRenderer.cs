using MarketingAutomation.Modules.Templates.Domain;
using MarketingAutomation.SharedKernel.Application;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Templates.Infrastructure;

/// <summary>Renders a stored template for the Messaging/Campaigns/Journeys pipeline (SharedKernel contract).</summary>
public sealed class TemplateRenderer(TemplatesDbContext db, LiquidRenderer liquid) : ITemplateRenderer
{
    public async Task<RenderedContent> RenderAsync(
        Guid templateId, IReadOnlyDictionary<string, object?> data, CancellationToken ct)
    {
        var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new NotFoundException("Template", templateId);

        var brand = await db.BrandKits.AsNoTracking().FirstOrDefaultAsync(ct);

        var subject = template.Subject is null ? null : liquid.Render(template.Subject, data, brand);
        var body = liquid.Render(template.HtmlBody, data, brand);
        var text = template.TextBody is null ? null : liquid.Render(template.TextBody, data, brand);

        return new RenderedContent(subject, body, text);
    }
}
