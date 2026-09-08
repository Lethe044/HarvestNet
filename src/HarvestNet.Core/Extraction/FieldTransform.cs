namespace HarvestNet.Core.Extraction;

/// <summary>
/// A simple, composable cleanup step applied to a field's raw extracted text before it is
/// stored or bound. Transforms run in the order they are listed, after the selector
/// (primary, fallback, or healed) has already found a value.
/// </summary>
public enum FieldTransform
{
    /// <summary>Removes leading and trailing whitespace.</summary>
    Trim,

    /// <summary>Converts the text to lower case (invariant culture).</summary>
    Lowercase,

    /// <summary>Converts the text to upper case (invariant culture).</summary>
    Uppercase,

    /// <summary>Collapses any run of whitespace (including newlines) into a single space and trims the ends.</summary>
    CollapseWhitespace,

    /// <summary>Removes everything except digits, a decimal point and a leading minus sign. Useful for turning "1,299 items" into "1299".</summary>
    StripNonDigits,

    /// <summary>Removes everything except digits, separators and a leading minus sign, for prices like "$1,299.00" or "1.299,00 €".</summary>
    StripCurrencySymbols
}
