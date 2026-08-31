namespace HarvestNet.Core.Crawling;

/// <summary>
/// A single page fetch request queued up by the crawler.
/// </summary>
public sealed class CrawlRequest
{
    /// <summary>The absolute URL to fetch.</summary>
    public required Uri Url { get; init; }

    /// <summary>How many link hops away from the seed URLs this request is.</summary>
    public int Depth { get; init; }

    /// <summary>Arbitrary metadata that travels with the request (useful for custom link extractors).</summary>
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
