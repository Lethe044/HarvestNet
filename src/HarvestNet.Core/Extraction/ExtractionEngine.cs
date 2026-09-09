using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using HarvestNet.Core.Healing;

namespace HarvestNet.Core.Extraction;

/// <summary>
/// Parses HTML and pulls out structured data, either into a strongly typed POCO decorated
/// with <see cref="HarvestFieldAttribute"/>, or into plain dictionaries described by a
/// runtime <see cref="FieldSpec"/> map (used by the CLI's JSON recipes).
///
/// Fields can read from CSS selectors, XPath expressions, regular expressions, or the
/// page's JSON-LD structured data, and can list fallback selectors that are tried, in
/// order, before falling back to a self-healing provider. JSON-LD blocks are read once
/// per page and shared by every item extracted from it, since that data is typically page
/// level metadata rather than something that varies per repeated item.
///
/// When every selector for a CSS or XPath field fails and a <see cref="ISelectorHealer"/>
/// was supplied, the engine asks it to propose a replacement selector, retries the
/// extraction with it, and remembers the fix so future runs do not need to ask again.
/// JSON-LD fields are not healed, since there is no HTML selector to repair.
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
        var jsonLdBlocks = StructuredDataReader.ReadJsonLd(document);
        var fieldMap = BuildFieldMap<T>();
        var values = await ExtractFieldsAsync(document, document, sourceUrl, typeof(T).Name, fieldMap, jsonLdBlocks, cancellationToken).ConfigureAwait(false);
        return BindToInstance<T>(values, fieldMap);
    }

    /// <summary>Extracts every repeated item matching <paramref name="itemSelector"/> into a strongly typed list.</summary>
    public async Task<List<T>> ExtractListAsync<T>(string html, Uri sourceUrl, string itemSelector, CancellationToken cancellationToken = default)
        where T : new()
    {
        var document = await ParseAsync(html, sourceUrl, cancellationToken).ConfigureAwait(false);
        var jsonLdBlocks = StructuredDataReader.ReadJsonLd(document);
        var fieldMap = BuildFieldMap<T>();
        var elements = await ResolveItemElementsAsync(document, sourceUrl, itemSelector, typeof(T).Name, cancellationToken).ConfigureAwait(false);

        var results = new List<T>();
        foreach (var element in elements)
        {
            var values = await ExtractFieldsAsync(document, element, sourceUrl, typeof(T).Name, fieldMap, jsonLdBlocks, cancellationToken).ConfigureAwait(false);
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
        var jsonLdBlocks = StructuredDataReader.ReadJsonLd(document);
        return await ExtractFieldsAsync(document, document, sourceUrl, itemTypeName, fields, jsonLdBlocks, cancellationToken).ConfigureAwait(false);
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
        var jsonLdBlocks = StructuredDataReader.ReadJsonLd(document);
        var elements = await ResolveItemElementsAsync(document, sourceUrl, itemSelector, itemTypeName, cancellationToken).ConfigureAwait(false);

        var results = new List<Dictionary<string, string?>>();
        foreach (var element in elements)
        {
            var values = await ExtractFieldsAsync(document, element, sourceUrl, itemTypeName, fields, jsonLdBlocks, cancellationToken).ConfigureAwait(false);
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
        IReadOnlyList<JsonElement> jsonLdBlocks,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>();

        foreach (var (fieldName, spec) in fields)
        {
            var value = ExtractFieldValue(scopeNode, spec, jsonLdBlocks);

            if (value is null && _healer is not null && spec.Kind != SelectorKind.JsonLd && spec.Kind != SelectorKind.MainContent)
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
                    value = ExtractForSelector(scopeNode, spec, healedSelector, jsonLdBlocks);
                    if (value is not null)
                    {
                        _healer.RememberHealedSelector(sourceUrl.Host, itemTypeName, fieldName, healedSelector);
                    }
                }
            }

            values[fieldName] = FieldTransformer.Apply(value, spec.Transforms);
        }

        return values;
    }

    /// <summary>Tries the field's primary selector, then each fallback selector in order, until one finds a value.</summary>
    private static string? ExtractFieldValue(INode scopeNode, FieldSpec spec, IReadOnlyList<JsonElement> jsonLdBlocks)
    {
        var value = ExtractForSelector(scopeNode, spec, spec.Selector, jsonLdBlocks);
        if (value is not null || spec.FallbackSelectors is null)
        {
            return value;
        }

        foreach (var fallbackSelector in spec.FallbackSelectors)
        {
            value = ExtractForSelector(scopeNode, spec, fallbackSelector, jsonLdBlocks);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? ExtractForSelector(INode scopeNode, FieldSpec spec, string selector, IReadOnlyList<JsonElement> jsonLdBlocks)
    {
        if (spec.Kind == SelectorKind.JsonLd)
        {
            return ExtractViaJsonLdPath(jsonLdBlocks, selector);
        }

        if (spec.Kind == SelectorKind.MainContent)
        {
            var contextElement = scopeNode switch
            {
                IElement scopeElement => scopeElement,
                IDocument document => document.DocumentElement,
                _ => null
            };

            return ContentExtractor.ExtractMainContent(contextElement);
        }

        if (spec.Kind == SelectorKind.XPath)
        {
            return ExtractViaXPath(scopeNode, selector, spec.Attribute);
        }

        if (scopeNode is not IParentNode parentNode)
        {
            return null;
        }

        var element = parentNode.QuerySelector(selector);
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

    private static string? ExtractViaJsonLdPath(IReadOnlyList<JsonElement> jsonLdBlocks, string path)
    {
        foreach (var block in jsonLdBlocks)
        {
            var value = StructuredDataReader.ResolveJsonPath(block, path);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? ExtractViaXPath(INode scopeNode, string xpath, string? attribute)
    {
        IElement? contextElement = scopeNode switch
        {
            IElement scopeElement => scopeElement,
            IDocument document => document.DocumentElement,
            _ => null
        };

        if (contextElement is null)
        {
            return null;
        }

        INode? node;
        try
        {
            node = contextElement.SelectSingleNode(xpath);
        }
        catch
        {
            return null;
        }

        if (node is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(attribute) && node is IElement element)
        {
            return element.GetAttribute(attribute);
        }

        var text = node.TextContent?.Trim();
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
                Required = attribute.Required,
                FallbackSelectors = attribute.FallbackSelectors,
                Transforms = attribute.Transforms
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
