using HarvestNet.Core.Healing;
using Xunit;

namespace HarvestNet.Tests;

public class HealingCacheTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"harvestnet-test-{Guid.NewGuid()}.json");

    [Fact]
    public void SetThenGet_ReturnsStoredSelector()
    {
        var cache = new HealingCache(_filePath);
        cache.Set("example.com", "price", ".product-price");

        var result = cache.TryGet("example.com", "price");

        Assert.Equal(".product-price", result);
    }

    [Fact]
    public void Get_UnknownField_ReturnsNull()
    {
        var cache = new HealingCache(_filePath);
        var result = cache.TryGet("example.com", "unknown");

        Assert.Null(result);
    }

    [Fact]
    public void NewCacheInstance_LoadsFromDisk()
    {
        var first = new HealingCache(_filePath);
        first.Set("example.com", "title", "h1.title");

        var second = new HealingCache(_filePath);
        var result = second.TryGet("example.com", "title");

        Assert.Equal("h1.title", result);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
