using System.Collections.Concurrent;
using System.Globalization;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// Fetches and caches robots.txt files so a crawl can check whether a URL is allowed,
/// and how long to wait between requests to that host, before requesting it.
/// </summary>
public sealed class RobotsTxtService
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, RobotsRules> _cache = new();

    public RobotsTxtService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsAllowedAsync(Uri url, string userAgent, CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(OriginOf(url), cancellationToken).ConfigureAwait(false);
        return rules.IsAllowed(url.PathAndQuery, userAgent);
    }

    /// <summary>Returns the site's requested Crawl-delay for the wildcard user agent, or null if it does not specify one.</summary>
    public async Task<TimeSpan?> GetCrawlDelayAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(OriginOf(url), cancellationToken).ConfigureAwait(false);
        return rules.CrawlDelay;
    }

    private static string OriginOf(Uri url) => $"{url.Scheme}://{url.Authority}";

    private async Task<RobotsRules> GetRulesAsync(string origin, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(origin, out var cached))
        {
            return cached;
        }

        RobotsRules rules;
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(origin + "/robots.txt"), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                rules = RobotsRules.Parse(content);
            }
            else
            {
                rules = RobotsRules.AllowAll();
            }
        }
        catch
        {
            // Sites without a reachable robots.txt are treated as fully allowed, matching
            // the common convention used by well behaved crawlers.
            rules = RobotsRules.AllowAll();
        }

        _cache[origin] = rules;
        return rules;
    }
}

/// <summary>
/// A minimal robots.txt parser covering the "User-agent: *" section: its Disallow rules
/// and an optional Crawl-delay. Per-bot sections and Allow overrides are intentionally out
/// of scope for now.
/// </summary>
internal sealed class RobotsRules
{
    private readonly List<string> _disallowPatterns = new();

    public TimeSpan? CrawlDelay { get; private set; }

    public static RobotsRules AllowAll() => new();

    public static RobotsRules Parse(string content)
    {
        var rules = new RobotsRules();
        var inWildcardSection = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim().ToLowerInvariant();
            var value = line[(separatorIndex + 1)..].Trim();

            if (key == "user-agent")
            {
                inWildcardSection = value == "*";
            }
            else if (key == "disallow" && inWildcardSection && value.Length > 0)
            {
                rules._disallowPatterns.Add(value);
            }
            else if (key == "crawl-delay" && inWildcardSection
                     && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                     && seconds >= 0)
            {
                rules.CrawlDelay = TimeSpan.FromSeconds(seconds);
            }
        }

        return rules;
    }

    public bool IsAllowed(string path, string userAgent)
    {
        foreach (var pattern in _disallowPatterns)
        {
            if (path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
