using AngleSharp;
using HarvestNet.Core;
using HarvestNet.Core.Crawling;
using HarvestNet.Core.Export;
using QuickStart;

var options = new CrawlOptions
{
    MaxConcurrency = 2,
    MaxDepth = 3,
    MaxPages = 20,
    DelayBetweenRequests = TimeSpan.FromMilliseconds(400)
};

var spider = new HarvestSpider<Quote>(options)
    .AddSeedUrl("https://quotes.toscrape.com/")
    .WithItemSelector(".quote")
    .WithLinkExtractor((baseUrl, html) => FindNextPage(baseUrl, html), maxDepth: 5)
    .WithSink(new CsvSink<Quote>("quotes.csv"));

// To turn on self-healing with a free Groq API key, uncomment the lines below.
// See the README for Gemini, Ollama and other free options.
//
// using HarvestNet.Core.Healing;
// var groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
// if (!string.IsNullOrEmpty(groqApiKey))
// {
//     var healer = new SelectorHealer(new GroqHealingProvider(groqApiKey), new HealingCache());
//     spider = spider.WithHealing(healer);
// }

Console.WriteLine("Crawling quotes.toscrape.com ...");
var summary = await spider.RunAsync();

Console.WriteLine($"Fetched {summary.PagesFetched} pages, extracted {summary.ItemsExtracted} quotes in {summary.Elapsed.TotalSeconds:F1}s.");
Console.WriteLine("Results saved to quotes.csv");

static IEnumerable<Uri> FindNextPage(Uri baseUrl, string html)
{
    var context = BrowsingContext.New(Configuration.Default);
    var document = context.OpenAsync(req => req.Content(html).Address(baseUrl.AbsoluteUri)).GetAwaiter().GetResult();
    var next = document.QuerySelector("li.next > a");
    var href = next?.GetAttribute("href");

    if (string.IsNullOrEmpty(href))
    {
        yield break;
    }

    if (Uri.TryCreate(baseUrl, href, out var absolute))
    {
        yield return absolute;
    }
}
