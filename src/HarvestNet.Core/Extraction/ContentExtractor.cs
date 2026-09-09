using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;

namespace HarvestNet.Core.Extraction;

/// <summary>
/// A lightweight, dependency-free "readability" style extractor: given a page (or any
/// HTML fragment), finds the element most likely to hold the main written content, such
/// as an article body or a blog post, and returns its text with navigation, ads, and
/// other boilerplate stripped out.
///
/// This is a heuristic based on text length, link density, paragraph count and comma
/// count, the same general approach used by most "reader mode" implementations. It works
/// well on typical article and blog layouts, but is not a guarantee for every site; for
/// anything that needs to be precise, a normal selector or JSON-LD field is a better fit.
/// </summary>
public static class ContentExtractor
{
    private static readonly string[] NoiseSelectors =
    {
        "script", "style", "nav", "header", "footer", "aside", "form", "iframe", "noscript", "svg", "button"
    };

    private static readonly string[] CandidateSelectors = { "article", "main", "section", "div", "td" };

    /// <summary>Parses <paramref name="html"/> and returns the main content's text, or null if nothing substantial was found.</summary>
    public static string? ExtractMainContent(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
        return ExtractMainContent(document.DocumentElement);
    }

    internal static string? ExtractMainContent(IElement? root)
    {
        if (root is null)
        {
            return null;
        }

        // Work on a detached clone so this never mutates a document that other fields
        // might still need to read from.
        var clone = (IElement)root.Clone(deep: true);

        foreach (var noisy in clone.QuerySelectorAll(string.Join(',', NoiseSelectors)).ToArray())
        {
            noisy.Remove();
        }

        IElement? best = null;
        var bestScore = 0.0;

        foreach (var candidate in clone.QuerySelectorAll(string.Join(',', CandidateSelectors)))
        {
            var score = Score(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best is null || bestScore <= 0)
        {
            return null;
        }

        var paragraphs = best.QuerySelectorAll("p")
            .Select(p => p.TextContent?.Trim())
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

        var text = paragraphs.Count > 0
            ? string.Join("\n\n", paragraphs)
            : best.TextContent?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Regex.Replace(text, @"[ \t]+", " ").Trim();
    }

    private static double Score(IElement element)
    {
        var text = element.TextContent ?? string.Empty;
        var textLength = text.Length;
        if (textLength < 100)
        {
            return 0;
        }

        var linkTextLength = element.QuerySelectorAll("a").Sum(a => a.TextContent?.Length ?? 0);
        var paragraphCount = element.QuerySelectorAll("p").Length;
        var commaCount = text.Count(c => c == ',');

        var linkDensity = textLength == 0 ? 0 : (double)linkTextLength / textLength;

        return textLength * (1 - linkDensity) + (paragraphCount * 25) + (commaCount * 2);
    }
}
