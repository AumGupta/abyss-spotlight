using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Registers a file transformation with the File Transformation plugin
/// (github.com/IAmParadox27/jellyfin-plugin-file-transformation) so that
/// Jellyfin's home-html.*.chunk.js is patched in-memory on every HTTP request
/// to inject the Spotlight iframe — no files modified on disk, works everywhere.
/// </summary>
public class SpotlightTransformService : IHostedService
{
    private readonly ILogger<SpotlightTransformService> _logger;

    // Confirmed anchor used by every spotlight implementation including Abyss's own setup.ps1
    // This string appears in home-html.*.chunk.js in Jellyfin 10.10.x
    internal const string InjectionAnchor = "movie,series,book\">";

    // The iframe snippet to inject immediately after the anchor.
    // Points to our embedded-resource endpoint served by SpotlightController.
    internal const string IframeSnippet =
        "<style>" +
        ".abyss-spotlight-frame{" +
        "width:100vw;" +
        "height:56.25vw;" +
        "max-height:85vh;" +
        "min-height:320px;" +
        "display:block;" +
        "border:none;" +
        "margin:-65px auto -40px auto;" +
        "pointer-events:auto;" +
        "}" +
        "</style>" +
        "<iframe class=\"abyss-spotlight-frame\"" +
        " src=\"/abyss-spotlight/spotlight.html\"" +
        " tabindex=\"0\"" +
        " allowfullscreen>" +
        "</iframe>";

    private static readonly Guid TransformId = new("b3c7a1e2-4f6d-4b9c-8e2a-1d5f3a7c9b0e");

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
        // Delay registration slightly so all plugin assemblies are guaranteed to be loaded
        // into their AssemblyLoadContexts before we search for File Transformation.
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            TryRegister();
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void TryRegister()
    {
        try
        {
            // File Transformation loads into a separate AssemblyLoadContext — search all of them
            var ftAssembly = AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .FirstOrDefault(a => a.FullName?.Contains(".FileTransformation", StringComparison.OrdinalIgnoreCase) ?? false);

            if (ftAssembly is null)
            {
                _logger.LogWarning(
                    "[AbyssSpotlight] File Transformation plugin not found. " +
                    "Spotlight banner will not appear. " +
                    "Install it from: https://www.iamparadox.dev/jellyfin/plugins/manifest.json");
                return;
            }

            _logger.LogInformation("[AbyssSpotlight] Found File Transformation assembly: {Name}", ftAssembly.FullName);

            var interfaceType = ftAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            if (interfaceType is null)
            {
                _logger.LogError("[AbyssSpotlight] PluginInterface type not found in File Transformation assembly.");
                return;
            }

            var registerMethod = interfaceType.GetMethod(
                "RegisterTransformation",
                BindingFlags.Public | BindingFlags.Static);

            if (registerMethod is null)
            {
                _logger.LogError("[AbyssSpotlight] RegisterTransformation method not found on PluginInterface.");
                return;
            }

            // Payload format as documented by File Transformation README
            var payload = JsonSerializer.Serialize(new
            {
                id                = TransformId.ToString(),
                fileNamePattern   = @"home-html\..+\.chunk\.js$",
                callbackAssembly  = typeof(SpotlightChunkTransformer).Assembly.FullName,
                callbackClass     = typeof(SpotlightChunkTransformer).FullName,
                callbackMethod    = nameof(SpotlightChunkTransformer.Transform)
            });

            _logger.LogInformation("[AbyssSpotlight] Registering transformation. Payload: {Payload}", payload);
            registerMethod.Invoke(null, new object?[] { payload });
            _logger.LogInformation("[AbyssSpotlight] Spotlight transformation registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AbyssSpotlight] Failed to register file transformation.");
        }
    }
}

/// <summary>
/// Callback invoked by File Transformation for every HTTP response matching home-html.*.chunk.js.
/// Must be public static — invoked via reflection from a different AssemblyLoadContext.
/// </summary>
public static class SpotlightChunkTransformer
{
    /// <summary>
    /// Receives the chunk file contents as JSON <c>{"contents":"..."}</c>,
    /// injects the Spotlight iframe after the known anchor, and returns the modified JSON.
    /// </summary>
    public static string Transform(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var contents = doc.RootElement.GetProperty("contents").GetString() ?? string.Empty;

            // Don't double-inject
            if (contents.Contains("abyss-spotlight-frame", StringComparison.Ordinal))
                return payload;

            // Anchor not found — Jellyfin may have updated its chunk; return unchanged
            if (!contents.Contains(SpotlightTransformService.InjectionAnchor, StringComparison.Ordinal))
                return payload;

            var patched = contents.Replace(
                SpotlightTransformService.InjectionAnchor,
                SpotlightTransformService.InjectionAnchor + SpotlightTransformService.IframeSnippet,
                StringComparison.Ordinal);

            return JsonSerializer.Serialize(new { contents = patched });
        }
        catch
        {
            // Never break Jellyfin — return original on any error
            return payload;
        }
    }
}