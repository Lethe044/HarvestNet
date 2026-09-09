namespace HarvestNet.Core.Crawling;

/// <summary>
/// Configuration for a crawl run: concurrency, politeness, retries and robots.txt behavior.
/// </summary>
public sealed class CrawlOptions
{
    /// <summary>Maximum number of requests that can be in flight at the same time.</summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>Maximum number of link-following hops from the seed URLs.</summary>
    public int MaxDepth { get; set; } = 3;

    /// <summary>Hard cap on the total number of pages fetched in a single run.</summary>
    public int MaxPages { get; set; } = 1000;

    /// <summary>Timeout applied to each individual HTTP request.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Minimum delay enforced between two requests to the same host.</summary>
    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Whether robots.txt rules should be checked before fetching a URL.</summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>User-Agent header sent with every request.</summary>
    public string UserAgent { get; set; } = "HarvestNet/1.0 (+https://github.com/Lethe044/HarvestNet)";

    /// <summary>Number of retry attempts for transient failures (429 and 5xx responses, network errors).</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay used for exponential backoff between retries.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Whether HTTP redirects are followed automatically.</summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// A pool of proxy URLs (for example "http://host:port") to rotate through, one per
    /// request, round robin. Leave empty to make requests directly.
    /// </summary>
    public List<string> ProxyPool { get; set; } = new();

    /// <summary>Username applied to every proxy in <see cref="ProxyPool"/>, if they require authentication.</summary>
    public string? ProxyUsername { get; set; }

    /// <summary>Password applied to every proxy in <see cref="ProxyPool"/>, if they require authentication.</summary>
    public string? ProxyPassword { get; set; }

    /// <summary>
    /// When set, fetched pages are cached to disk under this directory and reused on later
    /// runs instead of making a new request. Useful while iterating on selectors, since it
    /// avoids re-fetching the same pages over and over. Leave null to always fetch live.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// A pool of User-Agent strings to rotate through, one per request, round robin.
    /// Leave empty to send <see cref="UserAgent"/> on every request.
    /// </summary>
    public List<string> UserAgentPool { get; set; } = new();

    /// <summary>
    /// When true (the default), URLs are normalized before being checked against the
    /// visited set: tracking query parameters (utm_source and similar), the fragment, and
    /// a trailing slash are stripped. This avoids re-crawling pages that only differ by a
    /// tracking parameter. The original URL is still used for the actual request.
    /// </summary>
    public bool NormalizeUrls { get; set; } = true;

    /// <summary>
    /// Caps how many requests to the same host can be in flight at once, on top of the
    /// overall <see cref="MaxConcurrency"/> limit. Leave null (the default) to only apply
    /// the overall limit; set it when a crawl spans many domains and each one should see
    /// a gentler load than the crawl's total concurrency would otherwise allow.
    /// </summary>
    public int? MaxConcurrencyPerHost { get; set; }

    /// <summary>
    /// When true, the delay used for a host grows automatically after failures or 429
    /// responses from that host, and relaxes back down after a run of successes, instead
    /// of always using the fixed <see cref="DelayBetweenRequests"/>. Off by default so
    /// existing behavior does not change unless you opt in.
    /// </summary>
    public bool AdaptiveThrottling { get; set; }
}
