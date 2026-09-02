namespace HarvestNet.Cli;

/// <summary>
/// The shape of a HarvestNet recipe file: a JSON document describing what to crawl,
/// what to extract, and where to send the results. Lets someone use HarvestNet from the
/// command line without writing any C#.
/// </summary>
public sealed class Recipe
{
    public List<string> SeedUrls { get; set; } = new();

    /// <summary>Optional sitemap.xml URL. Its URLs are added to SeedUrls before the crawl starts.</summary>
    public string? SitemapUrl { get; set; }

    public string? ItemSelector { get; set; }
    public Dictionary<string, RecipeField> Fields { get; set; } = new();
    public string? LinkSelector { get; set; }
    public int MaxDepth { get; set; } = 1;
    public int MaxPages { get; set; } = 100;
    public int MaxConcurrency { get; set; } = 4;
    public int DelayMilliseconds { get; set; } = 500;
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>Proxy URLs to rotate through (for example "http://host:port"). Leave empty for direct requests.</summary>
    public List<string> ProxyPool { get; set; } = new();
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }

    /// <summary>Renders pages with a headless browser (via HarvestNet.Browser) instead of a plain HTTP GET.</summary>
    public bool UseBrowserRendering { get; set; }

    /// <summary>
    /// Where to save crawl progress so an interrupted run can resume. Defaults to
    /// "&lt;output path&gt;.checkpoint.json" when not set.
    /// </summary>
    public string? CheckpointPath { get; set; }

    public RecipeOutput Output { get; set; } = new();
    public RecipeHealing? Healing { get; set; }
}

public sealed class RecipeField
{
    public required string Selector { get; set; }
    public string? Attribute { get; set; }
    public string? Regex { get; set; }

    /// <summary>When true, Selector is interpreted as an XPath expression instead of a CSS selector.</summary>
    public bool Xpath { get; set; }

    public string? Description { get; set; }
    public bool Required { get; set; }
}

public sealed class RecipeOutput
{
    /// <summary>One of "json", "csv" or "sqlite".</summary>
    public string Format { get; set; } = "json";
    public string Path { get; set; } = "output.jsonl";
}

public sealed class RecipeHealing
{
    /// <summary>One of "none", "groq", "gemini", "ollama" or "openai-compatible".</summary>
    public string Provider { get; set; } = "none";

    /// <summary>An API key, or "env:VAR_NAME" to read it from an environment variable at run time.</summary>
    public string? ApiKey { get; set; }

    public string? Model { get; set; }

    /// <summary>Required for "ollama" and "openai-compatible" providers.</summary>
    public string? BaseUrl { get; set; }
}
