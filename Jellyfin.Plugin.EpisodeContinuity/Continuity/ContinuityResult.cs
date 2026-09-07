using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// The continuity picture around one episode.
/// </summary>
public sealed class ContinuityResult
{
    /// <summary>
    /// A result for items that are not episodes or have no series context.
    /// </summary>
    public static readonly ContinuityResult Empty = new();

    /// <summary>
    /// Gets or sets the analysed episode, or null when the item was not an episode.
    /// </summary>
    public EpisodeRef? Item { get; set; }

    /// <summary>
    /// Gets or sets the missing episodes immediately before <see cref="Item"/>, in aired order.
    /// </summary>
    public IReadOnlyList<EpisodeRef> MissingBefore { get; set; } = Array.Empty<EpisodeRef>();

    /// <summary>
    /// Gets or sets the missing episodes between <see cref="Item"/> and the next available one, in aired order.
    /// </summary>
    public IReadOnlyList<EpisodeRef> MissingAfter { get; set; } = Array.Empty<EpisodeRef>();

    /// <summary>
    /// Gets or sets the closest available episode before <see cref="Item"/>.
    /// </summary>
    public EpisodeRef? PreviousAvailable { get; set; }

    /// <summary>
    /// Gets or sets the closest available episode after <see cref="Item"/>.
    /// </summary>
    public EpisodeRef? NextAvailable { get; set; }

    /// <summary>
    /// Gets a value indicating whether starting <see cref="Item"/> skips over missing episodes.
    /// </summary>
    public bool HasGapBefore => MissingBefore.Count > 0;

    /// <summary>
    /// Gets a value indicating whether autoplaying past <see cref="Item"/> skips over missing episodes.
    /// </summary>
    public bool HasGapAfter => MissingAfter.Count > 0;
}
