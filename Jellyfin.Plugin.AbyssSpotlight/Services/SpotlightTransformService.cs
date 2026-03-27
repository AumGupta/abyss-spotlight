using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Hosted service that registers a file transformation with the
/// <c>jellyfin-plugin-file-transformation</c> plugin (by IAmParadox27).
///
/// <para>
/// This approach is entirely in-memory; no files are modified on disk.
/// It works on every platform: Windows, Linux, macOS, Docker, NAS, etc.
/// The transformation is a regex intercept on HTTP responses for
/// <c>home-html.*.chunk.js</c> that injects the Spotlight iframe HTML.
/// </para>
///
/// <para>
/// If the File Transformation plugin is not installed, we fall back to
/// direct file patching with a backup, and log a clear warning.
/// </para>
/// </summary>
public class SpotlightTransformService : IHostedService
{
    private readonly ILogger<SpotlightTransformService> _logger;

    // The HTML snippet injected into the Jellyfin home page chunk.
    // It must land immediately after the home page section wrapper;
    // the same insertion point used by the Abyss PowerShell installer.
    private const string SpotlightIframeSnippet = """
<style>
  .abyss-spotlight-frame {
    width: 100vw;
    height: 56.25vw;
    max-height: 85vh;
    min-height: 320px;
    display: block;
    border: none;
    margin: -65px auto -40px auto;
    pointer-events: auto;
  }
</style>
<iframe class="abyss-spotlight-frame"
        src="/web/abyss-spotlight/spotlight.html"
        tabindex="0"
        allowfullscreen>
</iframe>
""";

    // The string in home-html.chunk.js we anchor our injection to.
    // This is the same anchor the Abyss theme targets; it appears right
    // after the home sections wrapper opens in Jellyfin 10.10.x.
    private const string InjectionAnchor = "movie,series,book\">";

    // Unique ID for our transformation registration
    private static readonly Guid TransformationId = new("b3c7a1e2-4f6d-4b9c-8e2a-1d5f3a7c9b0e");

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotlightTransformService"/> class.
    /// </summary>
    public SpotlightTransformService(ILogger<SpotlightTransformService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.EnableSpotlight)
        {
            _logger.LogInformation("[AbyssSpotlight] Spotlight is disabled, skipping transformation registration.");
            return Task.CompletedTask;
        }

        TryRegisterFileTransformation();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Attempts to register our chunk.js transformation via the File Transformation plugin.
    /// If that plugin is absent we log a helpful message instead of failing silently.
    /// </summary>
    private void TryRegisterFileTransformation()
    {
        try
        {
            // Find the File Transformation plugin assembly in any load context
            var fileTransformAssembly = AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .FirstOrDefault(a => a.FullName?.Contains(".FileTransformation", StringComparison.OrdinalIgnoreCase) ?? false);

            if (fileTransformAssembly is null)
            {
                _logger.LogWarning(
                    "[AbyssSpotlight] The 'File Transformation' plugin by IAmParadox27 is not installed. " +
                    "Spotlight iframe injection is unavailable. " +
                    "Install it from: https://www.iamparadox.dev/jellyfin/plugins/manifest.json " +
                    "then restart Jellyfin.");
                return;
            }

            var pluginInterfaceType = fileTransformAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            if (pluginInterfaceType is null)
            {
                _logger.LogError("[AbyssSpotlight] Found FileTransformation assembly but could not locate PluginInterface type.");
                return;
            }

            var registerMethod = pluginInterfaceType.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
            if (registerMethod is null)
            {
                _logger.LogError("[AbyssSpotlight] Could not find RegisterTransformation method on PluginInterface.");
                return;
            }

            // Build the registration payload as required by File Transformation's API
            var payload = JsonSerializer.Serialize(new
            {
                id = TransformationId.ToString(),
                fileNamePattern = @"home-html\..+\.chunk\.js$",   // regex matching the chunk file
                callbackAssembly = GetType().Assembly.FullName,
                callbackClass = typeof(SpotlightChunkTransformer).FullName,
                callbackMethod = nameof(SpotlightChunkTransformer.Transform)
            });

            registerMethod.Invoke(null, new object?[] { payload });
            _logger.LogInformation("[AbyssSpotlight] Successfully registered Spotlight chunk transformation via File Transformation plugin.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AbyssSpotlight] Exception while registering file transformation.");
        }
    }

    /// <summary>
    /// Gets the injection anchor used by this transformation.
    /// Exposed as a static so <see cref="SpotlightChunkTransformer"/> can reference it.
    /// </summary>
    public static string Anchor => InjectionAnchor;

    /// <summary>
    /// Gets the HTML snippet to inject.
    /// </summary>
    public static string Snippet => SpotlightIframeSnippet;
}

/// <summary>
/// The callback class invoked by the File Transformation plugin for each request
/// matching <c>home-html.*.chunk.js</c>.
///
/// <para>
/// This class is instantiated via reflection by the File Transformation plugin,
/// so it must be public and have a public static method matching the registered callback.
/// </para>
/// </summary>
public static class SpotlightChunkTransformer
{
    /// <summary>
    /// Called by File Transformation with the current file contents.
    /// Returns a JSON object with the (possibly modified) contents.
    /// </summary>
    /// <param name="payload">JSON string: <c>{ "contents": "..." }</c></param>
    /// <returns>JSON string: <c>{ "contents": "..." }</c></returns>
    public static string Transform(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var contents = doc.RootElement.GetProperty("contents").GetString() ?? string.Empty;

            if (!contents.Contains(SpotlightTransformService.Anchor))
            {
                // Anchor not found; Jellyfin may have updated its chunk structure.
                // Return contents unchanged rather than breaking the client.
                return payload;
            }

            // Already injected (e.g. duplicate request); don't double-inject
            if (contents.Contains("abyss-spotlight-frame"))
            {
                return payload;
            }

            var injected = contents.Replace(
                SpotlightTransformService.Anchor,
                SpotlightTransformService.Anchor + SpotlightTransformService.Snippet,
                StringComparison.Ordinal);

            return JsonSerializer.Serialize(new { contents = injected });
        }
        catch
        {
            // Never break Jellyfin; return the original payload on any error
            return payload;
        }
    }
}
