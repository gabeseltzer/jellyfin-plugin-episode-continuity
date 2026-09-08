using System;
using Jellyfin.Plugin.EpisodeContinuity.Configuration;
using Jellyfin.Plugin.EpisodeContinuity.Playback;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class PlaybackWarningServiceTests
{
    [Fact]
    public void WebSession_SuppressesPush_OnlyWhileInterstitialEnabled()
    {
        var registry = new WebClientRegistry(() => DateTime.UtcNow, TimeSpan.FromMinutes(30));
        registry.Heartbeat("browser");
        var config = new PluginConfiguration { ServerPushMode = ServerPushMode.Toast };

        Assert.False(PlaybackWarningService.ShouldConsider(config, registry, "browser", true));

        config.InterstitialEnabled = false;
        Assert.True(PlaybackWarningService.ShouldConsider(config, registry, "browser", true));

        config.InterstitialEnabled = true;
        config.WebFeaturesEnabled = false;
        Assert.True(PlaybackWarningService.ShouldConsider(config, registry, "browser", true));
    }

    [Fact]
    public void OtherDevices_AreConsidered_UnlessOffOrNotEpisode()
    {
        var registry = new WebClientRegistry();
        var config = new PluginConfiguration { ServerPushMode = ServerPushMode.PauseAndModal };

        Assert.True(PlaybackWarningService.ShouldConsider(config, registry, "tv", true));
        Assert.False(PlaybackWarningService.ShouldConsider(config, registry, "tv", false));

        config.ServerPushMode = ServerPushMode.Off;
        Assert.False(PlaybackWarningService.ShouldConsider(config, registry, "tv", true));

        config.ServerPushMode = ServerPushMode.Toast;
        config.Enabled = false;
        Assert.False(PlaybackWarningService.ShouldConsider(config, registry, "tv", true));
    }
}
