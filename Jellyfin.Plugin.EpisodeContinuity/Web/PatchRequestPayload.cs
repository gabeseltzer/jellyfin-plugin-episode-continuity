namespace Jellyfin.Plugin.EpisodeContinuity.Web;

/// <summary>
/// The payload the File Transformation plugin hands to a callback.
/// </summary>
public sealed class PatchRequestPayload
{
    /// <summary>
    /// Gets or sets the current file contents.
    /// </summary>
    public string? Contents { get; set; }
}
