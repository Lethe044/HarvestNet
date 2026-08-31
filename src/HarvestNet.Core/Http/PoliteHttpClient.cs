using System.Net;
using HarvestNet.Core.Crawling;

namespace HarvestNet.Core.Http;

/// <summary>
/// A wrapper around <see cref="HttpClient"/> that applies the politeness rules configured
/// on <see cref="CrawlOptions"/>: robots.txt checks, per-host rate limiting and retries
/// with exponential backoff on transient failures.
/// </summary>
public sealed class PoliteHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CrawlOptions _options;
    private readonly DomainRateLimiter _rateLimiter;
    private readonly RobotsTxtService _robotsTxtService;

    public PoliteHttpClient(CrawlOptions options)
    {
        _options = options;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = options.FollowRedirects,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = options.RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

        _rateLimiter = new DomainRateLimiter(options.DelayBetweenRequests);
        _robotsTxtService = new RobotsTxtService(_httpClient);
    }

    public async Task<CrawlResult> FetchAsync(CrawlRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(request.Url, cancellationToken).ConfigureAwait(false);
                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode && attempt < _options.MaxRetries && IsRetryable(response.StatusCode))
                {
                    await DelayForRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
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

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 429 || code >= 500;
    }

    private Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        return Task.Delay(delay, cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();
}
