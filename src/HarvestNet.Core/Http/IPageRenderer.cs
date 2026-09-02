namespace HarvestNet.Core.Http;

/// <summary>
/// Renders a page using a real browser instead of a plain HTTP GET, for sites that need
/// JavaScript to produce their final HTML. Implemented by HarvestNet.Browser using
/// Playwright; pass an instance to <c>HarvestSpider&lt;T&gt;.WithBrowserRendering</c> to
/// use it for a crawl.
/// </summary>
public interface IPageRenderer : IAsyncDisposable
{
    Task<string> RenderAsync(Uri url, CancellationToken cancellationToken = default);
}
