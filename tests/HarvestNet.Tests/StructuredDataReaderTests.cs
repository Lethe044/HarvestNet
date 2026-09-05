using HarvestNet.Core.Extraction;
using Xunit;

namespace HarvestNet.Tests;

public class StructuredDataReaderTests
{
    private const string ProductHtml = """
        <html>
          <head>
            <meta property="og:title" content="Wireless Headphones" />
            <meta property="og:price:amount" content="59.99" />
            <meta name="description" content="Great sound, all day battery." />
            <script type="application/ld+json">
            {
              "@type": "Product",
              "name": "Wireless Headphones",
              "offers": { "price": "59.99", "priceCurrency": "USD" }
            }
            </script>
          </head>
          <body></body>
        </html>
        """;

    [Fact]
    public void ReadJsonLd_ParsesSingleObjectBlock()
    {
        var blocks = StructuredDataReader.ReadJsonLd(ProductHtml);

        Assert.Single(blocks);
        Assert.Equal("Product", blocks[0].GetProperty("@type").GetString());
    }

    [Fact]
    public void ReadOpenGraphTags_ReturnsOgProperties()
    {
        var tags = StructuredDataReader.ReadOpenGraphTags(ProductHtml);

        Assert.Equal("Wireless Headphones", tags["og:title"]);
        Assert.Equal("59.99", tags["og:price:amount"]);
        Assert.False(tags.ContainsKey("description"));
    }

    [Fact]
    public void ReadMetaTags_ReturnsNameProperties()
    {
        var tags = StructuredDataReader.ReadMetaTags(ProductHtml);

        Assert.Equal("Great sound, all day battery.", tags["description"]);
    }

    [Fact]
    public void ReadJsonLd_MalformedBlock_IsSkippedNotThrown()
    {
        const string html = """
            <script type="application/ld+json">{ not valid json </script>
            """;

        var blocks = StructuredDataReader.ReadJsonLd(html);

        Assert.Empty(blocks);
    }
}
