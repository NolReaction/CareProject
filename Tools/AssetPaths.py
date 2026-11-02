"""Resolve authored assets by the V4 catalog; never regenerate an old-path placeholder."""
from pathlib import Path
import json
import re


def asset_path(root, relative):
    root = Path(root)
    manifest = root / 'Assets/IntercomMVP/Editor/AssetOrganizationV4.json'
    if not manifest.exists():
        return root / relative
    for entry in json.loads(manifest.read_text())['files']:
        if entry['legacyPath'] != relative:
            continue
        # Preserve a deliberately relocated resource, matching the Editor GUID resolver.
        for candidate in (root / entry['destination'], root / relative):
            meta = Path(str(candidate) + '.meta')
            if meta.exists() and re.search(r'^guid: ' + entry['guid'] + r'$', meta.read_text(), re.M):
                return candidate
        for meta in (root / 'Assets').rglob('*.meta'):
            if re.search(r'^guid: ' + entry['guid'] + r'$', meta.read_text(), re.M):
                return Path(str(meta)[:-5])
        return root / entry['destination']
    return root / relative
