using System;
using Jellyfin.Plugin.EpisodeContinuity.Continuity;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class ContinuityMessagesTests
{
    private static EpisodeRef Ref(int s, int e, string? name, bool available) => new(Guid.NewGuid(), s, e, name, available);

    [Fact]
    public void SkipWarning_SingleMissing()
    {
        var result = new ContinuityResult
        {
            Item = Ref(1, 3, "Third", true),
            MissingBefore = [Ref(1, 2, "Second", false)]
        };

        Assert.Equal(
            "S01E02 \"Second\" is missing from your library. You are skipping straight to S01E03 \"Third\".",
            ContinuityMessages.SkipWarning(result));
    }

    [Fact]
    public void UpNextWarning_ManyMissing_CollapsesToRange()
    {
        var result = new ContinuityResult
        {
            Item = Ref(1, 1, null, true),
            MissingAfter = [Ref(1, 2, null, false), Ref(1, 3, null, false), Ref(1, 4, null, false), Ref(1, 5, null, false)],
            NextAvailable = Ref(1, 6, "Six", true)
        };

        Assert.Equal(
            "S01E02 to S01E05 (4 episodes) are missing from your library. Next available: S01E06 \"Six\".",
            ContinuityMessages.UpNextWarning(result));
    }

    [Fact]
    public void UpNextWarning_NoNextAvailable()
    {
        var result = new ContinuityResult
        {
            Item = Ref(1, 1, null, true),
            MissingAfter = [Ref(1, 2, null, false), Ref(1, 3, null, false)]
        };

        Assert.Equal(
            "S01E02, S01E03 are missing from your library. Nothing later is available.",
            ContinuityMessages.UpNextWarning(result));
    }
}
