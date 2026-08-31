namespace HarvestNet.Core.Healing;

/// <summary>
/// A source of selector repair suggestions, backed by an LLM. HarvestNet ships providers
/// for Groq, Gemini, a local Ollama server and any OpenAI-compatible endpoint, all of which
/// can run on a free tier or entirely locally.
/// </summary>
public interface IHealingProvider
{
    /// <summary>A short, human readable name used in log output.</summary>
    string Name { get; }

    /// <summary>
    /// Asks the underlying model for a new CSS selector. Returns null when the model could
    /// not propose one, or when the request failed.
    /// </summary>
    Task<string?> SuggestSelectorAsync(HealingRequest request, CancellationToken cancellationToken = default);
}
