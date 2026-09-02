namespace HarvestNet.Core.Healing;

/// <summary>
/// Tries multiple healing providers in order, moving on to the next one if a provider
/// throws or cannot propose a selector. Useful for pairing a fast free-tier API with a
/// local Ollama model as a fallback when the API is rate limited or unreachable.
/// </summary>
public sealed class HealingProviderChain : IHealingProvider
{
    private readonly IReadOnlyList<IHealingProvider> _providers;

    public string Name => "Chain(" + string.Join(" -> ", _providers.Select(p => p.Name)) + ")";

    public HealingProviderChain(params IHealingProvider[] providers)
    {
        if (providers.Length == 0)
        {
            throw new ArgumentException("At least one provider is required.", nameof(providers));
        }

        _providers = providers;
    }

    public async Task<string?> SuggestSelectorAsync(HealingRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.SuggestSelectorAsync(request, cancellationToken).ConfigureAwait(false);
                if (result is not null)
                {
                    return result;
                }
            }
            catch
            {
                // Move on to the next provider in the chain.
            }
        }

        return null;
    }
}
