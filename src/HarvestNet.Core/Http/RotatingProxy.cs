using System.Net;

namespace HarvestNet.Core.Http;

/// <summary>
/// A simple round robin <see cref="IWebProxy"/> used when <see cref="Crawling.CrawlOptions.ProxyPool"/>
/// contains more than one proxy. Credentials, when supplied, are shared across every proxy
/// in the pool, which covers the common case of a single login for a rotating proxy
/// gateway.
/// </summary>
internal sealed class RotatingProxy : IWebProxy
{
    private readonly List<Uri> _proxies;
    private int _index = -1;

    public RotatingProxy(IEnumerable<string> proxyUrls, ICredentials? credentials = null)
    {
        _proxies = proxyUrls.Select(p => new Uri(p)).ToList();
        if (_proxies.Count == 0)
        {
            throw new ArgumentException("At least one proxy URL is required.", nameof(proxyUrls));
        }

        Credentials = credentials;
    }

    public ICredentials? Credentials { get; set; }

    public Uri GetProxy(Uri destination)
    {
        var next = Interlocked.Increment(ref _index);
        var index = ((next % _proxies.Count) + _proxies.Count) % _proxies.Count;
        return _proxies[index];
    }

    public bool IsBypassed(Uri host) => false;
}
