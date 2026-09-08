using HarvestNet.Core.Extraction;
using Xunit;

namespace HarvestNet.Tests;

public class ExtractionEngineTests
{
    private const string SampleHtml = """
        <html>
          <body>
            <div class="quote">
              <span class="text">The only limit is your imagination.</span>
              <small class="author">Anonymous</small>
              <a class="tag-link" href="/tag/wisdom">wisdom</a>
            </div>
            <div class="quote">
              <span class="text">Simplicity is the ultimate sophistication.</span>
              <small class="author">Leonardo da Vinci</small>
              <a class="tag-link" href="/tag/design">design</a>
            </div>
          </body>
        </html>
        """;

    private sealed class Quote
    {
        [HarvestField(".text")]
        public string? Text { get; set; }

        [HarvestField(".author")]
        public string? Author { get; set; }

        [HarvestField(".tag-link", Attribute = "href")]
        public string? TagLink { get; set; }
    }

    [Fact]
    public async Task ExtractListAsync_TypedPoco_ReturnsAllItems()
    {
        var engine = new ExtractionEngine();
        var items = await engine.ExtractListAsync<Quote>(SampleHtml, new Uri("https://example.com/"), ".quote");

        Assert.Equal(2, items.Count);
        Assert.Equal("The only limit is your imagination.", items[0].Text);
        Assert.Equal("Anonymous", items[0].Author);
        Assert.Equal("/tag/wisdom", items[0].TagLink);
        Assert.Equal("Leonardo da Vinci", items[1].Author);
    }

    [Fact]
    public async Task ExtractListAsync_DynamicFields_ReturnsAllItems()
    {
        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["text"] = new FieldSpec { Selector = ".text" },
            ["author"] = new FieldSpec { Selector = ".author" }
        };

        var items = await engine.ExtractListAsync(SampleHtml, new Uri("https://example.com/"), ".quote", fields);

        Assert.Equal(2, items.Count);
        Assert.Equal("Simplicity is the ultimate sophistication.", items[1]["text"]);
        Assert.Equal("Leonardo da Vinci", items[1]["author"]);
    }

    [Fact]
    public async Task ExtractAsync_MissingSelector_FieldValueIsNull()
    {
        var engine = new ExtractionEngine();

        var fields = new Dictionary<string, FieldSpec>
        {
            ["missing"] = new FieldSpec { Selector = ".does-not-exist" }
        };

        var result = await engine.ExtractAsync(SampleHtml, new Uri("https://example.com/"), fields);

        Assert.Null(result["missing"]);
    }

    [Fact]
    public async Task ExtractAsync_XPathSelector_FindsElement()
    {
        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["firstAuthor"] = new FieldSpec { Selector = "//small[@class='author']", Kind = SelectorKind.XPath }
        };

        var result = await engine.ExtractAsync(SampleHtml, new Uri("https://example.com/"), fields);

        Assert.Equal("Anonymous", result["firstAuthor"]);
    }

    [Fact]
    public async Task ExtractAsync_JsonLdSelector_ReadsNestedPath()
    {
        const string html = """
            <html>
              <head>
                <script type="application/ld+json">
                { "name": "Wireless Headphones", "offers": { "price": "59.99" } }
                </script>
              </head>
              <body></body>
            </html>
            """;

        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["price"] = new FieldSpec { Selector = "offers.price", Kind = SelectorKind.JsonLd },
            ["missing"] = new FieldSpec { Selector = "offers.currency", Kind = SelectorKind.JsonLd }
        };

        var result = await engine.ExtractAsync(html, new Uri("https://example.com/"), fields);

        Assert.Equal("59.99", result["price"]);
        Assert.Null(result["missing"]);
    }

    [Fact]
    public async Task ExtractListAsync_FallbackSelector_UsedWhenPrimaryFindsNothing()
    {
        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["author"] = new FieldSpec
            {
                Selector = ".does-not-exist",
                FallbackSelectors = new List<string> { ".author" }
            }
        };

        var items = await engine.ExtractListAsync(SampleHtml, new Uri("https://example.com/"), ".quote", fields);

        Assert.Equal(2, items.Count);
        Assert.Equal("Anonymous", items[0]["author"]);
        Assert.Equal("Leonardo da Vinci", items[1]["author"]);
    }

    [Fact]
    public async Task ExtractListAsync_PrimarySelectorMatches_FallbackNotNeeded()
    {
        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["author"] = new FieldSpec
            {
                Selector = ".author",
                FallbackSelectors = new List<string> { ".this-would-be-wrong" }
            }
        };

        var items = await engine.ExtractListAsync(SampleHtml, new Uri("https://example.com/"), ".quote", fields);

        Assert.Equal("Anonymous", items[0]["author"]);
    }

    [Fact]
    public async Task ExtractListAsync_WithTransforms_CleansUpExtractedValue()
    {
        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["author"] = new FieldSpec
            {
                Selector = ".author",
                Transforms = new List<FieldTransform> { FieldTransform.Uppercase }
            }
        };

        var items = await engine.ExtractListAsync(SampleHtml, new Uri("https://example.com/"), ".quote", fields);

        Assert.Equal("ANONYMOUS", items[0]["author"]);
        Assert.Equal("LEONARDO DA VINCI", items[1]["author"]);
    }
}
