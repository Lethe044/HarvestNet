using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using HarvestNet.Core.Crawling;

namespace HarvestNet.Core.Http;

/// <summary>
/// A wrapper around <see cref="HttpClient"/> that applies the politeness rules configured
/// on <see cref="CrawlOptions"/>: robots.txt checks, per-host rate limiting, proxy and
/// User-Agent rotation, and retries with exponential backoff (or the server's own
/// Retry-After header) on transient failures. GET and POST (form submission) requests are
/// both supported, and <see cref="LoginAsync"/> can establish a session before crawling.
///
/// When <see cref="CrawlOptions.CacheDirectory"/> is set, a successful fetch is cached to
/// disk and reused on later calls for the same URL instead of making a new request.
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
    private readonly HostConcurrencyLimiter? _hostConcurrencyLimiter;
    private int _userAgentIndex = -1;

    public PoliteHttpClient(CrawlOptions options, IPageRenderer? renderer = null)
    {
        _options = options;
        _renderer = renderer;
        _hostConcurrencyLimiter = options.MaxConcurrencyPerHost is > 0
            ? new HostConcurrencyLimiter(options.MaxConcurrencyPerHost.Value)
            : null;

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

        if (!string.IsNullOrEmpty(options.CacheDirectory))
        {
            Directory.CreateDirectory(options.CacheDirectory);
        }
    }

    public async Task<CrawlResult> FetchAsync(CrawlRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var cachedHtml = request.Method == HttpMethod.Get ? TryReadCache(request.Url) : null;
        if (cachedHtml is not null)
        {
            return new CrawlResult
            {
                Url = request.Url,
                StatusCode = 200,
                Html = cachedHtml,
                Success = true,
                Depth = request.Depth,
                Elapsed = stopwatch.Elapsed
            };
        }

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

        var hostGate = _hostConcurrencyLimiter?.GetGate(request.Url.Host);
        if (hostGate is not null)
        {
            await hostGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var result = _renderer is not null
                ? await FetchWithRendererAsync(request, stopwatch, cancellationToken).ConfigureAwait(false)
                : await FetchWithHttpAsync(request, stopwatch, cancellationToken).ConfigureAwait(false);

            if (result.Success && result.Html is not null && request.Method == HttpMethod.Get)
            {
                WriteCache(request.Url, result.Html);
            }

            return result;
        }
        finally
        {
            hostGate?.Release();
        }
    }

    private async Task<CrawlResult> FetchWithHttpAsync(CrawlRequest request, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(request.Method, request.Url);
                if (request.FormData is not null && request.FormData.Count > 0)
                {
                    httpRequest.Content = new FormUrlEncodedContent(request.FormData);
                }

                if (_options.UserAgentPool.Count > 0)
                {
                    httpRequest.Headers.UserAgent.Clear();
                    httpRequest.Headers.UserAgent.ParseAdd(GetRotatingUserAgent());
                }

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Sends a POST with <paramref name="formData"/> to <paramref name="loginUrl"/>, before
    /// any crawling starts. Any cookies set by the response are kept by the underlying
    /// HttpClient and sent automatically with every later request in this crawl, which is
    /// what makes sites that require a login reachable at all.
    /// </summary>
    public async Task<bool> LoginAsync(Uri loginUrl, IReadOnlyDictionary<string, string> formData, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new FormUrlEncodedContent(formData);
            using var response = await _httpClient.PostAsync(loginUrl, content, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode || ((int)response.StatusCode is >= 300 and < 400);
        }
        catch
        {
            return false;
        }
    }

    private string GetRotatingUserAgent()
    {
        var next = Interlocked.Increment(ref _userAgentIndex);
        var index = ((next % _options.UserAgentPool.Count) + _options.UserAgentPool.Count) % _options.UserAgentPool.Count;
        return _options.UserAgentPool[index];
    }

    private string? TryReadCache(Uri url)
    {
        if (string.IsNullOrEmpty(_options.CacheDirectory))
        {
            return null;
        }

        var path = GetCachePath(url);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private void WriteCache(Uri url, string html)
    {
        if (string.IsNullOrEmpty(_options.CacheDirectory))
        {
            return;
        }

        try
        {
            File.WriteAllText(GetCachePath(url), html);
        }
        catch
        {
            // Caching is best effort and should never break a crawl.
        }
    }

    private string GetCachePath(Uri url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri)));
        return Path.Combine(_options.CacheDirectory!, hash + ".html");
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
