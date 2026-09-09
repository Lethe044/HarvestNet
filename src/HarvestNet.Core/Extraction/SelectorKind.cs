namespace HarvestNet.Core.Extraction;

/// <summary>
/// How a field's raw text should be interpreted once the target element is found.
/// </summary>
public enum SelectorKind
{
    /// <summary>Use the element's text content, or the attribute named in <see cref="FieldSpec.Attribute"/>.</summary>
    Css,

    /// <summary>Run a regular expression (stored in <see cref="FieldSpec.Attribute"/>) against the element's text content.</summary>
    RegexOnText,

    /// <summary>Use an XPath expression instead of a CSS selector.</summary>
    XPath,

    /// <summary>
    /// Read a value out of the page's JSON-LD structured data instead of the HTML tree.
    /// <see cref="FieldSpec.Selector"/> holds a dot-separated path (for example
    /// "offers.price") evaluated against every JSON-LD block found on the page, in order,
    /// until one contains that path.
    /// </summary>
    JsonLd,

    /// <summary>
    /// Uses a heuristic "readability" style extractor to find and return the page's main
    /// written content (an article body, a blog post), stripped of navigation and other
    /// boilerplate. <see cref="FieldSpec.Selector"/> is not used for this kind.
    /// </summary>
    MainContent
}
