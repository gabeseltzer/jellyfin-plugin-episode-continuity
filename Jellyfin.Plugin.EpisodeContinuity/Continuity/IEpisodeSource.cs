using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// Supplies every episode item of a series, including virtual (missing) ones.
/// </summary>
public interface IEpisodeSource
{
    /// <summary>
    /// Gets all episodes of a series.
    /// </summary>
    /// <param name="seriesId">The series id.</param>
    /// <returns>Episodes in any order; virtual items included.</returns>
    IReadOnlyList<Episode> GetSeriesEpisodes(Guid seriesId);
}
