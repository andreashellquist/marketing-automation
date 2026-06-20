using System.Text.RegularExpressions;
using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Templates.Domain;

public enum PreSendSeverity { Error, Warning }

public sealed record PreSendIssue(PreSendSeverity Severity, string Code, string Message);

/// <summary>
/// Static pre-send linting for a template. Errors block a marketing send (e.g. a missing
/// unsubscribe link, required by Gmail/Yahoo and by law); warnings are advisory.
/// </summary>
public static partial class PreSendChecker
{
    public static IReadOnlyList<PreSendIssue> Check(Template template)
    {
        var issues = new List<PreSendIssue>();
        var body = template.HtmlBody ?? string.Empty;

        if (template.Channel == Channel.Email)
        {
            if (string.IsNullOrWhiteSpace(template.Subject))
                issues.Add(new(PreSendSeverity.Error, "subject_missing", "Email templates need a subject line."));

            // Marketing email must offer an unsubscribe path (link or merge variable).
            var hasUnsubscribe = body.Contains("unsubscribe", StringComparison.OrdinalIgnoreCase)
                                 || body.Contains("List-Unsubscribe", StringComparison.OrdinalIgnoreCase);
            if (!hasUnsubscribe)
                issues.Add(new(PreSendSeverity.Error, "unsubscribe_missing",
                    "Marketing email must contain an unsubscribe link."));
        }

        // Merge tags without a default filter may render empty for some recipients.
        foreach (Match m in MergeTagRegex().Matches(body))
        {
            var inner = m.Groups[1].Value;
            if (!inner.Contains("default", StringComparison.OrdinalIgnoreCase) && !inner.Contains('.'))
                issues.Add(new(PreSendSeverity.Warning, "merge_no_fallback",
                    $"Merge tag '{{{{{inner}}}}}' has no fallback; it may render empty."));
        }

        // Empty links.
        foreach (Match m in EmptyHrefRegex().Matches(body))
            issues.Add(new(PreSendSeverity.Warning, "empty_link", "An anchor has an empty href."));

        return issues;
    }

    [GeneratedRegex(@"\{\{(.+?)\}\}")]
    private static partial Regex MergeTagRegex();

    [GeneratedRegex("href\\s*=\\s*\"\"")]
    private static partial Regex EmptyHrefRegex();
}
