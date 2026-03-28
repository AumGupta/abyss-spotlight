using Jellyfin.Plugin.AbyssSpotlight.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Runs on every Jellyfin startup and automatically:
///   1. Injects the Abyss CSS @import into Custom CSS (idempotent)
///   2. Applies custom CSS variable overrides if the user has changed them
///
/// No user interaction required — everything is applied the moment the plugin loads.
/// Uses Jellyfin's internal IServerConfigurationManager, so no HTTP calls or
/// credentials are needed. Works on every platform.
/// </summary>
public class BrandingService : IHostedService
{
    private readonly IServerConfigurationManager _configManager;
    private readonly ILogger<BrandingService> _logger;

    private const string AbyssImport  = "@import url('https://cdn.jsdelivr.net/gh/AumGupta/abyss-jellyfin@main/abyss.css');";
    private const string AbyssMarker  = "/* Abyss Spotlight plugin */";

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

    // ── CSS injection ─────────────────────────────────────────────────────────

    private void ApplyCSS(PluginConfiguration config)
    {
        try
        {
            var branding = _configManager.GetConfiguration<BrandingOptions>("branding");
            var existing = branding.CustomCss ?? string.Empty;

            var overrides  = BuildOverrides(config);
            var ourBlock   = $"{AbyssMarker}\n{AbyssImport}{overrides}";

            string updated;

            if (existing.Contains(AbyssImport))
            {
                // Already injected — just refresh the overrides block if they changed
                updated = ReplaceOurBlock(existing, ourBlock);

                if (updated == existing)
                {
                    _logger.LogInformation("[AbyssSpotlight] CSS already up to date, nothing to do.");
                    EnsureAppliedFlag(config);
                    return;
                }

                _logger.LogInformation("[AbyssSpotlight] Updating Abyss CSS overrides.");
            }
            else
            {
                // First install — prepend our block, preserve any existing user CSS below
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

    /// <summary>
    /// Builds the :root CSS override block. Returns empty string if all values are defaults.
    /// </summary>
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

    /// <summary>
    /// Replaces our managed CSS block while leaving any user CSS that follows untouched.
    /// </summary>
    private static string ReplaceOurBlock(string existing, string newBlock)
    {
        // Find start of our block (marker comment)
        var start = existing.IndexOf(AbyssMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            // Marker missing (manually edited) — replace import line only
            var importIdx = existing.IndexOf(AbyssImport, StringComparison.Ordinal);
            if (importIdx < 0) return existing;
            var lineEnd = existing.IndexOf('\n', importIdx + AbyssImport.Length);
            var tail = lineEnd >= 0 ? existing[(lineEnd + 1)..] : string.Empty;
            return $"{newBlock}\n\n{tail.TrimStart()}";
        }

        // Find end of our block — double newline separates it from user CSS
        var blockEnd = existing.IndexOf("\n\n", start + AbyssMarker.Length, StringComparison.Ordinal);
        var userCss  = blockEnd >= 0 ? existing[(blockEnd + 2)..].TrimStart() : string.Empty;

        return string.IsNullOrWhiteSpace(userCss)
            ? newBlock
            : $"{newBlock}\n\n{userCss}";
    }
}