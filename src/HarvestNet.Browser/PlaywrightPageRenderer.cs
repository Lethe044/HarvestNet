using HarvestNet.Core.Http;
using Microsoft.Playwright;

namespace HarvestNet.Browser;

/// <summary>
/// Renders pages using a headless Chromium instance through Playwright. This is what
/// makes HarvestNet able to scrape sites where the meaningful content only appears after
/// JavaScript runs, which a plain HTTP GET cannot see.
///
/// Requires the Playwright browser binaries to be installed once per machine:
/// <code>
/// dotnet tool install --global Microsoft.Playwright.CLI
/// playwright install chromium
/// </code>
/// The browser is launched lazily on first use and reused for every page rendered by this
/// instance; dispose it when the crawl is finished.
/// </summary>
public sealed class PlaywrightPageRenderer : IPageRenderer
{
    private readonly BrowserRenderOptions _options;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightPageRenderer(BrowserRenderOptions? options = null)
    {
        _options = options ?? new BrowserRenderOptions();
    }

    public async Task<string> RenderAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var browser = await GetBrowserAsync().ConfigureAwait(false);

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = _options.UserAgent
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            await page.GotoAsync(url.AbsoluteUri, new PageGotoOptions
            {
                Timeout = (float)_options.NavigationTimeout.TotalMilliseconds,
                WaitUntil = WaitUntilState.NetworkIdle
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_options.WaitForSelector))
            {
                await page.WaitForSelectorAsync(_options.WaitForSelector, new PageWaitForSelectorOptions
                {
                    Timeout = (float)_options.NavigationTimeout.TotalMilliseconds
                }).ConfigureAwait(false);
            }

            if (_options.ExtraDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.ExtraDelay, cancellationToken).ConfigureAwait(false);
            }

            return await page.ContentAsync().ConfigureAwait(false);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_browser is not null)
            {
                return _browser;
            }

            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _options.Headless
            }).ConfigureAwait(false);

            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();
        _initLock.Dispose();
    }
}
