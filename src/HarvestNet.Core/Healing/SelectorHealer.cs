namespace HarvestNet.Core.Healing;

/// <summary>
/// Default <see cref="ISelectorHealer"/> implementation: checks the cache, then asks the
/// configured <see cref="IHealingProvider"/>, and always fails soft (returns null) rather
/// than throwing, so a healing outage never brings a crawl down.
/// </summary>
public sealed class SelectorHealer : ISelectorHealer
{
    private readonly IHealingProvider _provider;
    private readonly HealingCache _cache;
    private readonly HealingOptions _options;

    public SelectorHealer(IHealingProvider provider, HealingCache cache, HealingOptions? options = null)
    {
        _provider = provider;
        _cache = cache;
        _options = options ?? new HealingOptions();
    }

    public async Task<string?> TryHealFieldSelectorAsync(
        Uri pageUrl,
        string scopeHtml,
        string oldSelector,
        string fieldName,
        string? fieldDescription,
        CancellationToken cancellationToken = default)
    {
        var cached = _cache.TryGet(pageUrl.Host, fieldName);
        if (cached is not null)
        {
            return cached;
        }

        if (!_options.Enabled)
        {
            return null;
        }

        var request = new HealingRequest
        {
            TruncatedHtml = scopeHtml,
            FieldName = fieldName,
            FieldDescription = fieldDescription,
            OldSelector = oldSelector,
            PageUrl = pageUrl
        };

        try
        {
            return await _provider.SuggestSelectorAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> TryHealContainerSelectorAsync(
        Uri pageUrl,
        string pageHtml,
        string oldSelector,
        string itemTypeName,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = "__container__" + itemTypeName;
        var cached = _cache.TryGet(pageUrl.Host, cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        if (!_options.Enabled)
        {
            return null;
        }

        var request = new HealingRequest
        {
            TruncatedHtml = pageHtml,
            FieldName = $"list container for {itemTypeName}",
            FieldDescription = "A CSS selector that matches every repeated item card or row on the page.",
            OldSelector = oldSelector,
            PageUrl = pageUrl
        };

        try
        {
            var selector = await _provider.SuggestSelectorAsync(request, cancellationToken).ConfigureAwait(false);
            if (selector is not null)
            {
                _cache.Set(pageUrl.Host, cacheKey, selector);
            }

            return selector;
        }
        catch
        {
            return null;
        }
    }

    public void RememberHealedSelector(string host, string typeName, string fieldName, string newSelector)
    {
        _cache.Set(host, fieldName, newSelector);
    }
}
