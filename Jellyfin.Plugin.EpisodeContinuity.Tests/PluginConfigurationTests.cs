using Jellyfin.Plugin.EpisodeContinuity.Configuration;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class PluginConfigurationTests
{
    [Fact]
    public void Defaults_AreEnabled()
    {
        var config = new PluginConfiguration();
        Assert.True(config.Enabled);
    }
}
