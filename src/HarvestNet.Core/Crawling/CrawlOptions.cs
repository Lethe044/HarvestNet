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
}
