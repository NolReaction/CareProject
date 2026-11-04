#!/usr/bin/env python3
"""Repository architecture contracts; does not execute Unity's importer.

Run from any directory: python Validation/check_architecture.py
"""
from pathlib import Path
import argparse
import json
import re
import xml.etree.ElementTree as ET

PURE_FILES = {
    'IntercomCameraTracking.cs', 'IntercomCharacterAnimationMath.cs',
    'IntercomFootstepClock.cs', 'IntercomFrameCapture.cs', 'IntercomGroundMath.cs',
    'IntercomHealthState.cs', 'IntercomHudLayout.cs', 'IntercomHurtResponse.cs',
    'IntercomImpactAudio.cs', 'IntercomInputCancellation.cs',
    'IntercomLocomotionCadence.cs', 'IntercomLocomotionDirection.cs',
    'IntercomMagazineState.cs', 'IntercomMissionState.cs', 'IntercomNavGrid.cs',
    'IntercomNpcBrain.cs', 'IntercomOcclusionGeometry.cs', 'IntercomOcclusionMath.cs',
    'IntercomOcclusionState.cs', 'IntercomProjectileHitChoice.cs',
    'IntercomPropState.cs', 'IntercomReloadControl.cs', 'IntercomTeam.cs',
    'IntercomTracerStyle.cs', 'IntercomTurnStepState.cs', 'IntercomTwinStickState.cs',
    'IntercomWeaponBalance.cs', 'IntercomWeaponPoseClock.cs',
}
DECLARATION = re.compile(r'\b(?:class|struct|enum|interface)\s+(I?Intercom\w+)')
REFERENCE = re.compile(r'\bI?Intercom\w+\b')
GUID = re.compile(r'^guid: ([0-9a-f]{32})$', re.M)


def unity_visible(path, assets):
    # Unity does not import dot-prefixed files/directories. In particular,
    # transient sync files are not assets and must never be assigned a .meta.
    return not any(part.startswith('.') for part in path.relative_to(assets).parts)


def code_only(text):
    # Preserve newlines so diagnostics continue to identify the source lines.
    return re.sub(r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"',
                  lambda match: '\n' * match[0].count('\n'), text, flags=re.S)


def without_editor_blocks(text):
    """Remove explicit UNITY_EDITOR branches, retaining unknown feature branches.

    This guards the existing WorldAnchor gizmo exception without banning Editor
    APIs in a release-excluded block. It is a contract check, not a C# parser.
    """
    result, stack, active = [], [], True
    for line in text.splitlines():
        directive = re.match(r'\s*#(if|elif|else|endif)\b(.*)', line)
        if directive:
            kind, expression = directive[1], directive[2].strip()
            condition = False if expression == 'UNITY_EDITOR' else True if expression == '!UNITY_EDITOR' else None
            if kind == 'if':
                stack.append((active, condition)); active = active and condition is not False
            elif kind == 'else' and stack:
                parent, previous = stack[-1]; active = parent and previous is not True
            elif kind == 'elif' and stack:
                parent, previous = stack[-1]
                active = parent and previous is not True and condition is not False
                combined = True if previous is True or condition is True else False if previous is False and condition is False else None
                stack[-1] = (parent, combined)
            elif kind == 'endif' and stack:
                active, _ = stack.pop()
            result.append('')
        else:
            result.append(line if active else '')
    return '\n'.join(result)


def audit(root, allow_flat=False):
    root = Path(root).resolve()
    project = root / 'Assets/IntercomMVP'
    failures, checks = [], 0

    def check(condition, message):
        nonlocal checks
        checks += 1
        if not condition: failures.append(message)

    runtime = sorted(path for path in (project / 'Runtime').rglob('*.cs') if unity_visible(path, root / 'Assets'))
    editor = sorted(path for path in (project / 'Editor').rglob('*.cs') if unity_visible(path, root / 'Assets'))
    check(bool(runtime), 'Runtime sources not found')
    check(bool(editor), 'Editor sources not found')
    if not allow_flat:
        for folder in ('Runtime', 'Editor'):
            check(not [path for path in (project / folder).glob('*.cs') if unity_visible(path, root / 'Assets')],
                  f'{folder} contains loose C# scripts instead of feature folders')
    declarations, declaration_paths = {}, {}
    for path in runtime:
        text = code_only(path.read_text())
        for name in DECLARATION.findall(text):
            declarations.setdefault(name, set()).add(path.name)
            declaration_paths.setdefault(name, set()).add(path)
        release = code_only(without_editor_blocks(path.read_text()))
        check(not re.search(r'\b(?:UnityEditor|AssetDatabase|EditorApplication|EditorUtility)\b', release),
              f'Runtime depends on an Editor API outside UNITY_EDITOR: {path.relative_to(root)}')
        check(not re.search(r'\bResources\s*\.\s*Load', release),
              f'Runtime introduces a resource path dependency: {path.relative_to(root)}')
        check(not re.search(r'\bnamespace\s+IntercomMvp\s*\.', text),
              f'Serialized namespace changed without an explicit migration: {path.relative_to(root)}')

    # Damage and surface contracts are shared by player, enemies and props.
    # Their implementation must not depend back on a specific gameplay feature.
    shared = project / 'Runtime/Shared'
    for path in runtime:
        if not path.is_relative_to(shared): continue
        for target in set(REFERENCE.findall(code_only(path.read_text()))) - {'IntercomMvp'}:
            locations = declaration_paths.get(target, set())
            if locations:
                check(any(item.is_relative_to(shared) for item in locations),
                      f'Shared layer depends on a feature: {path.name} -> {target}')

    for name in sorted(PURE_FILES):
        found = [path for path in runtime if path.name == name]
        check(len(found) == 1, f'Pure model has zero or duplicate sources: {name}')
        if len(found) != 1: continue
        text = code_only(found[0].read_text())
        check(not re.search(r'\b(?:UnityEngine|UnityEditor|MonoBehaviour|ScriptableObject)\b', text),
              f'Pure model depends on Unity: {name}')
        for target in set(REFERENCE.findall(text)) - {'IntercomMvp'}:
            paths = declarations.get(target, set())
            if paths:
                check(bool(paths & PURE_FILES), f'Pure model {name} depends on engine component {target}')

    for path in project.rglob('*.cs'):
        if not unity_visible(path, root / 'Assets'): continue
        check('Runtime' in path.relative_to(project).parts or 'Editor' in path.relative_to(project).parts,
              f'C# script is stored among art/content assets: {path.relative_to(root)}')

    # A partial component is one owner of a pose/state even when split across files.
    for family in [
        ['IntercomNpcController.cs', 'IntercomNpcFactory.cs'],
        ['IntercomGroundFeedback.cs', 'IntercomGroundFeedbackRunning.cs',
         'IntercomGroundFeedbackSoles.cs', 'IntercomGroundFeedbackTurning.cs'],
    ]:
        parents = {path.parent for path in runtime if path.name in family}
        check(len(parents) == 1, f'Partial component family spans feature folders: {family[0]}')

    guids = {}
    for path in sorted((root / 'Assets').rglob('*.meta')):
        if not unity_visible(path, root / 'Assets'): continue
        match = GUID.search(path.read_text())
        check(match is not None, f'Asset metadata lacks GUID: {path.relative_to(root)}')
        check(Path(str(path)[:-5]).exists(), f'Orphan metadata: {path.relative_to(root)}')
        if match:
            check(match[1] not in guids, f'Duplicate GUID {match[1]}: {path.relative_to(root)} / {guids.get(match[1])}')
            guids[match[1]] = str(path.relative_to(root))
    for path in project.rglob('*'):
        if not unity_visible(path, root / 'Assets'): continue
        if path.is_file() and path.suffix != '.meta':
            check(Path(str(path) + '.meta').is_file(), f'Asset lacks metadata: {path.relative_to(root)}')

    # GUID fallback must not hide a broken documented entry point. The navigator
    # declares both the stable identity and the canonical location of each asset.
    for paths_source in editor:
        if paths_source.name != 'IntercomProjectPaths.cs': continue
        keys = re.findall(r'new\s+AssetKey\(\s*"[^"]*"\s*,\s*"([0-9a-f]{32})"\s*,\s*"(Assets/[^"]+)"\s*\)',
                          paths_source.read_text())
        check(bool(keys), 'Project navigator contains no named asset entry points')
        for guid, relative in keys:
            check((root / relative).is_file(), f'Navigator canonical asset path does not exist: {relative}')
            sidecar = root / (relative + '.meta')
            match = GUID.search(sidecar.read_text()) if sidecar.is_file() else None
            check(match is not None and match[1] == guid,
                  f'Navigator asset GUID/path mismatch: {relative}')

    for project_file in (root / 'Validation').glob('*Tests/*.csproj'):
        document = ET.parse(project_file)
        for tag in ('Compile', 'Content'):
            for entry in document.iter(tag):
                if tag == 'Content' and entry.attrib.get('Condition', '').startswith('Exists('): continue
                include = (project_file.parent / entry.attrib['Include']).resolve()
                check(include.is_file(), f'Broken {tag} path in {project_file.name}: {entry.attrib["Include"]}')

    settings = root / 'ProjectSettings/EditorBuildSettings.asset'
    if settings.is_file():
        for asset, guid in re.findall(r'^\s+path: (Assets/[^\n]+)\n\s+guid: ([0-9a-f]{32})', settings.read_text(), re.M):
            check((root / asset).is_file(), f'Build scene path does not exist: {asset}')
            sidecar = root / (asset + '.meta')
            match = GUID.search(sidecar.read_text()) if sidecar.is_file() else None
            check(match is not None and match[1] == guid, f'Build scene GUID/path mismatch: {asset}')

    return dict(status='PASS' if not failures else 'FAIL', checks=checks,
                runtimeSources=len(runtime), editorSources=len(editor), pureModels=len(PURE_FILES),
                assetGuids=len(guids), failures=failures,
                scope='Repository layout, source contracts, metadata and path integrity; not Unity compilation/import')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--allow-flat-baseline', action='store_true')
    args = parser.parse_args()
    result = audit(args.root, args.allow_flat_baseline)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    raise SystemExit(result['status'] != 'PASS')


if __name__ == '__main__': main()
