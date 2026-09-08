using System.Collections.Concurrent;
using HarvestNet.Core;
using Xunit;

namespace HarvestNet.Tests;

public class FieldCoverageTrackerTests
{
    private sealed class Quote
    {
        public string? Text { get; set; }
        public string? Author { get; set; }
    }

    [Fact]
    public void Record_DictionaryItem_CountsNonEmptyValues()
    {
        var counts = new ConcurrentDictionary<string, int>();

        FieldCoverageTracker.Record(new Dictionary<string, string?> { ["text"] = "hi", ["author"] = null }, counts);
        FieldCoverageTracker.Record(new Dictionary<string, string?> { ["text"] = "hi again", ["author"] = "Anon" }, counts);

        Assert.Equal(2, counts["text"]);
        Assert.Equal(1, counts["author"]);
    }

    [Fact]
    public void Record_PocoItem_CountsNonNullProperties()
    {
        var counts = new ConcurrentDictionary<string, int>();

        FieldCoverageTracker.Record(new Quote { Text = "hi", Author = null }, counts);
        FieldCoverageTracker.Record(new Quote { Text = "hi again", Author = "Anon" }, counts);

        Assert.Equal(2, counts[nameof(Quote.Text)]);
        Assert.Equal(1, counts[nameof(Quote.Author)]);
    }

    [Fact]
    public void Record_EmptyStringValue_IsNotCountedAsPresent()
    {
        var counts = new ConcurrentDictionary<string, int>();

        FieldCoverageTracker.Record(new Dictionary<string, string?> { ["text"] = string.Empty }, counts);

        Assert.False(counts.ContainsKey("text"));
    }
}
