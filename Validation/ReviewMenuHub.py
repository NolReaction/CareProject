#!/usr/bin/env python3
"""Validate actual authored menu geometry/links without pretending to run Unity."""
from pathlib import Path
import json, math, re, sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import BuildMenuHub as hub
from UnitySceneIds import validate_scene_ids

def main():
 data=hub.generate();scene=hub.author.Scene((ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity').read_text())
 report=hub.configure(scene,data);first=scene.text();hub.configure(scene,data)
 assert scene.text()==first,'Repeated menu authoring changed current scene'
 validate_scene_ids(first)
 count=0
 for name,m in data['modules'].items():
  assert 0<len(m['vertices'])<65536
  assert all(math.isfinite(v) for vertex in m['vertices'] for v in vertex)
  for tri,mat in m['faces']:
   assert len(tri)==3 and len(set(tri))==3 and all(0<=i<len(m['vertices']) for i in tri)
   assert 0<=mat<len(m['materials'])
   a,b,c=[m['vertices'][i][:3] for i in tri]
   ab=[b[i]-a[i] for i in range(3)];ac=[c[i]-a[i] for i in range(3)]
   cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
   assert sum(x*x for x in cross)>1e-14,(name,'Degenerate triangle')
   normal=m['vertices'][tri[0]][3:6]
   assert sum(x*y for x,y in zip(cross,normal))>0,(name,'Inverted normal/winding')
   count+=1
  native=(hub.MESHES/(name+'.mesh')).read_text()
  assert '    m_VertexCount: '+str(len(m['vertices'])) in native
 root=report['root'];rt=report['rootTransform'];descendants={rt}
 while True:
  before=len(descendants)
  for i,t in scene.blocks.items():
   if t.startswith('--- !u!4 '):
    p=re.search(r'm_Father: \{fileID: (-?\d+)\}',t)
    if p and int(p[1]) in descendants:descendants.add(i)
  if len(descendants)==before:break
 owners={int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',scene.blocks[i])[1]) for i in descendants}
 assert len(owners)>=20
 for owner in owners:
  assert f'  m_Layer: {hub.LAYER}\n' in scene.blocks[owner]
  for block in scene.blocks.values():
   if 'm_GameObject: '+hub.local(owner) in block:
    assert not re.match(r'--- !u!(?:54|65|64|135|136|82) ',block),'Menu acquired physics or audio listener'
 cam=scene.blocks[report['menuCamera']];game=scene.blocks[report['gameplayCamera']]
 assert '  m_Enabled: 0\n' in cam and f'    m_Bits: {1<<hub.LAYER}\n' in cam
 assert 'm_TagString: Untagged' in scene.blocks[int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',cam)[1])]
 mask=int(re.search(r'm_CullingMask:\n    serializedVersion: \d+\n    m_Bits: (\d+)',game)[1])
 assert mask & (1<<hub.LAYER)==0
 # A designer's moved camera/prop is never overwritten by rerunning setup.
 anchor=report['anchors']['robotAnchor'];old=scene.blocks[anchor]
 scene.blocks[anchor]=re.sub(r'^  m_LocalPosition: .*$', '  m_LocalPosition: {x: 13, y: 7, z: -9}',old,flags=re.M)
 hub.configure(scene,data)
 assert 'm_LocalPosition: {x: 13, y: 7, z: -9}' in scene.blocks[anchor]
 shader=hub.meshbuild.meta(hub.BASE/'Shaders/WorkshopPreview.shader',kind='ShaderImporter',main=4800000)
 for name in hub.PALETTE:assert f'guid: {shader},' in (hub.MATERIALS/(name+'.mat')).read_text()
 result={**report,'status':'PASS','uniqueSourceTriangles':count,'authoredObjects':len(owners),'idempotent':True,
  'checks':['finite indexed geometry','positive face winding','bounded vertex/index counts','dedicated camera culling and tag','no physics/audio listener','authored anchor edits preserved','fixed studio materials'],
  'limitation':'Native mesh/scene data and Python adapter validation only; Unity import, shader compilation and Play Mode were not run.'}
 target=ROOT/'Validation/Reports/MenuHubReview.json';target.write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result,indent=2))
if __name__=='__main__':main()
