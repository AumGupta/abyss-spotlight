#!/usr/bin/env python3
"""
Bakes the Jellyfin plugin manifest.json with real values at CI time.
Reads: GITHUB_REPOSITORY, GITHUB_REF_NAME
Reads DLL: ./build/Jellyfin.Plugin.AbyssSpotlight.dll
Writes: Jellyfin.Plugin.AbyssSpotlight/manifest.json
"""

import hashlib
import json
import os
import sys
from datetime import datetime, timezone

repo      = os.environ["GITHUB_REPOSITORY"]          # e.g. AumGupta/abyss-spotlight
ref_name  = os.environ["GITHUB_REF_NAME"]            # e.g. v1.0.0.0
tag       = ref_name.lstrip("v")                     # e.g. 1.0.0.0
timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
dll_path  = "./build/Jellyfin.Plugin.AbyssSpotlight.dll"
download_url = f"https://github.com/{repo}/releases/download/{ref_name}/Jellyfin.Plugin.AbyssSpotlight.dll"

# Compute MD5 checksum
with open(dll_path, "rb") as f:
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
        "imageUrl": "https://raw.githubusercontent.com/AumGupta/abyss-jellyfin/main/docs/assets/favicon/apple-touch-icon.png",
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
print(f"  checksum:    {checksum}")
print(f"  sourceUrl:   {download_url}")
print(f"  timestamp:   {timestamp}")

# Write tag to GitHub output if running in Actions
github_output = os.environ.get("GITHUB_OUTPUT")
if github_output:
    with open(github_output, "a") as f:
        f.write(f"tag={tag}\n")