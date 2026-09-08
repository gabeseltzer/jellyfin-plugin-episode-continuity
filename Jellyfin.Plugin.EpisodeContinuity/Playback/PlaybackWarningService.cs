using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodeContinuity.Configuration;
using Jellyfin.Plugin.EpisodeContinuity.Continuity;
using Jellyfin.Plugin.EpisodeContinuity.Web;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodeContinuity.Playback;

/// <summary>
/// Pushes a warning to a session when it starts an episode that skips over missing episodes.
/// Also performs one-time startup work (File Transformation registration).
/// </summary>
public sealed class PlaybackWarningService : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ContinuityService _continuity;
    private readonly WebClientRegistry _webClients;
    private readonly FileTransformationBridge _fileTransformation;
    private readonly ILogger<PlaybackWarningService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _recentlyWarned = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackWarningService"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="libraryManager">The library manager, used for the startup diagnostic.</param>
    /// <param name="continuity">The continuity service.</param>
    /// <param name="webClients">The registry of devices running the web script.</param>
    /// <param name="fileTransformation">The File Transformation bridge.</param>
    /// <param name="logger">The logger.</param>
    public PlaybackWarningService(
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        ContinuityService continuity,
        WebClientRegistry webClients,
        FileTransformationBridge fileTransformation,
        ILogger<PlaybackWarningService> logger)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _continuity = continuity;
        _webClients = webClients;
        _fileTransformation = fileTransformation;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _fileTransformation.TryRegister();
        _logger.LogInformation("Episode Continuity playback warnings active");
        _ = Task.Run(WarnIfNothingCanBeDetected, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs a warning when the library has no virtual episodes and the numbering-gap fallback is off,
    /// because in that state no gap can ever be detected and the plugin fails silently.
    /// </summary>
    private void WarnIfNothingCanBeDetected()
    {
        try
        {
            var config = Plugin.CurrentConfiguration;
            if (!config.Enabled || config.TreatNumberingGapsAsMissing)
            {
                return;
            }

            var virtualEpisodes = _libraryManager.GetCount(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Episode],
                IsVirtualItem = true,
                Recursive = true,
            });

            if (virtualEpisodes == 0)
            {
                _logger.LogWarning(
                    "Episode Continuity found no virtual (missing) episodes in the library and the numbering-gaps option is off, "
                    + "so continuity warnings will never fire. Jellyfin only creates virtual episodes through the TVDB plugin's "
                    + "\"Missing Episode Fetcher\"; see the Requirements section of the plugin README");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Episode Continuity could not run its startup library check");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Decides whether a playback start should produce a warning, without side effects.
    /// </summary>
    /// <param name="config">The current configuration.</param>
    /// <param name="webClients">The registry of devices running the web script.</param>
    /// <param name="deviceId">The device that started playback.</param>
    /// <param name="isEpisode">Whether the item is an episode.</param>
    /// <returns>True when the server should look at continuity for this start.</returns>
    public static bool ShouldConsider(PluginConfiguration config, WebClientRegistry webClients, string? deviceId, bool isEpisode)
    {
        if (!config.Enabled || config.ServerPushMode == ServerPushMode.Off || !isEpisode)
        {
            return false;
        }

        // The web script only replaces the server warning while it is allowed to show its interstitial.
        var webHandlesIt = config.WebFeaturesEnabled && config.InterstitialEnabled && webClients.IsActive(deviceId);
        return !webHandlesIt;
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        _ = HandleAsync(e);
    }

    private async Task HandleAsync(PlaybackProgressEventArgs e)
    {
        try
        {
            var session = e.Session;
            if (session is null)
            {
                return;
            }

            var config = Plugin.CurrentConfiguration;
            if (!ShouldConsider(config, _webClients, session.DeviceId, e.Item is Episode))
            {
                return;
            }

            var key = $"{session.Id}:{e.Item.Id:N}";
            var now = DateTime.UtcNow;
            var cooldown = TimeSpan.FromSeconds(Math.Max(0, config.WarningCooldownSeconds));
            if (_recentlyWarned.TryGetValue(key, out var last) && now - last < cooldown)
            {
                return;
            }

            var result = _continuity.GetForItem(e.Item);
            if (!result.HasGapBefore)
            {
                return;
            }

            _recentlyWarned[key] = now;
            PruneRecentlyWarned(now, cooldown);

            var text = ContinuityMessages.SkipWarning(result);
            _logger.LogInformation("Warning session {Session} ({Client}): {Text}", session.Id, session.Client, text);

            switch (config.ServerPushMode)
            {
                case ServerPushMode.Toast:
                    await SendMessage(session.Id, text, Math.Max(1000, config.ToastDurationMs)).ConfigureAwait(false);
                    break;
                case ServerPushMode.Modal:
                    await SendMessage(session.Id, text, null).ConfigureAwait(false);
                    break;
                case ServerPushMode.PauseAndModal:
                    await SendPlaystate(session.Id, PlaystateCommand.Pause).ConfigureAwait(false);
                    await SendMessage(session.Id, text, null).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, config.AutoResumeSeconds))).ConfigureAwait(false);
                    await SendPlaystate(session.Id, PlaystateCommand.Unpause).ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Episode Continuity could not warn about a playback start");
        }
    }

    private Task SendMessage(string sessionId, string text, int? timeoutMs)
    {
        var command = new MessageCommand
        {
            Header = ContinuityMessages.Header,
            Text = text,
            TimeoutMs = timeoutMs
        };
        return _sessionManager.SendMessageCommand(sessionId, sessionId, command, CancellationToken.None);
    }

    private Task SendPlaystate(string sessionId, PlaystateCommand command)
    {
        return _sessionManager.SendPlaystateCommand(sessionId, sessionId, new PlaystateRequest { Command = command }, CancellationToken.None);
    }

    private void PruneRecentlyWarned(DateTime now, TimeSpan cooldown)
    {
        if (_recentlyWarned.Count < 256)
        {
            return;
        }

        foreach (var pair in _recentlyWarned)
        {
            if (now - pair.Value > cooldown)
            {
                _recentlyWarned.TryRemove(pair.Key, out _);
            }
        }
    }
}
