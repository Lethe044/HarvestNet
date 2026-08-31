using System.Net.Http.Json;
using System.Text.Json;

namespace HarvestNet.Core.Healing;

/// <summary>
/// Healing provider backed by Google's Gemini free-tier API.
/// </summary>
public sealed class GeminiHealingProvider : IHealingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string Name => "Gemini";

    public GeminiHealingProvider(string apiKey, string model = "gemini-2.0-flash", HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string?> SuggestSelectorAsync(HealingRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = SelectorHealingPrompt.SystemPrompt + "\n\n" + SelectorHealingPrompt.Build(request);

        var payload = new
        {
            contents = new object[]
            {
                new { parts = new object[] { new { text = prompt } } }
            },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 200 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = json
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return SelectorHealingPrompt.ExtractSelector(text);
    }
}
