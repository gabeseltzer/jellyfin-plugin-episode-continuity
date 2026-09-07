using System;

namespace Jellyfin.Plugin.EpisodeContinuity.Web;

/// <summary>
/// Builds and inserts the client script tag into jellyfin-web's index.html.
/// </summary>
public static class ScriptTag
{
    /// <summary>
    /// The URL fragment used to detect an already-injected tag.
    /// </summary>
    public const string Marker = "/EpisodeContinuity/client.js";

    /// <summary>
    /// Builds the script tag. The src is relative so it works under a base URL prefix.
    /// </summary>
    /// <param name="version">A cache-busting version string.</param>
    /// <returns>The HTML tag.</returns>
    public static string Build(string version)
    {
        return $"<script plugin=\"EpisodeContinuity\" src=\"..{Marker}?v={Uri.EscapeDataString(version)}\" defer></script>";
    }

    /// <summary>
    /// Inserts the tag before the closing body tag, once.
    /// </summary>
    /// <param name="html">The page HTML.</param>
    /// <param name="version">A cache-busting version string.</param>
    /// <returns>The page with the tag, or the original when it was already present or had no body tag.</returns>
    public static string Inject(string html, string version)
    {
        if (string.IsNullOrEmpty(html) || html.Contains(Marker, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyClose < 0)
        {
            return html;
        }

        return string.Concat(html.AsSpan(0, bodyClose), Build(version), "\n", html.AsSpan(bodyClose));
    }

    /// <summary>
    /// Gets the version string used for cache busting.
    /// </summary>
    /// <returns>The plugin assembly version.</returns>
    public static string CurrentVersion()
    {
        return Plugin.Instance?.Version?.ToString() ?? typeof(ScriptTag).Assembly.GetName().Version?.ToString() ?? "0";
    }
}
