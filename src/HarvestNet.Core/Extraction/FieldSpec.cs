namespace HarvestNet.Core.Extraction;

/// <summary>
/// Describes how to pull one field out of an HTML element. Used both by the attribute
/// based POCO extraction path and by the dictionary based dynamic path used by the CLI.
/// </summary>
public sealed class FieldSpec
{
    /// <summary>CSS selector used to find the element, relative to the current scope.</summary>
    public required string Selector { get; init; }

    /// <summary>How to interpret the found element's content.</summary>
    public SelectorKind Kind { get; init; } = SelectorKind.Css;

    /// <summary>
    /// HTML attribute to read instead of text content (for example "href" or "src").
    /// When <see cref="Kind"/> is <see cref="SelectorKind.RegexOnText"/>, this holds the regular expression instead.
    /// </summary>
    public string? Attribute { get; init; }

    /// <summary>
    /// Plain-language description of what this field represents. Sent to the self-healing
    /// provider so it has more context than the selector alone when a page changes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>When true, an item missing this field is dropped entirely.</summary>
    public bool Required { get; init; }

    /// <summary>
    /// Alternative selectors (same <see cref="Kind"/> as the primary one) tried in order
    /// when <see cref="Selector"/> finds nothing, before falling back to a self-healing
    /// provider if one is configured. Useful when a site serves more than one page
    /// template for what is conceptually the same field.
    /// </summary>
    public IReadOnlyList<string>? FallbackSelectors { get; init; }

    /// <summary>A cleanup pipeline applied to the raw extracted text, in order, once a selector has found a value.</summary>
    public IReadOnlyList<FieldTransform>? Transforms { get; init; }
}
