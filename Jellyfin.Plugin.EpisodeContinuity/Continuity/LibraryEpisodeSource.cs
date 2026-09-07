using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// <see cref="IEpisodeSource"/> backed by the Jellyfin library manager.
/// </summary>
public sealed class LibraryEpisodeSource : IEpisodeSource
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryEpisodeSource"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    public LibraryEpisodeSource(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<Episode> GetSeriesEpisodes(Guid seriesId)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Episode],
            AncestorIds = [seriesId],
            Recursive = true,
            // Deliberately no IsVirtualItem / IsMissing filter: virtual episodes are the signal.
        };

        return _libraryManager.GetItemList(query).OfType<Episode>().ToList();
    }
}
