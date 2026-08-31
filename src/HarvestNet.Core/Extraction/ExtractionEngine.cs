using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using HarvestNet.Core.Healing;

namespace HarvestNet.Core.Extraction;

/// <summary>
/// Parses HTML and pulls out structured data, either into a strongly typed POCO decorated
/// with <see cref="HarvestFieldAttribute"/>, or into plain dictionaries described by a
/// runtime <see cref="FieldSpec"/> map (used by the CLI's JSON recipes).
///
/// When a selector fails to find anything and a <see cref="ISelectorHealer"/> was supplied,
/// the engine asks it to propose a replacement selector, retries the extraction with it,
/// and remembers the fix so future runs do not need to ask again.
/// </summary>
public sealed class ExtractionEngine
{
    private const int MaxHtmlSnippetLength = 6000;

    private readonly ISelectorHealer? _healer;

    public ExtractionEngine(ISelectorHealer? healer = null)
    {
        _healer = healer;
    }

    /// <summary>Extracts a single strongly typed item from a whole page.</summary>
    public async Task<T?> ExtractAsync<T>(string html, Uri sourceUrl, CancellationToken cancellationToken = default)
        where T : new()
    {
        var document = await ParseAsync(html, sourceUrl, cancellationToken).ConfigureAwait(false);
        var fieldMap = BuildFieldMap<T>();
        var values = await ExtractFieldsAsync(document, document, sourceUrl, typeof(T).Name, fieldMap, cancellationToken).ConfigureAwait(false);
        return BindToInstance<T>(values, fieldMap);
    }

    /// <summary>Extracts every repeated item matching <paramref name="itemSelector"/> into a strongly typed list.</summary>
    public async Task<List<T>> ExtractListAsync<T>(string html, Uri sourceUrl, string itemSelector, CancellationToken cancellationToken = default)
        where T : new()
    {
        var document = await ParseAsync(html, sourceUrl, cancellationToken).ConfigureAwait(false);
        var fieldMap = BuildFieldMap<T>();
        var elements = await ResolveItemElementsAsync(document, sourceUrl, itemSelector, typeof(T).Name, cancellationToken).ConfigureAwait(false);

        var results = new List<T>();
        foreach (var element in elements)
        {
            var values = await ExtractFieldsAsync(document, element, sourceUrl, typeof(T).Name, fieldMap, cancellationToken).ConfigureAwait(false);
            var instance = BindToInstance<T>(values, fieldMap);
            if (instance is not null)
            {
                results.Add(instance);
            }
        }

        return results;
    }

    /// <summary>Extracts a single item from a whole page using a runtime field map, returning raw string values.</summary>
    public async Task<Dictionary<string, string?>> ExtractAsync(
        string html,
        Uri sourceUrl,
        IReadOnlyDictionary<string, FieldSpec> fields,
        string itemTypeName = "Item",
        CancellationToken cancellationToken = default)
    {
        var document = await ParseAsync(html, sourceUrl, cancellationToken).ConfigureAwait(false);
        return await ExtractFieldsAsync(document, document, sourceUrl, itemTypeName, fields, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Extracts every repeated item matching <paramref name="itemSelector"/> using a runtime field map.</summary>
    public async Task<List<Dictionary<string, string?>>> ExtractListAsync(
        string html,
        Uri sourceUrl,
        string itemSelector,
        IReadOnlyDictionary<string, FieldSpec> fields,
        string itemTypeName = "Item",
        CancellationToken cancellationToken = default)
    {
        var document = await ParseAsync(html, sourceUrl, cancellationToken).ConfigureAwait(false);
        var elements = await ResolveItemElementsAsync(document, sourceUrl, itemSelector, itemTypeName, cancellationToken).ConfigureAwait(false);

        var results = new List<Dictionary<string, string?>>();
        foreach (var element in elements)
        {
            var values = await ExtractFieldsAsync(document, element, sourceUrl, itemTypeName, fields, cancellationToken).ConfigureAwait(false);
            results.Add(values);
        }

        return results;
    }

    private static Task<IDocument> ParseAsync(string html, Uri sourceUrl, CancellationToken cancellationToken)
    {
        var context = BrowsingContext.New(Configuration.Default);
        return context.OpenAsync(req => req.Content(html).Address(sourceUrl.AbsoluteUri), cancellationToken);
    }

    private async Task<IReadOnlyList<IElement>> ResolveItemElementsAsync(
        IDocument document,
        Uri sourceUrl,
        string itemSelector,
        string itemTypeName,
        CancellationToken cancellationToken)
    {
        var elements = document.QuerySelectorAll(itemSelector);
        if (elements.Length > 0 || _healer is null)
        {
            return elements.ToArray();
        }

        var healedSelector = await _healer.TryHealContainerSelectorAsync(
            sourceUrl,
            Truncate(document.DocumentElement?.OuterHtml ?? string.Empty),
            itemSelector,
            itemTypeName,
            cancellationToken).ConfigureAwait(false);

        if (healedSelector is null)
        {
            return Array.Empty<IElement>();
        }

        return document.QuerySelectorAll(healedSelector).ToArray();
    }

    private async Task<Dictionary<string, string?>> ExtractFieldsAsync(
        IDocument document,
        INode scopeNode,
        Uri sourceUrl,
        string itemTypeName,
        IReadOnlyDictionary<string, FieldSpec> fields,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>();

        foreach (var (fieldName, spec) in fields)
        {
            var value = ExtractFieldValue(scopeNode, spec);

            if (value is null && _healer is not null)
            {
                var scopeHtml = scopeNode is IElement scopeElement ? scopeElement.OuterHtml : document.DocumentElement?.OuterHtml ?? string.Empty;

                var healedSelector = await _healer.TryHealFieldSelectorAsync(
                    sourceUrl,
                    Truncate(scopeHtml),
                    spec.Selector,
                    fieldName,
                    spec.Description,
                    cancellationToken).ConfigureAwait(false);

                if (healedSelector is not null)
                {
                    var healedSpec = new FieldSpec
                    {
                        Selector = healedSelector,
                        Kind = spec.Kind,
                        Attribute = spec.Attribute,
                        Description = spec.Description,
                        Required = spec.Required
                    };

                    value = ExtractFieldValue(scopeNode, healedSpec);
                    if (value is not null)
                    {
                        _healer.RememberHealedSelector(sourceUrl.Host, itemTypeName, fieldName, healedSelector);
                    }
                }
            }

            values[fieldName] = value;
        }

        return values;
    }

    private static string? ExtractFieldValue(INode scopeNode, FieldSpec spec)
    {
        if (scopeNode is not IParentNode parentNode)
        {
            return null;
        }

        var element = parentNode.QuerySelector(spec.Selector);
        if (element is null)
        {
            return null;
        }

        if (spec.Kind == SelectorKind.RegexOnText && !string.IsNullOrEmpty(spec.Attribute))
        {
            var match = Regex.Match(element.TextContent ?? string.Empty, spec.Attribute);
            return match.Success ? match.Value : null;
        }

        if (!string.IsNullOrEmpty(spec.Attribute))
        {
            return element.GetAttribute(spec.Attribute);
        }

        var text = element.TextContent?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static Dictionary<string, FieldSpec> BuildFieldMap<T>()
    {
        var map = new Dictionary<string, FieldSpec>();

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = property.GetCustomAttribute<HarvestFieldAttribute>();
            if (attribute is null)
            {
                continue;
            }

            map[property.Name] = new FieldSpec
            {
                Selector = attribute.Selector,
                Kind = attribute.Kind,
                Attribute = attribute.Attribute,
                Description = attribute.Description,
                Required = attribute.Required
            };
        }

        return map;
    }

    private static T? BindToInstance<T>(Dictionary<string, string?> values, Dictionary<string, FieldSpec> fieldMap)
        where T : new()
    {
        var instance = new T();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name);

        var anyFieldFound = false;

        foreach (var (fieldName, spec) in fieldMap)
        {
            values.TryGetValue(fieldName, out var value);

            if (value is null)
            {
                if (spec.Required)
                {
                    return default;
                }

                continue;
            }

            anyFieldFound = true;

            if (properties.TryGetValue(fieldName, out var property))
            {
                AssignValue(instance, property, value);
            }
        }

        return anyFieldFound ? instance : default;
    }

    private static void AssignValue(object instance, PropertyInfo property, string value)
    {
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        object? converted;

        if (targetType == typeof(string))
        {
            converted = value;
        }
        else if (targetType.IsEnum)
        {
            converted = Enum.TryParse(targetType, value, ignoreCase: true, out var enumValue) ? enumValue : null;
        }
        else if (targetType == typeof(decimal) || targetType == typeof(double) || targetType == typeof(float)
                 || targetType == typeof(int) || targetType == typeof(long))
        {
            var cleaned = Regex.Replace(value, @"[^\d.\-]", string.Empty);
            converted = cleaned.Length == 0 ? null : Convert.ChangeType(cleaned, targetType, CultureInfo.InvariantCulture);
        }
        else if (targetType == typeof(DateTime))
        {
            converted = DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                ? parsedDate
                : null;
        }
        else if (targetType == typeof(Uri))
        {
            converted = Uri.TryCreate(value, UriKind.Absolute, out var parsedUri) ? parsedUri : null;
        }
        else
        {
            converted = value;
        }

        if (converted is not null)
        {
            property.SetValue(instance, converted);
        }
    }

    private static string Truncate(string html) => html.Length <= MaxHtmlSnippetLength ? html : html[..MaxHtmlSnippetLength];
}
