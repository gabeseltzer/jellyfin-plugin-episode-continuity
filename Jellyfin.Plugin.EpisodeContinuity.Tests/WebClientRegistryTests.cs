using System;
using Jellyfin.Plugin.EpisodeContinuity.Playback;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class WebClientRegistryTests
{
    [Fact]
    public void HeartbeatMakesDeviceActiveUntilTtlExpires()
    {
        var now = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);
        var registry = new WebClientRegistry(() => now, TimeSpan.FromMinutes(5));

        Assert.False(registry.IsActive("dev"));
        registry.Heartbeat("dev");
        Assert.True(registry.IsActive("DEV"));

        now = now.AddMinutes(6);
        Assert.False(registry.IsActive("dev"));
    }

    [Fact]
    public void IgnoresBlankDeviceIds()
    {
        var registry = new WebClientRegistry();

        registry.Heartbeat(null);
        registry.Heartbeat(string.Empty);

        Assert.False(registry.IsActive(null));
        Assert.False(registry.IsActive(string.Empty));
    }
}
