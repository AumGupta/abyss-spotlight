using Jellyfin.Plugin.AbyssSpotlight.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Runs on every Jellyfin startup and automatically injects the Abyss CSS
/// into Custom CSS branding. No user interaction required.
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
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            _logger.LogWarning("[AbyssSpotlight] Plugin instance unavailable — skipping startup.");
            return Task.CompletedTask;
        }

        if (config.ApplyAbyssCSS)
        {
            ApplyCSS(config);
        }
        else
        {
            _logger.LogInformation("[AbyssSpotlight] CSS injection disabled by user config.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ApplyCSS(PluginConfiguration config)
    {
        try
        {
            // GetConfiguration() in Jellyfin 10.10 is non-generic — must pass Type and cast
            var branding = (BrandingOptions)_configManager.GetConfiguration("branding");
            var existing = branding.CustomCss ?? string.Empty;

            var overrides = BuildOverrides(config);
            var ourBlock  = $"{AbyssMarker}\n{AbyssImport}{overrides}";

            string updated;

            if (existing.Contains(AbyssImport))
            {
                updated = ReplaceOurBlock(existing, ourBlock);

                if (updated == existing)
                {
                    _logger.LogInformation("[AbyssSpotlight] CSS already up to date.");
                    EnsureAppliedFlag(config);
                    return;
                }

                _logger.LogInformation("[AbyssSpotlight] Updating Abyss CSS overrides.");
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

            EnsureAppliedFlag(config);
            _logger.LogInformation("[AbyssSpotlight] Abyss CSS applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AbyssSpotlight] Failed to apply Abyss CSS.");
        }
    }

    private static void EnsureAppliedFlag(PluginConfiguration config)
    {
        if (!config.CSSApplied)
        {
            config.CSSApplied = true;
            Plugin.Instance!.SaveConfiguration();
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