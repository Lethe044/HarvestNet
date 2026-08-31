using System.Text.Json;

namespace HarvestNet.Core.Healing;

/// <summary>
/// A small JSON-file-backed cache of selectors that were successfully healed in the past,
/// keyed by host and field name. This is what makes healing "sticky": once a selector is
/// fixed for a domain, later runs reuse it instead of calling the LLM again.
/// </summary>
public sealed class HealingCache
{
    private readonly string _filePath;
    private readonly Dictionary<string, Dictionary<string, string>> _entries;
    private readonly object _lock = new();

    public HealingCache(string filePath = "harvestnet-healed-selectors.json")
    {
        _filePath = filePath;
        _entries = Load();
    }

    public string? TryGet(string host, string fieldName)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(host, out var fields) && fields.TryGetValue(fieldName, out var selector))
            {
                return selector;
            }

            return null;
        }
    }

    public void Set(string host, string fieldName, string selector)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(host, out var fields))
            {
                fields = new Dictionary<string, string>();
                _entries[host] = fields;
            }

            fields[fieldName] = selector;
            Save();
        }
    }

    private Dictionary<string, Dictionary<string, string>> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, Dictionary<string, string>>();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                   ?? new Dictionary<string, Dictionary<string, string>>();
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, string>>();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Cache persistence is best effort. A failed write should never break the crawl.
        }
    }
}
