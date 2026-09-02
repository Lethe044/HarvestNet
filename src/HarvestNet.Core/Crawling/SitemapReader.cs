using System.Xml.Linq;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// Reads page URLs out of a sitemap.xml file, including sitemap index files that point to
/// other sitemaps. Useful for seeding a crawl without hand listing every page.
/// </summary>
public static class SitemapReader
{
    private const int MaxRecursionDepth = 3;

    public static async Task<List<Uri>> ReadUrlsAsync(
        Uri sitemapUrl,
        HttpClient httpClient,
        int maxUrls = 1000,
        CancellationToken cancellationToken = default)
    {
        var urls = new List<Uri>();
        await ReadRecursiveAsync(sitemapUrl, httpClient, urls, maxUrls, depth: 0, cancellationToken).ConfigureAwait(false);
        return urls;
    }

    /// <summary>Parses an already-downloaded sitemap document, without any network access. Exposed for testing and offline use.</summary>
    public static List<Uri> ParseUrls(string xml, int maxUrls = 1000)
    {
        var urls = new List<Uri>();
        AppendUrlsFromDocument(xml, urls, maxUrls);
        return urls;
    }

    private static async Task ReadRecursiveAsync(
        Uri sitemapUrl,
        HttpClient httpClient,
        List<Uri> urls,
        int maxUrls,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaxRecursionDepth || urls.Count >= maxUrls)
        {
            return;
        }

        string xml;
        try
        {
            xml = await httpClient.GetStringAsync(sitemapUrl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        var childSitemaps = ParseChildSitemaps(xml);

        foreach (var child in childSitemaps)
        {
            if (urls.Count >= maxUrls)
            {
                break;
            }

            if (Uri.TryCreate(child, UriKind.Absolute, out var childUri))
            {
                await ReadRecursiveAsync(childUri, httpClient, urls, maxUrls, depth + 1, cancellationToken).ConfigureAwait(false);
            }
        }

        AppendUrlsFromDocument(xml, urls, maxUrls);
    }

    private static List<string> ParseChildSitemaps(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            XNamespace ns = document.Root?.Name.Namespace ?? string.Empty;

            return document.Descendants(ns + "sitemap")
                .Elements(ns + "loc")
                .Select(e => e.Value.Trim())
                .Where(v => v.Length > 0)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void AppendUrlsFromDocument(string xml, List<Uri> urls, int maxUrls)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch
        {
            return;
        }

        XNamespace ns = document.Root?.Name.Namespace ?? string.Empty;

        var pageUrls = document.Descendants(ns + "url")
            .Elements(ns + "loc")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0);

        foreach (var pageUrl in pageUrls)
        {
            if (urls.Count >= maxUrls)
            {
                break;
            }

            if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri))
            {
                urls.Add(pageUri);
            }
        }
    }
}
