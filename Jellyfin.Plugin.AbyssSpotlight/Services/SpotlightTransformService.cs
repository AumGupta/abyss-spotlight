using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Registers an in-memory file transformation with the File Transformation plugin
/// (IAmParadox27) to inject the Spotlight iframe into home-html.*.chunk.js.
/// Retries for up to 60 seconds so timing of plugin load order doesn't matter.
/// </summary>
public class SpotlightTransformService : IHostedService
{
    private readonly ILogger<SpotlightTransformService> _logger;

    // The anchor string present in home-html.*.chunk.js in Jellyfin 10.10.x
    // Confirmed from Abyss setup.ps1 and multiple spotlight implementations
    internal const string InjectionAnchor = "movie,series,book\">";

    // Iframe snippet injected after the anchor
    internal const string IframeSnippet =
        "<style>.abyss-spotlight-frame{" +
        "width:100%;display:block;border:none;" +
        "aspect-ratio:16/9;max-height:85vh;min-height:320px;" +
        "margin-bottom:-60px;pointer-events:auto;" +
        "}</style>" +
        "<iframe class=\"abyss-spotlight-frame\"" +
        " src=\"/abyss-spotlight/spotlight.html\"" +
        " tabindex=\"0\" allowfullscreen></iframe>";

    private static readonly Guid TransformId = new("b3c7a1e2-4f6d-4b9c-8e2a-1d5f3a7c9b0e");
    private CancellationTokenSource? _cts;

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
        if (config is not null && !config.EnableSpotlight)
        {
            _logger.LogInformation("[AbyssSpotlight] Spotlight disabled in config, skipping registration.");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => RegisterWithRetry(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task RegisterWithRetry(CancellationToken ct)
    {
        // Try every 5 seconds for up to 60 seconds — handles any plugin load order
        var attempts = 0;
        const int maxAttempts = 12;

        while (attempts < maxAttempts && !ct.IsCancellationRequested)
        {
            attempts++;
            await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

            _logger.LogInformation("[AbyssSpotlight] Attempting File Transformation registration (attempt {A}/{M})...", attempts, maxAttempts);

            if (TryRegister())
                return;
        }

        _logger.LogError(
            "[AbyssSpotlight] File Transformation plugin not found after {M} attempts. " +
            "Make sure it is installed from: https://www.iamparadox.dev/jellyfin/plugins/manifest.json " +
            "and that Jellyfin has been fully restarted.", maxAttempts);
    }

    private bool TryRegister()
    {
        try
        {
            // Dump all loaded assemblies to help diagnose missing File Transformation
            var allAssemblies = AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .Select(a => a.FullName ?? "unknown")
                .ToList();

            _logger.LogDebug("[AbyssSpotlight] Loaded assemblies: {Assemblies}", string.Join(", ", allAssemblies));

            var ftAssembly = AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .FirstOrDefault(a =>
                    (a.FullName?.Contains("FileTransformation", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.FullName?.Contains("File.Transformation", StringComparison.OrdinalIgnoreCase) ?? false));

            if (ftAssembly is null)
            {
                _logger.LogDebug("[AbyssSpotlight] File Transformation assembly not yet loaded.");
                return false;
            }

            _logger.LogInformation("[AbyssSpotlight] Found File Transformation: {Name}", ftAssembly.FullName);

            // Try both known type names
            Type? interfaceType =
                ftAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface") ??
                ftAssembly.GetTypes().FirstOrDefault(t =>
                    t.Name.Equals("PluginInterface", StringComparison.OrdinalIgnoreCase));

            if (interfaceType is null)
            {
                _logger.LogError("[AbyssSpotlight] PluginInterface type not found. Available types: {Types}",
                    string.Join(", ", ftAssembly.GetTypes().Select(t => t.FullName)));
                return false;
            }

            var registerMethod = interfaceType.GetMethod(
                "RegisterTransformation",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            if (registerMethod is null)
            {
                _logger.LogError("[AbyssSpotlight] RegisterTransformation method not found. Available methods: {Methods}",
                    string.Join(", ", interfaceType.GetMethods().Select(m => m.Name)));
                return false;
            }

            var payload = JsonSerializer.Serialize(new
            {
                id               = TransformId.ToString(),
                fileNamePattern  = @"home-html\..+\.chunk\.js",
                callbackAssembly = typeof(SpotlightChunkTransformer).Assembly.FullName,
                callbackClass    = typeof(SpotlightChunkTransformer).FullName,
                callbackMethod   = nameof(SpotlightChunkTransformer.Transform)
            });

            _logger.LogInformation("[AbyssSpotlight] Registering transformation with payload: {P}", payload);

            // RegisterTransformation may be static or instance — handle both
            var instance = registerMethod.IsStatic ? null : Activator.CreateInstance(interfaceType);
            registerMethod.Invoke(instance, new object?[] { payload });

            _logger.LogInformation("[AbyssSpotlight] ✓ Spotlight transformation registered successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AbyssSpotlight] Exception during transformation registration.");
            return false;
        }
    }
}

/// <summary>
/// Callback invoked by File Transformation plugin for each home-html.*.chunk.js response.
/// Must be public static — called via reflection from a separate AssemblyLoadContext.
/// </summary>
public static class SpotlightChunkTransformer
{
    /// <summary>
    /// Receives <c>{"contents":"..."}</c>, injects the Spotlight iframe, returns modified JSON.
    /// </summary>
    public static string Transform(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var contents = doc.RootElement.GetProperty("contents").GetString() ?? string.Empty;

            // Idempotency guard
            if (contents.Contains("abyss-spotlight-frame", StringComparison.Ordinal))
                return payload;

            if (!contents.Contains(SpotlightTransformService.InjectionAnchor, StringComparison.Ordinal))
                return payload; // anchor not found — return unchanged, never break Jellyfin

            var patched = contents.Replace(
                SpotlightTransformService.InjectionAnchor,
                SpotlightTransformService.InjectionAnchor + SpotlightTransformService.IframeSnippet,
                StringComparison.Ordinal);

            return JsonSerializer.Serialize(new { contents = patched });
        }
        catch
        {
            return payload; // never break Jellyfin
        }
    }
}