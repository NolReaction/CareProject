"""Shared offline asset lookup. Resolve moved assets by GUID, never recreate legacy copies.

The catalog is cached per project and refreshed when its bytes change. Ordinary
lookups check the declared locations; one GUID index is built only for resources
the developer deliberately moved elsewhere.
"""
from pathlib import Path
import json
import re

_catalogs = {}
_indices = {}

CATALOG = 'Assets/IntercomMVP/Editor/Project/ProjectAssetsV13.json'


def clear_cache():
    _catalogs.clear()
    _indices.clear()


def _safe(relative):
    return (isinstance(relative, str) and relative.startswith('Assets/IntercomMVP/')
            and not Path(relative).is_absolute() and '\\' not in relative
            and '..' not in Path(relative).parts)


def _entries(root):
    candidates = [root / CATALOG]
    signature = tuple((str(p), p.stat().st_mtime_ns, p.stat().st_size)
                      for p in candidates if p.is_file())
    cached = _catalogs.get(root)
    if cached is not None and cached[0] == signature:
        return cached[1]
    table = {}
    for path in candidates:
        if not path.is_file():
            continue
        for entry in json.loads(path.read_text(encoding='utf-8')).get('files', []):
            if not all(entry.get(key) for key in ('legacyPath', 'destination', 'guid')):
                continue
            if not _safe(entry['legacyPath']) or not _safe(entry['destination']):
                raise ValueError('Catalog contains an unsafe Unity asset path')
            table[entry['legacyPath']] = entry
            table[entry['destination']] = entry
    _catalogs[root] = (signature, table)
    _indices.pop(root, None)
    return table


def _has_guid(path, guid):
    meta = Path(str(path) + '.meta')
    return path.exists() and meta.is_file() and re.search(
        r'^guid: ' + re.escape(guid) + r'$', meta.read_text(encoding='utf-8'), re.M)


def _guid_index(root, refresh=False):
    if refresh or root not in _indices:
        entries = {}
        for meta in (root / 'Assets').rglob('*.meta'):
            match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8'), re.M)
            if match:
                previous = entries.get(match[1])
                if previous is not None:
                    raise ValueError(f'Duplicate Unity GUID: {match[1]}')
                entries[match[1]] = Path(str(meta)[:-5])
        _indices[root] = entries
    return _indices[root]


def asset_path(root, relative):
    root = Path(root).resolve()
    relative = str(relative)
    if not _safe(relative):
        raise ValueError('Expected a project-relative Unity asset path')
    entry = _entries(root).get(relative)
    if entry is None:
        return root / relative
    for candidate in (root / entry['destination'], root / entry['legacyPath']):
        if _has_guid(candidate, entry['guid']):
            return candidate
    candidate = _guid_index(root).get(entry['guid'])
    if candidate is not None and _has_guid(candidate, entry['guid']):
        return candidate
    candidate = _guid_index(root, refresh=True).get(entry['guid'])
    return candidate if candidate is not None and _has_guid(candidate, entry['guid']) else root / entry['destination']


def script_path(root, name, editor=False):
    """Find a single existing source after feature-folder moves; never mint a script GUID."""
    if not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', name):
        raise ValueError('Expected a C# source basename')
    folder = Path(root) / 'Assets/IntercomMVP' / ('Editor' if editor else 'Runtime')
    matches = [p for p in folder.rglob(name + '.cs') if Path(str(p) + '.meta').is_file()]
    if len(matches) != 1:
        raise ValueError(f'Expected one source+meta for {name}, found {len(matches)}')
    return matches[0]
