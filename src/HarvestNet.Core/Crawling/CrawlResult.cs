namespace HarvestNet.Core.Crawling;

/// <summary>
/// The outcome of fetching a single page.
/// </summary>
public sealed class CrawlResult
{
    /// <summary>The URL that was fetched.</summary>
    public required Uri Url { get; init; }

    /// <summary>The HTTP status code returned, or 0 if the request never reached the server.</summary>
    public int StatusCode { get; init; }

    /// <summary>The raw HTML body, when the fetch succeeded.</summary>
    public string? Html { get; init; }

    /// <summary>True when the page was fetched successfully and is safe to parse.</summary>
    public bool Success { get; init; }

    /// <summary>A human readable error message when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }

    /// <summary>The crawl depth of the request that produced this result.</summary>
    public int Depth { get; init; }

    /// <summary>How long the fetch took, including retries.</summary>
    public TimeSpan Elapsed { get; init; }
}
