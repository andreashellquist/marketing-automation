using FluentValidation;
using MarketingAutomation.Modules.Templates.Domain;
using MarketingAutomation.Modules.Templates.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Templates.Application;

public sealed record TemplateDto(
    Guid Id, string Name, Channel Channel, int Version, string? Subject,
    string HtmlBody, string? TextBody, string? DesignJson, DateTimeOffset CreatedAt)
{
    public static TemplateDto From(Template t) => new(
        t.Id, t.Name, t.Channel, t.Version, t.Subject, t.HtmlBody, t.TextBody, t.DesignJson, t.CreatedAt);
}

// ---- Create / Update / Get ----------------------------------------------------------

public sealed record CreateTemplateCommand(
    string Name, Channel Channel, string? Subject, string HtmlBody, string? TextBody, string? DesignJson)
    : IRequest<TemplateDto>;

public sealed class CreateTemplateValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.HtmlBody).NotEmpty();
    }
}

public sealed class CreateTemplateHandler(TemplatesDbContext db) : IRequestHandler<CreateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(CreateTemplateCommand request, CancellationToken ct)
    {
        var template = new Template
        {
            Name = request.Name,
            Channel = request.Channel,
            Subject = request.Subject,
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
            DesignJson = request.DesignJson,
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync(ct);
        return TemplateDto.From(template);
    }
}

public sealed record UpdateTemplateCommand(
    Guid Id, string Name, string? Subject, string HtmlBody, string? TextBody, string? DesignJson)
    : IRequest<TemplateDto>;

public sealed class UpdateTemplateHandler(TemplatesDbContext db) : IRequestHandler<UpdateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(UpdateTemplateCommand request, CancellationToken ct)
    {
        var template = await Load(db, request.Id, ct);
        template.Name = request.Name;
        template.Subject = request.Subject;
        template.HtmlBody = request.HtmlBody;
        template.TextBody = request.TextBody;
        template.DesignJson = request.DesignJson;
        template.BumpVersion();
        await db.SaveChangesAsync(ct);
        return TemplateDto.From(template);
    }

    internal static async Task<Template> Load(TemplatesDbContext db, Guid id, CancellationToken ct) =>
        await db.Templates.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Template", id);
}

public sealed record GetTemplateQuery(Guid Id) : IRequest<TemplateDto>;

public sealed class GetTemplateHandler(TemplatesDbContext db) : IRequestHandler<GetTemplateQuery, TemplateDto>
{
    public async Task<TemplateDto> Handle(GetTemplateQuery request, CancellationToken ct)
    {
        var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Template", request.Id);
        return TemplateDto.From(template);
    }
}

// ---- Preview (render with sample data) ----------------------------------------------

public sealed record PreviewTemplateCommand(Guid Id, Dictionary<string, object?> Data) : IRequest<RenderedPreviewDto>;

public sealed record RenderedPreviewDto(string? Subject, string Html, string? Text);

public sealed class PreviewTemplateHandler(TemplatesDbContext db, LiquidRenderer liquid)
    : IRequestHandler<PreviewTemplateCommand, RenderedPreviewDto>
{
    public async Task<RenderedPreviewDto> Handle(PreviewTemplateCommand request, CancellationToken ct)
    {
        var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Template", request.Id);
        var brand = await db.BrandKits.AsNoTracking().FirstOrDefaultAsync(ct);

        return new RenderedPreviewDto(
            template.Subject is null ? null : liquid.Render(template.Subject, request.Data, brand),
            liquid.Render(template.HtmlBody, request.Data, brand),
            template.TextBody is null ? null : liquid.Render(template.TextBody, request.Data, brand));
    }
}

// ---- Pre-send checks ----------------------------------------------------------------

public sealed record PreflightTemplateQuery(Guid Id) : IRequest<IReadOnlyList<PreSendIssue>>;

public sealed class PreflightTemplateHandler(TemplatesDbContext db)
    : IRequestHandler<PreflightTemplateQuery, IReadOnlyList<PreSendIssue>>
{
    public async Task<IReadOnlyList<PreSendIssue>> Handle(PreflightTemplateQuery request, CancellationToken ct)
    {
        var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Template", request.Id);
        return PreSendChecker.Check(template);
    }
}

// ---- Brand kit ----------------------------------------------------------------------

public sealed record SetBrandKitCommand(
    string? LogoUrl, string? PrimaryColor, string? FontFamily, string? FooterHtml, string? CompanyAddress)
    : IRequest;

public sealed class SetBrandKitHandler(TemplatesDbContext db) : IRequestHandler<SetBrandKitCommand>
{
    public async Task Handle(SetBrandKitCommand request, CancellationToken ct)
    {
        var brand = await db.BrandKits.FirstOrDefaultAsync(ct);
        if (brand is null)
        {
            brand = new BrandKit();
            db.BrandKits.Add(brand);
        }
        brand.LogoUrl = request.LogoUrl;
        brand.PrimaryColor = request.PrimaryColor;
        brand.FontFamily = request.FontFamily;
        brand.FooterHtml = request.FooterHtml;
        brand.CompanyAddress = request.CompanyAddress;
        await db.SaveChangesAsync(ct);
    }
}
