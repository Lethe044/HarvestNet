using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;

namespace HarvestNet.Core.Extraction;

/// <summary>
/// Reads the structured metadata many modern sites embed in their pages: JSON-LD
/// (schema.org) blocks, Open Graph meta tags, and plain meta tags. This data tends to be
/// far more stable than the visible HTML, since it is meant to be machine readable, which
/// makes it a good first place to look before reaching for CSS or XPath selectors.
/// </summary>
public static class StructuredDataReader
{
    /// <summary>Parses every &lt;script type="application/ld+json"&gt; block on the page.</summary>
    public static List<JsonElement> ReadJsonLd(string html)
    {
        var document = ParseHtml(html);
        return ReadJsonLd(document);
    }

    internal static List<JsonElement> ReadJsonLd(IDocument document)
    {
        var results = new List<JsonElement>();

        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var text = script.TextContent?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            try
            {
                using var parsed = JsonDocument.Parse(text);
                if (parsed.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in parsed.RootElement.EnumerateArray())
                    {
                        results.Add(element.Clone());
                    }
                }
                else
                {
                    results.Add(parsed.RootElement.Clone());
                }
            }
            catch (JsonException)
            {
                // Malformed JSON-LD is common in the wild; skip that block rather than
                // failing the whole extraction.
            }
        }

        return results;
    }

    /// <summary>Reads every &lt;meta property="og:..."&gt; tag into a dictionary keyed by the full property name.</summary>
    public static Dictionary<string, string> ReadOpenGraphTags(string html) =>
        ReadMetaByAttribute(ParseHtml(html), "property", "og:");

    /// <summary>Reads every &lt;meta name="..."&gt; tag into a dictionary keyed by the name attribute.</summary>
    public static Dictionary<string, string> ReadMetaTags(string html) =>
        ReadMetaByAttribute(ParseHtml(html), "name", prefix: null);

    /// <summary>
    /// Resolves a dot-separated path (for example "offers.price") against a JSON-LD
    /// element, returning its value as a string, or null if the path does not exist.
    /// </summary>
    internal static string? ResolveJsonPath(JsonElement element, string path)
    {
        var current = element;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static IDocument ParseHtml(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        return context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
    }

    private static Dictionary<string, string> ReadMetaByAttribute(IDocument document, string attributeName, string? prefix)
    {
        var result = new Dictionary<string, string>();

        foreach (var meta in document.QuerySelectorAll("meta"))
        {
            var key = meta.GetAttribute(attributeName);
            var value = meta.GetAttribute("content");

            if (string.IsNullOrEmpty(key) || value is null)
            {
                continue;
            }

            if (prefix is not null && !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }
}
