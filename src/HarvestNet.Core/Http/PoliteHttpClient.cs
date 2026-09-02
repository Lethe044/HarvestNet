using System.Diagnostics;
using System.Net;
using HarvestNet.Core.Crawling;

namespace HarvestNet.Core.Http;

/// <summary>
/// A wrapper around <see cref="HttpClient"/> that applies the politeness rules configured
/// on <see cref="CrawlOptions"/>: robots.txt checks, per-host rate limiting, proxy
/// rotation, and retries with exponential backoff (or the server's own Retry-After header)
/// on transient failures.
///
/// When an <see cref="IPageRenderer"/> is supplied, pages are rendered through it (a real
/// browser) instead of a plain GET request, for sites that need JavaScript to produce
/// their final HTML.
/// </summary>
public sealed class PoliteHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CrawlOptions _options;
    private readonly DomainRateLimiter _rateLimiter;
    private readonly RobotsTxtService _robotsTxtService;
    private readonly IPageRenderer? _renderer;

    public PoliteHttpClient(CrawlOptions options, IPageRenderer? renderer = null)
    {
        _options = options;
        _renderer = renderer;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = options.FollowRedirects,
            AutomaticDecompression = DecompressionMethods.All
        };

        if (options.ProxyPool.Count > 0)
        {
            ICredentials? credentials = !string.IsNullOrEmpty(options.ProxyUsername)
                ? new NetworkCredential(options.ProxyUsername, options.ProxyPassword ?? string.Empty)
                : null;

            handler.Proxy = new RotatingProxy(options.ProxyPool, credentials);
            handler.UseProxy = true;
        }

        _httpClient = new HttpClient(handler)
        {
            Timeout = options.RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

        _rateLimiter = new DomainRateLimiter(options.DelayBetweenRequests);
        _robotsTxtService = new RobotsTxtService(_httpClient);
    }

    public async Task<CrawlResult> FetchAsync(CrawlRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (_options.RespectRobotsTxt)
        {
            var allowed = await _robotsTxtService.IsAllowedAsync(request.Url, _options.UserAgent, cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return new CrawlResult
                {
                    Url = request.Url,
                    StatusCode = 0,
                    Success = false,
                    Error = "Disallowed by robots.txt",
                    Depth = request.Depth,
                    Elapsed = stopwatch.Elapsed
                };
            }
        }

        await _rateLimiter.WaitAsync(request.Url.Host, cancellationToken).ConfigureAwait(false);

        return _renderer is not null
            ? await FetchWithRendererAsync(request, stopwatch, cancellationToken).ConfigureAwait(false)
            : await FetchWithHttpAsync(request, stopwatch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CrawlResult> FetchWithHttpAsync(CrawlRequest request, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(request.Url, cancellationToken).ConfigureAwait(false);
                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode && attempt < _options.MaxRetries && IsRetryable(response.StatusCode))
                {
                    var retryDelay = GetRetryDelay(response, attempt);
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return new CrawlResult
                {
                    Url = request.Url,
                    StatusCode = (int)response.StatusCode,
                    Html = html,
                    Success = response.IsSuccessStatusCode,
                    Error = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}",
                    Depth = request.Depth,
                    Elapsed = stopwatch.Elapsed
                };
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                lastException = ex;
                await DelayForRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        return new CrawlResult
        {
            Url = request.Url,
            StatusCode = 0,
            Success = false,
            Error = lastException?.Message ?? "Unknown error",
            Depth = request.Depth,
            Elapsed = stopwatch.Elapsed
        };
    }

    private async Task<CrawlResult> FetchWithRendererAsync(CrawlRequest request, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                var html = await _renderer!.RenderAsync(request.Url, cancellationToken).ConfigureAwait(false);
                return new CrawlResult
                {
                    Url = request.Url,
                    StatusCode = 200,
                    Html = html,
                    Success = true,
                    Depth = request.Depth,
                    Elapsed = stopwatch.Elapsed
                };
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                lastException = ex;
                await DelayForRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        return new CrawlResult
        {
            Url = request.Url,
            StatusCode = 0,
            Success = false,
            Error = lastException?.Message ?? "Render failed",
            Depth = request.Depth,
            Elapsed = stopwatch.Elapsed
        };
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 429 || code >= 500;
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter is not null)
        {
            if (response.Headers.RetryAfter.Delta is { } delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (response.Headers.RetryAfter.Date is { } date)
            {
                var wait = date - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    return wait;
                }
            }
        }

        return ExponentialBackoff(attempt);
    }

    private TimeSpan ExponentialBackoff(int attempt) =>
        TimeSpan.FromMilliseconds(_options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt));

    private Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(ExponentialBackoff(attempt), cancellationToken);

    public void Dispose() => _httpClient.Dispose();
}
