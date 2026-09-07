using System;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

internal static class EpisodeFactory
{
    public static Episode Real(int season, int number, string? name = null, int? end = null)
    {
        return new Episode
        {
            Id = Guid.NewGuid(),
            ParentIndexNumber = season,
            IndexNumber = number,
            IndexNumberEnd = end,
            Name = name ?? $"Real {season}x{number}",
            Path = $"/media/show/S{season:00}E{number:00}.mkv",
            IsVirtualItem = false
        };
    }

    public static Episode Virtual(int season, int number, string? name = null, DateTime? premiere = null)
    {
        return new Episode
        {
            Id = Guid.NewGuid(),
            ParentIndexNumber = season,
            IndexNumber = number,
            Name = name ?? $"Missing {season}x{number}",
            Path = null!,
            IsVirtualItem = true,
            PremiereDate = premiere
        };
    }
}
