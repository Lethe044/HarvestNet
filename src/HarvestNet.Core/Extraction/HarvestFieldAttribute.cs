namespace HarvestNet.Core.Extraction;

/// <summary>
/// Marks a property of a POCO as extractable, tying it to a CSS selector (or a regular
/// expression) that HarvestNet will evaluate against the scraped page or item element.
/// </summary>
/// <example>
/// <code>
/// public class Product
/// {
///     [HarvestField(".product-title")]
///     public string? Title { get; set; }
///
///     [HarvestField(".product-price", Description = "the current sale price shown to buyers")]
///     public string? Price { get; set; }
///
///     [HarvestField("a.product-link", Attribute = "href")]
///     public string? Url { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class HarvestFieldAttribute : Attribute
{
    /// <summary>CSS selector for this field, relative to the item or page scope.</summary>
    public string Selector { get; }

    /// <summary>How to interpret the found element. Defaults to reading its text content.</summary>
    public SelectorKind Kind { get; set; } = SelectorKind.Css;

    /// <summary>
    /// HTML attribute to read instead of text content, or the regular expression to apply
    /// when <see cref="Kind"/> is <see cref="SelectorKind.RegexOnText"/>.
    /// </summary>
    public string? Attribute { get; set; }

    /// <summary>When true, an item missing this field is dropped entirely.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// Optional plain-language description of the field, used as extra context for the
    /// self-healing provider when the selector stops matching.
    /// </summary>
    public string? Description { get; set; }

    public HarvestFieldAttribute(string selector)
    {
        Selector = selector;
    }
}
