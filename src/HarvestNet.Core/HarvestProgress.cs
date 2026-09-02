namespace HarvestNet.Core;

/// <summary>
/// A snapshot reported after each page is processed, for anything that wants to show live
/// progress (a CLI counter, a progress bar, a log line) during a long crawl.
/// </summary>
public sealed class HarvestProgress
{
    public required Uri LastUrl { get; init; }
    public int PagesFetched { get; init; }
    public int PagesFailed { get; init; }
    public int ItemsExtracted { get; init; }
}
