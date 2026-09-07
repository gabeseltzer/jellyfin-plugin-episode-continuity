using System;
using System.Linq;
using Jellyfin.Plugin.EpisodeContinuity.Continuity;
using MediaBrowser.Controller.Entities.TV;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class ContinuityAnalyzerTests
{
    private static readonly DateTime Today = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Contiguous_HasNoGaps()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var e2 = EpisodeFactory.Real(1, 2);
        var e3 = EpisodeFactory.Real(1, 3);

        var result = ContinuityAnalyzer.Analyze([e1, e2, e3], e2.Id, false, Today);

        Assert.False(result.HasGapBefore);
        Assert.False(result.HasGapAfter);
        Assert.Equal(e1.Id, result.PreviousAvailable?.Id);
        Assert.Equal(e3.Id, result.NextAvailable?.Id);
    }

    [Fact]
    public void VirtualEpisodeBetween_IsReportedBothWays()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var v2 = EpisodeFactory.Virtual(1, 2, "The Lost One");
        var e3 = EpisodeFactory.Real(1, 3);

        var fromE1 = ContinuityAnalyzer.Analyze([e3, v2, e1], e1.Id, false, Today);
        var fromE3 = ContinuityAnalyzer.Analyze([e3, v2, e1], e3.Id, false, Today);

        var after = Assert.Single(fromE1.MissingAfter);
        Assert.Equal("S01E02", after.Code);
        Assert.Equal("The Lost One", after.Name);
        Assert.Equal(e3.Id, fromE1.NextAvailable?.Id);

        var before = Assert.Single(fromE3.MissingBefore);
        Assert.Equal(v2.Id, before.Id);
        Assert.Equal(e1.Id, fromE3.PreviousAvailable?.Id);
    }

    [Fact]
    public void NumberingGap_IgnoredByDefault_ReportedWithDevSwitch()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var e3 = EpisodeFactory.Real(1, 3);

        var strict = ContinuityAnalyzer.Analyze([e1, e3], e3.Id, false, Today);
        var lenient = ContinuityAnalyzer.Analyze([e1, e3], e3.Id, true, Today);

        Assert.False(strict.HasGapBefore);
        var missing = Assert.Single(lenient.MissingBefore);
        Assert.Null(missing.Id);
        Assert.Equal(2, missing.EpisodeNumber);
        Assert.Equal(1, missing.SeasonNumber);
    }

    [Fact]
    public void NumberingGap_DoesNotCrossSeasons()
    {
        var s1e10 = EpisodeFactory.Real(1, 10);
        var s2e1 = EpisodeFactory.Real(2, 1);

        var result = ContinuityAnalyzer.Analyze([s1e10, s2e1], s2e1.Id, true, Today);

        Assert.False(result.HasGapBefore);
    }

    [Fact]
    public void VirtualTrailingEpisode_CrossesSeasonBoundary()
    {
        var s1e10 = EpisodeFactory.Real(1, 10);
        var s1e11 = EpisodeFactory.Virtual(1, 11);
        var s2e1 = EpisodeFactory.Real(2, 1);

        var result = ContinuityAnalyzer.Analyze([s1e10, s1e11, s2e1], s2e1.Id, false, Today);

        var missing = Assert.Single(result.MissingBefore);
        Assert.Equal("S01E11", missing.Code);
        Assert.Equal(s1e10.Id, result.PreviousAvailable?.Id);
    }

    [Fact]
    public void UnairedVirtualEpisode_IsNotMissing()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var future = EpisodeFactory.Virtual(1, 2, premiere: Today.AddDays(7));

        var result = ContinuityAnalyzer.Analyze([e1, future], e1.Id, false, Today);

        Assert.False(result.HasGapAfter);
        Assert.Null(result.NextAvailable);
    }

    [Fact]
    public void AiredVirtualEpisode_IsMissing()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var past = EpisodeFactory.Virtual(1, 2, premiere: Today.AddDays(-7));

        var result = ContinuityAnalyzer.Analyze([e1, past], e1.Id, false, Today);

        Assert.True(result.HasGapAfter);
    }

    [Fact]
    public void MultiEpisodeFile_CoversItsRange()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var e2and3 = EpisodeFactory.Real(1, 2, end: 3);
        var e4 = EpisodeFactory.Real(1, 4);

        var result = ContinuityAnalyzer.Analyze([e1, e2and3, e4], e4.Id, true, Today);

        Assert.False(result.HasGapBefore);
        Assert.Equal(e2and3.Id, result.PreviousAvailable?.Id);
    }

    [Fact]
    public void SpecialsAndUnnumbered_AreIgnored()
    {
        var special = EpisodeFactory.Virtual(0, 1);
        var unnumbered = EpisodeFactory.Real(1, 1);
        unnumbered.IndexNumber = null;
        var e1 = EpisodeFactory.Real(1, 1);
        var e2 = EpisodeFactory.Real(1, 2);

        var result = ContinuityAnalyzer.Analyze([special, unnumbered, e1, e2], e2.Id, true, Today);

        Assert.False(result.HasGapBefore);
        Assert.Equal(e1.Id, result.PreviousAvailable?.Id);
    }

    [Fact]
    public void DuplicateRealAndVirtual_PrefersReal()
    {
        var real = EpisodeFactory.Real(1, 2);
        var stale = EpisodeFactory.Virtual(1, 2);
        var e1 = EpisodeFactory.Real(1, 1);

        var result = ContinuityAnalyzer.Analyze([stale, real, e1], e1.Id, false, Today);

        Assert.False(result.HasGapAfter);
        Assert.Equal(real.Id, result.NextAvailable?.Id);
    }

    [Fact]
    public void UnknownItem_ReturnsEmpty()
    {
        var e1 = EpisodeFactory.Real(1, 1);

        var result = ContinuityAnalyzer.Analyze([e1], Guid.NewGuid(), false, Today);

        Assert.Same(ContinuityResult.Empty, result);
    }

    [Fact]
    public void LongRun_ListsEveryMissingEpisodeInOrder()
    {
        var e1 = EpisodeFactory.Real(1, 1);
        var missing = Enumerable.Range(2, 5).Select(n => EpisodeFactory.Virtual(1, n)).ToArray();
        var e7 = EpisodeFactory.Real(1, 7);
        Episode[] all = [e7, .. missing.Reverse(), e1];

        var result = ContinuityAnalyzer.Analyze(all, e7.Id, false, Today);

        Assert.Equal([2, 3, 4, 5, 6], result.MissingBefore.Select(m => m.EpisodeNumber));
    }
}
