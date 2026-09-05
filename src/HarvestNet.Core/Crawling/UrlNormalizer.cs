namespace HarvestNet.Core.Crawling;

/// <summary>
/// Normalizes a URL for the purposes of deduplication: strips common tracking query
/// parameters, the fragment, and a trailing slash. The result is only ever used to decide
/// whether a URL has already been seen; the original URL is still the one actually
/// fetched, so nothing about the real request changes.
/// </summary>
public static class UrlNormalizer
{
    private static readonly HashSet<string> TrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "gclid", "fbclid", "msclkid", "ref", "mc_cid", "mc_eid", "igshid"
    };

    public static Uri Normalize(Uri url)
    {
        var pairs = new List<string>();

        if (!string.IsNullOrEmpty(url.Query))
        {
            var query = url.Query.TrimStart('?');
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = pair.IndexOf('=');
                var rawKey = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
                var key = Uri.UnescapeDataString(rawKey);

                if (!TrackingParams.Contains(key))
                {
                    pairs.Add(pair);
                }
            }
        }

        var builder = new UriBuilder(url)
        {
            Fragment = string.Empty,
            Query = pairs.Count > 0 ? string.Join('&', pairs) : string.Empty
        };

        var path = builder.Path;
        if (path.Length > 1 && path.EndsWith('/'))
        {
            builder.Path = path.TrimEnd('/');
        }

        return builder.Uri;
    }
}
