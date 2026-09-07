using HarvestNet.Core.Extraction;
using HarvestNet.Core.Extraction.Schemas;
using Xunit;

namespace HarvestNet.Tests;

public class SchemaOrgTests
{
    private const string ProductPageHtml = """
        <html>
          <head>
            <script type="application/ld+json">
            {
              "@type": "Product",
              "name": "Wireless Headphones",
              "sku": "WH-1000",
              "brand": { "name": "Acme" },
              "offers": { "price": "59.99", "priceCurrency": "USD", "availability": "InStock" },
              "aggregateRating": { "ratingValue": "4.5", "reviewCount": "128" }
            }
            </script>
          </head>
          <body></body>
        </html>
        """;

    private const string ArticlePageHtml = """
        <html>
          <head>
            <script type="application/ld+json">
            {
              "@type": "NewsArticle",
              "headline": "Local team wins championship",
              "author": { "name": "Jane Reporter" },
              "datePublished": "2026-01-15",
              "publisher": { "name": "Daily News" }
            }
            </script>
          </head>
          <body></body>
        </html>
        """;

    [Fact]
    public async Task ExtractAsync_SchemaOrgProduct_PopulatesFromJsonLd()
    {
        var engine = new ExtractionEngine();
        var product = await engine.ExtractAsync<SchemaOrgProduct>(ProductPageHtml, new Uri("https://example.com/product/1"));

        Assert.NotNull(product);
        Assert.Equal("Wireless Headphones", product!.Name);
        Assert.Equal("WH-1000", product.Sku);
        Assert.Equal("Acme", product.Brand);
        Assert.Equal("59.99", product.Price);
        Assert.Equal("USD", product.Currency);
        Assert.Equal("InStock", product.Availability);
        Assert.Equal("4.5", product.RatingValue);
        Assert.Equal("128", product.ReviewCount);
    }

    [Fact]
    public async Task ExtractAsync_SchemaOrgArticle_PopulatesFromJsonLd()
    {
        var engine = new ExtractionEngine();
        var article = await engine.ExtractAsync<SchemaOrgArticle>(ArticlePageHtml, new Uri("https://example.com/news/1"));

        Assert.NotNull(article);
        Assert.Equal("Local team wins championship", article!.Headline);
        Assert.Equal("Jane Reporter", article.Author);
        Assert.Equal("2026-01-15", article.DatePublished);
        Assert.Equal("Daily News", article.Publisher);
    }
}
