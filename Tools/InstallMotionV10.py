#!/usr/bin/env python3
"""Idempotent hero feedback wiring; preserves every unrelated scene block.

The shipped IntercomMVP scene is already wired. This is an authoring helper,
not a script that players or ZIP recipients need to run.
"""
from pathlib import Path
import hashlib
import json
import re
from InstallNpcDemo import Scene, metadata
from UnitySceneIds import validate_scene_ids

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/IntercomMVP/Scenes/IntercomMVP.unity'


def install(text):
    scene = Scene(text)
    original = dict(scene.blocks)
    robot = scene.find_go('Robot')
    if robot is None:
        raise ValueError('Robot root not found')
    links = {key: scene.component(robot, name) for key, name in [
        ('health', 'IntercomHealth'), ('vitality', 'IntercomPlayerVitality'),
        ('motor', 'IntercomPlayerMotor')]}
    if not all(links.values()):
        raise ValueError('Existing hero health / motor / vitality are required')
    fields = ''.join(f'  {key}: {{fileID: {value}}}\n' for key, value in links.items())
    fields += '  pitchDegrees: 5.5\n  rollDegrees: 4\n  fullStrengthDamage: 20\n  hurtClips:\n'
    for i in (1, 2):
        asset = ROOT / f'Assets/IntercomMVP/Audio/Player/ChassisHurt_0{i}.wav'
        if not asset.is_file():
            raise FileNotFoundError(asset)
        fields += f'  - {{fileID: 8300000, guid: {metadata(asset)}, type: 3}}\n'
    fields += '  volume: 0.38\n  soundCooldown: 0.16\n'
    feedback = scene.add_component(robot, 'IntercomPlayerHurtFeedback', fields)
    result = scene.text()
    changed = [key for key, value in original.items() if scene.blocks[key] != value]
    assert set(changed) <= {robot}, changed
    return result, dict(hero=robot, feedback=feedback, links=links,
        existingBlocksChanged=changed, unrelatedBlocksPreserved=len(original)-len(changed),
        localIds=validate_scene_ids(result))


def main():
    before = SCENE.read_text()
    after, report = install(before)
    assert install(after)[0] == after, 'Repeat installation changed authored data'
    SCENE.write_text(after)
    report.update(status='PASS', revision='motion-v10', repeatInstallIdempotent=True,
        sceneSha256=hashlib.sha256(after.encode()).hexdigest(),
        scope='Serialized references and preservation only; no Unity import or Play Mode.')
    (ROOT / 'Validation/SceneMotionV10Review.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
