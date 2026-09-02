using HarvestNet.Core.Healing;
using Xunit;

namespace HarvestNet.Tests;

public class HealingProviderChainTests
{
    private sealed class FakeProvider : IHealingProvider
    {
        private readonly string? _result;
        private readonly bool _throws;

        public string Name { get; }

        public FakeProvider(string name, string? result = null, bool throws = false)
        {
            Name = name;
            _result = result;
            _throws = throws;
        }

        public Task<string?> SuggestSelectorAsync(HealingRequest request, CancellationToken cancellationToken = default)
        {
            if (_throws)
            {
                throw new InvalidOperationException("Simulated provider failure.");
            }

            return Task.FromResult(_result);
        }
    }

    private static HealingRequest MakeRequest() => new()
    {
        TruncatedHtml = "<div></div>",
        FieldName = "price",
        OldSelector = ".price",
        PageUrl = new Uri("https://example.com")
    };

    [Fact]
    public async Task SuggestSelectorAsync_FirstProviderSucceeds_ReturnsItsResult()
    {
        var chain = new HealingProviderChain(
            new FakeProvider("first", result: ".new-price"),
            new FakeProvider("second", result: ".fallback-price"));

        var result = await chain.SuggestSelectorAsync(MakeRequest());

        Assert.Equal(".new-price", result);
    }

    [Fact]
    public async Task SuggestSelectorAsync_FirstProviderReturnsNull_FallsBackToSecond()
    {
        var chain = new HealingProviderChain(
            new FakeProvider("first", result: null),
            new FakeProvider("second", result: ".fallback-price"));

        var result = await chain.SuggestSelectorAsync(MakeRequest());

        Assert.Equal(".fallback-price", result);
    }

    [Fact]
    public async Task SuggestSelectorAsync_FirstProviderThrows_FallsBackToSecond()
    {
        var chain = new HealingProviderChain(
            new FakeProvider("first", throws: true),
            new FakeProvider("second", result: ".fallback-price"));

        var result = await chain.SuggestSelectorAsync(MakeRequest());

        Assert.Equal(".fallback-price", result);
    }

    [Fact]
    public async Task SuggestSelectorAsync_AllProvidersFail_ReturnsNull()
    {
        var chain = new HealingProviderChain(
            new FakeProvider("first", result: null),
            new FakeProvider("second", throws: true));

        var result = await chain.SuggestSelectorAsync(MakeRequest());

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_NoProviders_Throws()
    {
        Assert.Throws<ArgumentException>(() => new HealingProviderChain());
    }
}
