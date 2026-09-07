using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodeContinuity.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.EpisodeContinuity;

/// <summary>
/// The Episode Continuity plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin's stable identifier.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("3f8a1c6e-9d2b-4a7f-b5e4-c1d0e8f7a2b9");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Episode Continuity";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the current configuration, or defaults when the plugin has not been initialised.
    /// </summary>
    public static PluginConfiguration CurrentConfiguration => Instance?.Configuration ?? new PluginConfiguration();

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        ];
    }
}
