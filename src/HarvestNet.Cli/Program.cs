using System.Text.Json;
using AngleSharp;
using HarvestNet.Core;
using HarvestNet.Core.Crawling;
using HarvestNet.Core.Export;
using HarvestNet.Core.Extraction;
using HarvestNet.Core.Healing;

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
          harvestnet init <recipe-name>      Create a starter recipe file
          harvestnet run <recipe.json>       Run a scraping recipe
          harvestnet version                 Print the version

        A recipe is a JSON file describing what to crawl and how to extract data.
        Run "harvestnet init my-recipe" to generate a starter file you can edit.
        """);
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("HarvestNet CLI 1.0.0");
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

        if (recipe is null || recipe.SeedUrls.Count == 0)
        {
            Console.Error.WriteLine("Recipe must include at least one seed URL.");
            return 1;
        }

        var options = new CrawlOptions
        {
            MaxConcurrency = recipe.MaxConcurrency,
            MaxDepth = recipe.MaxDepth,
            MaxPages = recipe.MaxPages,
            DelayBetweenRequests = TimeSpan.FromMilliseconds(recipe.DelayMilliseconds),
            RespectRobotsTxt = recipe.RespectRobotsTxt
        };

        var fields = recipe.Fields.ToDictionary(
            kvp => kvp.Key,
            kvp => new FieldSpec
            {
                Selector = kvp.Value.Selector,
                Kind = string.IsNullOrEmpty(kvp.Value.Regex) ? SelectorKind.Css : SelectorKind.RegexOnText,
                Attribute = string.IsNullOrEmpty(kvp.Value.Regex) ? kvp.Value.Attribute : kvp.Value.Regex,
                Description = kvp.Value.Description,
                Required = kvp.Value.Required
            });

        var spider = new HarvestSpider<Dictionary<string, string?>>(options)
            .WithFields(fields, "Item");

        foreach (var seed in recipe.SeedUrls)
        {
            spider.AddSeedUrl(seed);
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

        if (recipe.Healing is not null && recipe.Healing.Provider != "none")
        {
            var provider = BuildHealingProvider(recipe.Healing);
            if (provider is not null)
            {
                var cacheFile = $".harvestnet-cache-{new Uri(recipe.SeedUrls[0]).Host}.json";
                spider.WithHealing(new SelectorHealer(provider, new HealingCache(cacheFile)));
                Console.WriteLine($"Self-healing enabled using {provider.Name}");
            }
            else
            {
                Console.WriteLine("Self-healing was configured but could not be initialized (missing API key or base URL). Continuing without it.");
            }
        }

        IResultSink<Dictionary<string, string?>> sink = recipe.Output.Format.ToLowerInvariant() switch
        {
            "csv" => new CsvSink<Dictionary<string, string?>>(recipe.Output.Path),
            "sqlite" => new SqliteSink<Dictionary<string, string?>>(recipe.Output.Path),
            _ => new JsonLinesSink<Dictionary<string, string?>>(recipe.Output.Path)
        };

        spider.WithSink(sink);

        Console.WriteLine($"Starting crawl of {recipe.SeedUrls.Count} seed URL(s)...");
        var summary = await spider.RunAsync().ConfigureAwait(false);

        Console.WriteLine($"Done in {summary.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"  Pages fetched: {summary.PagesFetched}");
        Console.WriteLine($"  Pages failed:  {summary.PagesFailed}");
        Console.WriteLine($"  Items found:   {summary.ItemsExtracted}");
        Console.WriteLine($"  Output:        {recipe.Output.Path}");

        return 0;
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
        var apiKey = ResolveSecret(healing.ApiKey);

        return healing.Provider.ToLowerInvariant() switch
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
}
