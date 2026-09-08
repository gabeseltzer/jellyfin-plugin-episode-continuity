using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.EpisodeContinuity.Configuration;

/// <summary>
/// How the server warns a client that has no web script running.
/// </summary>
public enum ServerPushMode
{
    /// <summary>No server-side warning.</summary>
    Off,

    /// <summary>A non-blocking toast; playback continues.</summary>
    Toast,

    /// <summary>A blocking dialog with an OK button; video keeps playing underneath.</summary>
    Modal,

    /// <summary>Pause playback, show a dialog, resume after a delay.</summary>
    PauseAndModal
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how the server warns clients on playback start.
    /// </summary>
    public ServerPushMode ServerPushMode { get; set; } = ServerPushMode.Toast;

    /// <summary>
    /// Gets or sets how long a toast stays on screen, in milliseconds.
    /// </summary>
    public int ToastDurationMs { get; set; } = 8000;

    /// <summary>
    /// Gets or sets how many seconds to wait before resuming in <see cref="ServerPushMode.PauseAndModal"/> mode.
    /// </summary>
    public int AutoResumeSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets how many seconds to wait before warning the same session about the same episode again.
    /// Absorbs duplicate playback-start reports and seeks; zero warns on every start.
    /// </summary>
    public int WarningCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets a value indicating whether the web client script is injected at all.
    /// </summary>
    public bool WebFeaturesEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the web "next episode" dialog gets a warning.
    /// </summary>
    public bool UpNextWarningEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the web pre-play interstitial is shown.
    /// </summary>
    public bool InterstitialEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interstitial countdown length, in seconds.
    /// </summary>
    public int InterstitialCountdownSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether non-contiguous episode numbers on disk count as missing
    /// episodes in addition to provider-known virtual episodes. Hidden from the config page; intended for
    /// development against libraries with no metadata match.
    /// </summary>
    public bool TreatNumberingGapsAsMissing { get; set; }
}
