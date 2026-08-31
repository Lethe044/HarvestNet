using HarvestNet.Core.Export;
using Xunit;

namespace HarvestNet.Tests;

public class CsvSinkTests
{
    private sealed class Product
    {
        public string Name { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
    }

    [Fact]
    public async Task WriteAsync_ProducesHeaderAndEscapedRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"harvestnet-csv-{Guid.NewGuid()}.csv");

        await using (var sink = new CsvSink<Product>(path))
        {
            await sink.WriteAsync(new Product { Name = "Widget, Deluxe", Price = "19.99" });
            await sink.WriteAsync(new Product { Name = "Simple Widget", Price = "9.99" });
        }

        var lines = await File.ReadAllLinesAsync(path);

        Assert.Equal("Name,Price", lines[0]);
        Assert.Equal("\"Widget, Deluxe\",19.99", lines[1]);
        Assert.Equal("Simple Widget,9.99", lines[2]);

        File.Delete(path);
    }

    [Fact]
    public async Task WriteAsync_DictionaryMode_UsesKeysAsColumns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"harvestnet-csv-dict-{Guid.NewGuid()}.csv");

        await using (var sink = new CsvSink<Dictionary<string, string?>>(path))
        {
            await sink.WriteAsync(new Dictionary<string, string?> { ["title"] = "Hello", ["price"] = "5" });
        }

        var lines = await File.ReadAllLinesAsync(path);

        Assert.Equal("title,price", lines[0]);
        Assert.Equal("Hello,5", lines[1]);

        File.Delete(path);
    }
}
