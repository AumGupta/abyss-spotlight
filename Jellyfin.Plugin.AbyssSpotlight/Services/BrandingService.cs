using Jellyfin.Plugin.AbyssSpotlight.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Automatically injects the Abyss CSS @import into Jellyfin's Custom CSS on every startup.
/// Idempotent — safe to run repeatedly. Uses Jellyfin's internal config manager,
/// no HTTP calls or credentials required. Works on every platform.
/// </summary>
public class BrandingService : IHostedService
{
    private readonly IServerConfigurationManager _configManager;
    private readonly ILogger<BrandingService> _logger;

    private const string AbyssImport = "@import url('https://cdn.jsdelivr.net/gh/AumGupta/abyss-jellyfin@main/abyss.css');";
    private const string AbyssMarker = "/* Abyss Spotlight plugin */";

    /// <summary>
    /// Initializes a new instance of the <see cref="BrandingService"/> class.
    /// </summary>
    public BrandingService(
        IServerConfigurationManager configManager,
        ILogger<BrandingService> logger)
    {
        _configManager = configManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ApplyCSS();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ApplyCSS()
    {
        try
        {
            var branding = (BrandingOptions)_configManager.GetConfiguration("branding");
            var existing = branding.CustomCss ?? string.Empty;

            var config   = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            var overrides = BuildOverrides(config);
            var ourBlock  = $"{AbyssMarker}\n{AbyssImport}{overrides}";

            string updated;

            if (existing.Contains(AbyssImport))
            {
                updated = ReplaceOurBlock(existing, ourBlock);
                if (updated == existing)
                {
                    _logger.LogInformation("[AbyssSpotlight] Abyss CSS already up to date.");
                    return;
                }
                _logger.LogInformation("[AbyssSpotlight] Refreshing Abyss CSS overrides.");
            }
            else
            {
                updated = string.IsNullOrWhiteSpace(existing)
                    ? ourBlock
                    : $"{ourBlock}\n\n{existing.TrimStart()}";
                _logger.LogInformation("[AbyssSpotlight] Injecting Abyss CSS for the first time.");
            }

            branding.CustomCss = updated;
            _configManager.SaveConfiguration("branding", branding);
            _logger.LogInformation("[AbyssSpotlight] Abyss CSS applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AbyssSpotlight] Failed to apply Abyss CSS.");
        }
    }

    private static string BuildOverrides(PluginConfiguration config)
    {
        bool customAccent    = config.AccentColor    != "245, 245, 247";
        bool customRadius    = config.BorderRadius   != "24px";
        bool customIndicator = config.IndicatorColor != "55, 55, 55";

        if (!customAccent && !customRadius && !customIndicator)
            return string.Empty;

        return $$"""


:root {
    --abyss-accent: {{config.AccentColor}};
    --abyss-radius: {{config.BorderRadius}};
    --abyss-indicator: {{config.IndicatorColor}};
}
""";
    }

    private static string ReplaceOurBlock(string existing, string newBlock)
    {
        var start = existing.IndexOf(AbyssMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            var importIdx = existing.IndexOf(AbyssImport, StringComparison.Ordinal);
            if (importIdx < 0) return existing;
            var lineEnd = existing.IndexOf('\n', importIdx + AbyssImport.Length);
            var tail = lineEnd >= 0 ? existing[(lineEnd + 1)..] : string.Empty;
            return $"{newBlock}\n\n{tail.TrimStart()}";
        }

        var blockEnd = existing.IndexOf("\n\n", start + AbyssMarker.Length, StringComparison.Ordinal);
        var userCss  = blockEnd >= 0 ? existing[(blockEnd + 2)..].TrimStart() : string.Empty;

        return string.IsNullOrWhiteSpace(userCss)
            ? newBlock
            : $"{newBlock}\n\n{userCss}";
    }
}