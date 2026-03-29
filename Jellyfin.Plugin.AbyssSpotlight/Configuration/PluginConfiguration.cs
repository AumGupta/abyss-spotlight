using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AbyssSpotlight.Configuration;

/// <summary>
/// Plugin configuration for Abyss Spotlight.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the Spotlight banner is enabled.
    /// Requires the File Transformation plugin by IAmParadox27.
    /// </summary>
    public bool EnableSpotlight { get; set; } = true;

    /// <summary>
    /// Gets or sets the Abyss accent colour (R, G, B — no rgb() wrapper).
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
}