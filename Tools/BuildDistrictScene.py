"""Offline deterministic blockout serializer for the committed Unity scene.
Run after editing DistrictLayout.json. Runtime/editor regeneration uses the same data.
Only generated environment descendants, ground extent/material, light and follow bounds change.
"""
from pathlib import Path
import re,json,math,copy
ROOT=Path(__file__).resolve().parents[1]
SCENE=ROOT/'Assets/IntercomMVP/Scenes/IntercomMVP.unity'
DATA=ROOT/'Assets/IntercomMVP/World/DistrictLayout.json'
text=SCENE.read_text(); chunks=re.split(r'(?=^--- !u!)',text,flags=re.M);prefix=chunks.pop(0)
docs={int(re.match(r'--- !u!\d+ &(\d+)',s)[1]):s for s in chunks}
def info(i,key):return re.search(r'^  '+key+r': \{fileID: (-?\d+)\}',docs[i],re.M)
trans={i: s for i,s in docs.items() if s.startswith('--- !u!4 ')}
rootT=1550949731;rootGO=int(info(rootT,'m_GameObject')[1]);desc=set();pending=[rootT]
while pending:
 parent=pending.pop()
 for i in trans:
  f=info(i,'m_Father')
  if f and int(f[1])==parent and i not in desc:desc.add(i);pending.append(i)
oldGO={int(info(i,'m_GameObject')[1]) for i in desc}
remove=set(desc)|oldGO
for i in docs:
 m=info(i,'m_GameObject')
 if m and int(m[1]) in oldGO:remove.add(i)
# old root marker removed if regenerating; all unrelated root components preserved.
markerGuid=re.search(r'guid: (\w+)',(ROOT/'Assets/IntercomMVP/Runtime/IntercomDistrictMarker.cs.meta').read_text())[1]
for i,s in docs.items():
 if markerGuid in s:remove.add(i)
renderer=next(s for s in docs.values() if s.startswith('--- !u!23 '))
boxCollider=next(s for s in docs.values() if s.startswith('--- !u!65 '))
layout=json.loads(DATA.read_text());gen={};seq=6100000000
roles=['Zone','Building','Route','Cover','Prop','Boundary','PlayerSpawn','EnemySpawn','Loot','Exit','Target']
def guid(rel):return re.search(r'guid: (\w+)',(ROOT/(rel+'.meta')).read_text())[1]
anchorGuid=guid('Assets/IntercomMVP/Runtime/IntercomWorldAnchor.cs');occluderGuid=guid('Assets/IntercomMVP/Runtime/IntercomOccluder.cs')
def nextid():
 global seq
 seq+=1;return seq
nodes={};objects=[]
def node(name,parent,pos=(0,0,0),scale=(1,1,1),yaw=0):
 go,t=nextid(),nextid();n=dict(go=go,t=t,name=name,parent=parent,pos=pos,scale=scale,yaw=yaw,children=[],components=[t]);nodes[t]=n;objects.append(n)
 if parent in nodes:nodes[parent]['children'].append(t)
 return n
def component(n,kind,body):
 i=nextid();n['components'].append(i);gen[i]=f'--- !u!{kind} &{i}\n'+body;return i
def mono(n,script,name,fields):
 return component(n,114,f'''MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n['go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.{name}
'''+fields)
def anchor(n,key,role,fp):mono(n,anchorGuid,'IntercomWorldAnchor',f'  anchorId: {key}\n  role: {roles.index(role)}\n  footprint: {{x: {fp[0]}, y: {fp[1]}}}\n  designerNote: Replace visual children; retain anchor and collision setup.\n')
def mesh(n,mat,shape='Cube',shadow=True,collision=False):
 materialGuid=guid('Assets/IntercomMVP/Art/PrototypeEnvironment/'+mat+'.mat')
 r=renderer.split('\n',1)[1];r=re.sub(r'm_GameObject: \{fileID: \d+\}',f"m_GameObject: {{fileID: {n['go']}}}",r)
 r=re.sub(r'(m_Materials:\n  - )\{[^}]+\}',r'\g<1>{fileID: 2100000, guid: '+materialGuid+', type: 2}',r)
 r=re.sub(r'm_CastShadows: \d+',f'm_CastShadows: {int(shadow)}',r);rid=component(n,23,r)
 mid={'Cube':10202,'Capsule':10208}[shape]
 component(n,33,f'''MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n['go']}}}
  m_Mesh: {{fileID: {mid}, guid: 0000000000000000e000000000000000, type: 0}}
''')
 if collision:
  solid=node('Collision - '+n['name'],n['parent'],n['pos'],n['scale'],n['yaw'])
  c=boxCollider.split('\n',1)[1];c=re.sub(r'm_GameObject: \{fileID: \d+\}',f"m_GameObject: {{fileID: {solid['go']}}}",c);component(solid,65,c)
 return rid
rootnodes=[]
for group in layout['groups']:
 n=node(group['name'],rootT,group['position']);rootnodes.append(n['t']);anchor(n,group['key'],group['role'],group['footprint'])
 mono(n,occluderGuid,'IntercomOccluder',f"  hideWhenBlocking: {int(group['hide'])}\n")
 for b in group['blocks']:
  p=node(b['name'],n['t'],b['position'],b['size'],b['yaw']);mesh(p,b['material'],b['shape'],b['shadow'],b['collider'])
tr=node('17 - AFK test targets',rootT);rootnodes.append(tr['t']);mono(tr,occluderGuid,'IntercomOccluder','  hideWhenBlocking: 0\n')
for target in layout['targets']:
 n=node(target['name'],tr['t'],target['position']);anchor(n,target['key'],'Target',[.7,.7])
 component(n,136,f'''CapsuleCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n['go']}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 2
  m_Radius: 0.35
  m_Height: 1.6
  m_Direction: 1
  m_Center: {{x: 0, y: 0.8, z: 0}}
''')
 v=node('Visual - replace capsule',n['t']);b=node('Capsule body',v['t'],[0,.8,0],[.7,.8,.7]);rid=mesh(b,target['material'],'Capsule')
 visor=node('Visor',v['t'],[0,1.05,-.327],[.4,.15,.035]);mesh(visor,'Door')
 mono(n,'0af7c8fd4f835ac4b2fe096ed4288621','IntercomTargetDummy',f"  visual: {{fileID: {v['t']}}}\n  bodyRenderer: {{fileID: {rid}}}\n  maxHealth: 5\n  resetSeconds: 2.5\n")
# marker attached to the pre-existing environment root
pseudo=dict(go=rootGO,components=[]);marker=mono(pseudo,markerGuid,'IntercomDistrictMarker',f"  layoutVersion: {layout['version']}\n  worldSize: {{x: {layout['width']}, y: {layout['depth']}}}\n")
rootdoc=docs[rootGO]
for i in remove:rootdoc=re.sub(r'  - component: \{fileID: '+str(i)+r'\}\n','',rootdoc)
rootdoc=rootdoc.replace('  m_Layer:',f'  - component: {{fileID: {marker}}}\n  m_Layer:',1);docs[rootGO]=rootdoc
docs[rootT]=re.sub(r'  m_Children:.*?(?=  m_Father:)', '  m_Children:\n'+''.join(f'  - {{fileID: {i}}}\n' for i in rootnodes),docs[rootT],flags=re.S)
def vec(v):return '{x: '+str(v[0])+', y: '+str(v[1])+', z: '+str(v[2])+'}'
for n in objects:
 components=''.join(f'  - component: {{fileID: {i}}}\n' for i in n['components'])
 gen[n['go']]=f'''--- !u!1 &{n['go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{components}  m_Layer: 0
  m_Name: {n['name']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
'''
 a=math.radians(n['yaw'])/2;children='  m_Children:\n'+''.join(f'  - {{fileID: {i}}}\n' for i in n['children']) if n['children'] else '  m_Children: []\n'
 gen[n['t']]=f'''--- !u!4 &{n['t']}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n['go']}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: {math.sin(a)}, z: 0, w: {math.cos(a)}}}
  m_LocalPosition: {vec(n['pos'])}
  m_LocalScale: {vec(n['scale'])}
  m_ConstrainProportionsScale: 0
{children}  m_Father: {{fileID: {n['parent']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: {n['yaw']}, z: 0}}
'''
for i in remove:docs.pop(i,None)
# Ground primitive plane is10x10; its MeshCollider scales with it.
docs[1981137703]=re.sub(r'm_LocalScale: \{[^}]+\}',f'm_LocalScale: {{x: {layout["width"]/10}, y: 1, z: {layout["depth"]/10}}}',docs[1981137703])
groundGuid=guid('Assets/IntercomMVP/Art/PrototypeEnvironment/Backdrop.mat')
docs[1981137700]=re.sub(r'(m_Materials:\n  - )\{[^}]+\}',r'\g<1>{fileID: 2100000, guid: '+groundGuid+', type: 2}',docs[1981137700])
for i,s in list(docs.items()):
 if '  groundMinimum:' in s:
  s=re.sub(r'groundMinimum: \{[^}]+\}',f'groundMinimum: {{x: {-layout["width"]/2}, y: {-layout["depth"]/2}}}',s);s=re.sub(r'groundMaximum: \{[^}]+\}',f'groundMaximum: {{x: {layout["width"]/2}, y: {layout["depth"]/2}}}',s)
 if s.startswith('--- !u!108 '):s=re.sub(r'm_Color: \{[^}]+\}','m_Color: {r: 1, g: 0.96, b: 0.86, a: 1}',s).replace('m_Intensity: 1.3','m_Intensity: 1.25').replace('m_Strength: 0.8','m_Strength: 0.72')
 if s.startswith('--- !u!104 '):
  for field,c in [('Sky',(.70,.78,.86)),('Equator',(.52,.59,.59)),('Ground',(.34,.39,.34))]:s=re.sub(r'm_Ambient'+field+r'Color: \{[^}]+\}',f'm_Ambient{field}Color: {{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: 1}}',s)
 docs[i]=s
docs.update(gen)
SCENE.write_text(prefix+''.join(docs[i] for i in sorted(docs)))
print('Serialized',len(objects),'district objects; preserved',len(docs)-len(gen),'other scene documents; bytes',SCENE.stat().st_size)
