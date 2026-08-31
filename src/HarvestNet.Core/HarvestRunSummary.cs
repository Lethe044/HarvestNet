namespace HarvestNet.Core;

/// <summary>
/// Statistics about a completed <see cref="HarvestSpider{T}.RunAsync"/> call.
/// </summary>
public sealed class HarvestRunSummary
{
    internal int PagesFetchedInternal;
    internal int PagesFailedInternal;
    internal int ItemsExtractedInternal;

    public int PagesFetched => PagesFetchedInternal;
    public int PagesFailed => PagesFailedInternal;
    public int ItemsExtracted => ItemsExtractedInternal;
    public TimeSpan Elapsed { get; internal set; }
}
