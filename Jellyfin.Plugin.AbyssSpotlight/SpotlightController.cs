using System.Net.Mime;
using System.Reflection;
using Jellyfin.Plugin.AbyssSpotlight.Configuration;
using MediaBrowser.Controller.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AbyssSpotlight.Controllers;

/// <summary>
/// API controller for Abyss Spotlight.
///
/// <para>
/// Serves the embedded spotlight web assets (HTML + CSS) at:
///   GET /abyss-spotlight/spotlight.html
///   GET /abyss-spotlight/spotlight.css
///
/// These routes are what the injected iframe src="/web/abyss-spotlight/spotlight.html" points to.
/// Because these are served from the Jellyfin server itself, they work on every platform
/// regardless of where Jellyfin is installed or whether /jellyfin-web is writable.
/// </para>
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
    /// Serves the Spotlight HTML page (the iframe content).
    /// Anonymous access is required — the iframe loads before the user is authenticated
    /// in the page context; the spotlight.html itself reads the Jellyfin auth token
    /// from the parent window's localStorage.
    /// </summary>
    [HttpGet("spotlight.html")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSpotlightHtml()
        => ServeEmbeddedResource("spotlight.html", "text/html");

    /// <summary>
    /// Serves the Spotlight CSS file.
    /// </summary>
    [HttpGet("spotlight.css")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSpotlightCss()
        => ServeEmbeddedResource("spotlight.css", "text/css");

    /// <summary>
    /// Returns the current plugin configuration as JSON.
    /// Requires admin authentication.
    /// </summary>
    [HttpGet("config")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PluginConfiguration> GetConfig()
    {
        return Ok(Plugin.Instance?.Configuration ?? new PluginConfiguration());
    }

    /// <summary>
    /// Saves updated plugin configuration.
    /// Requires admin authentication.
    /// </summary>
    [HttpPost("config")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult SaveConfig([FromBody] PluginConfiguration config)
    {
        if (Plugin.Instance is null)
        {
            return BadRequest("Plugin not initialised.");
        }

        Plugin.Instance.Configuration.ApplyAbyssCSS = config.ApplyAbyssCSS;
        Plugin.Instance.Configuration.EnableSpotlight = config.EnableSpotlight;
        Plugin.Instance.Configuration.ConfigureHomeSections = config.ConfigureHomeSections;
        Plugin.Instance.Configuration.AccentColor = config.AccentColor;
        Plugin.Instance.Configuration.BorderRadius = config.BorderRadius;
        Plugin.Instance.Configuration.IndicatorColor = config.IndicatorColor;

        // Reset applied flag so BrandingService re-applies on next restart
        Plugin.Instance.Configuration.CSSApplied = false;
        Plugin.Instance.SaveConfiguration();

        _logger.LogInformation("[AbyssSpotlight] Configuration saved.");
        return NoContent();
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private IActionResult ServeEmbeddedResource(string fileName, string contentType)
    {
        var resourceName = $"{ResourcePrefix}.{fileName}";
        var stream = ResourceAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            _logger.LogError("[AbyssSpotlight] Embedded resource not found: {Resource}", resourceName);
            return NotFound($"Resource '{fileName}' not found.");
        }

        // Stream will be disposed by the framework after the response is written
        return File(stream, contentType);
    }
}
