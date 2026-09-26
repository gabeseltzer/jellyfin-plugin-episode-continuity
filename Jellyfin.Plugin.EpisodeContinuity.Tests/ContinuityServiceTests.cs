using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodeContinuity.Configuration;
using Jellyfin.Plugin.EpisodeContinuity.Continuity;
using MediaBrowser.Controller.Entities.TV;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class ContinuityServiceTests
{
    private static readonly Guid SeriesId = Guid.NewGuid();

    [Fact]
    public void GetForItem_AlternateVersion_ReportsTheGap()
    {
        // Jellyfin 12 keeps alternate versions out of the parent item, and the series query returns
        // only the primary; playback can still be reported against the alternate's id.
        var e1 = InSeries(EpisodeFactory.Real(1, 1));
        var v2 = InSeries(EpisodeFactory.Virtual(1, 2));
        var primary = InSeries(EpisodeFactory.Real(1, 3));
        var alternate = InSeries(EpisodeFactory.Real(1, 3));
        var service = Service([e1, v2, primary]);

        var result = service.GetForItem(alternate);

        Assert.True(result.HasGapBefore);
        Assert.Equal("S01E02", Assert.Single(result.MissingBefore).Code);
    }

    [Fact]
    public void GetForItem_NonEpisode_IsEmpty()
    {
        var service = Service([]);

        Assert.Same(ContinuityResult.Empty, service.GetForItem(new Series()));
        Assert.Same(ContinuityResult.Empty, service.GetForItem((MediaBrowser.Controller.Entities.BaseItem?)null));
    }

    private static Episode InSeries(Episode episode)
    {
        episode.SeriesId = SeriesId;
        return episode;
    }

    private static ContinuityService Service(IReadOnlyList<Episode> episodes)
    {
        // The library manager is only used to resolve ids, which these tests bypass.
        return new ContinuityService(null!, new FixedEpisodeSource(episodes), () => new PluginConfiguration());
    }

    private sealed class FixedEpisodeSource(IReadOnlyList<Episode> episodes) : IEpisodeSource
    {
        public IReadOnlyList<Episode> GetSeriesEpisodes(Guid seriesId)
        {
            return seriesId == SeriesId ? episodes : [];
        }
    }
}
