namespace HarvestNet.Core.Extraction;

/// <summary>
/// How a field's raw text should be interpreted once the target element is found.
/// </summary>
public enum SelectorKind
{
    /// <summary>Use the element's text content, or the attribute named in <see cref="FieldSpec.Attribute"/>.</summary>
    Css,

    /// <summary>Run a regular expression (stored in <see cref="FieldSpec.Attribute"/>) against the element's text content.</summary>
    RegexOnText
}
