using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodeContinuity.Web;

/// <summary>
/// Registers our index.html transformation with the File Transformation plugin when it is installed.
/// Uses reflection because plugins load in separate <see cref="AssemblyLoadContext"/>s.
/// </summary>
public sealed class FileTransformationBridge
{
    private const string AssemblyNameFragment = "Jellyfin.Plugin.FileTransformation";
    private const string InterfaceTypeName = "Jellyfin.Plugin.FileTransformation.PluginInterface";
    private const string RegisterMethodName = "RegisterTransformation";

    private readonly InjectionState _state;
    private readonly ILogger<FileTransformationBridge> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransformationBridge"/> class.
    /// </summary>
    /// <param name="state">The shared injection state.</param>
    /// <param name="logger">The logger.</param>
    public FileTransformationBridge(InjectionState state, ILogger<FileTransformationBridge> logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <summary>
    /// Attempts the registration. Safe to call when the plugin is absent.
    /// </summary>
    /// <returns>True when File Transformation accepted the registration.</returns>
    public bool TryRegister()
    {
        try
        {
            var assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(a => a.GetName().Name?.Contains(AssemblyNameFragment, StringComparison.Ordinal) == true);
            if (assembly is null)
            {
                _logger.LogDebug("File Transformation plugin not present; using built-in index.html middleware");
                return false;
            }

            var method = assembly.GetType(InterfaceTypeName)?.GetMethod(RegisterMethodName, BindingFlags.Public | BindingFlags.Static);
            if (method is null || method.GetParameters().Length != 1)
            {
                _logger.LogWarning("File Transformation plugin found but its registration API is unrecognised; using built-in middleware");
                return false;
            }

            // Build the JObject with File Transformation's own Newtonsoft copy to avoid a cross-context type mismatch.
            var payloadType = method.GetParameters()[0].ParameterType;
            var parse = payloadType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
            if (parse is null)
            {
                _logger.LogWarning("File Transformation payload type {Type} has no Parse(string); using built-in middleware", payloadType.FullName);
                return false;
            }

            var json = BuildPayloadJson();
            var payload = parse.Invoke(null, [json]);
            method.Invoke(null, [payload]);

            _state.FileTransformationActive = true;
            _logger.LogInformation("Registered index.html transformation with the File Transformation plugin");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register with the File Transformation plugin; using built-in middleware");
            _state.FileTransformationActive = false;
            return false;
        }
    }

    /// <summary>
    /// Builds the registration payload JSON expected by File Transformation.
    /// </summary>
    /// <returns>The JSON text.</returns>
    internal static string BuildPayloadJson()
    {
        var assemblyName = typeof(TransformationPatches).Assembly.FullName ?? string.Empty;
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            id = Plugin.PluginId.ToString(),
            fileNamePattern = "index.html",
            callbackAssembly = assemblyName,
            callbackClass = typeof(TransformationPatches).FullName,
            callbackMethod = nameof(TransformationPatches.IndexHtml)
        });
    }
}
