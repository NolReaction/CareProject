#!/usr/bin/env python3
"""Offline catalog regression: relocation, cache refresh, custom moves, safe paths."""
from pathlib import Path
import json
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools'))
from AssetPaths import asset_path, clear_cache, script_path, CATALOG

checks = 0
def check(ok, label):
    global checks
    checks += 1
    assert ok, label

def main():
    with tempfile.TemporaryDirectory(prefix='careproject-paths-') as directory:
        root = Path(directory)
        old = 'Assets/IntercomMVP/Audio/Test.wav'
        new = 'Assets/IntercomMVP/Content/Shared/Audio/Test.wav'
        guid = 'e' * 32
        path = root / new
        path.parent.mkdir(parents=True)
        path.write_bytes(b'sound')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\n')
        catalog = root / CATALOG
        catalog.parent.mkdir(parents=True)
        data = dict(revision='architecture-v13', files=[dict(legacyPath=old, destination=new, guid=guid)])
        catalog.write_text(json.dumps(data))
        clear_cache()
        check(asset_path(root, old) == path, 'legacy name resolves canonical asset')
        check(asset_path(root, new) == path, 'canonical name resolves same asset')
        check(asset_path(root, 'Assets/IntercomMVP/Content/New.asset') == root/'Assets/IntercomMVP/Content/New.asset', 'unknown authored asset uses exact requested path')
        custom = root/'Assets/IntercomMVP/Custom/Test.wav'
        custom.parent.mkdir(parents=True)
        path.rename(custom); Path(str(path)+'.meta').rename(str(custom)+'.meta')
        check(asset_path(root, old) == custom, 'custom GUID relocation respected')
        second = custom.with_name('Again.wav')
        custom.rename(second); Path(str(custom)+'.meta').rename(str(second)+'.meta')
        check(asset_path(root, old) == second, 'cached custom index refreshes after another move')
        second.unlink();Path(str(second)+'.meta').unlink()
        check(asset_path(root, old) == path, 'missing asset chooses canonical destination, never old placeholder')
        data['files'][0]['destination'] = 'Assets/IntercomMVP/Content/Longer/Test.wav'
        catalog.write_text(json.dumps(data))
        check(asset_path(root, old) == root/data['files'][0]['destination'], 'changed catalog invalidates cached entries')
        for bad in ('../escape', '/tmp/escape', 'Assets/IntercomMVP/../escape', 'Assets\\IntercomMVP\\escape'):
            rejected = False
            try: asset_path(root, bad)
            except ValueError: rejected = True
            check(rejected, 'unsafe requested path rejected')
        data['files'][0]['destination'] = '../escape'
        catalog.write_text(json.dumps(data))
        rejected = False
        try: asset_path(root, old)
        except ValueError: rejected = True
        check(rejected, 'unsafe registry entry rejected')
        source = root/'Assets/IntercomMVP/Runtime/Feature/Component.cs'
        source.parent.mkdir(parents=True);source.write_text('// source')
        Path(str(source)+'.meta').write_text('guid: '+guid+'\n')
        check(script_path(root, 'Component') == source, 'nested component source found with original meta')
        (source.parent/'MissingMeta.cs').write_text('// no identity')
        rejected = False
        try: script_path(root, 'MissingMeta')
        except ValueError: rejected = True
        check(rejected, 'missing meta never synthesized')
        duplicate = source.parent.parent/'Other/Component.cs'
        duplicate.parent.mkdir();duplicate.write_text('// duplicate');Path(str(duplicate)+'.meta').write_text('guid: '+'f'*32+'\n')
        rejected = False
        try: script_path(root, 'Component')
        except ValueError: rejected = True
        check(rejected, 'ambiguous source rejected')
    print(f'PASS {checks} offline asset path/cache/source identity checks')

if __name__ == '__main__': main()
