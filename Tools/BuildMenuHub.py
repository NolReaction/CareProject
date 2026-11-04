#!/usr/bin/env python3
"""Build the reusable, visual-only menu rooms. Never rewrites the gameplay scene.

`generate()` writes native meshes/materials. `configure(scene, generated)` is an
explicit, idempotent adapter for AuthorNpcScene.Scene, called by the scene owner.
Existing authored object poses and material edits are preserved on repeat calls.
"""
from pathlib import Path
import argparse, json, math, re
import numpy as np
import BuildTutorialDistrict as meshbuild
import AuthorNpcScene as author

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Assets/IntercomMVP/Content/Frontend'
MESHES=BASE/'Meshes'
MATERIALS=BASE/'Materials'
LAYER=30
ORIGIN=(0,-80,0)
PALETTE={
 'HubPlaster':(.60,.63,.58), 'HubFloor':(.25,.32,.33),
 'HubTrim':(.13,.20,.21), 'HubWood':(.48,.37,.26),
 'HubCushion':(.36,.48,.43), 'HubPaper':(.83,.78,.64),
 'HubBrass':(.66,.50,.28), 'HubRust':(.54,.29,.22),
 'HubScreen':(.36,.56,.52),
}
# Local room positions. Every prop is a separately replaceable GameObject.
ROOMS=[
 ('01 Lounge',0,[('Sofa','HubSofa',(-1.35,0,.6),0),('Television','HubTelevision',(1.55,0,-.3),-12),
   ('Coffee table','HubCoffeeTable',(-.3,0,-1.15),-7),('Dispatch board','HubDispatchBoard',(1.9,0,2.35),0),
   ('Plant','HubPlant',(-3,0,2.05),0)]),
 ('02 Workshop',11,[('Workbench','HubWorkbench',(-.6,0,1.45),0),('Service robot placeholder','HubServiceRobot',(-.55,0,-.25),-8),
   ('Parts trolley','HubPartsTrolley',(2.3,0,.8),-6),('Plant','HubPlant',(-3,0,2.05),0)]),
 ('03 Armory',22,[('Weapon board','HubWeaponBoard',(-1.0,0,2.30),0),('Locker','HubLocker',(2.45,0,1.8),0),
   ('Workbench','HubWorkbench',(-.65,0,.4),0),('Parts trolley','HubPartsTrolley',(-2.8,0,-.8),6)]),
]

def ensure_material(name,color):
 path=MATERIALS/(name+'.mat')
 if not path.exists():
  text=(ROOT/'Assets/IntercomMVP/Content/Environments/TutorialYard/Materials/Trim.mat').read_text()
  text=re.sub(r'^  m_Name: .*$', '  m_Name: '+name,text,flags=re.M)
  shader=meshbuild.meta(BASE/'Shaders/WorkshopPreview.shader',kind='ShaderImporter',main=4800000)
  text=re.sub(r'^  m_Shader: .*$', '  m_Shader: {fileID: 4800000, guid: '+shader+', type: 3}',text,flags=re.M)
  text=re.sub(r'm_Texture: \{fileID: 2800000, guid: [0-9a-f]+, type: 3\}','m_Texture: {fileID: 0}',text)
  for key in ['BaseColor','Color']:
   text=re.sub(r'(- _'+key+r': )\{[^}]+\}',lambda m:m[1]+author.rgba((*color,1)),text)
  text=re.sub(r'(- _Smoothness: )[^\n]+',r'\g<1>0.22',text)
  text=re.sub(r'(- _Metallic: )[^\n]+',r'\g<1>0',text)
  path.write_text(text)
 meshbuild.meta(path)

def generate():
 for folder in [BASE,MESHES,MATERIALS]:folder.mkdir(parents=True,exist_ok=True);meshbuild.meta(folder)
 for name,color in PALETTE.items():ensure_material(name,color)
 old=meshbuild.MESHES;meshbuild.MESHES=MESHES
 try:
  modules={}
  def module(name):
   m=meshbuild.Mesh(name);modules[name]=m;return m
  m=module('HubRoomShell')
  m.box((0,-.18,0),(8.6,.36,6.7),'HubTrim',.10)
  m.box((0,.015,0),(8.25,.08,6.35),'HubFloor',.06)
  m.box((0,1.6,3.05),(8.4,3.2,.24),'HubPlaster',.05)
  m.box((0,.26,2.88),(8.4,.36,.14),'HubTrim',.02)
  # Cut-away side walls keep the room open from all tab-camera angles.
  m.box((-4.12,.52,0),(.18,1.05,6.1),'HubPlaster',.05)
  m.box((4.12,.52,0),(.18,1.05,6.1),'HubPlaster',.05)
  for x in [-3.75,-2.5,-1.25,0,1.25,2.5,3.75]:m.box((x,.065,0),(.025,.014,6.1),'HubTrim',0)
  for z in [-2.4,-1.2,0,1.2,2.4]:m.box((0,.066,z),(8,.015,.025),'HubTrim',0)
  m.box((0,2.78,2.88),(7.9,.08,.1),'HubBrass',.01)
  m.box((0,2.55,2.84),(1.1,.32,.11),'HubTrim',.03)
  for x in [-.32,-.16,0,.16,.32]:m.box((x,2.55,2.77),(.075,.17,.04),'HubPaper',.006)

  m=module('HubSofa')
  for x in [-1.1,1.1]:
   for z in [-.37,.37]:m.cylinder((x,.18,z),.075,.25,'HubWood',8)
  m.box((0,.48,0),(2.85,.52,1.15),'HubTrim',.12)
  m.box((0,1.02,.42),(2.85,.98,.40),'HubCushion',.14)
  for x in [-.65,.65]:m.box((x,.81,-.07),(1.2,.28,.89),'HubCushion',.12)
  for x in [-1.36,1.36]:m.box((x,.86,-.04),(.42,.74,1.17),'HubCushion',.13)
  m.box((.72,1.18,.12),(.54,.53,.24),'HubRust',.12,yaw=-10)
  m.box((-.9,.97,-.35),(.47,.09,.65),'HubPaper',.05,yaw=8)

  m=module('HubTelevision')
  m.box((0,.42,0),(1.55,.8,.77),'HubWood',.08)
  m.box((0,.90,.08),(1.74,.14,.91),'HubBrass',.04)
  m.box((0,1.48,.08),(1.35,1.02,.52),'HubTrim',.12)
  m.box((-.08,1.48,-.205),(1.05,.73,.08),'HubScreen',.11)
  for x in [.52,.64]:m.cylinder((x,1.45,-.26),.055,.045,'HubBrass',12,axis='z')
  for x,y in [(-.31,1.57),(.08,1.57)]:m.box((x,y,-.255),(.17,.16,.025),'HubPaper',.025)
  m.box((-.11,1.30,-.255),(.43,.05,.025),'HubPaper',.01)
  for x,yaw in [(-.2,-23),(.2,23)]:m.box((x,2.13,.08),(.04,.53,.04),'HubBrass',.01,yaw=yaw)

  m=module('HubCoffeeTable')
  m.box((0,.60,0),(1.6,.13,.93),'HubWood',.10)
  for x in [-.6,.6]:m.box((x,.31,0),(.13,.55,.60),'HubTrim',.04)
  m.box((-.3,.70,0),(.45,.035,.54),'HubPaper',.01,yaw=-9)
  m.cylinder((.43,.79,.03),.13,.22,'HubPaper',12)
  m.cylinder((.43,.904,.03),.095,.007,'HubTrim',12)
  m.box((.02,.705,-.2),(.16,.06,.32),'HubTrim',.02,yaw=12)

  m=module('HubDispatchBoard')
  m.box((0,1.72,0),(2.0,1.60,.17),'HubWood',.06)
  m.box((0,1.72,-.10),(1.83,1.41,.055),'HubCushion',.03)
  for x,y,rot in [(-.49,1.94,-7),(.38,1.99,5),(-.16,1.42,9),(.61,1.40,-4)]:
   m.box((x,y,-.145),(.53,.43,.025),'HubPaper',.008,yaw=rot)
   m.box((x,y+.14,-.17),(.08,.04,.018),'HubBrass',.005)

  m=module('HubWorkbench')
  m.box((0,1.12,0),(3.12,.20,1.05),'HubWood',.07)
  for x in [-1.28,1.28]:m.box((x,.57,0),(.21,1.04,.83),'HubTrim',.04)
  m.box((0,.35,0),(2.56,.12,.77),'HubBrass',.03)
  for x in [-.92,-.3,.32,.94]:
   m.box((x,.77,-.39),(.54,.4,.13),'HubCushion',.025)
   m.box((x,.81,-.47),(.18,.035,.035),'HubBrass',.006)
  m.box((-.8,1.30,.16),(.56,.20,.43),'HubTrim',.025)
  m.cylinder((.49,1.34,.09),.17,.24,'HubBrass',12)
  m.box((.13,1.25,-.23),(.58,.055,.13),'HubRust',.02,yaw=12)
  m.box((1.07,1.32,.11),(.34,.20,.37),'HubPaper',.025)

  m=module('HubServiceRobot')
  m.cylinder((0,.13,0),1.01,.19,'HubTrim',24)
  m.cylinder((0,.25,0),.91,.075,'HubBrass',24)
  # Honest replaceable preview droid; source hero mesh and animation are untouched.
  m.box((0,1.20,0),(.90,1.12,.63),'HubCushion',.14)
  m.box((0,1.35,-.34),(.57,.42,.08),'HubTrim',.055)
  for x in [-.16,.16]:m.box((x,1.39,-.39),(.12,.12,.035),'HubPaper',.02)
  m.box((0,.91,-.35),(.45,.15,.055),'HubBrass',.02)
  for x in [-.38,.38]:
   m.box((x,.41,-.15),(.41,.24,.64),'HubWood',.09)
   m.cylinder((x,.65,0),.11,.21,'HubBrass',12)
  for x in [-.62,.62]:
   m.box((x,.99,0),(.20,.58,.23),'HubBrass',.05)
   m.box((x,.70,-.07),(.25,.25,.28),'HubTrim',.05)
  m.cylinder((0,1.88,0),.035,.32,'HubBrass',8)
  m.cylinder((0,2.04,0),.075,.09,'HubRust',10)

  m=module('HubPartsTrolley')
  for x in [-.4,.4]:
   for z in [-.25,.25]:m.cylinder((x,.15,z),.12,.08,'HubTrim',12,axis='x')
  m.box((0,.29,0),(1.0,.14,.73),'HubTrim',.04)
  m.box((0,.74,0),(1.0,.14,.73),'HubBrass',.04)
  for x in [-.43,.43]:m.box((x,.54,0),(.07,.55,.60),'HubCushion',.02)
  m.box((0,.99,.26),(.83,.40,.08),'HubTrim',.02)
  for x in [-.28,.05,.30]:m.cylinder((x,.90,-.03),.12,.19,'HubWood',12)
  m.box((0,.44,-.08),(.63,.22,.40),'HubRust',.04)

  m=module('HubWeaponBoard')
  m.box((0,1.92,0),(3.35,1.65,.18),'HubWood',.06)
  m.box((0,1.92,-.12),(3.1,1.4,.08),'HubTrim',.03)
  # Empty pale rack outlines are placeholders, not invented purchasable guns.
  for y in [1.58,2.19]:
   m.box((-.1,y,-.19),(1.9,.10,.075),'HubPaper',.025)
   m.box((-.75,y-.14,-.19),(.16,.27,.075),'HubPaper',.025)
   for x in [-.96,.84]:m.box((x,y-.15,-.30),(.075,.10,.28),'HubBrass',.02)
   m.box((1.15,y,-.20),(.23,.20,.05),'HubBrass',.015)

  m=module('HubLocker')
  m.box((0,1.2,0),(1.19,2.35,.82),'HubTrim',.07)
  for x in [-.29,.29]:
   m.box((x,1.25,-.44),(.54,2.13,.09),'HubCushion',.045)
   m.box((x+.12,1.26,-.51),(.05,.26,.06),'HubBrass',.012)
   for y in [1.91,2.03,2.15]:m.box((x,y,-.495),(.30,.035,.025),'HubTrim',.006)
  m.box((0,2.44,0),(1.28,.16,.90),'HubBrass',.04)

  m=module('HubPlant')
  m.cylinder((0,.32,0),.34,.61,'HubWood',12)
  m.cylinder((0,.64,0),.38,.12,'HubBrass',12)
  m.cylinder((0,.70,0),.30,.04,'HubTrim',12)
  for x,z,h in [(-.13,0,.61),(.12,.07,.82),(.01,-.12,.98),(.20,-.11,.52)]:
   m.box((x,.70+h*.5,z),(.19,h,.22),'HubCushion',.085,yaw=x*70)
  exported={name:mesh.export() for name,mesh in modules.items()}
  return {'modules':exported,'palette':PALETTE,'rooms':ROOMS,'origin':ORIGIN,'layer':LAYER,
   'triangles':sum(m['triangles'] for m in exported.values())}
 finally:meshbuild.MESHES=old

def local(n):return '{fileID: '+str(n)+'}'
def asset(path,n):return '{fileID: '+str(n)+', guid: '+meshbuild.meta(path)+', type: 2}'
def add_renderer(scene,go,name,materials):
 if any(t.startswith('--- !u!33 ') and 'm_GameObject: '+local(go) in t for t in scene.blocks.values()):return
 mf,mr=scene.ids.allocate(),scene.ids.allocate()
 scene.blocks[go]=scene.blocks[go].replace('  m_Layer:',f'  - component: {local(mf)}\n  - component: {local(mr)}\n  m_Layer:',1)
 scene.blocks[mf]=f'--- !u!33 &{mf}\nMeshFilter:\n'+author.common(go)+'  m_Mesh: '+asset(MESHES/(name+'.mesh'),4300000)+'\n'
 template=next(t for t in scene.blocks.values() if t.startswith('--- !u!23 '))
 template=re.sub(r'^--- !u!23 &(-?\d+)',f'--- !u!23 &{mr}',template)
 template=re.sub(r'm_GameObject: \{fileID: -?\d+\}','m_GameObject: '+local(go),template)
 template=re.sub(r'  m_Materials:\n(?:  - .*\n)+','  m_Materials:\n'+''.join('  - '+asset(MATERIALS/(m+'.mat'),2100000)+'\n' for m in materials),template)
 scene.blocks[mr]=template

def set_new_pose(scene,tr,position,rotation):
 scene.blocks[tr]=re.sub(r'^  m_LocalRotation: .*$', '  m_LocalRotation: '+meshbuild.v4(rotation),scene.blocks[tr],flags=re.M)

def camera_pose(x):
 pos=np.array((x+7.1,6.2,-10.0));target=np.array((x+2.3,1.0,1.0))
 forward=(target-pos);forward/=np.linalg.norm(forward)
 right=np.cross((0,1,0),forward);right/=np.linalg.norm(right)
 up=np.cross(forward,right);matrix=np.column_stack([right,up,forward])
 # The selected pose has a positive trace; convert this orthonormal basis to XYZW.
 w=math.sqrt(1+np.trace(matrix))*.5
 q=((matrix[2,1]-matrix[1,2])/(4*w),(matrix[0,2]-matrix[2,0])/(4*w),(matrix[1,0]-matrix[0,1])/(4*w),w)
 return list(pos),q

def configure(scene,data):
 """Mutate only our new hub objects and the existing FrontEnd.roomCamera link.
 The caller remains responsible for writing/validating the finished scene.
 """
 root,rt=scene.add_object('MVP_MenuHub',position=ORIGIN)
 scene.blocks[root]=re.sub(r'^  m_Layer: \d+$',f'  m_Layer: {LAYER}',scene.blocks[root],flags=re.M)
 def obj(name,parent,pos=(0,0,0),yaw=0):
  existing=scene.find_go(name)
  go,tr=scene.add_object(name,parent,pos)
  scene.blocks[go]=re.sub(r'^  m_Layer: \d+$',f'  m_Layer: {LAYER}',scene.blocks[go],flags=re.M)
  if existing is None:set_new_pose(scene,tr,pos,meshbuild.yawq(yaw))
  return go,tr
 for name,x,props in ROOMS:
  _,room=obj('Menu Hub - '+name,rt,(x,0,0))
  go,_=obj('Menu '+name+' - cutaway shell',room);add_renderer(scene,go,'HubRoomShell',data['modules']['HubRoomShell']['materials'])
  for prop,mesh,pos,yaw in props:
   go,_=obj('Menu '+name+' - '+prop,room,pos,yaw);add_renderer(scene,go,mesh,data['modules'][mesh]['materials'])
 anchors={}
 for key,x in [('home',0),('play',0),('robot',11),('armory',22)]:
  pos,q=camera_pose(x);name='Menu Camera Anchor - '+key.title();existing=scene.find_go(name)
  _,tr=obj(name,rt,pos)
  if existing is None:set_new_pose(scene,tr,pos,q)
  anchors[key+'Anchor']=tr
 camera_go,ct=obj('Menu Room Camera',rt)
 existing=next((i for i,t in scene.blocks.items() if t.startswith('--- !u!20 ') and 'm_GameObject: '+local(camera_go) in t),None)
 game_go=scene.find_go('Main Camera')
 if game_go is None:raise ValueError('Missing gameplay Main Camera')
 game_camera=next(i for i,t in scene.blocks.items() if t.startswith('--- !u!20 ') and 'm_GameObject: '+local(game_go) in t)
 # The yard must never render menu props, even if its camera range changes later.
 block=scene.blocks[game_camera]
 mask=re.search(r'(  m_CullingMask:\n    serializedVersion: \d+\n    m_Bits: )(\d+)',block)
 if mask:
  scene.blocks[game_camera]=block[:mask.start(2)]+str(int(mask[2]) & ~(1<<LAYER))+block[mask.end(2):]
 if existing is None:
  existing=scene.ids.allocate();scene.blocks[camera_go]=scene.blocks[camera_go].replace('  m_Layer:',f'  - component: {local(existing)}\n  m_Layer:',1)
  t=scene.blocks[game_camera]
  t=re.sub(r'^--- !u!20 &(-?\d+)',f'--- !u!20 &{existing}',t)
  t=re.sub(r'm_GameObject: \{fileID: -?\d+\}','m_GameObject: '+local(camera_go),t)
  for key,value in [('m_Enabled','0'),('m_ClearFlags','2'),('orthographic','1'),('orthographic size','3.9'),('near clip plane','.1'),('far clip plane','55'),('m_Depth','2'),('m_HDR','0'),('m_AllowMSAA','0'),('m_OcclusionCulling','0')]:t=re.sub(r'^  '+re.escape(key)+r': .*$',f'  {key}: {value}',t,flags=re.M)
  t=re.sub(r'^  m_BackGroundColor: .*$', '  m_BackGroundColor: {r: .13, g: .18, b: .19, a: 1}',t,flags=re.M)
  t=re.sub(r'(  m_CullingMask:\n    serializedVersion: \d+\n    m_Bits: )\d+',r'\g<1>'+str(1<<LAYER),t)
  scene.blocks[existing]=t
 # URP uses camera-specific settings. Copy only the game camera's serializer shape,
 # never its stack, volumes or presentation effects onto the studio preview.
 additional=[(i,t) for i,t in scene.blocks.items() if t.startswith('--- !u!114 ') and 'UniversalAdditionalCameraData' in t]
 if not any('m_GameObject: '+local(camera_go) in t for i,t in additional):
  template=next((t for i,t in additional if 'm_GameObject: '+local(game_go) in t),None)
  if template:
   ident=scene.ids.allocate();scene.blocks[camera_go]=scene.blocks[camera_go].replace('  m_Layer:',f'  - component: {local(ident)}\n  m_Layer:',1)
   template=re.sub(r'^--- !u!114 &(-?\d+)',f'--- !u!114 &{ident}',template)
   template=re.sub(r'm_GameObject: \{fileID: -?\d+\}','m_GameObject: '+local(camera_go),template)
   for key in ['m_RenderShadows','m_RenderPostProcessing','m_Dithering','m_StopNaN']:
    template=re.sub(r'^  '+key+r': .*$',f'  {key}: 0',template,flags=re.M)
   template=re.sub(r'^  m_Cameras:(?: \[\]\n|\n(?:  - .*\n)*)','  m_Cameras: []\n',template,flags=re.M)
   scene.blocks[ident]=template
 # Camera is deliberately untagged: Camera.main always identifies gameplay.
 fields='  roomCamera: '+local(existing)+'\n  gameplayCamera: '+local(game_camera)+'\n'+''.join('  '+k+': '+local(v)+'\n' for k,v in anchors.items())+'  travelSharpness: 5\n'
 rig=scene.add_component(root,'IntercomFrontEndRoomCamera',fields)
 frontend_go=scene.find_go('MVP_NpcSandbox');frontend=scene.component(frontend_go,'IntercomFrontEnd')
 if frontend is None:raise ValueError('Configure the FrontEnd before configuring the menu hub')
 text=scene.blocks[frontend]
 if '  roomCamera: {fileID: 0}' in text:scene.blocks[frontend]=text.replace('  roomCamera: {fileID: 0}','  roomCamera: '+local(rig))
 elif not re.search(r'^  roomCamera:',text,re.M):scene.blocks[frontend]+='  roomCamera: '+local(rig)+'\n'
 return {'root':root,'rootTransform':rt,'roomCameraComponent':rig,'menuCamera':existing,'gameplayCamera':game_camera,'anchors':anchors,'layer':LAYER,'origin':ORIGIN,'modules':len(data['modules']),'triangles':data['triangles']}

if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--render-json',type=Path);args=p.parse_args()
 data=generate()
 if args.render_json:args.render_json.parent.mkdir(parents=True,exist_ok=True);args.render_json.write_text(json.dumps(data))
 print(json.dumps({'modules':len(data['modules']),'triangles':data['triangles'],'scene':'unchanged; use configure(scene,data) explicitly'},indent=2))
