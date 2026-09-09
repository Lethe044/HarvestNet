using HarvestNet.Core.Extraction;
using Xunit;

namespace HarvestNet.Tests;

public class ContentExtractorTests
{
    private const string ArticlePage = """
        <html>
          <head><title>Test</title></head>
          <body>
            <nav><a href="/">Home</a><a href="/about">About</a><a href="/contact">Contact</a></nav>
            <header><h1>Site Name</h1></header>
            <article>
              <h1>A long article about C# and web scraping</h1>
              <p>Web scraping in .NET has historically lacked the kind of modern, actively
              maintained tooling that Python developers have enjoyed for years, which is a
              real gap in the ecosystem worth addressing properly.</p>
              <p>Self healing selectors, using a large language model to repair a broken
              CSS selector automatically, is one way to make scrapers meaningfully more
              resilient to the constant churn of front end redesigns across the web.</p>
              <p>Combined with JSON-LD extraction, proxy rotation, and a resumable
              crawler, a modern .NET scraping framework becomes genuinely production
              ready rather than a toy project.</p>
            </article>
            <aside><a href="/ad1">Buy now</a><a href="/ad2">Sale</a><a href="/ad3">Click here</a></aside>
            <footer><a href="/privacy">Privacy</a><a href="/terms">Terms</a></footer>
          </body>
        </html>
        """;

    [Fact]
    public void ExtractMainContent_PicksArticleOverNavigationAndFooter()
    {
        var content = ContentExtractor.ExtractMainContent(ArticlePage);

        Assert.NotNull(content);
        Assert.Contains("Self healing selectors", content);
        Assert.Contains("JSON-LD extraction", content);
        Assert.DoesNotContain("Buy now", content);
        Assert.DoesNotContain("Privacy", content);
    }

    [Fact]
    public void ExtractMainContent_ThinPage_ReturnsNull()
    {
        const string html = "<html><body><nav><a href=\"/\">Home</a></nav><p>Hi</p></body></html>";

        var content = ContentExtractor.ExtractMainContent(html);

        Assert.Null(content);
    }

    [Fact]
    public void ExtractMainContent_InternalOverload_DoesNotMutateSharedDocument()
    {
        var context = AngleSharp.BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = context.OpenAsync(req => req.Content(ArticlePage)).GetAwaiter().GetResult();

        var content = ContentExtractor.ExtractMainContent(document.DocumentElement);

        Assert.NotNull(content);
        // If ContentExtractor had removed noise elements in place instead of on a clone,
        // the nav (still needed by other fields extracted from the same document) would
        // be gone from the original document here.
        Assert.NotNull(document.QuerySelector("nav"));
        Assert.NotNull(document.QuerySelector("footer"));
    }
}
