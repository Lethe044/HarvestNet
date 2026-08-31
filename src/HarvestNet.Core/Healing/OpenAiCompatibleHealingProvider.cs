using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HarvestNet.Core.Healing;

/// <summary>
/// Healing provider for any endpoint that speaks the OpenAI chat completions format. Covers
/// OpenRouter's free models, LM Studio, vLLM, and similar self-hosted or free-tier servers
/// without needing a dedicated provider class for each one.
/// </summary>
public sealed class OpenAiCompatibleHealingProvider : IHealingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;

    public string Name => "OpenAI-compatible endpoint";

    public OpenAiCompatibleHealingProvider(string baseUrl, string apiKey, string model, HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string?> SuggestSelectorAsync(HealingRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = SelectorHealingPrompt.SystemPrompt },
                new { role = "user", content = SelectorHealingPrompt.Build(request) }
            },
            temperature = 0.1,
            max_tokens = 200
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        if (!string.IsNullOrEmpty(_apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return SelectorHealingPrompt.ExtractSelector(content);
    }
}
