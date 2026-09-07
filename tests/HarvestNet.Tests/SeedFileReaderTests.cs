using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class SeedFileReaderTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"harvestnet-seeds-{Guid.NewGuid()}.txt");

    [Fact]
    public void ReadUrls_SkipsBlankLinesAndComments()
    {
        File.WriteAllLines(_filePath, new[]
        {
            "# a list of product pages",
            "https://example.com/product/1",
            "",
            "   ",
            "https://example.com/product/2",
            "# https://example.com/should-be-ignored"
        });

        var urls = SeedFileReader.ReadUrls(_filePath);

        Assert.Equal(2, urls.Count);
        Assert.Contains(urls, u => u.AbsoluteUri == "https://example.com/product/1");
        Assert.Contains(urls, u => u.AbsoluteUri == "https://example.com/product/2");
    }

    [Fact]
    public void ReadUrls_SkipsInvalidUrls()
    {
        File.WriteAllLines(_filePath, new[] { "not a url", "https://example.com/ok" });

        var urls = SeedFileReader.ReadUrls(_filePath);

        Assert.Single(urls);
        Assert.Equal("https://example.com/ok", urls[0].AbsoluteUri);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
