namespace HarvestNet.Browser;

/// <summary>
/// Options controlling how <see cref="PlaywrightPageRenderer"/> loads a page before
/// returning its HTML.
/// </summary>
public sealed class BrowserRenderOptions
{
    /// <summary>Whether the browser runs without a visible window. Keep this true on servers and in CI.</summary>
    public bool Headless { get; set; } = true;

    /// <summary>Maximum time to wait for navigation and, if set, for <see cref="WaitForSelector"/>.</summary>
    public TimeSpan NavigationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// An optional CSS selector to wait for before reading the page content, useful when
    /// the meaningful content is injected by JavaScript after the initial page load.
    /// </summary>
    public string? WaitForSelector { get; set; }

    /// <summary>An additional fixed delay applied after navigation, for pages with animations or lazy loading.</summary>
    public TimeSpan ExtraDelay { get; set; } = TimeSpan.Zero;

    /// <summary>User-Agent string sent by the browser.</summary>
    public string UserAgent { get; set; } = "HarvestNet/1.1 (+https://github.com/Lethe044/HarvestNet)";
}
