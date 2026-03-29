#!/usr/bin/env python3
"""
Bakes the Jellyfin plugin manifest.json with real values at CI time.
Jellyfin requires the sourceUrl to point to a ZIP file, not a raw DLL.
"""

import hashlib
import json
import os
from datetime import datetime, timezone

repo         = os.environ["GITHUB_REPOSITORY"]
ref_name     = os.environ["GITHUB_REF_NAME"]
tag          = ref_name.lstrip("v")
timestamp    = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
zip_path     = "./Jellyfin.Plugin.AbyssSpotlight.zip"
download_url = f"https://github.com/{repo}/releases/download/{ref_name}/Jellyfin.Plugin.AbyssSpotlight.zip"

# Jellyfin validates MD5 of the downloaded zip
with open(zip_path, "rb") as f:
    checksum = hashlib.md5(f.read()).hexdigest()

manifest = [
    {
        "category": "General",
        "description": (
            "Cinematic Spotlight home banner and Abyss theme integration. "
            "Shows your current Continue Watching item as a full-width backdrop banner "
            "with metadata pills and a resume button. Applies the Abyss CSS theme automatically."
        ),
        "guid": "4a7e9b2c-1f3d-4c8a-9e5f-6d0b2a3c4e5f",
        "imageUrl": "https://github.com/AumGupta/abyss-spotlight/blob/fe226c29df0115597e675b5dfac037d9604ba947/assets/banner.png?raw=true",
        "name": "Abyss Spotlight",
        "overview": "Cinematic Spotlight home banner and Abyss theme integration for Jellyfin.",
        "owner": "AumGupta",
        "targetAbi": "10.10.0.0",
        "timestamp": timestamp,
        "versions": [
            {
                "changelog": f"Release {tag}",
                "checksum": checksum,
                "targetAbi": "10.10.0.0",
                "timestamp": timestamp,
                "version": tag,
                "sourceUrl": download_url,
            }
        ],
    }
]

out_path = "Jellyfin.Plugin.AbyssSpotlight/manifest.json"
with open(out_path, "w") as f:
    json.dump(manifest, f, indent=2)

print(f"Manifest written to {out_path}")
print(f"  version:     {tag}")
print(f"  checksum:    {checksum}  (MD5 of zip)")
print(f"  sourceUrl:   {download_url}")
print(f"  timestamp:   {timestamp}")

github_output = os.environ.get("GITHUB_OUTPUT")
if github_output:
    with open(github_output, "a") as f:
        f.write(f"tag={tag}\n")