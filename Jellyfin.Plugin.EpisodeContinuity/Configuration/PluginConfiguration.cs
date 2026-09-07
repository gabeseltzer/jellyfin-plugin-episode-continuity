using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.EpisodeContinuity.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin is active.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
