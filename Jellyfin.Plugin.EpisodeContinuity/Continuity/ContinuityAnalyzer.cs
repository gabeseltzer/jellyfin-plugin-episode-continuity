using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// Pure gap detection over a series' episode list. No Jellyfin services involved.
/// </summary>
public static class ContinuityAnalyzer
{
    /// <summary>
    /// Analyses the continuity around one episode.
    /// </summary>
    /// <param name="seriesEpisodes">Every episode item of the series, virtual ones included.</param>
    /// <param name="itemId">The episode to analyse.</param>
    /// <param name="treatNumberingGapsAsMissing">Also treat non-contiguous numbers within a season as missing.</param>
    /// <param name="now">The reference time for deciding whether a virtual episode has aired.</param>
    /// <returns>The continuity result, or <see cref="ContinuityResult.Empty"/> when the item is not in the list.</returns>
    public static ContinuityResult Analyze(
        IReadOnlyList<Episode> seriesEpisodes,
        Guid itemId,
        bool treatNumberingGapsAsMissing,
        DateTime? now = null)
    {
        var today = (now ?? DateTime.UtcNow).Date;
        var timeline = BuildTimeline(seriesEpisodes, treatNumberingGapsAsMissing, today);

        var index = timeline.FindIndex(e => e.Id == itemId);
        if (index < 0)
        {
            return ContinuityResult.Empty;
        }

        var missingBefore = new List<EpisodeRef>();
        EpisodeRef? previousAvailable = null;
        for (var i = index - 1; i >= 0; i--)
        {
            if (timeline[i].IsAvailable)
            {
                previousAvailable = timeline[i];
                break;
            }

            missingBefore.Insert(0, timeline[i]);
        }

        var missingAfter = new List<EpisodeRef>();
        EpisodeRef? nextAvailable = null;
        for (var i = index + 1; i < timeline.Count; i++)
        {
            if (timeline[i].IsAvailable)
            {
                nextAvailable = timeline[i];
                break;
            }

            missingAfter.Add(timeline[i]);
        }

        return new ContinuityResult
        {
            Item = timeline[index],
            MissingBefore = missingBefore,
            MissingAfter = missingAfter,
            PreviousAvailable = previousAvailable,
            NextAvailable = nextAvailable
        };
    }

    /// <summary>
    /// Builds the ordered timeline of episode positions for a series.
    /// </summary>
    /// <param name="seriesEpisodes">Every episode item of the series.</param>
    /// <param name="treatNumberingGapsAsMissing">Also synthesise missing entries for numbering gaps.</param>
    /// <param name="today">The date used to drop unaired virtual episodes.</param>
    /// <returns>Positions in aired order; one entry per episode number, available entries winning ties.</returns>
    internal static List<EpisodeRef> BuildTimeline(IReadOnlyList<Episode> seriesEpisodes, bool treatNumberingGapsAsMissing, DateTime today)
    {
        var refs = new List<EpisodeRef>();
        foreach (var episode in seriesEpisodes)
        {
            var season = episode.ParentIndexNumber;
            var number = episode.IndexNumber;
            if (season is null or <= 0 || number is null)
            {
                continue;
            }

            var available = IsAvailable(episode);
            if (!available && episode.PremiereDate.HasValue && episode.PremiereDate.Value.Date > today)
            {
                // Not out yet; not "missing".
                continue;
            }

            // A multi-episode file covers IndexNumber..IndexNumberEnd; register each position it covers.
            var end = Math.Max(number.Value, episode.IndexNumberEnd ?? number.Value);
            for (var n = number.Value; n <= end; n++)
            {
                refs.Add(new EpisodeRef(episode.Id, season.Value, n, episode.Name, available));
            }
        }

        // One entry per (season, number): prefer available, then the first seen.
        var byPosition = refs
            .GroupBy(r => (r.SeasonNumber, r.EpisodeNumber))
            .Select(g => g.OrderByDescending(r => r.IsAvailable).First())
            .OrderBy(r => r.SeasonNumber)
            .ThenBy(r => r.EpisodeNumber)
            .ToList();

        if (!treatNumberingGapsAsMissing)
        {
            return byPosition;
        }

        var withGaps = new List<EpisodeRef>(byPosition.Count);
        for (var i = 0; i < byPosition.Count; i++)
        {
            var current = byPosition[i];
            if (i > 0)
            {
                var previous = byPosition[i - 1];
                if (previous.SeasonNumber == current.SeasonNumber)
                {
                    for (var n = previous.EpisodeNumber + 1; n < current.EpisodeNumber; n++)
                    {
                        withGaps.Add(new EpisodeRef(null, current.SeasonNumber, n, null, false));
                    }
                }
            }

            withGaps.Add(current);
        }

        return withGaps;
    }

    private static bool IsAvailable(Episode episode)
    {
        // Avoids BaseItem.LocationType, which needs the static file system service.
        return !episode.IsVirtualItem && !string.IsNullOrEmpty(episode.Path);
    }
}
