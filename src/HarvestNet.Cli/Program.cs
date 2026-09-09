using System.Net.Http.Json;
using System.Text.Json;
using AngleSharp;
using HarvestNet.Browser;
using HarvestNet.Core;
using HarvestNet.Core.Crawling;
using HarvestNet.Core.Export;
using HarvestNet.Core.Extraction;
using HarvestNet.Core.Healing;
using HarvestNet.Core.Http;

namespace HarvestNet.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            return PrintUsage();
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "run" => await RunAsync(args).ConfigureAwait(false),
                "init" => Init(args),
                "validate" => Validate(args),
                "stats" => await StatsAsync(args).ConfigureAwait(false),
                "test-selector" => await TestSelectorAsync(args).ConfigureAwait(false),
                "diff" => await DiffAsync(args).ConfigureAwait(false),
                "watch" => await WatchAsync(args).ConfigureAwait(false),
                "version" => PrintVersion(),
                "help" or "-h" or "--help" => PrintUsage(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
        HarvestNet CLI

        Usage:
          harvestnet init <recipe-name>                 Create a starter recipe file
          harvestnet validate <recipe.json>              Check a recipe for common mistakes
          harvestnet run <recipe.json>                   Run a scraping recipe
          harvestnet watch <recipe.json> [options]       Re-run a recipe on a schedule and report changes
          harvestnet diff <old.jsonl> <new.jsonl> --key <field>   Compare two JSON Lines snapshots
          harvestnet stats <output.jsonl>                Summarize an existing JSON Lines output file
          harvestnet test-selector <url> <selector>      Try a selector against a live page
          harvestnet version                             Print the version

        Options for test-selector:
          --attribute <name>   Read this HTML attribute instead of text content
          --xpath               Treat <selector> as an XPath expression instead of CSS

        Options for watch:
          --interval <seconds>  How long to wait between runs (default 3600)
          --key <field>          Field used to match items between runs when reporting changes

        A recipe is a JSON file describing what to crawl and how to extract data.
        Run "harvestnet init my-recipe" to generate a starter file you can edit.
        If a run is interrupted, running the same recipe again resumes automatically
        from its checkpoint file instead of starting over.
        """);
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("HarvestNet CLI 1.5.0");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
    }

    private static int Init(string[] args)
    {
        var name = args.Length > 1 ? args[1] : "recipe";
        var fileName = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json";

        var starter = new Recipe
        {
            SeedUrls = new List<string> { "https://quotes.toscrape.com/" },
            ItemSelector = ".quote",
            Fields = new Dictionary<string, RecipeField>
            {
                ["text"] = new RecipeField { Selector = ".text" },
                ["author"] = new RecipeField { Selector = ".author" }
            },
            LinkSelector = "li.next > a",
            MaxDepth = 3,
            MaxPages = 20,
            Output = new RecipeOutput { Format = "json", Path = "output.jsonl" }
        };

        var json = JsonSerializer.Serialize(starter, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fileName, json);
        Console.WriteLine($"Created {fileName}. Edit it, then run: harvestnet run {fileName}");
        return 0;
    }

    private static int Validate(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: harvestnet validate <recipe.json>");
            return 1;
        }

        var path = args[1];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Recipe file not found: {path}");
            return 1;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read file: {ex.Message}");
            return 1;
        }

        Recipe? recipe;
        try
        {
            recipe = JsonSerializer.Deserialize<Recipe>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid JSON: {ex.Message}");
            return 1;
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        if (recipe is null)
        {
            errors.Add("Recipe is empty or could not be parsed.");
        }
        else
        {
            if (recipe.SeedUrls.Count == 0 && string.IsNullOrEmpty(recipe.SitemapUrl)
                && string.IsNullOrEmpty(recipe.SeedUrlsFile) && recipe.SeedRequest is null)
            {
                errors.Add("No seed URLs: set seedUrls, sitemapUrl, seedUrlsFile, or seedRequest.");
            }

            foreach (var seed in recipe.SeedUrls)
            {
                if (!Uri.TryCreate(seed, UriKind.Absolute, out _))
                {
                    errors.Add($"Invalid seed URL: \"{seed}\"");
                }
            }

            if (recipe.Fields.Count == 0)
            {
                warnings.Add("No fields defined: the recipe will not extract anything.");
            }

            foreach (var (name, field) in recipe.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Selector) && !field.MainContent)
                {
                    errors.Add($"Field \"{name}\" has an empty selector.");
                }

                var kindCount = new[] { field.Xpath, field.JsonLd, field.MainContent }.Count(flag => flag);
                if (kindCount > 1)
                {
                    warnings.Add($"Field \"{name}\" sets more than one of xpath/jsonLd/mainContent; only one is used (mainContent, then jsonLd, then xpath).");
                }
            }

            if (string.IsNullOrEmpty(recipe.ItemSelector) && recipe.Fields.Count > 0)
            {
                warnings.Add("No itemSelector set: fields will be extracted once from the whole page rather than once per repeated item.");
            }

            if (!string.IsNullOrEmpty(recipe.LinkSelector) && recipe.AutoPagination)
            {
                warnings.Add("Both linkSelector and autoPagination are set; linkSelector takes priority and autoPagination is ignored.");
            }

            if (recipe.UseBrowserRendering && (recipe.ProxyPool.Count > 0 || recipe.UserAgentPool.Count > 0))
            {
                warnings.Add("Proxy and User-Agent rotation only apply to plain HTTP requests, not to browser rendering.");
            }

            if (recipe.Healing is not null && recipe.Healing.Provider != "none")
            {
                var providerNames = recipe.Healing.Provider.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (providerNames.Length == 0)
                {
                    warnings.Add("healing.provider is set but empty; self-healing will be disabled.");
                }

                foreach (var providerName in providerNames)
                {
                    var normalized = providerName.ToLowerInvariant();
                    if (normalized is not ("groq" or "gemini" or "ollama" or "openai-compatible"))
                    {
                        errors.Add($"Unknown healing provider: \"{providerName}\"");
                    }
                    else if (normalized is "groq" or "gemini" && string.IsNullOrEmpty(recipe.Healing.ApiKey))
                    {
                        warnings.Add($"Healing provider \"{providerName}\" usually needs an apiKey.");
                    }
                    else if (normalized == "openai-compatible" && string.IsNullOrEmpty(recipe.Healing.BaseUrl))
                    {
                        errors.Add("Healing provider \"openai-compatible\" needs a baseUrl.");
                    }
                }
            }

            if (recipe.Output.Format.ToLowerInvariant() is not ("json" or "csv" or "sqlite"))
            {
                errors.Add($"Unknown output format: \"{recipe.Output.Format}\" (expected json, csv, or sqlite).");
            }
        }

        foreach (var warning in warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        foreach (var error in errors)
        {
            Console.Error.WriteLine($"Error: {error}");
        }

        if (errors.Count == 0)
        {
            Console.WriteLine(warnings.Count == 0
                ? "Recipe looks good."
                : $"Recipe is runnable, with {warnings.Count} warning(s).");
        }

        return errors.Count == 0 ? 0 : 1;
    }

    private static async Task<int> StatsAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: harvestnet stats <output.jsonl>");
            return 1;
        }

        var path = args[1];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        var lineCount = 0;
        var malformedCount = 0;
        var fieldCounts = new Dictionary<string, int>();

        foreach (var line in await File.ReadAllLinesAsync(path).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    malformedCount++;
                    continue;
                }

                lineCount++;

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var isPresent = property.Value.ValueKind switch
                    {
                        JsonValueKind.Null => false,
                        JsonValueKind.String => !string.IsNullOrEmpty(property.Value.GetString()),
                        _ => true
                    };

                    if (isPresent)
                    {
                        fieldCounts[property.Name] = fieldCounts.GetValueOrDefault(property.Name) + 1;
                    }
                }
            }
            catch (JsonException)
            {
                malformedCount++;
            }
        }

        Console.WriteLine($"Items:     {lineCount}");
        if (malformedCount > 0)
        {
            Console.WriteLine($"Malformed: {malformedCount} line(s) skipped");
        }

        if (lineCount > 0 && fieldCounts.Count > 0)
        {
            Console.WriteLine("Field coverage:");
            foreach (var (field, count) in fieldCounts.OrderBy(kvp => kvp.Key))
            {
                Console.WriteLine($"  {field}: {(double)count / lineCount:P0} ({count}/{lineCount})");
            }
        }

        return 0;
    }

    private static async Task<int> TestSelectorAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: harvestnet test-selector <url> <selector> [--attribute name] [--xpath]");
            return 1;
        }

        var url = args[1];
        var selector = args[2];
        string? attribute = null;
        var kind = SelectorKind.Css;

        for (var i = 3; i < args.Length; i++)
        {
            if (args[i] == "--attribute" && i + 1 < args.Length)
            {
                attribute = args[++i];
            }
            else if (args[i] == "--xpath")
            {
                kind = SelectorKind.XPath;
            }
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HarvestNet/1.5 (+https://github.com/Lethe044/HarvestNet)");

        Console.WriteLine($"Fetching {url} ...");
        var html = await httpClient.GetStringAsync(url).ConfigureAwait(false);

        var engine = new ExtractionEngine();
        var fields = new Dictionary<string, FieldSpec>
        {
            ["value"] = new FieldSpec { Selector = selector, Kind = kind, Attribute = attribute }
        };

        var result = await engine.ExtractAsync(html, new Uri(url), fields).ConfigureAwait(false);

        if (result["value"] is null)
        {
            Console.WriteLine("No match found for that selector.");
            return 1;
        }

        Console.WriteLine($"Match: {result["value"]}");
        return 0;
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: harvestnet run <recipe.json>");
            return 1;
        }

        var path = args[1];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Recipe file not found: {path}");
            return 1;
        }

        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var recipe = JsonSerializer.Deserialize<Recipe>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (recipe is null || (recipe.SeedUrls.Count == 0 && string.IsNullOrEmpty(recipe.SitemapUrl)
            && string.IsNullOrEmpty(recipe.SeedUrlsFile) && recipe.SeedRequest is null))
        {
            Console.Error.WriteLine("Recipe must include at least one seed URL, a sitemapUrl, a seedUrlsFile, or a seedRequest.");
            return 1;
        }

        var options = new CrawlOptions
        {
            MaxConcurrency = recipe.MaxConcurrency,
            MaxConcurrencyPerHost = recipe.MaxConcurrencyPerHost,
            MaxDepth = recipe.MaxDepth,
            MaxPages = recipe.MaxPages,
            DelayBetweenRequests = TimeSpan.FromMilliseconds(recipe.DelayMilliseconds),
            RespectRobotsTxt = recipe.RespectRobotsTxt,
            AdaptiveThrottling = recipe.AdaptiveThrottling,
            ProxyPool = recipe.ProxyPool,
            ProxyUsername = recipe.ProxyUsername,
            ProxyPassword = recipe.ProxyPassword,
            CacheDirectory = recipe.CacheDirectory,
            UserAgentPool = recipe.UserAgentPool,
            NormalizeUrls = recipe.NormalizeUrls
        };

        if (!string.IsNullOrEmpty(recipe.SitemapUrl))
        {
            using var sitemapHttpClient = new HttpClient();
            sitemapHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            Console.WriteLine($"Reading sitemap {recipe.SitemapUrl} ...");
            var sitemapUrls = await SitemapReader.ReadUrlsAsync(new Uri(recipe.SitemapUrl), sitemapHttpClient, recipe.MaxPages).ConfigureAwait(false);
            Console.WriteLine($"Discovered {sitemapUrls.Count} URL(s) from the sitemap.");

            foreach (var url in sitemapUrls)
            {
                recipe.SeedUrls.Add(url.AbsoluteUri);
            }
        }

        if (!string.IsNullOrEmpty(recipe.SeedUrlsFile))
        {
            if (!File.Exists(recipe.SeedUrlsFile))
            {
                Console.Error.WriteLine($"seedUrlsFile not found: {recipe.SeedUrlsFile}");
                return 1;
            }

            var fileUrls = SeedFileReader.ReadUrls(recipe.SeedUrlsFile);
            Console.WriteLine($"Loaded {fileUrls.Count} seed URL(s) from {recipe.SeedUrlsFile}.");

            foreach (var url in fileUrls)
            {
                recipe.SeedUrls.Add(url.AbsoluteUri);
            }
        }

        if (recipe.SeedUrls.Count == 0 && recipe.SeedRequest is null)
        {
            Console.Error.WriteLine("No seed URLs to crawl (empty seedUrls, nothing found in the sitemap/file, and no seedRequest).");
            return 1;
        }

        var fields = recipe.Fields.ToDictionary(
            kvp => kvp.Key,
            kvp => new FieldSpec
            {
                Selector = kvp.Value.Selector,
                Kind = kvp.Value.JsonLd
                    ? SelectorKind.JsonLd
                    : kvp.Value.MainContent
                        ? SelectorKind.MainContent
                        : kvp.Value.Xpath
                            ? SelectorKind.XPath
                            : string.IsNullOrEmpty(kvp.Value.Regex) ? SelectorKind.Css : SelectorKind.RegexOnText,
                Attribute = string.IsNullOrEmpty(kvp.Value.Regex) ? kvp.Value.Attribute : kvp.Value.Regex,
                Description = kvp.Value.Description,
                Required = kvp.Value.Required,
                FallbackSelectors = kvp.Value.FallbackSelectors,
                Transforms = ParseTransforms(kvp.Value.Transforms)
            });

        var spider = new HarvestSpider<Dictionary<string, string?>>(options)
            .WithFields(fields, "Item");

        foreach (var seed in recipe.SeedUrls)
        {
            spider.AddSeedUrl(seed);
        }

        if (recipe.SeedRequest is not null)
        {
            var method = recipe.SeedRequest.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Get : HttpMethod.Post;
            spider.AddSeedRequest(new CrawlRequest
            {
                Url = new Uri(recipe.SeedRequest.Url),
                Method = method,
                FormData = ResolveFormData(recipe.SeedRequest.FormData)
            });
        }

        if (recipe.Login is not null)
        {
            spider.WithLogin(recipe.Login.Url, ResolveFormData(recipe.Login.FormData));
        }

        if (!string.IsNullOrEmpty(recipe.ItemSelector))
        {
            spider.WithItemSelector(recipe.ItemSelector);
        }

        if (!string.IsNullOrEmpty(recipe.LinkSelector))
        {
            var linkSelector = recipe.LinkSelector;
            spider.WithLinkExtractor((baseUrl, html) => ExtractLinks(html, baseUrl, linkSelector), recipe.MaxDepth);
        }
        else if (recipe.AutoPagination)
        {
            spider.WithLinkExtractor(PaginationHelper.FollowNextLink(), recipe.MaxDepth);
        }

        if (!string.IsNullOrEmpty(recipe.DeduplicateBy))
        {
            if (recipe.DeduplicateBy == "*")
            {
                spider.WithDeduplication();
            }
            else
            {
                var dedupField = recipe.DeduplicateBy;
                spider.WithDeduplication(item => item.TryGetValue(dedupField, out var value) ? value ?? string.Empty : string.Empty);
            }
        }

        PlaywrightPageRenderer? renderer = null;
        if (recipe.UseBrowserRendering)
        {
            renderer = new PlaywrightPageRenderer(new BrowserRenderOptions
            {
                ScreenshotDirectory = recipe.ScreenshotDirectory
            });
            spider.WithBrowserRendering(renderer);
            Console.WriteLine("Browser rendering enabled (Playwright). Make sure 'playwright install chromium' has been run once on this machine.");
        }

        if (recipe.Healing is not null && recipe.Healing.Provider != "none")
        {
            var provider = BuildHealingProvider(recipe.Healing);
            if (provider is not null)
            {
                var representativeHost = recipe.SeedUrls.Count > 0
                    ? new Uri(recipe.SeedUrls[0]).Host
                    : recipe.SeedRequest is not null ? new Uri(recipe.SeedRequest.Url).Host : "unknown-host";
                var cacheFile = $".harvestnet-cache-{representativeHost}.json";
                spider.WithHealing(new SelectorHealer(provider, new HealingCache(cacheFile)));
                Console.WriteLine($"Self-healing enabled using {provider.Name}");
            }
            else
            {
                Console.WriteLine("Self-healing was configured but could not be initialized (missing API key or base URL). Continuing without it.");
            }
        }

        var checkpointPath = recipe.CheckpointPath ?? recipe.Output.Path + ".checkpoint.json";
        var isResuming = File.Exists(checkpointPath);
        spider.WithCheckpoint(checkpointPath);

        if (isResuming)
        {
            Console.WriteLine("Found an existing checkpoint. Resuming the previous run instead of starting over.");
        }

        IResultSink<Dictionary<string, string?>> sink = recipe.Output.Format.ToLowerInvariant() switch
        {
            "csv" => new CsvSink<Dictionary<string, string?>>(recipe.Output.Path, append: isResuming),
            "sqlite" => new SqliteSink<Dictionary<string, string?>>(recipe.Output.Path),
            _ => new JsonLinesSink<Dictionary<string, string?>>(recipe.Output.Path, append: isResuming)
        };

        spider.WithSink(sink);

        var progress = new Progress<HarvestProgress>(p =>
            Console.Write($"\rFetched {p.PagesFetched} pages, {p.ItemsExtracted} items, {p.PagesFailed} failed...   "));
        spider.WithProgress(progress);

        var seedDescription = recipe.SeedUrls.Count > 0
            ? $"{recipe.SeedUrls.Count} seed URL(s)"
            : "a POST seed request";
        Console.WriteLine($"Starting crawl of {seedDescription}...");

        try
        {
            var summary = await spider.RunAsync().ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"Done in {summary.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"  Pages fetched: {summary.PagesFetched}");
            Console.WriteLine($"  Pages failed:  {summary.PagesFailed}");
            Console.WriteLine($"  Items found:   {summary.ItemsExtracted}");
            Console.WriteLine($"  Output:        {recipe.Output.Path}");

            if (summary.FieldCoverage.Count > 0)
            {
                Console.WriteLine("  Field coverage:");
                foreach (var (field, coverage) in summary.FieldCoverage.OrderBy(kvp => kvp.Key))
                {
                    Console.WriteLine($"    {field}: {coverage:P0}");
                }
            }

            if (!string.IsNullOrEmpty(recipe.WebhookUrl))
            {
                await NotifyWebhookAsync(recipe.WebhookUrl, new
                {
                    recipe = path,
                    pagesFetched = summary.PagesFetched,
                    pagesFailed = summary.PagesFailed,
                    itemsExtracted = summary.ItemsExtracted,
                    finishedAtUtc = DateTime.UtcNow
                }).ConfigureAwait(false);
            }

            return 0;
        }
        finally
        {
            if (renderer is not null)
            {
                await renderer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<int> WatchAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: harvestnet watch <recipe.json> [--interval seconds] [--key fieldName]");
            return 1;
        }

        var recipePath = args[1];
        var intervalSeconds = 3600;
        string? keyField = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--interval" && i + 1 < args.Length && int.TryParse(args[++i], out var parsedInterval))
            {
                intervalSeconds = parsedInterval;
            }
            else if (args[i] == "--key" && i + 1 < args.Length)
            {
                keyField = args[++i];
            }
        }

        if (!File.Exists(recipePath))
        {
            Console.Error.WriteLine($"Recipe file not found: {recipePath}");
            return 1;
        }

        Console.WriteLine($"Watching {recipePath} every {intervalSeconds}s. Press Ctrl+C to stop.");

        while (true)
        {
            var json = await File.ReadAllTextAsync(recipePath).ConfigureAwait(false);
            var recipe = JsonSerializer.Deserialize<Recipe>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (recipe is null)
            {
                Console.Error.WriteLine("Could not read recipe.");
                return 1;
            }

            var previousPath = recipe.Output.Path + ".previous";
            var hasPrevious = File.Exists(recipe.Output.Path);
            if (hasPrevious)
            {
                File.Copy(recipe.Output.Path, previousPath, overwrite: true);
            }

            Console.WriteLine($"[{DateTime.Now:u}] Running recipe...");
            await RunAsync(new[] { "run", recipePath }).ConfigureAwait(false);

            if (hasPrevious && keyField is not null && recipe.Output.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[{DateTime.Now:u}] Comparing to previous run...");
                await DiffAsync(new[] { "diff", previousPath, recipe.Output.Path, "--key", keyField }).ConfigureAwait(false);
            }

            Console.WriteLine($"[{DateTime.Now:u}] Sleeping for {intervalSeconds}s...");
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds)).ConfigureAwait(false);
        }
    }

    private static async Task<int> DiffAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: harvestnet diff <old.jsonl> <new.jsonl> --key <fieldName>");
            return 1;
        }

        var oldPath = args[1];
        var newPath = args[2];
        string? keyField = null;

        for (var i = 3; i < args.Length; i++)
        {
            if (args[i] == "--key" && i + 1 < args.Length)
            {
                keyField = args[++i];
            }
        }

        if (keyField is null)
        {
            Console.Error.WriteLine("Usage: harvestnet diff <old.jsonl> <new.jsonl> --key <fieldName>");
            return 1;
        }

        var oldItems = await LoadJsonLinesByKeyAsync(oldPath, keyField).ConfigureAwait(false);
        var newItems = await LoadJsonLinesByKeyAsync(newPath, keyField).ConfigureAwait(false);

        var added = newItems.Keys.Except(oldItems.Keys).ToList();
        var removed = oldItems.Keys.Except(newItems.Keys).ToList();
        var common = newItems.Keys.Intersect(oldItems.Keys).ToList();
        var changed = common.Where(key => oldItems[key] != newItems[key]).ToList();

        Console.WriteLine($"Added:     {added.Count}");
        Console.WriteLine($"Removed:   {removed.Count}");
        Console.WriteLine($"Changed:   {changed.Count}");
        Console.WriteLine($"Unchanged: {common.Count - changed.Count}");

        PrintKeySample("Added", added);
        PrintKeySample("Removed", removed);
        PrintKeySample("Changed", changed);

        return 0;
    }

    private static void PrintKeySample(string label, List<string> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{label} keys:");
        foreach (var key in keys.Take(20))
        {
            Console.WriteLine($"  {key}");
        }

        if (keys.Count > 20)
        {
            Console.WriteLine($"  ... and {keys.Count - 20} more");
        }
    }

    private static async Task<Dictionary<string, string>> LoadJsonLinesByKeyAsync(string path, string keyField)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var line in await File.ReadAllLinesAsync(path).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (!document.RootElement.TryGetProperty(keyField, out var keyElement))
                {
                    continue;
                }

                var key = keyElement.ToString();
                if (!string.IsNullOrEmpty(key))
                {
                    result[key] = line;
                }
            }
            catch (JsonException)
            {
                // Skip malformed lines rather than failing the whole comparison.
            }
        }

        return result;
    }

    private static async Task NotifyWebhookAsync(string webhookUrl, object payload)
    {
        try
        {
            using var httpClient = new HttpClient();
            await httpClient.PostAsJsonAsync(webhookUrl, payload).ConfigureAwait(false);
            Console.WriteLine("Webhook notification sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: failed to send webhook notification ({ex.Message}).");
        }
    }

    private static IEnumerable<Uri> ExtractLinks(string html, Uri baseUrl, string linkSelector)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html).Address(baseUrl.AbsoluteUri)).GetAwaiter().GetResult();

        var links = new List<Uri>();
        foreach (var element in document.QuerySelectorAll(linkSelector))
        {
            var href = element.GetAttribute("href");
            if (string.IsNullOrEmpty(href))
            {
                continue;
            }

            if (Uri.TryCreate(baseUrl, href, out var absolute))
            {
                links.Add(absolute);
            }
        }

        return links;
    }

    private static IHealingProvider? BuildHealingProvider(RecipeHealing healing)
    {
        var providerNames = healing.Provider.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (providerNames.Length == 0)
        {
            return null;
        }

        if (providerNames.Length == 1)
        {
            return BuildSingleProvider(providerNames[0], healing);
        }

        var providers = providerNames
            .Select(name => BuildSingleProvider(name, healing))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToArray();

        return providers.Length == 0 ? null : new HealingProviderChain(providers);
    }

    private static IHealingProvider? BuildSingleProvider(string providerName, RecipeHealing healing)
    {
        var apiKey = ResolveSecret(healing.ApiKey);

        return providerName.ToLowerInvariant() switch
        {
            "groq" => string.IsNullOrEmpty(apiKey) ? null : new GroqHealingProvider(apiKey, healing.Model ?? "llama-3.3-70b-versatile"),
            "gemini" => string.IsNullOrEmpty(apiKey) ? null : new GeminiHealingProvider(apiKey, healing.Model ?? "gemini-2.0-flash"),
            "ollama" => new OllamaHealingProvider(healing.BaseUrl ?? "http://localhost:11434", healing.Model ?? "llama3.1"),
            "openai-compatible" => string.IsNullOrEmpty(healing.BaseUrl) ? null : new OpenAiCompatibleHealingProvider(healing.BaseUrl, apiKey ?? string.Empty, healing.Model ?? "gpt-4o-mini"),
            _ => null
        };
    }

    private static string? ResolveSecret(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.GetEnvironmentVariable(value["env:".Length..]);
        }

        return value;
    }

    private static Dictionary<string, string> ResolveFormData(Dictionary<string, string> formData) =>
        formData.ToDictionary(kvp => kvp.Key, kvp => ResolveSecret(kvp.Value) ?? string.Empty);

    private static List<FieldTransform>? ParseTransforms(List<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return null;
        }

        var transforms = new List<FieldTransform>();
        foreach (var name in names)
        {
            if (Enum.TryParse<FieldTransform>(name, ignoreCase: true, out var transform))
            {
                transforms.Add(transform);
            }
            else
            {
                Console.WriteLine($"Warning: unknown transform \"{name}\" ignored.");
            }
        }

        return transforms;
    }
}
