using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class PaginationHelperTests
{
    [Fact]
    public void FollowNextLink_FindsRelNextAnchor()
    {
        const string html = """
            <html><body>
              <a rel="next" href="/page/2">Next</a>
            </body></html>
            """;

        var extractor = PaginationHelper.FollowNextLink();
        var links = extractor(new Uri("https://example.com/page/1"), html).ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com/page/2", links[0].AbsoluteUri);
    }

    [Fact]
    public void FollowNextLink_FallsBackToPaginationClass()
    {
        const string html = """
            <html><body>
              <div class="pagination"><a class="next" href="/page/3">Next</a></div>
            </body></html>
            """;

        var extractor = PaginationHelper.FollowNextLink();
        var links = extractor(new Uri("https://example.com/page/2"), html).ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com/page/3", links[0].AbsoluteUri);
    }

    [Fact]
    public void FollowNextLink_CustomSelectorTakesPriority()
    {
        const string html = """
            <html><body>
              <a rel="next" href="/wrong">Next</a>
              <a class="my-next" href="/right">Next</a>
            </body></html>
            """;

        var extractor = PaginationHelper.FollowNextLink(".my-next");
        var links = extractor(new Uri("https://example.com/"), html).ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com/right", links[0].AbsoluteUri);
    }

    [Fact]
    public void FollowNextLink_NoNextLink_ReturnsEmpty()
    {
        const string html = "<html><body><p>No more pages.</p></body></html>";

        var extractor = PaginationHelper.FollowNextLink();
        var links = extractor(new Uri("https://example.com/"), html).ToList();

        Assert.Empty(links);
    }
}
