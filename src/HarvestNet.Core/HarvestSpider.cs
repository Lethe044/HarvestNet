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
/// together crawling, extraction, optional self-healing and one or more output sinks.
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
    private Func<Uri, string, IEnumerable<Uri>>? _linkExtractor;
    private int _maxDepth = 1;
    private IReadOnlyDictionary<string, FieldSpec>? _explicitFields;
    private string _itemTypeName = typeof(T).Name;

    public HarvestSpider(CrawlOptions? options = null)
    {
        _crawlOptions = options ?? new CrawlOptions();
    }

    public HarvestSpider<T> AddSeedUrl(string url)
    {
        _seedUrls.Add(new Uri(url));
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

    public HarvestSpider<T> WithSink(IResultSink<T> sink)
    {
        _sinks.Add(sink);
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

    public async Task<HarvestRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var summary = new HarvestRunSummary();
        var stopwatch = Stopwatch.StartNew();

        using var httpClient = new PoliteHttpClient(_crawlOptions);
        var extractionEngine = new ExtractionEngine(_healer);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentLevel = _seedUrls.Select(url => new CrawlRequest { Url = url, Depth = 0 }).ToList();
        var depth = 0;

        while (currentLevel.Count > 0 && summary.PagesFetched < _crawlOptions.MaxPages)
        {
            var toProcess = currentLevel
                .Where(r => visited.Add(r.Url.AbsoluteUri))
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
        }

        foreach (var sink in _sinks)
        {
            await sink.DisposeAsync().ConfigureAwait(false);
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
                        await WriteToSinksAsync(typed, cancellationToken).ConfigureAwait(false);
                        Interlocked.Increment(ref summary.ItemsExtractedInternal);
                    }
                }
            }
            else
            {
                var items = await extractionEngine.ExtractListAsync<T>(result.Html, request.Url, _itemSelector, cancellationToken).ConfigureAwait(false);
                foreach (var item in items)
                {
                    await WriteToSinksAsync(item, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref summary.ItemsExtractedInternal);
                }
            }
        }
        else if (_explicitFields is not null)
        {
            var raw = await extractionEngine.ExtractAsync(result.Html, request.Url, _explicitFields, _itemTypeName, cancellationToken).ConfigureAwait(false);
            if (raw is T typed)
            {
                await WriteToSinksAsync(typed, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref summary.ItemsExtractedInternal);
            }
        }
        else
        {
            var item = await extractionEngine.ExtractAsync<T>(result.Html, request.Url, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                await WriteToSinksAsync(item, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref summary.ItemsExtractedInternal);
            }
        }

        return _linkExtractor is null ? Enumerable.Empty<Uri>() : _linkExtractor(request.Url, result.Html);
    }

    private async Task WriteToSinksAsync(T item, CancellationToken cancellationToken)
    {
        foreach (var sink in _sinks)
        {
            await sink.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }
}
