using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodeContinuity.Web;

/// <summary>
/// Rewrites jellyfin-web's index.html at request time to add the client script tag.
/// Nothing is written to disk; on any error the original response is served.
/// </summary>
public sealed class ScriptInjectionStartupFilter : IStartupFilter
{
    private readonly InjectionState _state;
    private readonly ILogger<ScriptInjectionStartupFilter> _logger;
    private int _loggedOnce;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptInjectionStartupFilter"/> class.
    /// </summary>
    /// <param name="state">The shared injection state.</param>
    /// <param name="logger">The logger.</param>
    public ScriptInjectionStartupFilter(InjectionState state, ILogger<ScriptInjectionStartupFilter> logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(InvokeAsync);
            next(app);
        };
    }

    /// <summary>
    /// Checks whether a request path is the web app shell.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>True for /web, /web/ and /web/index.html, with or without a base URL prefix.</returns>
    public static bool IsIndexRequest(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The middleware body.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="next">The next middleware.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        var config = Plugin.CurrentConfiguration;
        if (!IsIndexRequest(context.Request.Path.Value)
            || !HttpMethods.IsGet(context.Request.Method)
            || !config.Enabled
            || !config.WebFeaturesEnabled
            || _state.FileTransformationActive)
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Make the static handler return a plain, complete 200 we can rewrite.
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("Range");
        context.Request.Headers.Remove("If-Range");

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next().ConfigureAwait(false);
        }
        catch
        {
            context.Response.Body = originalBody;
            throw;
        }

        context.Response.Body = originalBody;
        buffer.Seek(0, SeekOrigin.Begin);

        var isHtml = context.Response.StatusCode == StatusCodes.Status200OK
            && (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false);
        if (!isHtml)
        {
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            return;
        }

        string html;
        using (var reader = new StreamReader(buffer, Encoding.UTF8, true, 1024, leaveOpen: true))
        {
            html = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        try
        {
            var injected = ScriptTag.Inject(html, ScriptTag.CurrentVersion());
            if (!ReferenceEquals(injected, html) && System.Threading.Interlocked.Exchange(ref _loggedOnce, 1) == 0)
            {
                _logger.LogInformation("Injected the Episode Continuity client script into index.html");
            }

            html = injected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Script injection failed; serving original index.html");
        }

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html;charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.Remove("ETag");
        context.Response.Headers.Remove("Last-Modified");
        context.Response.Headers.Remove("Accept-Ranges");
        await originalBody.WriteAsync(bytes).ConfigureAwait(false);
    }
}
