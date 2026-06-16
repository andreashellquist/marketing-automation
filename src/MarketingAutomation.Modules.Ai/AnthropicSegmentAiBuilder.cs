using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.Extensions.Options;

namespace MarketingAutomation.Modules.Ai;

/// <summary>
/// Turns a natural-language audience description into a segment AST using Claude
/// (SharedKernel contract). The model returns the AST as JSON; we parse it into a
/// <see cref="SegmentGroup"/> for confirmation in the visual builder. AI output is always
/// a draft — it never sends anything by itself.
/// </summary>
public sealed class AnthropicSegmentAiBuilder : ISegmentAiBuilder
{
    private readonly AnthropicClient _client;
    private readonly AiOptions _options;

    private const string SystemPrompt = """
        You translate a marketing audience description into a segment definition AST.
        Reply with ONLY a JSON object, no prose, matching this shape:
        {
          "combinator": "and" | "or",
          "leaves": [
            {
              "kind": "field" | "attribute" | "event",
              "negate": false,
              "field": "email|phone|firstName|lastName|locale|timezone (field) or attribute key",
              "op": "eq|neq|contains|set|notset",
              "value": "string",
              "eventName": "for kind=event, e.g. order.completed",
              "minCount": 1,
              "withinDays": 30
            }
          ],
          "groups": [ /* nested groups, same shape, optional */ ]
        }
        Use "field" for standard profile fields, "attribute" for custom attributes,
        and "event" for behavioral conditions ("bought twice in 90 days" =>
        kind:event, eventName, minCount:2, withinDays:90). Negate an event for
        "has NOT done X". Omit fields that don't apply.
        """;

    public AnthropicSegmentAiBuilder(IOptions<AiOptions> options)
    {
        _options = options.Value;
        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<SegmentGroup> BuildAsync(string description, CancellationToken ct)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = 2000,
            System = SystemPrompt,
            Messages = [new() { Role = Role.User, Content = description }],
        });

        var text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The model returned no text content.");

        var json = ExtractJsonObject(text);
        return JsonSerializer.Deserialize<SegmentGroup>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not parse a segment definition from the model output.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Defensively pull the first JSON object out of the model's reply.</summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
