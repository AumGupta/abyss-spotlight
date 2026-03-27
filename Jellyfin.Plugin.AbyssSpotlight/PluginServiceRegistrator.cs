using Jellyfin.Plugin.AbyssSpotlight.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AbyssSpotlight;

/// <summary>
/// Registers plugin services into the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // The BrandingService runs once at startup: applies Abyss CSS and home section config
        serviceCollection.AddHostedService<BrandingService>();

        // The SpotlightTransformService registers a file transformation with the FileTransformation
        // plugin (if present) to inject the spotlight iframe into home-html.*.chunk.js in-memory
        serviceCollection.AddHostedService<SpotlightTransformService>();
    }
}
