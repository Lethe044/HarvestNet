namespace HarvestNet.Core.Healing;

/// <summary>
/// Everything a healing provider needs to propose a replacement selector.
/// </summary>
public sealed class HealingRequest
{
    /// <summary>A truncated HTML snippet (page or item scope) for the model to inspect.</summary>
    public required string TruncatedHtml { get; init; }

    /// <summary>The logical name of the field that could not be found.</summary>
    public required string FieldName { get; init; }

    /// <summary>Optional plain-language description of the field, from <see cref="Extraction.FieldSpec.Description"/>.</summary>
    public string? FieldDescription { get; init; }

    /// <summary>The selector that used to work but no longer finds anything.</summary>
    public required string OldSelector { get; init; }

    /// <summary>The URL of the page being scraped, for context and logging.</summary>
    public required Uri PageUrl { get; init; }
}
