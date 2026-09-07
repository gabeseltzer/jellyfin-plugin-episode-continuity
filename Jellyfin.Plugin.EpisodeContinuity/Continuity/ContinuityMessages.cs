using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.EpisodeContinuity.Continuity;

/// <summary>
/// Human-readable text shared by the server push and the web overlay.
/// </summary>
public static class ContinuityMessages
{
    /// <summary>
    /// The header used for skip warnings.
    /// </summary>
    public const string Header = "Missing episode";

    /// <summary>
    /// Builds the body of a "you are skipping" warning for an episode with a gap before it.
    /// </summary>
    /// <param name="result">A result with <see cref="ContinuityResult.HasGapBefore"/> set.</param>
    /// <returns>The message text.</returns>
    public static string SkipWarning(ContinuityResult result)
    {
        var missing = Describe(result.MissingBefore);
        var target = result.Item is null ? "this episode" : Describe(result.Item);
        var verb = result.MissingBefore.Count == 1 ? "is" : "are";
        return $"{missing} {verb} missing from your library. You are skipping straight to {target}.";
    }

    /// <summary>
    /// Builds the body of an autoplay warning for an episode with a gap after it.
    /// </summary>
    /// <param name="result">A result with <see cref="ContinuityResult.HasGapAfter"/> set.</param>
    /// <returns>The message text.</returns>
    public static string UpNextWarning(ContinuityResult result)
    {
        var missing = Describe(result.MissingAfter);
        var verb = result.MissingAfter.Count == 1 ? "is" : "are";
        var next = result.NextAvailable is null
            ? "Nothing later is available."
            : $"Next available: {Describe(result.NextAvailable)}.";
        return $"{missing} {verb} missing from your library. {next}";
    }

    /// <summary>
    /// Describes one episode, e.g. "Episode 2 (The Title)" or "S02E01".
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The description.</returns>
    public static string Describe(EpisodeRef episode)
    {
        var label = episode.Code;
        return string.IsNullOrWhiteSpace(episode.Name) ? label : $"{label} \"{episode.Name}\"";
    }

    /// <summary>
    /// Describes a list of episodes, collapsing long runs into a range.
    /// </summary>
    /// <param name="episodes">Episodes in aired order.</param>
    /// <returns>The description.</returns>
    public static string Describe(IReadOnlyList<EpisodeRef> episodes)
    {
        if (episodes.Count == 0)
        {
            return "No episodes";
        }

        if (episodes.Count == 1)
        {
            return Describe(episodes[0]);
        }

        if (episodes.Count <= 3)
        {
            return string.Join(", ", episodes.Select(Describe));
        }

        return $"{episodes[0].Code} to {episodes[^1].Code} ({episodes.Count} episodes)";
    }
}
