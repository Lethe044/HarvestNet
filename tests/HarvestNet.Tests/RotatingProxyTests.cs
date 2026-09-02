using HarvestNet.Core.Http;
using Xunit;

namespace HarvestNet.Tests;

public class RotatingProxyTests
{
    [Fact]
    public void GetProxy_CyclesThroughPoolInOrder()
    {
        var proxy = new RotatingProxy(new[] { "http://proxy-a:8080", "http://proxy-b:8080" });

        var first = proxy.GetProxy(new Uri("https://example.com"));
        var second = proxy.GetProxy(new Uri("https://example.com"));
        var third = proxy.GetProxy(new Uri("https://example.com"));

        Assert.Equal("proxy-a", first.Host);
        Assert.Equal("proxy-b", second.Host);
        Assert.Equal("proxy-a", third.Host);
    }

    [Fact]
    public void Constructor_EmptyPool_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RotatingProxy(Array.Empty<string>()));
    }

    [Fact]
    public void IsBypassed_AlwaysFalse()
    {
        var proxy = new RotatingProxy(new[] { "http://proxy-a:8080" });
        Assert.False(proxy.IsBypassed(new Uri("https://example.com")));
    }
}
