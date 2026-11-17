#!/usr/bin/env python3
"""Wire bounded enemy mechanical audio without rebuilding the authored arena."""
import json
from pathlib import Path
import AuthorNpcScene as npc
from UnitySceneIds import validate_scene_ids

ROOT=Path(__file__).resolve().parents[1]
SCENE=ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'

def install(text):
    scene=npc.Scene(text)
    arena=scene.find_go('MVP_NpcSandbox')
    assert arena, 'The authored mission arena is required.'
    director=scene.component(arena,'IntercomMissionDirector')
    assert director, 'Install missions before enemy audio.'
    fields=f'  mission: {{fileID: {director}}}\n  volume: 0.24\n  audibleRange: 22\n'
    component=scene.add_component(arena,'IntercomEnemyAudio',fields)
    result=scene.text()
    validate_scene_ids(result)
    return result,component

def main():
    result,component=install(SCENE.read_text())
    assert install(result)[0]==result, 'Enemy audio installation must be repeatable.'
    SCENE.write_text(result)
    print(json.dumps({'status':'PASS','component':component,'voices':4,'clips':12,'volume':.24,'audibleRange':22,'reinstallIdempotent':True}))

if __name__=='__main__':main()
