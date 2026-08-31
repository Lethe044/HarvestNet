using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class RobotsRulesTests
{
    [Fact]
    public void Parse_DisallowedPath_IsBlocked()
    {
        var content = "User-agent: *\nDisallow: /private\nDisallow: /admin\n";
        var rules = RobotsRules.Parse(content);

        Assert.False(rules.IsAllowed("/private/data", "HarvestNet/1.0"));
        Assert.False(rules.IsAllowed("/admin", "HarvestNet/1.0"));
    }

    [Fact]
    public void Parse_AllowedPath_IsAllowed()
    {
        var content = "User-agent: *\nDisallow: /private\n";
        var rules = RobotsRules.Parse(content);

        Assert.True(rules.IsAllowed("/public/page", "HarvestNet/1.0"));
    }

    [Fact]
    public void AllowAll_AllowsEverything()
    {
        var rules = RobotsRules.AllowAll();
        Assert.True(rules.IsAllowed("/anything", "HarvestNet/1.0"));
    }

    [Fact]
    public void Parse_IgnoresOtherUserAgentSections()
    {
        var content = "User-agent: BadBot\nDisallow: /\nUser-agent: *\nDisallow: /only-this\n";
        var rules = RobotsRules.Parse(content);

        Assert.True(rules.IsAllowed("/public", "HarvestNet/1.0"));
        Assert.False(rules.IsAllowed("/only-this", "HarvestNet/1.0"));
    }
}
