# Abyss Spotlight; Jellyfin Plugin

A Jellyfin plugin that delivers the **Abyss theme** and **Spotlight home banner** as a proper installable plugin; no `.exe`, no manual file editing, works on every platform.

![Abyss Spotlight Banner](https://raw.githubusercontent.com/AumGupta/abyss-jellyfin/main/docs/assets/images/preview.png)

---

## What it does

| Feature | Description |
|---|---|
| **Abyss CSS** | Automatically injects the Abyss stylesheet into Jellyfin's Custom CSS on startup |
| **Spotlight banner** | Cinematic Continue Watching banner; backdrop, metadata pills, resume button |
| **Config page** | Dashboard → Plugins → Abyss Spotlight; toggle features, customise colours and radius |
| **Cross-platform** | Works on Windows, Linux, macOS, Docker, Synology NAS, Unraid; anywhere Jellyfin runs |

---

## Requirements

- **Jellyfin 10.10.x**
- **[File Transformation plugin](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)** by IAmParadox27  
  *(required for the Spotlight iframe injection; the CSS-only mode works without it)*

---

## Installation

### 1. Add the File Transformation plugin (for Spotlight)

In Jellyfin Dashboard → Administration → Plugins → Repositories, add:

```
https://www.iamparadox.dev/jellyfin/plugins/manifest.json
```

Install **File Transformation** and restart Jellyfin.

### 2. Add this plugin's repository

Add another repository with your manifest URL:

```
https://raw.githubusercontent.com/YOUR_USERNAME/YOUR_REPO/main/Jellyfin.Plugin.AbyssSpotlight/manifest.json
```

Install **Abyss Spotlight** and restart Jellyfin.

### 3. Configure

Go to **Dashboard → Plugins → Abyss Spotlight** to:
- Toggle Spotlight and CSS application
- Customise accent colour, border radius, indicator colour
- View whether the CSS has been applied

---

## How it works

### Abyss CSS
The plugin reads Jellyfin's branding configuration directly via `IServerConfigurationManager`; no HTTP calls, no credentials. It prepends the Abyss `@import` line and any custom CSS variable overrides to your Custom CSS field.

### Spotlight injection
Instead of modifying `home-html.*.chunk.js` on disk (fragile, breaks on Jellyfin updates, requires write permissions), the plugin registers a **File Transformation** callback. When Jellyfin serves the chunk file over HTTP, the File Transformation plugin intercepts the response in-memory and injects the Spotlight `<iframe>` snippet. The chunk on disk is never touched.

### Spotlight assets
`spotlight.html` and `spotlight.css` are compiled into the plugin DLL as embedded resources and served by the plugin's own API controller at:

```
GET /abyss-spotlight/spotlight.html
GET /abyss-spotlight/spotlight.css
```

This means no files need to be copied anywhere; it works even in read-only container deployments.

---

## Manual build

```bash
cd Jellyfin.Plugin.AbyssSpotlight
dotnet build --configuration Release -o ./dist
```

Copy `dist/Jellyfin.Plugin.AbyssSpotlight.dll` to your Jellyfin plugins folder (e.g. `/config/plugins/AbyssSpotlight/`) and restart.

---

## Credits

- [Abyss theme](https://github.com/AumGupta/abyss-jellyfin) by AumGupta; MIT License
- [File Transformation plugin](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) by IAmParadox27; GPL-3.0
- [Jellyfin plugin template](https://github.com/jellyfin/jellyfin-plugin-template)

## License

MIT
