using System.Text.Json;

namespace HarvestNet.Core.Crawling;

/// <summary>
/// The on-disk state of an in-progress crawl: which URLs have been visited, which are
/// still queued, and the running totals. Used by <c>HarvestSpider&lt;T&gt;.WithCheckpoint</c>
/// so an interrupted crawl (a crash, a closed terminal, a killed process) can resume
/// instead of starting over.
/// </summary>
public sealed class CrawlCheckpoint
{
    public List<string> Visited { get; set; } = new();
    public List<CheckpointUrl> Frontier { get; set; } = new();
    public int Depth { get; set; }
    public int PagesFetched { get; set; }
    public int PagesFailed { get; set; }
    public int ItemsExtracted { get; set; }

    public static CrawlCheckpoint? LoadOrNull(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<CrawlCheckpoint>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string filePath, CrawlCheckpoint checkpoint)
    {
        try
        {
            var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Checkpoint persistence is best effort and should never break a crawl.
        }
    }

    public static void Delete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}

/// <summary>One queued URL inside a <see cref="CrawlCheckpoint"/>.</summary>
public sealed class CheckpointUrl
{
    public string Url { get; set; } = string.Empty;
    public int Depth { get; set; }
}
