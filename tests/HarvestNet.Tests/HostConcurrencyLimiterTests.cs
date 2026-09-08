using HarvestNet.Core.Http;
using Xunit;

namespace HarvestNet.Tests;

public class HostConcurrencyLimiterTests
{
    [Fact]
    public void GetGate_SameHost_ReturnsSameSemaphore()
    {
        var limiter = new HostConcurrencyLimiter(2);

        var first = limiter.GetGate("example.com");
        var second = limiter.GetGate("example.com");

        Assert.Same(first, second);
    }

    [Fact]
    public void GetGate_DifferentHosts_ReturnsDifferentSemaphores()
    {
        var limiter = new HostConcurrencyLimiter(2);

        var a = limiter.GetGate("a.example.com");
        var b = limiter.GetGate("b.example.com");

        Assert.NotSame(a, b);
    }

    [Fact]
    public async Task GetGate_RespectsConfiguredLimit()
    {
        var limiter = new HostConcurrencyLimiter(1);
        var gate = limiter.GetGate("example.com");

        await gate.WaitAsync();
        Assert.Equal(0, gate.CurrentCount);

        gate.Release();
        Assert.Equal(1, gate.CurrentCount);
    }
}
