using System.Collections.Concurrent;
using System.Diagnostics;
using HarvestNet.Core.Crawling;
using HarvestNet.Core.Export;
using HarvestNet.Core.Extraction;
using HarvestNet.Core.Healing;
using HarvestNet.Core.Http;

namespace HarvestNet.Core;

/// <summary>
/// The main entry point for using HarvestNet as a library: a fluent builder that wires
/// together crawling, extraction, optional self-healing, optional browser rendering and
/// one or more output sinks.
///
/// Two extraction modes are supported. Give it a POCO decorated with
/// <see cref="HarvestFieldAttribute"/> for compile-time-checked scraping, or call
/// <see cref="WithFields"/> with a runtime field map (using
/// <c>HarvestSpider&lt;Dictionary&lt;string, string?&gt;&gt;</c>) when the schema is only
/// known at runtime, as the CLI does with JSON recipes.
/// </summary>
public sealed class HarvestSpider<T> where T : new()
{
    private readonly CrawlOptions _crawlOptions;
    private readonly List<Uri> _seedUrls = new();
    private readonly List<IResultSink<T>> _sinks = new();
    private string? _itemSelector;
    private ISelectorHealer? _healer;
    private IPageRenderer? _renderer;
    private IProgress<HarvestProgress>? _progress;
    private string? _checkpointFilePath;
    private Func<Uri, string, IEnumerable<Uri>>? _linkExtractor;
    private int _maxDepth = 1;
    private IReadOnlyDictionary<string, FieldSpec>? _explicitFields;
    private string _itemTypeName = typeof(T).Name;
    private ItemDeduplicator<T>? _deduplicator;

    public HarvestSpider(CrawlOptions? options = null)
    {
        _crawlOptions = options ?? new CrawlOptions();
    }

    public HarvestSpider<T> AddSeedUrl(string url)
    {
        _seedUrls.Add(new Uri(url));
        return this;
    }

    public HarvestSpider<T> AddSeedUrls(IEnumerable<Uri> urls)
    {
        _seedUrls.AddRange(urls);
        return this;
    }

    public HarvestSpider<T> WithItemSelector(string cssSelector)
    {
        _itemSelector = cssSelector;
        return this;
    }

    /// <summary>Use a runtime field map instead of <see cref="HarvestFieldAttribute"/> reflection. T must be Dictionary&lt;string, string?&gt;.</summary>
    public HarvestSpider<T> WithFields(IReadOnlyDictionary<string, FieldSpec> fields, string? itemTypeName = null)
    {
        _explicitFields = fields;
        if (itemTypeName is not null)
        {
            _itemTypeName = itemTypeName;
        }

        return this;
    }

    public HarvestSpider<T> WithHealing(ISelectorHealer healer)
    {
        _healer = healer;
        return this;
    }

    /// <summary>
    /// Renders every page through <paramref name="renderer"/> (a real browser) instead of a
    /// plain HTTP GET. Use this for sites that need JavaScript to produce their final HTML.
    /// The caller owns the renderer's lifecycle and should dispose it after the run.
    /// </summary>
    public HarvestSpider<T> WithBrowserRendering(IPageRenderer renderer)
    {
        _renderer = renderer;
        return this;
    }

    /// <summary>Reports a <see cref="HarvestProgress"/> snapshot after every page is processed.</summary>
    public HarvestSpider<T> WithProgress(IProgress<HarvestProgress> progress)
    {
        _progress = progress;
        return this;
    }

    /// <summary>
    /// Enables checkpointing to <paramref name="filePath"/>. If the file already exists when
    /// <see cref="RunAsync"/> is called, the crawl resumes from it instead of starting over
    /// from the seed URLs. The file is deleted automatically once a crawl finishes cleanly.
    /// </summary>
    public HarvestSpider<T> WithCheckpoint(string filePath)
    {
        _checkpointFilePath = filePath;
        return this;
    }

    public HarvestSpider<T> WithSink(IResultSink<T> sink)
    {
        _sinks.Add(sink);
        return this;
    }

    /// <summary>
    /// Skips writing an item that has already been produced during this run. With no
    /// <paramref name="keySelector"/>, two items are considered the same when they
    /// serialize to identical JSON; pass one (for example an item's URL or SKU) to
    /// deduplicate by a specific field instead, which is both faster and more precise.
    /// </summary>
    public HarvestSpider<T> WithDeduplication(Func<T, string>? keySelector = null)
    {
        _deduplicator = new ItemDeduplicator<T>(keySelector);
        return this;
    }

    /// <summary>
    /// Enables link following. <paramref name="linkExtractor"/> receives the current page's
    /// URL and HTML and returns the absolute URLs to enqueue next.
    /// </summary>
    public HarvestSpider<T> WithLinkExtractor(Func<Uri, string, IEnumerable<Uri>> linkExtractor, int maxDepth = 2)
    {
        _linkExtractor = linkExtractor;
        _maxDepth = maxDepth;
        return this;
    }

    private string GetVisitedKey(Uri url) =>
        _crawlOptions.NormalizeUrls ? UrlNormalizer.Normalize(url).AbsoluteUri : url.AbsoluteUri;

    public async Task<HarvestRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var summary = new HarvestRunSummary();
        var stopwatch = Stopwatch.StartNew();

        using var httpClient = new PoliteHttpClient(_crawlOptions, _renderer);
        var extractionEngine = new ExtractionEngine(_healer);

        var checkpoint = _checkpointFilePath is not null ? CrawlCheckpoint.LoadOrNull(_checkpointFilePath) : null;

        HashSet<string> visited;
        List<CrawlRequest> currentLevel;
        int depth;

        if (checkpoint is not null)
        {
            visited = new HashSet<string>(checkpoint.Visited, StringComparer.OrdinalIgnoreCase);
            currentLevel = checkpoint.Frontier
                .Select(u => new CrawlRequest { Url = new Uri(u.Url), Depth = u.Depth })
                .ToList();
            depth = checkpoint.Depth;
            summary.PagesFetchedInternal = checkpoint.PagesFetched;
            summary.PagesFailedInternal = checkpoint.PagesFailed;
            summary.ItemsExtractedInternal = checkpoint.ItemsExtracted;
        }
        else
        {
            visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            currentLevel = _seedUrls.Select(url => new CrawlRequest { Url = url, Depth = 0 }).ToList();
            depth = 0;
        }

        while (currentLevel.Count > 0 && summary.PagesFetched < _crawlOptions.MaxPages)
        {
            var toProcess = currentLevel
                .Where(r => visited.Add(GetVisitedKey(r.Url)))
                .Take(Math.Max(0, _crawlOptions.MaxPages - summary.PagesFetched))
                .ToList();

            if (toProcess.Count == 0)
            {
                break;
            }

            using var throttler = new SemaphoreSlim(Math.Max(1, _crawlOptions.MaxConcurrency));
            var nextLevelBag = new ConcurrentBag<Uri>();
            var currentDepth = depth;

            var tasks = toProcess.Select(async request =>
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var discovered = await ProcessAsync(request, httpClient, extractionEngine, summary, cancellationToken).ConfigureAwait(false);
                    if (_linkExtractor is not null && currentDepth < _maxDepth)
                    {
                        foreach (var link in discovered)
                        {
                            nextLevelBag.Add(link);
                        }
                    }
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            currentLevel = nextLevelBag.Distinct()
                .Select(url => new CrawlRequest { Url = url, Depth = currentDepth + 1 })
                .ToList();
            depth++;

            if (_checkpointFilePath is not null)
            {
                CrawlCheckpoint.Save(_checkpointFilePath, new CrawlCheckpoint
                {
                    Visited = visited.ToList(),
                    Frontier = currentLevel.Select(r => new CheckpointUrl { Url = r.Url.AbsoluteUri, Depth = r.Depth }).ToList(),
                    Depth = depth,
                    PagesFetched = summary.PagesFetched,
                    PagesFailed = summary.PagesFailed,
                    ItemsExtracted = summary.ItemsExtracted
                });
            }
        }

        foreach (var sink in _sinks)
        {
            await sink.DisposeAsync().ConfigureAwait(false);
        }

        if (_checkpointFilePath is not null)
        {
            CrawlCheckpoint.Delete(_checkpointFilePath);
        }

        summary.Elapsed = stopwatch.Elapsed;
        return summary;
    }

    private async Task<IEnumerable<Uri>> ProcessAsync(
        CrawlRequest request,
        PoliteHttpClient httpClient,
        ExtractionEngine extractionEngine,
        HarvestRunSummary summary,
        CancellationToken cancellationToken)
    {
        var result = await httpClient.FetchAsync(request, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref summary.PagesFetchedInternal);

        if (!result.Success || result.Html is null)
        {
            Interlocked.Increment(ref summary.PagesFailedInternal);
            ReportProgress(request.Url, summary);
            return Enumerable.Empty<Uri>();
        }

        if (_itemSelector is not null)
        {
            if (_explicitFields is not null)
            {
                var rawItems = await extractionEngine.ExtractListAsync(result.Html, request.Url, _itemSelector, _explicitFields, _itemTypeName, cancellationToken).ConfigureAwait(false);
                foreach (var raw in rawItems)
                {
                    if (raw is T typed)
                    {
                        await WriteItemIfNotDuplicateAsync(typed, summary, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                var items = await extractionEngine.ExtractListAsync<T>(result.Html, request.Url, _itemSelector, cancellationToken).ConfigureAwait(false);
                foreach (var item in items)
                {
                    await WriteItemIfNotDuplicateAsync(item, summary, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else if (_explicitFields is not null)
        {
            var raw = await extractionEngine.ExtractAsync(result.Html, request.Url, _explicitFields, _itemTypeName, cancellationToken).ConfigureAwait(false);
            if (raw is T typed)
            {
                await WriteItemIfNotDuplicateAsync(typed, summary, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var item = await extractionEngine.ExtractAsync<T>(result.Html, request.Url, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                await WriteItemIfNotDuplicateAsync(item, summary, cancellationToken).ConfigureAwait(false);
            }
        }

        ReportProgress(request.Url, summary);

        return _linkExtractor is null ? Enumerable.Empty<Uri>() : _linkExtractor(request.Url, result.Html);
    }

    private async Task WriteItemIfNotDuplicateAsync(T item, HarvestRunSummary summary, CancellationToken cancellationToken)
    {
        if (_deduplicator is not null && _deduplicator.IsDuplicate(item))
        {
            return;
        }

        await WriteToSinksAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref summary.ItemsExtractedInternal);
    }

    private void ReportProgress(Uri lastUrl, HarvestRunSummary summary)
    {
        _progress?.Report(new HarvestProgress
        {
            LastUrl = lastUrl,
            PagesFetched = summary.PagesFetched,
            PagesFailed = summary.PagesFailed,
            ItemsExtracted = summary.ItemsExtracted
        });
    }

    private async Task WriteToSinksAsync(T item, CancellationToken cancellationToken)
    {
        foreach (var sink in _sinks)
        {
            await sink.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }
}
