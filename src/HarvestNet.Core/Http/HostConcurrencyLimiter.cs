using System.Collections.Concurrent;

namespace HarvestNet.Core.Http;

/// <summary>
/// Caps how many requests to the same host can be in flight at once, independent of the
/// crawl's overall concurrency limit. Useful when a crawl spans many different domains
/// (from a sitemap or a seed file) and each individual site should see a gentler load
/// even while the crawl as a whole runs at full concurrency.
/// </summary>
internal sealed class HostConcurrencyLimiter
{
    private readonly int _maxPerHost;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostGates = new();

    public HostConcurrencyLimiter(int maxPerHost)
    {
        _maxPerHost = Math.Max(1, maxPerHost);
    }

    public SemaphoreSlim GetGate(string host) =>
        _hostGates.GetOrAdd(host, _ => new SemaphoreSlim(_maxPerHost, _maxPerHost));
}
