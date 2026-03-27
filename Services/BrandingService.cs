using Jellyfin.Plugin.AbyssSpotlight.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Services;

/// <summary>
/// Hosted service that applies the Abyss CSS to Jellyfin's branding configuration on startup.
/// Uses Jellyfin's internal IServerConfigurationManager; no HTTP calls, no credentials,
/// works on every platform (Windows, Linux, macOS, Docker, NAS, etc.).
/// </summary>
public class BrandingService : IHostedService
{
    private readonly IServerConfigurationManager _configManager;
    private readonly ILogger<BrandingService> _logger;

    // The @import line we inject. It is idempotent; we check for it before adding.
    private const string AbyssImport = "@import url('https://cdn.jsdelivr.net/gh/AumGupta/abyss-jellyfin@main/abyss.css');";
    private const string AbyssComment = "/* Applied by Abyss Spotlight plugin; https://github.com/AumGupta/abyss-jellyfin */";

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
            _logger.LogWarning("[AbyssSpotlight] Plugin instance not available, skipping branding setup.");
            return Task.CompletedTask;
        }

        if (config.ApplyAbyssCSS)
        {
            ApplyAbyssCSS(config);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ApplyAbyssCSS(PluginConfiguration config)
    {
        try
        {
            // Read current branding config from Jellyfin's own config system
            var brandingConfig = _configManager.GetConfiguration<BrandingOptions>("branding");
            var currentCss = brandingConfig.CustomCss ?? string.Empty;

            // Build the CSS block we want to inject
            var accentOverride = BuildCssOverrides(config);
            var desiredBlock = $"{AbyssComment}\n{AbyssImport}{accentOverride}";

            // Idempotency: if our import is already there, just update the overrides
            if (currentCss.Contains(AbyssImport))
            {
                _logger.LogInformation("[AbyssSpotlight] Abyss CSS import already present, ensuring overrides are up to date.");

                // Replace only our managed block; leave any user CSS after our block untouched
                var updatedCss = ReplaceAbyssBlock(currentCss, desiredBlock);
                if (updatedCss != currentCss)
                {
                    brandingConfig.CustomCss = updatedCss;
                    _configManager.SaveConfiguration("branding", brandingConfig);
                    _logger.LogInformation("[AbyssSpotlight] Abyss CSS overrides updated.");
                }

                config.CSSApplied = true;
                Plugin.Instance!.SaveConfiguration();
                return;
            }

            // Prepend our block so it comes first, user customisations below
            brandingConfig.CustomCss = string.IsNullOrWhiteSpace(currentCss)
                ? desiredBlock
                : $"{desiredBlock}\n\n{currentCss.TrimStart()}";

            _configManager.SaveConfiguration("branding", brandingConfig);
            config.CSSApplied = true;
            Plugin.Instance!.SaveConfiguration();

            _logger.LogInformation("[AbyssSpotlight] Abyss CSS applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AbyssSpotlight] Failed to apply Abyss CSS.");
        }
    }

    /// <summary>
    /// Builds the CSS variable override block from the plugin config.
    /// Returns an empty string if all values are defaults.
    /// </summary>
    private static string BuildCssOverrides(PluginConfiguration config)
    {
        // Only emit :root block if at least one value differs from the Abyss defaults
        bool hasCustomAccent = config.AccentColor != "245, 245, 247";
        bool hasCustomRadius = config.BorderRadius != "24px";
        bool hasCustomIndicator = config.IndicatorColor != "55, 55, 55";

        if (!hasCustomAccent && !hasCustomRadius && !hasCustomIndicator)
        {
            return string.Empty;
        }

        return $"""


:root {{
    --abyss-accent: {config.AccentColor};
    --abyss-radius: {config.BorderRadius};
    --abyss-indicator: {config.IndicatorColor};
}}
""";
    }

    /// <summary>
    /// Replaces the Abyss-managed block (between our comment marker and the next blank line)
    /// while leaving any user CSS that follows it untouched.
    /// </summary>
    private static string ReplaceAbyssBlock(string existingCss, string newBlock)
    {
        var commentIndex = existingCss.IndexOf(AbyssComment, StringComparison.Ordinal);
        if (commentIndex < 0)
        {
            // No comment marker found; just replace the import line directly
            var importIndex = existingCss.IndexOf(AbyssImport, StringComparison.Ordinal);
            if (importIndex < 0) return existingCss;
            var end = existingCss.IndexOf('\n', importIndex + AbyssImport.Length);
            var tail = end >= 0 ? existingCss[(end + 1)..] : string.Empty;
            return $"{newBlock}\n\n{tail.TrimStart()}";
        }

        // Find the end of our block (double newline = separation from user CSS)
        var blockEnd = existingCss.IndexOf("\n\n", commentIndex + AbyssComment.Length, StringComparison.Ordinal);
        var userCss = blockEnd >= 0 ? existingCss[(blockEnd + 2)..].TrimStart() : string.Empty;
        return string.IsNullOrWhiteSpace(userCss)
            ? newBlock
            : $"{newBlock}\n\n{userCss}";
    }
}
