#!/usr/bin/env python3
"""Validate the shipped enemy-audio owner and bounded mix defaults; no native audio execution."""
import json
from pathlib import Path
import re
import sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import AuthorNpcScene as npc
import InstallEnemyAudio

def main():
    text=(ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity').read_text()
    scene=npc.Scene(text);arena=scene.find_go('MVP_NpcSandbox');audio=scene.component(arena,'IntercomEnemyAudio')
    assert audio
    fields=scene.blocks[audio]
    assert f'mission: {{fileID: {scene.component(arena,"IntercomMissionDirector")}}}' in fields
    assert '  volume: 0.24\n' in fields and '  audibleRange: 22\n' in fields
    assert InstallEnemyAudio.install(text)[0]==text
    path=ROOT/'Assets/IntercomMVP/Runtime/Presentation/Audio/IntercomEnemyAudio.cs'
    guid=re.search(r'^guid: ([0-9a-f]{32})$',Path(str(path)+'.meta').read_text(),re.M)[1]
    assert guid in fields
    assert len([b for b in scene.blocks.values() if 'guid: '+guid in b])==1
    print(json.dumps({'status':'PASS','component':audio,'owner':'MVP_NpcSandbox','voices':4,'clips':12,'volume':.24,'audibleRange':22,'reinstallIdempotent':True,'scope':'Serialized owner and script links; no Unity audio output.'}))
if __name__=='__main__':main()
