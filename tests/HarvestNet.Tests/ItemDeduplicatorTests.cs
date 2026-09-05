using HarvestNet.Core;
using Xunit;

namespace HarvestNet.Tests;

public class ItemDeduplicatorTests
{
    [Fact]
    public void IsDuplicate_SameContentTwice_SecondCallIsDuplicate()
    {
        var deduplicator = new ItemDeduplicator<string>(null);

        Assert.False(deduplicator.IsDuplicate("a"));
        Assert.True(deduplicator.IsDuplicate("a"));
    }

    [Fact]
    public void IsDuplicate_DifferentContent_NeitherIsDuplicate()
    {
        var deduplicator = new ItemDeduplicator<string>(null);

        Assert.False(deduplicator.IsDuplicate("a"));
        Assert.False(deduplicator.IsDuplicate("b"));
    }

    [Fact]
    public void IsDuplicate_WithKeySelector_UsesKeyInsteadOfFullContent()
    {
        var deduplicator = new ItemDeduplicator<(string Id, string Name)>(x => x.Id);

        Assert.False(deduplicator.IsDuplicate(("1", "Alice")));
        Assert.True(deduplicator.IsDuplicate(("1", "Alicia")));
    }
}
