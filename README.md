# HarvestNet

A self-healing web scraping and crawling framework for .NET.

Every time a site you scrape changes its markup, your selectors break and your pipeline
goes quiet until someone notices and fixes it by hand. HarvestNet adds an optional layer
that catches this automatically: when a CSS selector stops matching, it hands the page to
a free-tier LLM, asks for a replacement selector, checks that the replacement actually
works, and remembers the fix so it never has to ask again for that field on that site.

Python has had this kind of tooling for a while (Scrapy, Scrapling, Crawlee and others).
.NET has not: outside of raw HTML parsers like AngleSharp and HtmlAgilityPack, there has
been no actively maintained, AI-assisted scraping framework built for C#. HarvestNet is
an attempt to close that gap.

## Why this exists

Scraping code rots. A site redesigns its product cards, renames a CSS class, or wraps a
price in a new `<span>`, and every scraper pointed at that page silently starts returning
nulls. Most teams find out when a report looks wrong, not when it happens. HarvestNet does
not eliminate this problem, but it gives you a way to recover from it automatically instead
of dropping everything to go read the new page source.

The healing step only runs when extraction actually fails, so a healthy scrape that keeps
matching all its selectors never calls an LLM at all. Combined with a free tier like Groq's,
this means the AI-assisted part of HarvestNet effectively costs nothing for most projects.

## Features

- **Polite crawling by default**: per-host rate limiting, robots.txt checks, automatic
  retries with exponential backoff (or the server's own Retry-After header) on 429 and
  5xx responses, and configurable concurrency.
- **Two ways to define what to extract**: a typed C# class with `[HarvestField]`
  attributes for compile-time safety, or a JSON field map for cases where the schema is
  only known at run time (this is what the CLI uses).
- **CSS selectors, XPath, JSON-LD, or regular expressions** for any field, mixed freely
  within the same item. JSON-LD reads a page's schema.org structured data directly,
  which tends to be far more stable than the visible HTML.
- **Self-healing selectors**, powered by your choice of Groq, Gemini, a local Ollama
  model, or any OpenAI-compatible endpoint, with automatic fallback across multiple
  providers if you chain them. Healed selectors are cached to disk per host and field, so
  the cost of a fix is paid once.
- **Optional browser rendering** through HarvestNet.Browser (Playwright), for pages that
  need JavaScript to produce their final HTML.
- **GET and POST requests**, including form submissions, plus an optional login step so
  pages behind a sign-in can be reached.
- **Fallback selectors per field**, tried before self-healing kicks in, for sites that
  serve more than one page template for the same kind of content.
- **Ready-made schema.org models** (`SchemaOrgProduct`, `SchemaOrgArticle`) for scraping
  JSON-LD compliant product and article pages with zero selectors.
- **Automatic pagination detection** (`PaginationHelper`) for the common "next page" link
  patterns, when you would rather not hand write one.
- **Resumable crawls**: enable a checkpoint file and an interrupted run picks up where it
  left off instead of starting over.
- **Proxy and User-Agent rotation** across a pool, with shared credentials for gateway
  style proxy providers.
- **Response caching**, so iterating on selectors does not mean re-fetching the same
  pages over and over.
- **Smart URL deduplication**, ignoring tracking parameters, fragments and trailing
  slashes when deciding whether a page has already been visited, plus optional item level
  deduplication for content that appears more than once.
- **Sitemap.xml or plain text file seeding**, so a crawl can discover its URLs instead of
  listing them all by hand.
- **Live progress reporting** through a simple `IProgress<HarvestProgress>` callback.
- **Change tracking**: `harvestnet diff` compares two runs, and `harvestnet watch`
  re-runs a recipe on a schedule and reports what changed, optionally notifying a webhook.
- **Three output sinks** out of the box: JSON Lines, CSV and SQLite, all safe under
  concurrent writes.
- **A CLI with no code required**: describe a scrape as a JSON recipe and run it with
  `harvestnet run recipe.json`, or try a single selector against a live page with
  `harvestnet test-selector`.
- **A clean library API** for anything more custom: build a `HarvestSpider<T>`, add seed
  URLs, wire up sinks and a link extractor, and call `RunAsync()`.

## Installation

### As a library

```bash
dotnet add package HarvestNet.Core
```

To scrape JavaScript-rendered pages, also add the browser rendering package and install
Playwright's browser binaries once per machine:

```bash
dotnet add package HarvestNet.Browser
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

### As a command line tool

```bash
dotnet tool install --global HarvestNet.Cli
```

This installs the `harvestnet` command.

(Both packages are not yet published to NuGet as of this writing. See the "Publishing"
section below for how to push them from this repository.)

## Quick start: the library

```csharp
using HarvestNet.Core;
using HarvestNet.Core.Export;
using HarvestNet.Core.Extraction;

public class Quote
{
    [HarvestField(".text")]
    public string? Text { get; set; }

    [HarvestField(".author")]
    public string? Author { get; set; }
}

var spider = new HarvestSpider<Quote>()
    .AddSeedUrl("https://quotes.toscrape.com/")
    .WithItemSelector(".quote")
    .WithSink(new CsvSink<Quote>("quotes.csv"));

var summary = await spider.RunAsync();
Console.WriteLine($"Extracted {summary.ItemsExtracted} quotes from {summary.PagesFetched} pages.");
```

Run the full version of this example, including pagination, from `samples/QuickStart`.

## Quick start: the CLI

Generate a starter recipe:

```bash
harvestnet init my-scrape
```

Edit `my-scrape.json`:

```json
{
  "seedUrls": ["https://quotes.toscrape.com/"],
  "itemSelector": ".quote",
  "fields": {
    "text": { "selector": ".text" },
    "author": { "selector": ".author" }
  },
  "linkSelector": "li.next > a",
  "maxDepth": 5,
  "maxPages": 50,
  "output": { "format": "json", "path": "quotes.jsonl" }
}
```

Run it:

```bash
harvestnet run my-scrape.json
```

A more complete example, including self-healing, lives in
`samples/recipes/quotes-example.json`.

## Turning on self-healing

Self-healing is optional and off by default. It needs one LLM provider, all of which have
a free tier or run entirely locally.

| Provider | Cost | Setup |
|---|---|---|
| Groq | Free tier | Create an API key at console.groq.com |
| Gemini | Free tier | Create an API key at aistudio.google.com |
| Ollama | Free, local | Install Ollama and pull a model, e.g. `ollama pull llama3.1` |
| Any OpenAI-compatible endpoint | Depends on the provider | Point it at OpenRouter's free models, LM Studio, or your own server |

In code:

```csharp
using HarvestNet.Core.Healing;

var healer = new SelectorHealer(
    new GroqHealingProvider(Environment.GetEnvironmentVariable("GROQ_API_KEY")!),
    new HealingCache());

var spider = new HarvestSpider<Quote>()
    .AddSeedUrl("https://quotes.toscrape.com/")
    .WithItemSelector(".quote")
    .WithHealing(healer)
    .WithSink(new CsvSink<Quote>("quotes.csv"));
```

In a recipe:

```json
{
  "healing": {
    "provider": "groq",
    "apiKey": "env:GROQ_API_KEY",
    "model": "llama-3.3-70b-versatile"
  }
}
```

`apiKey` accepts either a literal key or `env:VARIABLE_NAME` to read it from an
environment variable at run time, so you never have to commit a key to a recipe file.

If you have a paid API key for a stronger model, you can use it too: `GeminiHealingProvider`
and `OpenAiCompatibleHealingProvider` both work with any model name your account has access
to. HarvestNet never requires a paid key; it just does not get in the way if you have one.

Chaining providers is also supported, so a fast API can fall back to a local model:

```csharp
var healer = new SelectorHealer(
    new HealingProviderChain(
        new GroqHealingProvider(groqApiKey),
        new OllamaHealingProvider()),
    new HealingCache());
```

In a recipe, chain providers with a comma: `"provider": "groq,ollama"`. Note that a
recipe's `apiKey` and `model` are shared across every provider in the chain, so this only
works cleanly when at most one provider in the chain actually needs a key (Ollama does
not). For chains where each provider needs its own key or model, build the chain in code
instead.

## JSON-LD, XPath and regular expressions

Many modern sites embed schema.org structured data in a
`<script type="application/ld+json">` block, meant to be read by search engines. It is
usually far more stable than the visible HTML, so it is worth checking before reaching
for a selector:

```csharp
public class Product
{
    [HarvestField("name", Kind = SelectorKind.JsonLd)]
    public string? Name { get; set; }

    [HarvestField("offers.price", Kind = SelectorKind.JsonLd)]
    public string? Price { get; set; }
}
```

The selector is a dot-separated path into the JSON-LD object (for example
`offers.price`), evaluated against every JSON-LD block on the page in order until one of
them has that path. In a recipe, set `"jsonLd": true` on a field:

```json
"price": { "selector": "offers.price", "jsonLd": true }
```

JSON-LD fields are read once per page and shared by every item extracted from it, are not
passed to a self-healing provider (there is no HTML selector to repair), and the
underlying JSON-LD data is also available directly through `StructuredDataReader.ReadJsonLd`,
along with `ReadOpenGraphTags` and `ReadMetaTags` for a page's Open Graph and meta tags.

Every field can also use a CSS selector (the default) or an XPath expression:

```csharp
public class Product
{
    [HarvestField(".product-title")]
    public string? Title { get; set; }

    [HarvestField("//span[@class='price']", Kind = SelectorKind.XPath)]
    public string? Price { get; set; }

    [HarvestField(".sku-text", Kind = SelectorKind.RegexOnText, Attribute = @"SKU-\d+")]
    public string? Sku { get; set; }
}
```

In a recipe, set `"xpath": true` on a field to treat its selector as XPath:

```json
"price": { "selector": "//span[@class='price']", "xpath": true }
```

Item containers (the repeating element passed to `WithItemSelector` or `itemSelector`)
are CSS only for now; XPath containers are on the roadmap. When a field's XPath is
evaluated inside an item (rather than against the whole page), write it relative to that
item using a leading `.//`, for example `.//span[@class='price']`, the same way you
would in any XPath tool.

## Scraping JavaScript-rendered pages

Some sites only produce their real content after JavaScript runs. For those, render with
a real browser instead of a plain HTTP request:

```csharp
using HarvestNet.Browser;

await using var renderer = new PlaywrightPageRenderer();

var spider = new HarvestSpider<Quote>()
    .AddSeedUrl("https://example.com/")
    .WithItemSelector(".quote")
    .WithBrowserRendering(renderer)
    .WithSink(new CsvSink<Quote>("quotes.csv"));

await spider.RunAsync();
```

In a recipe, set `"useBrowserRendering": true`. This needs the Playwright browser
binaries installed once per machine (see Installation above); without them, rendering
will fail with a clear error from Playwright telling you to run `playwright install`.

Rendering a page with a real browser is much slower than a plain HTTP request, so use it
only for the sites that actually need it.

## Forms, search endpoints and logins

Not every page is reachable with a plain GET. To submit a form or hit a search endpoint,
add a POST seed:

```csharp
var spider = new HarvestSpider<Product>()
    .AddPostSeed("https://example.com/search", new Dictionary<string, string>
    {
        ["query"] = "wireless headphones",
        ["sort"] = "price-asc"
    })
    .WithItemSelector(".result")
    .WithSink(new CsvSink<Product>("results.csv"));
```

In a recipe, use `seedRequest` instead of (or alongside) `seedUrls`:

```json
"seedRequest": {
  "url": "https://example.com/search",
  "method": "POST",
  "formData": { "query": "wireless headphones", "sort": "price-asc" }
}
```

For pages behind a login, send the login POST first; any cookies the response sets are
kept for the rest of the crawl automatically:

```csharp
var spider = new HarvestSpider<Product>()
    .WithLogin("https://example.com/login", new Dictionary<string, string>
    {
        ["username"] = "me",
        ["password"] = Environment.GetEnvironmentVariable("SITE_PASSWORD")!
    })
    .AddSeedUrl("https://example.com/account/orders");
```

In a recipe:

```json
"login": {
  "url": "https://example.com/login",
  "formData": { "username": "me", "password": "env:SITE_PASSWORD" }
}
```

Just like `healing.apiKey`, any form data value in a recipe can be `env:VARIABLE_NAME` to
read it from an environment variable at run time instead of writing it into the file, so
real credentials never need to be committed.

## Fallback selectors

Before reaching for AI healing, it is often enough to just list a second selector to try:

```csharp
[HarvestField(".price", FallbackSelectors = new[] { ".price-old", ".sale-price" })]
public string? Price { get; set; }
```

In a recipe:

```json
"price": { "selector": ".price", "fallbackSelectors": [".price-old", ".sale-price"] }
```

Fallbacks are tried in order, only when the primary selector finds nothing, and only
then does self-healing (if configured) get involved.

## Automatic pagination

If you would rather not hand write a "next page" selector, `PaginationHelper` recognizes
the common patterns (`rel="next"`, `.pagination .next`, `a.next`, and a few others):

```csharp
var spider = new HarvestSpider<Quote>()
    .AddSeedUrl("https://example.com/")
    .WithItemSelector(".quote")
    .WithLinkExtractor(PaginationHelper.FollowNextLink(), maxDepth: 10);
```

Pass your own selectors first if a site needs one: `PaginationHelper.FollowNextLink(".my-next-button")`.
In a recipe, set `"autoPagination": true` instead of `linkSelector`.

## Ready-made schema.org models

Many product and article pages already publish enough JSON-LD to skip selectors
entirely:

```csharp
using HarvestNet.Core.Extraction.Schemas;

var engine = new ExtractionEngine();
var product = await engine.ExtractAsync<SchemaOrgProduct>(html, url);
Console.WriteLine($"{product?.Name}: {product?.Price} {product?.Currency}");
```

`SchemaOrgProduct` covers name, sku, description, brand, price, currency, availability,
image, and aggregate rating. `SchemaOrgArticle` covers headline, author, publisher, and
publish/modified dates. A field the page does not publish is simply left null.

## Resumable crawls

For long crawls that might get interrupted, enable a checkpoint file:

```csharp
var spider = new HarvestSpider<Quote>()
    .AddSeedUrl("https://example.com/")
    .WithItemSelector(".quote")
    .WithCheckpoint("crawl-state.json")
    .WithSink(new CsvSink<Quote>("quotes.csv"));
```

If the process is killed partway through, running the same code again picks up from the
last completed depth level instead of starting over. The checkpoint file is deleted
automatically once a crawl finishes cleanly. The CLI does this for every recipe
automatically, writing to `<output path>.checkpoint.json` unless you set
`checkpointPath` yourself; if you re-run `harvestnet run` on a recipe with a leftover
checkpoint, it resumes and tells you so.

## Proxies, User-Agents and caching

Rotate through a pool of proxies, one per request:

```csharp
var options = new CrawlOptions
{
    ProxyPool = new List<string> { "http://proxy-a:8080", "http://proxy-b:8080" },
    ProxyUsername = "myuser",
    ProxyPassword = "mypassword"
};
```

The same username and password are used for every proxy in the pool, which covers the
common case of a single login for a rotating proxy gateway. In a recipe, set
`proxyPool`, `proxyUsername` and `proxyPassword`.

User-Agent strings can be rotated the same way, independently of proxies:

```csharp
var options = new CrawlOptions
{
    UserAgentPool = new List<string> { "Mozilla/5.0 ...", "Mozilla/5.0 ..." }
};
```

In a recipe, set `userAgentPool`. Proxy and User-Agent rotation apply to plain HTTP
requests only, not to browser rendering.

While iterating on selectors, re-fetching the same pages on every run both wastes time
and puts unnecessary load on the site. Point `CacheDirectory` at a folder and successful
fetches are cached there and reused automatically:

```csharp
var options = new CrawlOptions { CacheDirectory = "./.harvestnet-cache" };
```

In a recipe, set `cacheDirectory`. Delete the folder (or point at a new one) whenever you
actually want fresh data.

## Deduplication

Two kinds of duplication come up in scraping: the same page reached by two different
URLs, and the same item appearing on more than one page.

For URLs, HarvestNet strips tracking parameters (`utm_source` and similar), the
fragment, and a trailing slash before checking whether a URL has already been visited,
which is on by default (`CrawlOptions.NormalizeUrls = true`). Set it to false if you need
exact URL matching instead.

For items, opt in explicitly:

```csharp
var spider = new HarvestSpider<Product>()
    .AddSeedUrl("https://example.com/")
    .WithItemSelector(".product")
    .WithDeduplication(product => product.Sku)
    .WithSink(new CsvSink<Product>("products.csv"));
```

With no key selector, two items are considered the same when they serialize to identical
JSON. In a recipe, set `deduplicateBy` to a field name, or to `"*"` to deduplicate by
full item content.

## Seeding from a sitemap or a file

Instead of listing every seed URL by hand:

```csharp
using var httpClient = new HttpClient();
var urls = await SitemapReader.ReadUrlsAsync(new Uri("https://example.com/sitemap.xml"), httpClient);

var spider = new HarvestSpider<Quote>().AddSeedUrls(urls);
```

`SitemapReader` also follows sitemap index files (a sitemap that points to other
sitemaps), up to a few levels deep. In a recipe, set `sitemapUrl` and its URLs are added
to `seedUrls` automatically before the crawl starts.

If you already have a list of URLs saved somewhere (exported from a spreadsheet, for
example), `SeedFileReader` reads one per line, skipping blank lines and lines starting
with `#`:

```csharp
var urls = SeedFileReader.ReadUrls("product-urls.txt");
var spider = new HarvestSpider<Product>().AddSeedUrls(urls);
```

In a recipe, set `seedUrlsFile` to the file's path.

## Progress reporting

```csharp
var progress = new Progress<HarvestProgress>(p =>
    Console.WriteLine($"{p.PagesFetched} pages, {p.ItemsExtracted} items so far"));

var spider = new HarvestSpider<Quote>()
    .AddSeedUrl("https://example.com/")
    .WithProgress(progress);
```

The CLI shows this as a live, updating counter while a recipe runs.

## Trying a selector before writing a recipe

```bash
harvestnet test-selector https://example.com/product/123 ".price"
harvestnet test-selector https://example.com/product/123 "//span[@class='price']" --xpath
harvestnet test-selector https://example.com/product/123 "a.details" --attribute href
```

This fetches the page once and shows what the selector matches, without needing a full
recipe file.

## Tracking changes over time

Two CLI commands turn a one-off scrape into an ongoing monitor. `harvestnet diff`
compares two JSON Lines snapshots by a key field:

```bash
harvestnet diff yesterday.jsonl today.jsonl --key sku
```

It reports how many items were added, removed, or changed, and lists the affected keys.

`harvestnet watch` re-runs a recipe on a schedule and diffs each run against the last one
automatically:

```bash
harvestnet watch recipe.json --interval 3600 --key sku
```

This only compares runs when the recipe's output format is `json`. Set `webhookUrl` in
the recipe to have a short JSON summary (pages fetched, items found, timestamp) POSTed
somewhere (a Slack or Discord incoming webhook both accept a plain JSON body) once each
run finishes:

```json
"webhookUrl": "https://hooks.example.com/services/..."
```

## Output formats

Set `output.format` in a recipe (or pick a sink directly in code) to `json`, `csv`, or
`sqlite`. All three sinks buffer writes safely across the concurrent requests HarvestNet
makes, so you do not need to add your own locking.

## How selector healing actually works

1. HarvestNet tries your CSS selector as usual.
2. If it finds nothing, and healing is enabled, it checks a local JSON cache
   (`harvestnet-healed-selectors.json` by default) for a previously healed selector for
   that host and field.
3. If nothing is cached, it sends a truncated HTML snippet, the field name, your optional
   description, and the old selector to the configured provider, and asks for a JSON
   response with a new selector.
4. The proposed selector is tried against the same page. If it actually finds something,
   it is used for that extraction and written to the cache.
5. If it does not find anything, or the provider call fails for any reason, HarvestNet
   falls back to leaving the field empty rather than crashing the crawl.

Because the healed selector is cached per host and field, the LLM is only called the first
time a break is detected on a given site, not on every subsequent page or run.

## Comparison

| | HarvestNet | AngleSharp / HtmlAgilityPack | Scrapy / Scrapling (Python) |
|---|---|---|---|
| Language | C# / .NET | C# / .NET | Python |
| Crawl orchestration | Built in | You write it yourself | Built in |
| Self-healing selectors | Yes, optional | No | Some tools in this space have it |
| CLI, no code required | Yes | No | Varies |
| Free to run | Yes | Yes | Yes |

AngleSharp and HtmlAgilityPack are excellent HTML parsers and HarvestNet actually uses
AngleSharp under the hood. What they do not give you is the crawling, retry, rate
limiting, and self-healing layer on top, which is what HarvestNet adds.

## Roadmap

- XPath and JSON-LD support for item containers, not just individual fields.
- A pluggable dedupe/storage backend for very large crawls (beyond the JSON checkpoint
  file, which is fine for most projects but not built for millions of URLs).
- Per-provider API keys when chaining healing providers through a recipe.
- Graceful shutdown for `harvestnet watch` and richer notification formats (Slack/Discord
  specific payloads, not just a generic JSON POST).
- Per-domain concurrency limits, for crawls that span several sites at once.
- More healing providers as free-tier APIs come and go.

Contributions toward any of these are very welcome; see CONTRIBUTING.md.

## Contributing

Bug reports, feature ideas and pull requests are all welcome. See CONTRIBUTING.md for
how the project is organized and how to get set up locally.

## License

MIT. See LICENSE.
