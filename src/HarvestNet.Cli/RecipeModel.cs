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

    /// <summary>Optional path to a text file with one seed URL per line (blank lines and lines starting with '#' are ignored).</summary>
    public string? SeedUrlsFile { get; set; }

    /// <summary>Optional single POST/form seed request, used instead of or alongside SeedUrls (for example a search form submission).</summary>
    public RecipeSeedRequest? SeedRequest { get; set; }

    /// <summary>Optional login POST sent before crawling starts, so pages behind a login can be reached.</summary>
    public RecipeLogin? Login { get; set; }

    public string? ItemSelector { get; set; }
    public Dictionary<string, RecipeField> Fields { get; set; } = new();
    public string? LinkSelector { get; set; }

    /// <summary>When true and LinkSelector is not set, a built-in "next page" link detector is used instead.</summary>
    public bool AutoPagination { get; set; }

    public int MaxDepth { get; set; } = 1;
    public int MaxPages { get; set; } = 100;
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>Caps requests to the same host, on top of MaxConcurrency, for crawls that span many domains.</summary>
    public int? MaxConcurrencyPerHost { get; set; }

    public int DelayMilliseconds { get; set; } = 500;
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>Proxy URLs to rotate through (for example "http://host:port"). Leave empty for direct requests.</summary>
    public List<string> ProxyPool { get; set; } = new();
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }

    /// <summary>Renders pages with a headless browser (via HarvestNet.Browser) instead of a plain HTTP GET.</summary>
    public bool UseBrowserRendering { get; set; }

    /// <summary>When UseBrowserRendering is true, saves a full-page screenshot per page to this directory. Useful for debugging.</summary>
    public string? ScreenshotDirectory { get; set; }

    /// <summary>
    /// Where to save crawl progress so an interrupted run can resume. Defaults to
    /// "&lt;output path&gt;.checkpoint.json" when not set.
    /// </summary>
    public string? CheckpointPath { get; set; }

    /// <summary>When set, fetched pages are cached under this directory and reused on later runs of this recipe instead of being fetched again.</summary>
    public string? CacheDirectory { get; set; }

    /// <summary>User-Agent strings to rotate through, one per request. Leave empty to send a single fixed User-Agent.</summary>
    public List<string> UserAgentPool { get; set; } = new();

    /// <summary>When true (the default), tracking query parameters, the fragment, and a trailing slash are ignored when deciding whether a URL has already been visited.</summary>
    public bool NormalizeUrls { get; set; } = true;

    /// <summary>Field name used to tell two items apart when skipping duplicates. Leave unset to keep every extracted item, or set it to "*" to deduplicate by full item content.</summary>
    public string? DeduplicateBy { get; set; }

    /// <summary>An optional URL to POST a short JSON summary to once the crawl finishes.</summary>
    public string? WebhookUrl { get; set; }

    public RecipeOutput Output { get; set; } = new();
    public RecipeHealing? Healing { get; set; }
}

public sealed class RecipeSeedRequest
{
    public required string Url { get; set; }

    /// <summary>"GET" or "POST". Defaults to POST, since a GET seed request is just a regular seed URL.</summary>
    public string Method { get; set; } = "POST";

    public Dictionary<string, string> FormData { get; set; } = new();
}

public sealed class RecipeLogin
{
    public required string Url { get; set; }
    public Dictionary<string, string> FormData { get; set; } = new();
}

public sealed class RecipeField
{
    public required string Selector { get; set; }
    public string? Attribute { get; set; }
    public string? Regex { get; set; }

    /// <summary>When true, Selector is interpreted as an XPath expression instead of a CSS selector.</summary>
    public bool Xpath { get; set; }

    /// <summary>When true, Selector is interpreted as a dot-separated path into the page's JSON-LD data (for example "offers.price") instead of an HTML selector.</summary>
    public bool JsonLd { get; set; }

    /// <summary>Alternative selectors (same kind as Selector) tried in order if Selector finds nothing, before self-healing is attempted.</summary>
    public List<string>? FallbackSelectors { get; set; }

    /// <summary>Cleanup steps applied in order once a value is found: trim, lowercase, uppercase, collapsewhitespace, stripnondigits, stripcurrencysymbols.</summary>
    public List<string>? Transforms { get; set; }

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
