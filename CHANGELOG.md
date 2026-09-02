# Changelog

All notable changes to this project are documented in this file.

## [1.1.0] - 2026-09-02

### Added

- HarvestNet.Browser: optional Playwright-based headless browser rendering for pages that
  need JavaScript to produce their final HTML, wired in through `WithBrowserRendering`.
- XPath as an alternative to CSS selectors, settable per field (`SelectorKind.XPath` in
  code, `"xpath": true` in a recipe), alongside the existing CSS and regex options.
- Resumable crawls: `WithCheckpoint` saves progress to disk after every depth level, so an
  interrupted run resumes instead of starting over. The CLI does this automatically for
  every recipe.
- Proxy rotation across a configurable pool, with shared credentials for gateway style
  proxy providers (`CrawlOptions.ProxyPool`, `ProxyUsername`, `ProxyPassword`).
- Retry-After header support: a 429 response now waits exactly as long as the server asks
  instead of always falling back to exponential backoff.
- `HealingProviderChain`, which tries multiple healing providers in order and falls back
  to the next one on failure or an empty result.
- Sitemap.xml seeding through `SitemapReader`, including recursive sitemap index files. A
  recipe's `sitemapUrl` field adds discovered URLs to `seedUrls` automatically.
- Live progress reporting through `IProgress<HarvestProgress>` and `WithProgress`. The CLI
  shows this as an updating line while a recipe runs.
- New CLI command, `harvestnet test-selector`, for trying a single CSS, XPath or regex
  selector against a live page without writing a full recipe.

## [1.0.0] - 2026-08-31

### Added

- Core crawling engine with per-host rate limiting, robots.txt support, automatic retries
  with exponential backoff, and configurable concurrency and depth limits.
- CSS selector based extraction, either through a `[HarvestField]` attribute on a plain
  C# class or through a runtime field map for dynamic use cases.
- Self-healing selectors: when a selector stops matching, a configured LLM provider is
  asked to suggest a replacement, which is validated against the page and cached to disk
  so the same fix is reused on every future run without another API call.
- Healing providers for Groq, Gemini, a local Ollama server, and any OpenAI-compatible
  endpoint (OpenRouter, LM Studio, and similar).
- Output sinks for JSON Lines, CSV and SQLite, all safe to write to concurrently.
- `harvestnet` command line tool with `init` and `run` commands, driven by JSON recipe
  files, so a scrape can be set up without writing any C#.
- Link following for multi-page crawls (pagination, "next page" links, and so on).
- A runnable QuickStart sample and an example recipe, both targeting the public scraping
  practice site quotes.toscrape.com.
- Full unit test suite covering robots.txt parsing, the healing cache, extraction, CSV
  output and the healing response parser.
