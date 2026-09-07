using Jellyfin.Plugin.EpisodeContinuity.Web;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

public class ScriptTagTests
{
    [Fact]
    public void Inject_AddsTagBeforeBodyClose()
    {
        var html = "<html><body><div>app</div></body></html>";

        var result = ScriptTag.Inject(html, "1.2.3");

        Assert.Contains("<script plugin=\"EpisodeContinuity\" src=\"../EpisodeContinuity/client.js?v=1.2.3\" defer></script>\n</body>", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Inject_IsIdempotent()
    {
        var once = ScriptTag.Inject("<body></body>", "1");

        var twice = ScriptTag.Inject(once, "1");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Inject_LeavesPagesWithoutBodyAlone()
    {
        Assert.Equal("<div/>", ScriptTag.Inject("<div/>", "1"));
        Assert.Equal(string.Empty, ScriptTag.Inject(string.Empty, "1"));
    }

    [Fact]
    public void TransformationPatches_UsesSameInjection()
    {
        var html = TransformationPatches.IndexHtml(new PatchRequestPayload { Contents = "<body></body>" });

        Assert.Contains(ScriptTag.Marker, html, System.StringComparison.Ordinal);
    }

    [Fact]
    public void FileTransformationPayload_HasExpectedShape()
    {
        var json = System.Text.Json.JsonDocument.Parse(FileTransformationBridge.BuildPayloadJson()).RootElement;

        Assert.Equal(Plugin.PluginId.ToString(), json.GetProperty("id").GetString());
        Assert.Equal("index.html", json.GetProperty("fileNamePattern").GetString());
        Assert.Equal(typeof(TransformationPatches).FullName, json.GetProperty("callbackClass").GetString());
        Assert.Equal("IndexHtml", json.GetProperty("callbackMethod").GetString());
        Assert.StartsWith("Jellyfin.Plugin.EpisodeContinuity,", json.GetProperty("callbackAssembly").GetString(), System.StringComparison.Ordinal);
    }
}
