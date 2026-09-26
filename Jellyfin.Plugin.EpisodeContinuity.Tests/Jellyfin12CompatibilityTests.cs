using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.EpisodeContinuity.Api;
using Xunit;

namespace Jellyfin.Plugin.EpisodeContinuity.Tests;

/// <summary>
/// Guards the requirements Jellyfin 12 places on plugins (https://jellyfin.org/posts/jellyfin-release-12.0/).
/// </summary>
public class Jellyfin12CompatibilityTests
{
    private const string TargetAbi = "12.0.0.0";

    [Theory]
    [InlineData("api_key")]
    [InlineData("X-Emby-Token")]
    [InlineData("X-Emby-Authorization")]
    [InlineData("X-MediaBrowser-Token")]
    public void ClientScript_AvoidsLegacyAuthorization(string legacy)
    {
        // 12 turns EnableLegacyAuthorization off by default, so these would 401.
        Assert.DoesNotContain(legacy, ClientScript(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PluginTargetsDotNet10()
    {
        var framework = typeof(Plugin).Assembly
            .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
            .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>()
            .Single();

        Assert.Equal(".NETCoreApp,Version=v10.0", framework.FrameworkName);
    }

    [Fact]
    public void PackagingMetadata_TargetsJellyfin12()
    {
        var buildYaml = File.ReadAllText(RepoFile("build.yaml"));
        Assert.Contains($"targetAbi: \"{TargetAbi}\"", buildYaml, StringComparison.Ordinal);
        Assert.Contains("framework: \"net10.0\"", buildYaml, StringComparison.Ordinal);

        using var meta = JsonDocument.Parse(File.ReadAllText(RepoFile("meta.json")));
        Assert.Equal(TargetAbi, meta.RootElement.GetProperty("targetAbi").GetString());

        var csproj = File.ReadAllText(RepoFile("Jellyfin.Plugin.EpisodeContinuity/Jellyfin.Plugin.EpisodeContinuity.csproj"));
        foreach (Match reference in Regex.Matches(csproj, "Include=\"Jellyfin\\.(Controller|Model)\" Version=\"([^\"]+)\""))
        {
            Assert.StartsWith("12.", reference.Groups[2].Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PackagingMetadata_AgreesOnVersion()
    {
        var version = typeof(Plugin).Assembly.GetName().Version!.ToString();

        Assert.Contains($"version: \"{version}\"", File.ReadAllText(RepoFile("build.yaml")), StringComparison.Ordinal);
    }

    private static string ClientScript()
    {
        using var stream = typeof(EpisodeContinuityController).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.EpisodeContinuity.Web.WebResources.client.js");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Jellyfin.Plugin.EpisodeContinuity.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, relative);
    }
}
