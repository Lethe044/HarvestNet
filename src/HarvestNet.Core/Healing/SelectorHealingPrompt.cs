using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarvestNet.Core.Healing;

/// <summary>
/// Builds the prompt sent to a healing provider and parses its reply. Kept separate from
/// the individual providers so every provider asks the same question the same way.
/// </summary>
internal static class SelectorHealingPrompt
{
    public const string SystemPrompt =
        "You are a CSS selector repair assistant for a web scraping tool. " +
        "You will be given a snippet of HTML, the name of a data field, an optional description, " +
        "and the CSS selector that used to work but no longer finds the field. " +
        "Look at the HTML and figure out a new CSS selector that would find the same kind of data today. " +
        "Reply with a JSON object of the exact shape {\"selector\": \"...\", \"confidence\": 0-1} and nothing else. " +
        "If you cannot find a plausible selector, reply with {\"selector\": null, \"confidence\": 0}.";

    public static string Build(HealingRequest request)
    {
        var descriptionLine = string.IsNullOrWhiteSpace(request.FieldDescription)
            ? string.Empty
            : $"Field description: {request.FieldDescription}\n";

        return $"Page URL: {request.PageUrl}\n" +
               $"Field name: {request.FieldName}\n" +
               descriptionLine +
               $"Old selector (no longer works): {request.OldSelector}\n\n" +
               $"HTML snippet:\n{request.TruncatedHtml}";
    }

    public static string? ExtractSelector(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        var cleaned = rawResponse.Trim();
        cleaned = Regex.Replace(cleaned, "^```(json)?", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, "```$", string.Empty).Trim();

        try
        {
            using var document = JsonDocument.Parse(cleaned);
            if (document.RootElement.TryGetProperty("selector", out var selectorElement) &&
                selectorElement.ValueKind == JsonValueKind.String)
            {
                var selector = selectorElement.GetString();
                return string.IsNullOrWhiteSpace(selector) ? null : selector;
            }

            return null;
        }
        catch (JsonException)
        {
            var match = Regex.Match(cleaned, "\"selector\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
