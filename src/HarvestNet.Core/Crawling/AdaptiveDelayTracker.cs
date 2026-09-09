using System.Collections.Concurrent;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// When enabled via <see cref="CrawlOptions.AdaptiveThrottling"/>, gradually increases the
/// delay used for a host after failures or 429 responses, and gradually relaxes it back
/// down after a run of successes, instead of using one fixed delay for the whole crawl.
/// </summary>
internal sealed class AdaptiveDelayTracker
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    private const double MinMultiplier = 1.0;
    private const double MaxMultiplier = 16.0;

    private readonly TimeSpan _baseDelay;
    private readonly ConcurrentDictionary<string, double> _multipliers = new();

    public AdaptiveDelayTracker(TimeSpan baseDelay)
    {
        _baseDelay = baseDelay;
    }

    public TimeSpan GetDelay(string host)
    {
        var multiplier = _multipliers.GetOrAdd(host, MinMultiplier);
        var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * multiplier);
        return delay > MaxDelay ? MaxDelay : delay;
    }

    public void ReportSuccess(string host) =>
        _multipliers.AddOrUpdate(host, MinMultiplier, (_, current) => Math.Max(MinMultiplier, current * 0.85));

    public void ReportFailure(string host) =>
        _multipliers.AddOrUpdate(host, 2.0, (_, current) => Math.Min(MaxMultiplier, current * 2.0));
}
