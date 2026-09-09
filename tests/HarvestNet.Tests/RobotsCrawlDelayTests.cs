using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class RobotsCrawlDelayTests
{
    [Fact]
    public void Parse_CrawlDelay_IsRead()
    {
        var content = "User-agent: *\nCrawl-delay: 2.5\nDisallow: /private\n";
        var rules = RobotsRules.Parse(content);

        Assert.Equal(TimeSpan.FromSeconds(2.5), rules.CrawlDelay);
    }

    [Fact]
    public void Parse_NoCrawlDelay_IsNull()
    {
        var content = "User-agent: *\nDisallow: /private\n";
        var rules = RobotsRules.Parse(content);

        Assert.Null(rules.CrawlDelay);
    }

    [Fact]
    public void Parse_CrawlDelayInOtherAgentSection_IsIgnored()
    {
        var content = "User-agent: BadBot\nCrawl-delay: 60\nUser-agent: *\nDisallow: /\n";
        var rules = RobotsRules.Parse(content);

        Assert.Null(rules.CrawlDelay);
    }

    [Fact]
    public void AllowAll_HasNoCrawlDelay()
    {
        Assert.Null(RobotsRules.AllowAll().CrawlDelay);
    }
}
