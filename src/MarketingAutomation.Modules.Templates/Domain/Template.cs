using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Templates.Domain;

/// <summary>
/// A reusable, versioned content template. Bodies are Liquid (merge tags like
/// {{ firstName }}, fallbacks via {{ firstName | default: "there" }}). The drag-and-drop
/// editor's design JSON is stored alongside the compiled HTML so the visual builder can
/// round-trip it; sends always use the compiled body re-rendered with recipient data.
/// </summary>
public sealed class Template : TenantEntity
{
    public required string Name { get; set; }
    public Channel Channel { get; set; } = Channel.Email;
    public int Version { get; private set; } = 1;

    // Email: Subject + HtmlBody (+ optional TextBody). SMS: HtmlBody holds the message text.
    public string? Subject { get; set; }
    public required string HtmlBody { get; set; }
    public string? TextBody { get; set; }

    /// <summary>Drag-and-drop editor design document (e.g. Unlayer JSON). Opaque to the backend.</summary>
    public string? DesignJson { get; set; }

    public void BumpVersion() => Version++;
}

/// <summary>Per-tenant brand assets injected into every render as brand* merge variables.</summary>
public sealed class BrandKit : TenantEntity
{
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? FontFamily { get; set; }
    public string? FooterHtml { get; set; }
    public string? CompanyAddress { get; set; }
}
