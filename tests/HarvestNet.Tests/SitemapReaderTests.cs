using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class SitemapReaderTests
{
    private const string SimpleSitemap = """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
          <url><loc>https://example.com/page-1</loc></url>
          <url><loc>https://example.com/page-2</loc></url>
        </urlset>
        """;

    [Fact]
    public void ParseUrls_ReturnsAllLocEntries()
    {
        var urls = SitemapReader.ParseUrls(SimpleSitemap);

        Assert.Equal(2, urls.Count);
        Assert.Contains(urls, u => u.AbsoluteUri == "https://example.com/page-1");
        Assert.Contains(urls, u => u.AbsoluteUri == "https://example.com/page-2");
    }

    [Fact]
    public void ParseUrls_RespectsMaxUrls()
    {
        var urls = SitemapReader.ParseUrls(SimpleSitemap, maxUrls: 1);
        Assert.Single(urls);
    }

    [Fact]
    public void ParseUrls_InvalidXml_ReturnsEmptyList()
    {
        var urls = SitemapReader.ParseUrls("not xml at all");
        Assert.Empty(urls);
    }
}
