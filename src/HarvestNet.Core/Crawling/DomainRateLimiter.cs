using System.Collections.Concurrent;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// Enforces a minimum delay between consecutive requests to the same host, so a crawl
/// does not hammer a single site even when overall concurrency is high. The delay for a
/// host can be raised above the configured base delay by an adaptive tracker (after
/// repeated failures) or by a one-off minimum for that call (for example a robots.txt
/// Crawl-delay directive), whichever is largest at the time.
/// </summary>
public sealed class DomainRateLimiter
{
    private readonly TimeSpan _delay;
    private readonly AdaptiveDelayTracker? _adaptiveTracker;
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _domainLocks = new();

    public DomainRateLimiter(TimeSpan delay)
        : this(delay, adaptiveTracker: null)
    {
    }

    internal DomainRateLimiter(TimeSpan delay, AdaptiveDelayTracker? adaptiveTracker)
    {
        _delay = delay;
        _adaptiveTracker = adaptiveTracker;
    }

    /// <summary>
    /// Waits, if necessary, until enough time has passed since the last request to
    /// <paramref name="host"/>. <paramref name="minimumDelay"/>, when larger than the
    /// configured or adaptive delay, is used instead for this call only.
    /// </summary>
    public async Task WaitAsync(string host, TimeSpan? minimumDelay = null, CancellationToken cancellationToken = default)
    {
        var gate = _domainLocks.GetOrAdd(host, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = _adaptiveTracker?.GetDelay(host) ?? _delay;
            if (minimumDelay is { } explicitMinimum && explicitMinimum > delay)
            {
                delay = explicitMinimum;
            }

            if (_lastRequestTimes.TryGetValue(host, out var last))
            {
                var elapsed = DateTime.UtcNow - last;
                if (elapsed < delay)
                {
                    await Task.Delay(delay - elapsed, cancellationToken).ConfigureAwait(false);
                }
            }

            _lastRequestTimes[host] = DateTime.UtcNow;
        }
        finally
        {
            gate.Release();
        }
    }
}
