using System.Collections.Concurrent;
using System.Reflection;

namespace HarvestNet.Core;

/// <summary>
/// Counts, per field name, how many extracted items actually had a non-empty value for
/// that field. This is what powers <see cref="HarvestRunSummary.FieldCoverage"/>, a quick
/// way to notice a selector that has quietly started failing on some fraction of pages
/// without breaking the whole crawl.
/// </summary>
internal static class FieldCoverageTracker
{
    public static void Record<T>(T item, ConcurrentDictionary<string, int> counts)
    {
        if (item is Dictionary<string, string?> dictionary)
        {
            foreach (var (key, value) in dictionary)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    counts.AddOrUpdate(key, 1, (_, existing) => existing + 1);
                }
            }

            return;
        }

        if (item is null)
        {
            return;
        }

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = property.GetValue(item);
            var isPresent = value switch
            {
                null => false,
                string text => !string.IsNullOrEmpty(text),
                _ => true
            };

            if (isPresent)
            {
                counts.AddOrUpdate(property.Name, 1, (_, existing) => existing + 1);
            }
        }
    }
}
