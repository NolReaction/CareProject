#!/usr/bin/env python3
"""Check V9 scene wiring, bounded profiles, ownership and repeat-safe installation."""
from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools'))
import AuthorMissionScene as installer
import AuthorNpcScene as npc
from UnitySceneIds import validate_scene_ids


def field(text, name):
    match = re.search(r'^  ' + re.escape(name) + r': (.*)$', text, re.M)
    assert match, name
    return match[1]


def local(text, name):
    return int(re.fullmatch(r'\{fileID: (-?\d+)\}', field(text, name))[1])


def scalar(text, name):
    value = float(field(text, name))
    assert math.isfinite(value), name
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-scene', type=Path)
    args = parser.parse_args()
    scene_path = ROOT / 'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'
    text = scene_path.read_text()
    scene = npc.Scene(text)
    robot = scene.find_go('Robot'); arena = scene.find_go('MVP_NpcSandbox')
    assert robot and arena
    director = scene.component(arena, 'IntercomMissionDirector')
    legacy = scene.component(arena, 'IntercomNpcArena')
    nav = scene.component(arena, 'IntercomNpcNavigation')
    input_id = scene.component(robot, 'IntercomPlayerInput')
    assert all([director, legacy, nav, input_id])
    assert scalar(scene.blocks[director], 'm_Enabled') == 1
    assert scalar(scene.blocks[legacy], 'm_Enabled') == 0, 'V8 bootstrap would create untracked enemies'
    for name, component in [('playerHealth', 'IntercomHealth'), ('playerMotor', 'IntercomPlayerMotor'), ('playerVitality', 'IntercomPlayerVitality')]:
        assert local(scene.blocks[director], name) == scene.component(robot, component)
    assert local(scene.blocks[director], 'navigation') == nav
    assert local(scene.blocks[input_id], 'mission') == director
    assert local(scene.blocks[nav], 'obstacleRoot') == scene.transform(scene.find_go('MVP_TestEnvironment'))
    assert scalar(scene.blocks[nav], 'maximumSearchesPerFrame') == 4
    points_match = re.search(r'^  spawnPoints:\n((?:  - \{fileID: \d+\}\n)+)', scene.blocks[director], re.M)
    points = [int(x) for x in re.findall(r'fileID: (\d+)', points_match[1])]
    assert len(points) == len(set(points)) == 9
    assert points == [scene.transform(scene.find_go('Mission Spawn ' + str(i + 1).zfill(2))) for i in range(9)]
    for name, go in [('allySpawn', 'Rescue Ally Spawn'), ('extractionPoint', 'Rescue Extraction')]:
        ident = local(scene.blocks[director], name)
        assert ident == scene.transform(scene.find_go(go))
        points.append(ident)
    positions = []
    for ident in points:
        assert local(scene.blocks[ident], 'm_Father') == scene.transform(arena)
        position = [float(x) for x in re.findall(r'[xyz]: ([^,}]+)', field(scene.blocks[ident], 'm_LocalPosition'))]
        assert len(position) == 3 and all(math.isfinite(x) for x in position)
        positions.append(position)
    assert math.dist(positions[-2], positions[-1]) > 15, 'Rescue should require an actual escort route'

    profiles = {}
    for name, mode, slot in [('Kill50', 0, 'eliminationDefinition'), ('Rescue', 1, 'rescueDefinition')]:
        path = ROOT / f'Assets/IntercomMVP/Content/Missions/{name}.asset'
        data = path.read_text(); guid = installer.metadata(path, 11400000)
        profiles[name] = guid
        assert f'guid: {guid},' in field(scene.blocks[director], slot)
        assert npc.script('IntercomMissionDefinition') in field(data, 'm_Script')
        assert scalar(data, 'mode') == mode
        assert scalar(data, 'killTarget') == 50
        assert 1 <= scalar(data, 'maximumAlive') <= 8
        assert 1 <= scalar(data, 'waveSize') <= 50
        assert .1 <= scalar(data, 'spawnInterval') <= 10
        assert 0 <= scalar(data, 'waveRestSeconds') <= 10
        assert 3 <= scalar(data, 'minimumSpawnDistance') <= 8
        assert scalar(data, 'allyHealth') == 60
        assert scalar(data, 'allyMoveSpeed') > 0
        assert .5 <= scalar(data, 'recruitDistance') <= 2
        assert .1 <= scalar(data, 'recruitSeconds') <= 2
        assert .5 <= scalar(data, 'extractionRadius') <= 3
    enemy_path = ROOT / 'Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset'
    enemy = enemy_path.read_text(); enemy_guid = installer.metadata(enemy_path, 11400000)
    assert f'guid: {enemy_guid},' in field(scene.blocks[director], 'enemyDefinition')
    for name, expected in [('moveSpeed', 3), ('windupSeconds', .3), ('recoverySeconds', .48), ('attackRange', 1.05),
                           ('attackCommitSeconds', .08), ('attackTrackingTurnSpeed', 900), ('repathSeconds', .32),
                           ('detectionRadius', 14), ('loseTargetSeconds', 4), ('leashRadius', 40)]:
        assert scalar(enemy, name) == expected, name
    # Ownership is bidirectional: no detached components or mismatched roots.
    components, owners = {}, {}
    for ident, block in scene.blocks.items():
        if block.startswith('--- !u!1 '):
            components[ident] = [int(x) for x in re.findall(r'^  - component: \{fileID: (-?\d+)\}', block, re.M)]
        match = re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}', block, re.M)
        if match and int(match[1]): owners[ident] = int(match[1])
    for go, attached in components.items():
        assert len(attached) == len(set(attached))
        for component in attached: assert owners[component] == go
    for component, go in owners.items(): assert component in components[go]
    assert installer.install(text, profiles, enemy_guid)[0] == text
    assert npc.install(text, enemy_guid)[0] == text, 'Legacy V8 installer must preserve V9 and keep old bootstrap disabled'
    report_path = ROOT / 'Validation/MissionSceneReview.json'
    report = json.loads(report_path.read_text()) if report_path.exists() else {}
    report.update(revision='missions-scene-v9', status='PASS', sceneSha256=hashlib.sha256(scene_path.read_bytes()).hexdigest(),
                  localIds=validate_scene_ids(text), repeatInstallIdempotent=True, legacyInstallPreservesV9=True,
                  legacySandboxDisabled=True, missionProfiles=list(profiles), spawnMarkers=positions[:-2],
                  allySpawn=positions[-2], extraction=positions[-1], heroStart=[-7, 0, -12],
                  maximumActiveEnemies={'Elimination': 6, 'Rescue': 5}, killGoal=50,
                  sourceSha256={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
                                for p in [scene_path, enemy_path] + list((ROOT/'Assets/IntercomMVP/Content/Missions').glob('*.asset'))},
                  scope='Serialized links, profile limits, component ownership and repeated installation. Separate MissionSceneRouteTests execute production navigation on authored map. No Unity runtime.')
    if args.baseline_scene:
        baseline = npc.Scene(args.baseline_scene.read_text())
        changed = [i for i, block in baseline.blocks.items() if scene.blocks.get(i) != block]
        assert set(changed) == {arena, scene.transform(arena), input_id, nav, legacy}, changed
        assert not set(baseline.blocks) - set(scene.blocks)
        report.update(existingBlocksChanged=changed, unrelatedBlocksPreserved=len(baseline.blocks)-len(changed),
                      baselineSceneSha256=hashlib.sha256(args.baseline_scene.read_bytes()).hexdigest())
    report_path.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: report[key] for key in ['status', 'localIds', 'legacySandboxDisabled', 'legacyInstallPreservesV9', 'maximumActiveEnemies', 'killGoal']}, indent=2))


if __name__ == '__main__': main()
