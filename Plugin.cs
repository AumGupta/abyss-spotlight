using System.Reflection;
using Jellyfin.Plugin.AbyssSpotlight.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AbyssSpotlight;

/// <summary>
/// Abyss Spotlight plugin main class.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The unique identifier for this plugin.
    /// </summary>
    public static readonly Guid PluginGuid = new("4a7e9b2c-1f3d-4c8a-9e5f-6d0b2a3c4e5f");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the singleton instance of the plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Abyss Spotlight";

    /// <inheritdoc />
    public override Guid Id => PluginGuid;

    /// <inheritdoc />
    public override string Description => "Cinematic Spotlight home banner and Abyss theme integration for Jellyfin.";

    /// <summary>
    /// Gets all embedded web pages this plugin exposes to the Jellyfin dashboard.
    /// This registers the config page so it appears under Dashboard → Plugins.
    /// </summary>
    /// <returns>Enumerable of <see cref="PluginPageInfo"/>.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        // The HTML config page is embedded as a resource and served by Jellyfin automatically
        // when IHasWebPages is implemented. The name must match the embedded resource path.
        return new[]
        {
            new PluginPageInfo
            {
                Name = "AbyssSpotlightConfig",
                EmbeddedResourcePath = $"{GetType().Namespace}.Pages.config.html",
                EnableInMainMenu = false,
            }
        };
    }
}
