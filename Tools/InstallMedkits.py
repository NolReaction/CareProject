#!/usr/bin/env python3
"""Install the two reusable V22 test medkits without rebuilding or retuning the yard."""
import json
from pathlib import Path
import re

import AuthorNpcScene as npc
from UnitySceneIds import validate_scene_ids

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'


def install(text):
    scene = npc.Scene(text)
    arena = scene.find_go('MVP_NpcSandbox')
    assert arena, 'The delivered yard mission arena is required.'
    director = scene.component(arena, 'IntercomMissionDirector')
    assert director, 'Install missions before installing medkits.'
    definition = (ROOT / 'Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset').read_text()
    def material(name):
        return re.search(r'^  '+name+r': (\{[^\n]+\})$', definition, re.M)[1]
    fields = f'  mission: {{fileID: {director}}}\n'
    for target, source in [('bodyMaterial','bodyMaterial'),('accentMaterial','detailMaterial'),('glowMaterial','telegraphMaterial')]:
        fields += f'  {target}: {material(source)}\n'
    fields += '  firstPosition: {x: -4.5, y: 0, z: -9.5}\n  secondPosition: {x: 5, y: 0, z: 3}\n  healing: 35\n  pickupRadius: 0.85\n'
    component = scene.add_component(arena, 'IntercomMedkitPickups', fields)
    result = scene.text()
    validate_scene_ids(result)
    return result, component


def main():
    original=SCENE.read_text()
    result, component=install(original)
    assert install(result)[0] == result, 'Medkit installer must be idempotent.'
    SCENE.write_text(result)
    print(json.dumps({'status':'PASS','component':component,'medkits':2,'healing':35,
                      'positions':[[-4.5,0,-9.5],[5,0,3]],'reinstallIdempotent':True},indent=2))


if __name__ == '__main__':
    main()
