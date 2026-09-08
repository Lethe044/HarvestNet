using System.Collections.Concurrent;

namespace HarvestNet.Core;

/// <summary>
/// Statistics about a completed <see cref="HarvestSpider{T}.RunAsync"/> call.
/// </summary>
public sealed class HarvestRunSummary
{
    internal int PagesFetchedInternal;
    internal int PagesFailedInternal;
    internal int ItemsExtractedInternal;
    internal readonly ConcurrentDictionary<string, int> FieldPresentCountsInternal = new();

    public int PagesFetched => PagesFetchedInternal;
    public int PagesFailed => PagesFailedInternal;
    public int ItemsExtracted => ItemsExtractedInternal;
    public TimeSpan Elapsed { get; internal set; }

    /// <summary>
    /// For each field name, the fraction (0 to 1) of extracted items where that field had
    /// a non-empty value. A field sitting far below the others is usually a sign that its
    /// selector broke for some page template while still working for others.
    /// </summary>
    public IReadOnlyDictionary<string, double> FieldCoverage
    {
        get
        {
            var itemCount = ItemsExtracted;
            if (itemCount == 0)
            {
                return new Dictionary<string, double>();
            }

            return FieldPresentCountsInternal.ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value / itemCount);
        }
    }
}
