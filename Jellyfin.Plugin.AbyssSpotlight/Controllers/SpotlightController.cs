using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Controllers;

/// <summary>
/// Serves the embedded Spotlight web assets (HTML + CSS).
/// Config GET/POST is handled by Jellyfin's built-in plugin configuration API:
///   GET  /Plugins/{pluginId}/Configuration
///   POST /Plugins/{pluginId}/Configuration
/// </summary>
[ApiController]
[Route("abyss-spotlight")]
public class SpotlightController : ControllerBase
{
    private static readonly Assembly ResourceAssembly = typeof(SpotlightController).Assembly;
    private const string ResourcePrefix = "Jellyfin.Plugin.AbyssSpotlight.Web";

    private readonly ILogger<SpotlightController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotlightController"/> class.
    /// </summary>
    public SpotlightController(ILogger<SpotlightController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Serves spotlight.html — the iframe content. AllowAnonymous because the iframe
    /// loads before the Jellyfin auth context is available; the HTML reads the token itself.
    /// </summary>
    [HttpGet("spotlight.html")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSpotlightHtml()
        => ServeEmbeddedResource("spotlight.html", "text/html");

    /// <summary>
    /// Serves spotlight.css.
    /// </summary>
    [HttpGet("spotlight.css")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSpotlightCss()
        => ServeEmbeddedResource("spotlight.css", "text/css");

    private IActionResult ServeEmbeddedResource(string fileName, string contentType)
    {
        var resourceName = $"{ResourcePrefix}.{fileName}";
        var stream = ResourceAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            _logger.LogError("[AbyssSpotlight] Embedded resource not found: {Resource}", resourceName);
            return NotFound($"Resource '{fileName}' not found.");
        }

        return File(stream, contentType);
    }
}