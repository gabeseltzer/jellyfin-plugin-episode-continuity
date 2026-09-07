using System;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// A lightweight reference to an episode position in a series.
/// </summary>
/// <param name="Id">The library item id, or null when the episode is only inferred from numbering.</param>
/// <param name="SeasonNumber">The season number.</param>
/// <param name="EpisodeNumber">The episode number.</param>
/// <param name="Name">The episode title, if known.</param>
/// <param name="IsAvailable">True when a playable file exists for the episode.</param>
public sealed record EpisodeRef(Guid? Id, int SeasonNumber, int EpisodeNumber, string? Name, bool IsAvailable)
{
    /// <summary>
    /// Gets the conventional SxxExx label.
    /// </summary>
    public string Code => $"S{SeasonNumber:00}E{EpisodeNumber:00}";
}
