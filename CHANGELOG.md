# Changelog

All notable changes to this project are documented in this file.

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
