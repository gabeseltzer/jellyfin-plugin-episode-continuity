using Jellyfin.Plugin.EpisodeContinuity.Continuity;
using Jellyfin.Plugin.EpisodeContinuity.Playback;
using Jellyfin.Plugin.EpisodeContinuity.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.EpisodeContinuity;

/// <summary>
/// Registers the plugin's services with the host's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IEpisodeSource, LibraryEpisodeSource>();
        serviceCollection.AddSingleton<ContinuityService>();
        serviceCollection.AddSingleton<WebClientRegistry>();
        serviceCollection.AddSingleton<InjectionState>();
        serviceCollection.AddSingleton<FileTransformationBridge>();
        serviceCollection.AddSingleton<IStartupFilter, ScriptInjectionStartupFilter>();
        serviceCollection.AddHostedService<PlaybackWarningService>();
    }
}
