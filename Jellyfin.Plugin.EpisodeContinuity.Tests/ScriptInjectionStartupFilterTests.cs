using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodeContinuity.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class ScriptInjectionStartupFilterTests
{
    [Theory]
    [InlineData("/web/index.html", true)]
    [InlineData("/web/", true)]
    [InlineData("/web", true)]
    [InlineData("/jellyfin/web/index.html", true)]
    [InlineData("/web/main.js", false)]
    [InlineData("/Items/abc", false)]
    [InlineData("", false)]
    public void IsIndexRequest(string path, bool expected)
    {
        Assert.Equal(expected, ScriptInjectionStartupFilter.IsIndexRequest(path));
    }

    [Fact]
    public async Task InjectsIntoHtmlIndexResponse()
    {
        var (context, body) = MakeContext("/web/index.html");
        var filter = new ScriptInjectionStartupFilter(new InjectionState(), NullLogger<ScriptInjectionStartupFilter>.Instance);

        await filter.InvokeAsync(context, () => WriteAsync(context, "text/html", "<html><body>x</body></html>"));

        var html = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains(ScriptTag.Marker, html, System.StringComparison.Ordinal);
        Assert.Equal(body.Length, context.Response.ContentLength);
        Assert.False(context.Request.Headers.ContainsKey("Accept-Encoding"));
    }

    [Fact]
    public async Task PassesThroughNonHtml()
    {
        var (context, body) = MakeContext("/web/index.html");
        var filter = new ScriptInjectionStartupFilter(new InjectionState(), NullLogger<ScriptInjectionStartupFilter>.Instance);

        await filter.InvokeAsync(context, () => WriteAsync(context, "application/json", "{}"));

        Assert.Equal("{}", Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task PassesThroughWhenFileTransformationOwnsInjection()
    {
        var (context, body) = MakeContext("/web/index.html");
        var state = new InjectionState { FileTransformationActive = true };
        var filter = new ScriptInjectionStartupFilter(state, NullLogger<ScriptInjectionStartupFilter>.Instance);

        await filter.InvokeAsync(context, () => WriteAsync(context, "text/html", "<body></body>"));

        Assert.Equal("<body></body>", Encoding.UTF8.GetString(body.ToArray()));
        Assert.True(context.Request.Headers.ContainsKey("Accept-Encoding"));
    }

    [Fact]
    public async Task IgnoresOtherPaths()
    {
        var (context, body) = MakeContext("/web/main.js");
        var filter = new ScriptInjectionStartupFilter(new InjectionState(), NullLogger<ScriptInjectionStartupFilter>.Instance);

        await filter.InvokeAsync(context, () => WriteAsync(context, "text/html", "<body></body>"));

        Assert.Equal("<body></body>", Encoding.UTF8.GetString(body.ToArray()));
    }

    private static (DefaultHttpContext Context, MemoryStream Body) MakeContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.Headers["Accept-Encoding"] = "gzip";
        var body = new MemoryStream();
        context.Response.Body = body;
        return (context, body);
    }

    private static async Task WriteAsync(HttpContext context, string contentType, string content)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(content);
        await context.Response.Body.WriteAsync(bytes);
    }
}
