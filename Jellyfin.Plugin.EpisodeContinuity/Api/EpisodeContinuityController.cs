using System;
using System.IO;
using System.Net.Mime;
using Jellyfin.Plugin.EpisodeContinuity.Continuity;
using Jellyfin.Plugin.EpisodeContinuity.Playback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.EpisodeContinuity.Api;

/// <summary>
/// REST surface used by the web client script.
/// </summary>
[ApiController]
[Route("EpisodeContinuity")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class EpisodeContinuityController : ControllerBase
{
    private readonly ContinuityService _continuity;
    private readonly WebClientRegistry _webClients;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeContinuityController"/> class.
    /// </summary>
    /// <param name="continuity">The continuity service.</param>
    /// <param name="webClients">The registry of devices running the web script.</param>
    public EpisodeContinuityController(ContinuityService continuity, WebClientRegistry webClients)
    {
        _continuity = continuity;
        _webClients = webClients;
    }

    /// <summary>
    /// Gets the continuity picture around an episode.
    /// </summary>
    /// <param name="itemId">The episode item id.</param>
    /// <returns>The continuity result; empty for non-episodes.</returns>
    [HttpGet("Items/{itemId}/Continuity")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ContinuityResult> GetContinuity([FromRoute] Guid itemId)
    {
        if (!Plugin.CurrentConfiguration.Enabled)
        {
            return ContinuityResult.Empty;
        }

        return _continuity.GetForItem(itemId);
    }

    /// <summary>
    /// Gets the settings the client script needs.
    /// </summary>
    /// <returns>The client configuration.</returns>
    [HttpGet("ClientConfig")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ClientConfig> GetClientConfig()
    {
        var config = Plugin.CurrentConfiguration;
        var enabled = config.Enabled && config.WebFeaturesEnabled;
        return new ClientConfig
        {
            Enabled = enabled,
            UpNextWarningEnabled = enabled && config.UpNextWarningEnabled,
            InterstitialEnabled = enabled && config.InterstitialEnabled,
            InterstitialCountdownSeconds = Math.Max(1, config.InterstitialCountdownSeconds)
        };
    }

    /// <summary>
    /// Lets the web script announce it is active on a device so the server does not also push warnings there.
    /// </summary>
    /// <param name="hello">The device id.</param>
    /// <returns>No content.</returns>
    [HttpPost("Hello")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Hello([FromBody] HelloRequest hello)
    {
        _webClients.Heartbeat(hello?.DeviceId);
        return NoContent();
    }

    /// <summary>
    /// Serves the client script. Anonymous because index.html loads before login.
    /// </summary>
    /// <returns>The JavaScript.</returns>
    [HttpGet("client.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetClientScript()
    {
        var stream = typeof(EpisodeContinuityController).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.EpisodeContinuity.Web.WebResources.client.js");
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-cache";
        return File(stream, "application/javascript");
    }

    /// <summary>
    /// Settings exposed to the client script.
    /// </summary>
    public sealed class ClientConfig
    {
        /// <summary>Gets or sets a value indicating whether web features are on at all.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets a value indicating whether the up-next warning is on.</summary>
        public bool UpNextWarningEnabled { get; set; }

        /// <summary>Gets or sets a value indicating whether the interstitial is on.</summary>
        public bool InterstitialEnabled { get; set; }

        /// <summary>Gets or sets the interstitial countdown in seconds.</summary>
        public int InterstitialCountdownSeconds { get; set; }
    }

    /// <summary>
    /// Body of the Hello call.
    /// </summary>
    public sealed class HelloRequest
    {
        /// <summary>Gets or sets the Jellyfin device id.</summary>
        public string? DeviceId { get; set; }
    }
}
