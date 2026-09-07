namespace Jellyfin.Plugin.EpisodeContinuity.Web;

/// <summary>
/// Shared switch telling the middleware whether another component already injects the script.
/// </summary>
public sealed class InjectionState
{
    /// <summary>
    /// Gets or sets a value indicating whether the File Transformation plugin handles injection.
    /// </summary>
    public bool FileTransformationActive { get; set; }
}
