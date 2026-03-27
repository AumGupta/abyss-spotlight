using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AbyssSpotlight.Configuration;

/// <summary>
/// Plugin configuration for Abyss Spotlight.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the Abyss CSS should be applied to the Jellyfin branding.
    /// </summary>
    public bool ApplyAbyssCSS { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Spotlight banner is enabled.
    /// </summary>
    public bool EnableSpotlight { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the home sections should be automatically configured.
    /// </summary>
    public bool ConfigureHomeSections { get; set; } = true;

    /// <summary>
    /// Gets or sets the Abyss accent colour (R, G, B — no rgb() wrapper, comma separated).
    /// </summary>
    public string AccentColor { get; set; } = "245, 245, 247";

    /// <summary>
    /// Gets or sets the Abyss global border radius.
    /// </summary>
    public string BorderRadius { get; set; } = "24px";

    /// <summary>
    /// Gets or sets the Abyss indicator pill background colour (R, G, B).
    /// </summary>
    public string IndicatorColor { get; set; } = "55, 55, 55";

    /// <summary>
    /// Gets or sets a value indicating whether the Abyss CSS has been successfully applied.
    /// Used to track state across restarts.
    /// </summary>
    public bool CSSApplied { get; set; } = false;
}
