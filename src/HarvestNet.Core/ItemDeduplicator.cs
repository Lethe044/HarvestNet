using System.Collections.Concurrent;
using System.Text.Json;

namespace HarvestNet.Core;

/// <summary>
/// Tracks which items have already been produced during a crawl, so the same item found
/// on two different pages (overlapping pagination, a product linked from several category
/// pages, and so on) is only written once. Safe to use from multiple concurrent requests.
/// </summary>
internal sealed class ItemDeduplicator<T>
{
    private readonly Func<T, string>? _keySelector;
    private readonly ConcurrentDictionary<string, byte> _seenKeys = new();

    public ItemDeduplicator(Func<T, string>? keySelector)
    {
        _keySelector = keySelector;
    }

    /// <summary>Returns true if an item with the same key (or, with no key selector, the same serialized content) was already seen.</summary>
    public bool IsDuplicate(T item)
    {
        var key = _keySelector is not null ? _keySelector(item) : JsonSerializer.Serialize(item);
        return !_seenKeys.TryAdd(key, 0);
    }
}
