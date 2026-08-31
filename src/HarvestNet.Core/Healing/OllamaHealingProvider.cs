using System.Net.Http.Json;
using System.Text.Json;

namespace HarvestNet.Core.Healing;

/// <summary>
/// Healing provider backed by a local Ollama server. This is the fully offline, zero-cost,
/// zero-signup option: point it at any locally pulled model.
/// </summary>
public sealed class OllamaHealingProvider : IHealingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public string Name => "Ollama (local)";

    public OllamaHealingProvider(string baseUrl = "http://localhost:11434", string model = "llama3.1", HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string?> SuggestSelectorAsync(HealingRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = SelectorHealingPrompt.SystemPrompt + "\n\n" + SelectorHealingPrompt.Build(request);

        var payload = new
        {
            model = _model,
            prompt,
            stream = false,
            options = new { temperature = 0.1 }
        };

        using var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/generate", payload, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = json.TryGetProperty("response", out var responseElement) ? responseElement.GetString() : null;
        return SelectorHealingPrompt.ExtractSelector(text);
    }
}
