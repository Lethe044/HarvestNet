using System.Collections.Concurrent;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// Enforces a minimum delay between consecutive requests to the same host, so a crawl
/// does not hammer a single site even when overall concurrency is high.
/// </summary>
public sealed class DomainRateLimiter
{
    private readonly TimeSpan _delay;
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _domainLocks = new();

    public DomainRateLimiter(TimeSpan delay)
    {
        _delay = delay;
    }

    /// <summary>
    /// Waits, if necessary, until enough time has passed since the last request to <paramref name="host"/>.
    /// </summary>
    public async Task WaitAsync(string host, CancellationToken cancellationToken = default)
    {
        var gate = _domainLocks.GetOrAdd(host, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_lastRequestTimes.TryGetValue(host, out var last))
            {
                var elapsed = DateTime.UtcNow - last;
                if (elapsed < _delay)
                {
                    await Task.Delay(_delay - elapsed, cancellationToken).ConfigureAwait(false);
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
