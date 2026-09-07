using System;
using Jellyfin.Plugin.EpisodeContinuity.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// Resolves library items to continuity results.
/// </summary>
public sealed class ContinuityService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IEpisodeSource _episodeSource;
    private readonly Func<PluginConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuityService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="episodeSource">The episode source.</param>
    public ContinuityService(ILibraryManager libraryManager, IEpisodeSource episodeSource)
        : this(libraryManager, episodeSource, () => Plugin.CurrentConfiguration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuityService"/> class with an explicit configuration source.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="episodeSource">The episode source.</param>
    /// <param name="configuration">Supplies the current configuration.</param>
    public ContinuityService(ILibraryManager libraryManager, IEpisodeSource episodeSource, Func<PluginConfiguration> configuration)
    {
        _libraryManager = libraryManager;
        _episodeSource = episodeSource;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the continuity result for a library item id.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The result; <see cref="ContinuityResult.Empty"/> when the item is not an episode.</returns>
    public ContinuityResult GetForItem(Guid itemId)
    {
        return GetForItem(_libraryManager.GetItemById(itemId));
    }

    /// <summary>
    /// Gets the continuity result for a library item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The result; <see cref="ContinuityResult.Empty"/> when the item is not an episode.</returns>
    public ContinuityResult GetForItem(BaseItem? item)
    {
        if (item is not Episode episode)
        {
            return ContinuityResult.Empty;
        }

        var seriesId = episode.SeriesId;
        if (seriesId.Equals(Guid.Empty))
        {
            seriesId = episode.FindSeriesId();
        }

        if (seriesId.Equals(Guid.Empty))
        {
            return ContinuityResult.Empty;
        }

        var episodes = _episodeSource.GetSeriesEpisodes(seriesId);
        return ContinuityAnalyzer.Analyze(episodes, episode.Id, _configuration().TreatNumberingGapsAsMissing);
    }
}
