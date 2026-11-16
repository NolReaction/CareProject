#!/usr/bin/env python3
"""Check serialized V22 medkits and conservative yard reachability; no Unity physics claim."""
from collections import deque
import json
import math
from pathlib import Path
import re
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import AuthorNpcScene as npc
import InstallMedkits


def main():
    path=ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'
    text=path.read_text();scene=npc.Scene(text)
    arena=scene.find_go('MVP_NpcSandbox');medkits=scene.component(arena,'IntercomMedkitPickups')
    assert medkits
    block=scene.blocks[medkits]
    assert f'mission: {{fileID: {scene.component(arena,"IntercomMissionDirector")}}}' in block
    positions=[]
    for key in ['firstPosition','secondPosition']:
        values=re.search(r'^  '+key+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',block,re.M)
        positions.append(tuple(map(float,values.groups())))
    assert positions==[(-4.5,0,-9.5),(5,0,3)]
    assert '  healing: 35\n' in block and '  pickupRadius: 0.85\n' in block
    for key in ['bodyMaterial','accentMaterial','glowMaterial']:
        guid=re.search(r'^  '+key+r': \{fileID: 2100000, guid: ([0-9a-f]{32}), type: 2\}',block,re.M)[1]
        assert any(re.search(r'^guid: '+guid+r'$',p.read_text(),re.M) for p in (ROOT/'Assets').rglob('*.mat.meta'))
    assert InstallMedkits.install(text)[0]==text,'Repeated installation changed authored scene'
    layout=json.loads((ROOT/'Assets/IntercomMVP/Content/Environments/TutorialYard/Layout/DistrictLayout.json').read_text())
    obstacles=[]
    for group in layout['groups']:
        for b in group['blocks']:
            if not b['collider']:continue
            size=[a*v for a,v in zip(b.get('collisionSize',[1,1,1]),b['size'])]
            center=[a*v for a,v in zip(b.get('collisionCenter',[0,0,0]),b['size'])]
            c,s=math.cos(math.radians(b['yaw'])),math.sin(math.radians(b['yaw']))
            x=group['position'][0]+b['position'][0]+c*center[0]+s*center[2]
            y=group['position'][1]+b['position'][1]+center[1]
            z=group['position'][2]+b['position'][2]-s*center[0]+c*center[2]
            if y+size[1]/2 <= .18:continue
            hx=(abs(c)*size[0]+abs(s)*size[2])/2+.5
            hz=(abs(s)*size[0]+abs(c)*size[2])/2+.5
            obstacles.append((x,z,hx,hz,b['name']))
    for px,_,pz in positions:
        for x,z,hx,hz,name in obstacles:
            assert not(abs(px-x)<hx and abs(pz-z)<hz),(name,px,pz)
    def free(ix,iz):
        x,z=ix*.25,iz*.25
        return -17<=x<=17 and -15<=z<=15 and not any(abs(x-a)<hx and abs(z-b)<hz for a,b,hx,hz,_ in obstacles)
    start=(-28,-48);pending=deque([start]);seen={start}
    while pending:
        x,z=pending.popleft()
        for cell in [(x-1,z),(x+1,z),(x,z-1),(x,z+1)]:
            if cell not in seen and free(*cell):seen.add(cell);pending.append(cell)
    for x,_,z in positions:assert (round(x/.25),round(z/.25)) in seen,('unreachable medkit',x,z)
    print(json.dumps({'status':'PASS','medkits':positions,'healing':35,'component':medkits,'safeInflatedFootprints':True,
                      'reachableFromPlayerSpawn':True,'reinstallIdempotent':True,'checkedObstacles':len(obstacles),
                      'scope':'Serialized links, material references and conservative 0.25m grid connectivity; no Unity importer/PhysX.'},indent=2))


if __name__=='__main__':main()
