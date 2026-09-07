using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.EpisodeContinuity.Playback;

/// <summary>
/// Tracks which devices currently run the injected web script, so the server does not also push warnings to them.
/// </summary>
public sealed class WebClientRegistry
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTime> _clock;
    private readonly TimeSpan _ttl;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebClientRegistry"/> class.
    /// </summary>
    public WebClientRegistry()
        : this(() => DateTime.UtcNow, DefaultTtl)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebClientRegistry"/> class with an explicit clock.
    /// </summary>
    /// <param name="clock">Supplies the current UTC time.</param>
    /// <param name="ttl">How long a heartbeat keeps a device active.</param>
    public WebClientRegistry(Func<DateTime> clock, TimeSpan ttl)
    {
        _clock = clock;
        _ttl = ttl;
    }

    /// <summary>
    /// Records a heartbeat from the web script on a device.
    /// </summary>
    /// <param name="deviceId">The Jellyfin device id.</param>
    public void Heartbeat(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        _lastSeen[deviceId] = _clock();
    }

    /// <summary>
    /// Checks whether the web script was recently active on a device.
    /// </summary>
    /// <param name="deviceId">The Jellyfin device id.</param>
    /// <returns>True when a heartbeat arrived within the time-to-live.</returns>
    public bool IsActive(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || !_lastSeen.TryGetValue(deviceId, out var seen))
        {
            return false;
        }

        if (_clock() - seen > _ttl)
        {
            _lastSeen.TryRemove(deviceId, out _);
            return false;
        }

        return true;
    }
}
