namespace Jellyfin.Plugin.EpisodeContinuity.Web;

/// <summary>
/// Callbacks invoked by the File Transformation plugin via reflection. Signatures must stay stable.
/// </summary>
public static class TransformationPatches
{
    /// <summary>
    /// Injects the client script tag into index.html.
    /// </summary>
    /// <param name="payload">The current index.html.</param>
    /// <returns>The transformed HTML.</returns>
    public static string IndexHtml(PatchRequestPayload payload)
    {
        var html = payload?.Contents ?? string.Empty;
        if (!Plugin.CurrentConfiguration.Enabled || !Plugin.CurrentConfiguration.WebFeaturesEnabled)
        {
            return html;
        }

        return ScriptTag.Inject(html, ScriptTag.CurrentVersion());
    }
}
