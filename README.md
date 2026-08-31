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
  retries with exponential backoff on 429 and 5xx responses, and configurable concurrency.
- **Two ways to define what to extract**: a typed C# class with `[HarvestField]`
  attributes for compile-time safety, or a JSON field map for cases where the schema is
  only known at run time (this is what the CLI uses).
- **Self-healing selectors**, powered by your choice of Groq, Gemini, a local Ollama
  model, or any OpenAI-compatible endpoint. Healed selectors are cached to disk per host
  and field, so the cost of a fix is paid once.
- **Three output sinks** out of the box: JSON Lines, CSV and SQLite, all safe under
  concurrent writes.
- **A CLI with no code required**: describe a scrape as a JSON recipe and run it with
  `harvestnet run recipe.json`.
- **A clean library API** for anything more custom: build a `HarvestSpider<T>`, add seed
  URLs, wire up sinks and a link extractor, and call `RunAsync()`.

## Installation

### As a library

```bash
dotnet add package HarvestNet.Core
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

- JavaScript-rendered page support through a Playwright-based renderer, for sites that
  need a real browser to produce their HTML.
- XPath as an alternative to CSS selectors.
- A pluggable dedupe/storage backend for very large, resumable crawls.
- More healing providers as free-tier APIs come and go.

Contributions toward any of these are very welcome; see CONTRIBUTING.md.

## Contributing

Bug reports, feature ideas and pull requests are all welcome. See CONTRIBUTING.md for
how the project is organized and how to get set up locally.

## License

MIT. See LICENSE.
