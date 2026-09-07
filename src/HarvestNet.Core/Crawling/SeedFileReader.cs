namespace HarvestNet.Core.Crawling;

/// <summary>
/// Reads a plain text file of seed URLs, one per line. Blank lines and lines starting
/// with '#' are ignored, so a file can double as a simple, commentable URL list.
/// </summary>
public static class SeedFileReader
{
    public static List<Uri> ReadUrls(string filePath)
    {
        var urls = new List<Uri>();

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (Uri.TryCreate(line, UriKind.Absolute, out var uri))
            {
                urls.Add(uri);
            }
        }

        return urls;
    }
}
