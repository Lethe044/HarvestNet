using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class UrlNormalizerTests
{
    [Fact]
    public void Normalize_RemovesTrackingParams()
    {
        var result = UrlNormalizer.Normalize(new Uri("https://example.com/page?utm_source=x&id=5"));
        Assert.Equal("https://example.com/page?id=5", result.AbsoluteUri);
    }

    [Fact]
    public void Normalize_RemovesFragment()
    {
        var result = UrlNormalizer.Normalize(new Uri("https://example.com/page#section"));
        Assert.Equal("https://example.com/page", result.AbsoluteUri);
    }

    [Fact]
    public void Normalize_TrimsTrailingSlash()
    {
        var result = UrlNormalizer.Normalize(new Uri("https://example.com/page/"));
        Assert.Equal("https://example.com/page", result.AbsoluteUri);
    }

    [Fact]
    public void Normalize_RootPath_KeepsSingleSlash()
    {
        var result = UrlNormalizer.Normalize(new Uri("https://example.com/"));
        Assert.Equal("https://example.com/", result.AbsoluteUri);
    }

    [Fact]
    public void Normalize_NoTrackingParams_KeepsQueryAsIs()
    {
        var result = UrlNormalizer.Normalize(new Uri("https://example.com/search?q=shoes&page=2"));
        Assert.Equal("https://example.com/search?q=shoes&page=2", result.AbsoluteUri);
    }
}
