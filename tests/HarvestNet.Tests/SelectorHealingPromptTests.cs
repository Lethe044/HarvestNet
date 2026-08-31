using HarvestNet.Core.Healing;
using Xunit;

namespace HarvestNet.Tests;

public class SelectorHealingPromptTests
{
    [Fact]
    public void ExtractSelector_ParsesCleanJson()
    {
        var response = "{\"selector\": \".new-price-class\", \"confidence\": 0.9}";
        var result = SelectorHealingPrompt.ExtractSelector(response);

        Assert.Equal(".new-price-class", result);
    }

    [Fact]
    public void ExtractSelector_StripsMarkdownCodeFence()
    {
        var response = "```json\n{\"selector\": \".fixed\", \"confidence\": 0.7}\n```";
        var result = SelectorHealingPrompt.ExtractSelector(response);

        Assert.Equal(".fixed", result);
    }

    [Fact]
    public void ExtractSelector_NullSelector_ReturnsNull()
    {
        var response = "{\"selector\": null, \"confidence\": 0}";
        var result = SelectorHealingPrompt.ExtractSelector(response);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSelector_InvalidJson_FallsBackToRegex()
    {
        var response = "Sure thing, here it is: \"selector\": \".fallback-class\" hope that helps!";
        var result = SelectorHealingPrompt.ExtractSelector(response);

        Assert.Equal(".fallback-class", result);
    }
}
