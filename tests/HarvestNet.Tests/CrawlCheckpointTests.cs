using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class CrawlCheckpointTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"harvestnet-checkpoint-{Guid.NewGuid()}.json");

    [Fact]
    public void SaveThenLoad_RoundTripsState()
    {
        var checkpoint = new CrawlCheckpoint
        {
            Visited = new List<string> { "https://example.com/a", "https://example.com/b" },
            Frontier = new List<CheckpointUrl> { new() { Url = "https://example.com/c", Depth = 1 } },
            Depth = 1,
            PagesFetched = 2,
            PagesFailed = 0,
            ItemsExtracted = 5
        };

        CrawlCheckpoint.Save(_filePath, checkpoint);
        var loaded = CrawlCheckpoint.LoadOrNull(_filePath);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Visited.Count);
        Assert.Single(loaded.Frontier);
        Assert.Equal("https://example.com/c", loaded.Frontier[0].Url);
        Assert.Equal(5, loaded.ItemsExtracted);
    }

    [Fact]
    public void LoadOrNull_MissingFile_ReturnsNull()
    {
        var result = CrawlCheckpoint.LoadOrNull(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid()}.json"));
        Assert.Null(result);
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        CrawlCheckpoint.Save(_filePath, new CrawlCheckpoint());
        Assert.True(File.Exists(_filePath));

        CrawlCheckpoint.Delete(_filePath);
        Assert.False(File.Exists(_filePath));
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
