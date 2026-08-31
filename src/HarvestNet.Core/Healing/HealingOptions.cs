namespace HarvestNet.Core.Healing;

/// <summary>
/// Runtime knobs for the healing orchestrator.
/// </summary>
public sealed class HealingOptions
{
    /// <summary>Master switch. When false, the healer never calls the underlying provider.</summary>
    public bool Enabled { get; set; } = true;
}
