using AngleSharp;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// Ready-made link extractors for the common "follow the next page link" pagination
/// pattern, so most crawls do not need to hand write a next-page selector.
/// </summary>
public static class PaginationHelper
{
    private static readonly string[] DefaultNextPageSelectors =
    {
        "a[rel='next']",
        "link[rel='next']",
        ".pagination .next a",
        ".pagination-next a",
        "a.next",
        "li.next > a"
    };

    /// <summary>
    /// Returns a link extractor for <c>HarvestSpider&lt;T&gt;.WithLinkExtractor</c> that
    /// looks for a "next page" link using a list of common CSS selectors. Any selectors
    /// passed in are tried first, before the built-in defaults.
    /// </summary>
    public static Func<Uri, string, IEnumerable<Uri>> FollowNextLink(params string[] additionalSelectors)
    {
        var selectors = additionalSelectors.Concat(DefaultNextPageSelectors).ToArray();

        return (baseUrl, html) =>
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = context.OpenAsync(req => req.Content(html).Address(baseUrl.AbsoluteUri)).GetAwaiter().GetResult();

            foreach (var selector in selectors)
            {
                var element = document.QuerySelector(selector);
                var href = element?.GetAttribute("href");
                if (string.IsNullOrEmpty(href))
                {
                    continue;
                }

                if (Uri.TryCreate(baseUrl, href, out var absolute))
                {
                    return new[] { absolute };
                }
            }

            return Array.Empty<Uri>();
        };
    }
}
