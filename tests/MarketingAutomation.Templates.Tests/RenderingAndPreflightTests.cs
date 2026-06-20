using MarketingAutomation.Modules.Templates.Domain;
using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Templates.Tests;

public class RenderingTests
{
    private readonly LiquidRenderer _renderer = new();

    [Fact]
    public void Merge_tags_are_filled_from_data()
    {
        var html = _renderer.Render("Hi {{ firstName }}, welcome!",
            new Dictionary<string, object?> { ["firstName"] = "Alice" });
        Assert.Equal("Hi Alice, welcome!", html);
    }

    [Fact]
    public void Missing_field_uses_the_default_filter()
    {
        var html = _renderer.Render("Hi {{ firstName | default: \"there\" }}!",
            new Dictionary<string, object?>());
        Assert.Equal("Hi there!", html);
    }

    [Fact]
    public void Missing_field_without_default_renders_empty()
    {
        var html = _renderer.Render("Hi {{ firstName }}!", new Dictionary<string, object?>());
        Assert.Equal("Hi !", html);
    }

    [Fact]
    public void Brand_variables_are_available()
    {
        var brand = new BrandKit { LogoUrl = "https://cdn/logo.png", PrimaryColor = "#FF0000" };
        var html = _renderer.Render(
            "<img src=\"{{ brandLogoUrl }}\"><span style=\"color:{{ brandPrimaryColor }}\">x</span>",
            new Dictionary<string, object?>(), brand);
        Assert.Contains("https://cdn/logo.png", html);
        Assert.Contains("#FF0000", html);
    }

    [Fact]
    public void Conditional_liquid_renders()
    {
        var html = _renderer.Render(
            "{% if vip %}VIP{% else %}Standard{% endif %}",
            new Dictionary<string, object?> { ["vip"] = true });
        Assert.Equal("VIP", html);
    }
}

public class PreSendCheckerTests
{
    private static Template Email(string html, string? subject = "Hello") =>
        new() { Name = "t", Channel = Channel.Email, Subject = subject, HtmlBody = html };

    [Fact]
    public void Marketing_email_without_unsubscribe_is_an_error()
    {
        var issues = PreSendChecker.Check(Email("<p>Buy now</p>"));
        Assert.Contains(issues, i => i.Severity == PreSendSeverity.Error && i.Code == "unsubscribe_missing");
    }

    [Fact]
    public void Email_with_unsubscribe_link_passes_that_check()
    {
        var issues = PreSendChecker.Check(Email("<p>Buy</p><a href=\"https://x/u\">Unsubscribe</a>"));
        Assert.DoesNotContain(issues, i => i.Code == "unsubscribe_missing");
    }

    [Fact]
    public void Missing_subject_is_an_error()
    {
        var issues = PreSendChecker.Check(Email("<a href=\"u\">unsubscribe</a>", subject: null));
        Assert.Contains(issues, i => i.Code == "subject_missing");
    }

    [Fact]
    public void Merge_tag_without_fallback_warns_but_with_default_does_not()
    {
        var withoutFallback = PreSendChecker.Check(Email("Hi {{ firstName }} <a href=\"u\">unsubscribe</a>"));
        Assert.Contains(withoutFallback, i => i.Code == "merge_no_fallback");

        var withFallback = PreSendChecker.Check(
            Email("Hi {{ firstName | default: \"there\" }} <a href=\"u\">unsubscribe</a>"));
        Assert.DoesNotContain(withFallback, i => i.Code == "merge_no_fallback");
    }

    [Fact]
    public void Empty_href_warns()
    {
        var issues = PreSendChecker.Check(Email("<a href=\"\">x</a> <a href=\"u\">unsubscribe</a>"));
        Assert.Contains(issues, i => i.Code == "empty_link");
    }
}
