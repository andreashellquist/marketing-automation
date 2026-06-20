using Fluid;

namespace MarketingAutomation.Modules.Templates.Domain;

/// <summary>
/// Renders Liquid bodies with recipient data plus brand variables. Missing merge fields
/// render empty (Liquid semantics) — authors supply fallbacks with the <c>default</c> filter.
/// </summary>
public sealed class LiquidRenderer
{
    private static readonly FluidParser Parser = new();

    public string Render(string source, IReadOnlyDictionary<string, object?> data, BrandKit? brand = null)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        if (!Parser.TryParse(source, out var template, out var error))
            throw new InvalidOperationException($"Template parse error: {error}");

        var context = new TemplateContext();
        foreach (var (key, value) in data)
        {
            context.SetValue(key, value);
        }

        if (brand is not null)
        {
            context.SetValue("brandLogoUrl", brand.LogoUrl);
            context.SetValue("brandPrimaryColor", brand.PrimaryColor);
            context.SetValue("brandFontFamily", brand.FontFamily);
            context.SetValue("brandFooterHtml", brand.FooterHtml);
            context.SetValue("brandCompanyAddress", brand.CompanyAddress);
        }

        return template.Render(context);
    }
}
