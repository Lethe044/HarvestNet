using System.Text.RegularExpressions;

namespace HarvestNet.Core.Extraction;

/// <summary>
/// Applies a pipeline of <see cref="FieldTransform"/> steps to a field's raw extracted
/// value.
/// </summary>
public static class FieldTransformer
{
    public static string? Apply(string? value, IReadOnlyList<FieldTransform>? transforms)
    {
        if (value is null || transforms is null || transforms.Count == 0)
        {
            return value;
        }

        var result = value;
        foreach (var transform in transforms)
        {
            result = ApplyOne(result, transform);
        }

        return result;
    }

    private static string ApplyOne(string value, FieldTransform transform) => transform switch
    {
        FieldTransform.Trim => value.Trim(),
        FieldTransform.Lowercase => value.ToLowerInvariant(),
        FieldTransform.Uppercase => value.ToUpperInvariant(),
        FieldTransform.CollapseWhitespace => Regex.Replace(value, @"\s+", " ").Trim(),
        FieldTransform.StripNonDigits => Regex.Replace(value, @"[^\d.\-]", string.Empty),
        FieldTransform.StripCurrencySymbols => Regex.Replace(value, @"[^\d.,\-]", string.Empty).Trim(),
        _ => value
    };
}
