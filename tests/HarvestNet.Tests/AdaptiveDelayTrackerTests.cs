using HarvestNet.Core.Crawling;
using Xunit;

namespace HarvestNet.Tests;

public class AdaptiveDelayTrackerTests
{
    [Fact]
    public void GetDelay_NoReports_ReturnsBaseDelay()
    {
        var tracker = new AdaptiveDelayTracker(TimeSpan.FromMilliseconds(500));

        Assert.Equal(TimeSpan.FromMilliseconds(500), tracker.GetDelay("example.com"));
    }

    [Fact]
    public void ReportFailure_IncreasesDelay()
    {
        var tracker = new AdaptiveDelayTracker(TimeSpan.FromMilliseconds(500));

        tracker.ReportFailure("example.com");

        Assert.True(tracker.GetDelay("example.com") > TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void ReportFailure_ThenSuccess_DelayComesBackDown()
    {
        var tracker = new AdaptiveDelayTracker(TimeSpan.FromMilliseconds(500));

        tracker.ReportFailure("example.com");
        tracker.ReportFailure("example.com");
        var afterFailures = tracker.GetDelay("example.com");

        for (var i = 0; i < 20; i++)
        {
            tracker.ReportSuccess("example.com");
        }

        var afterSuccesses = tracker.GetDelay("example.com");

        Assert.True(afterSuccesses < afterFailures);
        Assert.Equal(TimeSpan.FromMilliseconds(500), afterSuccesses);
    }

    [Fact]
    public void GetDelay_DifferentHosts_TrackedIndependently()
    {
        var tracker = new AdaptiveDelayTracker(TimeSpan.FromMilliseconds(500));

        tracker.ReportFailure("slow.example.com");

        Assert.True(tracker.GetDelay("slow.example.com") > tracker.GetDelay("fast.example.com"));
    }

    [Fact]
    public void GetDelay_NeverExceedsThirtySeconds()
    {
        var tracker = new AdaptiveDelayTracker(TimeSpan.FromSeconds(10));

        for (var i = 0; i < 10; i++)
        {
            tracker.ReportFailure("example.com");
        }

        Assert.True(tracker.GetDelay("example.com") <= TimeSpan.FromSeconds(30));
    }
}
