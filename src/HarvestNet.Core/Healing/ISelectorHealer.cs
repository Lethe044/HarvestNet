namespace HarvestNet.Core.Healing;

/// <summary>
/// Orchestrates selector repair: checks a persistent cache first, falls back to an
/// <see cref="IHealingProvider"/> when nothing is cached, and remembers successful fixes.
/// </summary>
public interface ISelectorHealer
{
    Task<string?> TryHealFieldSelectorAsync(
        Uri pageUrl,
        string scopeHtml,
        string oldSelector,
        string fieldName,
        string? fieldDescription,
        CancellationToken cancellationToken = default);

    Task<string?> TryHealContainerSelectorAsync(
        Uri pageUrl,
        string pageHtml,
        string oldSelector,
        string itemTypeName,
        CancellationToken cancellationToken = default);

    void RememberHealedSelector(string host, string typeName, string fieldName, string newSelector);
}
