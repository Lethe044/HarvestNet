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

    /// <summary>The HTTP method to use. Defaults to GET; set to POST for form or search submissions.</summary>
    public HttpMethod Method { get; init; } = HttpMethod.Get;

    /// <summary>
    /// Form fields sent as an application/x-www-form-urlencoded body when <see cref="Method"/>
    /// is POST. Ignored for GET requests.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FormData { get; init; }

    /// <summary>Arbitrary metadata that travels with the request (useful for custom link extractors).</summary>
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
