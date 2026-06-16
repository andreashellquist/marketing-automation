namespace MarketingAutomation.Modules.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Anthropic API key. Falls back to the ANTHROPIC_API_KEY environment variable.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Generation model. Defaults to the latest Opus.</summary>
    public string Model { get; set; } = "claude-opus-4-8";
}
