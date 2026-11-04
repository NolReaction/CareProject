#!/usr/bin/env python3
"""Install V9 missions over the authored V8 scene without rebuilding its map.

The delivered scene is already installed. Repeating this command preserves the
mission profiles, spawn transforms and existing authored component settings.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid

import AuthorNpcScene as npc
from UnitySceneIds import SceneIdAllocator, validate_scene_ids

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'
MISSIONS = ROOT / 'Assets/IntercomMVP/Content/Missions'
SPAWNS = [(-14.5, 0, 1), (-11, 0, 7), (-6, 0, 8), (0, 0, 12),
          (7.5, 0, 8), (13.5, 0, 0), (12, 0, -7), (2, 0, -12), (-11, 0, -12)]
ALLY = (2, 0, 11)
EXTRACTION = (-7, 0, -12)


def metadata(path, main=None):
    path = Path(path)
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)[1]
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'CareProject/missions-v9/' + path.relative_to(ROOT).as_posix()).hex
    text = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if path.is_dir(): text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif main is not None: text += f'NativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {main}\n'
    meta.write_text(text)
    return guid


def profile(name, mode):
    path = MISSIONS / (name + '.asset')
    if not path.exists():
        values = dict(mode=mode, displayName='Уничтожить 50 роботов' if mode == 0 else 'Спасение',
                      killTarget=50, maximumAlive=6 if mode == 0 else 5,
                      waveSize=10 if mode == 0 else 6, spawnInterval=1.2 if mode == 0 else 1.6,
                      waveRestSeconds=2.5 if mode == 0 else 3, minimumSpawnDistance=5,
                      visibleSpawnEvery=3, minimumVisibleSpawnDistance=5.5, visibleSpawnWarningSeconds=.35,
                      rescueEnemySupply=30, allyHealth=60, allyMoveSpeed=3.4,
                      recruitDistance=1.6, recruitSeconds=.6, extractionRadius=2, allyWaitDistance=8)
        text = npc.YAML + '--- !u!114 &11400000\nMonoBehaviour:\n' + npc.common(0)
        text += '  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        text += f'  m_Script: {{fileID: 11500000, guid: {npc.script("IntercomMissionDefinition")}, type: 3}}\n'
        text += f'  m_Name: {name}\n  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.IntercomMissionDefinition\n'
        text += ''.join(f'  {key}: {value}\n' for key, value in values.items())
        path.write_text(text)
    return metadata(path, 11400000)


def ensure_assets():
    MISSIONS.mkdir(parents=True, exist_ok=True)
    metadata(MISSIONS)
    profiles = {name: profile(name, mode) for name, mode in [('Kill50', 0), ('Rescue', 1)]}
    enemy = ROOT / 'Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset'
    text = enemy.read_text()
    # Only migrate known V8 defaults; authored changes survive explicit reruns.
    upgrades = {'attackRange': (1.15, 1.05), 'windupSeconds': (.45, .3),
                'recoverySeconds': (.65, .48), 'moveSpeed': (2.25, 2.65), 'repathSeconds': (.35, .32),
                'detectionRadius': (10, 14), 'loseTargetSeconds': (2.5, 6), 'leashRadius': (18, 40)}
    for key, (old, new) in upgrades.items():
        match = re.search(r'^  ' + key + r': ([^\n]+)$', text, re.M)
        if match and float(match[1]) == old:
            text = re.sub(r'^  ' + key + r': [^\n]+$', f'  {key}: {new}', text, flags=re.M)
    for key, value in {'attackCommitSeconds': .08, 'attackTrackingTurnSpeed': 900,
                       'separationStrength': .9, 'stuckRecoverySeconds': .4}.items():
        if not re.search(r'^  ' + key + r': ', text, re.M): text += f'  {key}: {value}\n'
    enemy.write_text(text)
    return profiles, metadata(enemy, 11400000)


def set_field(block, key, value):
    line = f'  {key}: {value}\n'
    if re.search(r'^  ' + re.escape(key) + r': ', block, re.M):
        return re.sub(r'^  ' + re.escape(key) + r': [^\n]*\n', lambda _: line, block, flags=re.M)
    return block + line


def install(text, profiles, enemy_guid):
    scene = npc.Scene(text)
    scene.ids = SceneIdAllocator(scene.blocks, 8900000000)
    before = dict(scene.blocks)
    robot = scene.find_go('Robot')
    arena = scene.find_go('MVP_NpcSandbox')
    assert robot and arena, 'V9 requires the delivered V8 player and NPC sandbox.'
    arena_transform = scene.transform(arena)
    input_id = scene.component(robot, 'IntercomPlayerInput')
    health = scene.component(robot, 'IntercomHealth')
    motor = scene.component(robot, 'IntercomPlayerMotor')
    vitality = scene.component(robot, 'IntercomPlayerVitality')
    nav = scene.component(arena, 'IntercomNpcNavigation')
    legacy_arena = scene.component(arena, 'IntercomNpcArena')
    assert all([input_id, health, motor, vitality, nav, legacy_arena])
    director = scene.component(arena, 'IntercomMissionDirector')
    first_install = director is None
    markers = [scene.add_object('Mission Spawn ' + str(i + 1).zfill(2), arena_transform, p)[1]
               for i, p in enumerate(SPAWNS)]
    ally = scene.add_object('Rescue Ally Spawn', arena_transform, ALLY)[1]
    extraction = scene.add_object('Rescue Extraction', arena_transform, EXTRACTION)[1]
    if first_install:
        fields = ''.join(f'  {key}: {{fileID: {value}}}\n' for key, value in
                         [('playerHealth', health), ('playerMotor', motor), ('playerVitality', vitality), ('navigation', nav)])
        fields += f'  enemyDefinition: {{fileID: 11400000, guid: {enemy_guid}, type: 2}}\n  spawnPoints:\n'
        fields += ''.join(f'  - {{fileID: {point}}}\n' for point in markers)
        fields += f'  allySpawn: {{fileID: {ally}}}\n  extractionPoint: {{fileID: {extraction}}}\n'
        fields += f'  eliminationDefinition: {{fileID: 11400000, guid: {profiles["Kill50"]}, type: 2}}\n'
        fields += f'  rescueDefinition: {{fileID: 11400000, guid: {profiles["Rescue"]}, type: 2}}\n'
        director = scene.add_component(arena, 'IntercomMissionDirector', fields)
        scene.blocks[nav] = set_field(scene.blocks[nav], 'maximumSearchesPerFrame', '4')
    # Existing authoredSpawnPoints are deliberately never rewritten. Scene
    # designers can add ground/hatch points independently of perimeter markers.
    # The legacy bootstrap must not create three extra untracked enemies.
    scene.blocks[legacy_arena] = set_field(scene.blocks[legacy_arena], 'm_Enabled', '0')
    if not re.search(r'^  mission: \{fileID: [1-9]', scene.blocks[input_id], re.M):
        scene.blocks[input_id] = set_field(scene.blocks[input_id], 'mission', f'{{fileID: {director}}}')
    result = scene.text()
    counts = validate_scene_ids(result)
    changed = [i for i, block in before.items() if scene.blocks[i] != block]
    assert set(changed) <= {arena, arena_transform, input_id, nav, legacy_arena}, changed
    return result, dict(director=director, navigation=nav, legacyArena=legacy_arena,
                        input=input_id, spawnTransforms=markers, allyTransform=ally,
                        extractionTransform=extraction, existingBlocksChanged=changed,
                        unrelatedBlocksPreserved=len(before) - len(changed), localIds=counts)


def main():
    profiles, enemy = ensure_assets()
    original = SCENE.read_text()
    result, report = install(original, profiles, enemy)
    assert install(result, profiles, enemy)[0] == result, 'Repeated mission install is not idempotent.'
    SCENE.write_text(result)
    report.update(revision='missions-scene-v9', status='PASS',
                  sceneSha256=hashlib.sha256(result.encode()).hexdigest(),
                  baselineSceneSha256=hashlib.sha256(original.encode()).hexdigest(),
                  repeatInstallIdempotent=True,
                  scope='Serialized scene installation only; no Unity importer or runtime execution.')
    (ROOT / 'Validation/SceneMissionsV9Review.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__': main()
