using HarvestNet.Core.Extraction;

namespace QuickStart;

public sealed class Quote
{
    [HarvestField(".text")]
    public string? Text { get; set; }

    [HarvestField(".author")]
    public string? Author { get; set; }
}
