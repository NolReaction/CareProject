#!/usr/bin/env python3
"""Install the authored V8 NPC sandbox without regenerating the courtyard.

Repeated runs preserve existing health/profile/navigation tuning and spawn markers.
The shipped scene already contains the result; normal ZIP installation needs no run.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
from UnitySceneIds import SceneIdAllocator, validate_scene_ids
from AssetPaths import script_path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'
ENEMY = ROOT / 'Assets/IntercomMVP/Content/Enemies/Udarnik'
MATERIALS = ENEMY / 'Materials'
PROFILE = ENEMY / 'Settings/UdarnikCapsule.asset'
SPAWNS = [(-6, 0, -3), (3.5, 0, 1), (1, 0, 9)]
YAML = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
ROOT_CLASS = 1660057539

def metadata(path, main=None):
    path = Path(path)
    sidecar = Path(str(path)+'.meta')
    if sidecar.exists():
        return re.search(r'^guid: ([0-9a-f]{32})$', sidecar.read_text(), re.M)[1]
    relative = path.relative_to(ROOT).as_posix()
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'CareProject/npc-v8/'+relative).hex
    text = 'fileFormatVersion: 2\nguid: '+guid+'\n'
    if path.is_dir(): text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif main is not None: text += f'NativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {main}\n'
    sidecar.write_text(text)
    return guid

def script(name):
    return metadata(script_path(ROOT, name))

def rgba(color): return '{r: %g, g: %g, b: %g, a: %g}' % color
def vec(point): return '{x: %g, y: %g, z: %g}' % tuple(point)
def common(owner):
    return ('  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n'
            '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
            f'  m_GameObject: {{fileID: {owner}}}\n')

def ensure_material(name, color, transparent=False):
    path = MATERIALS/(name+'.mat')
    if not path.exists():
        template = ('Assets/IntercomMVP/Content/Weapons/Pistols/Effects/Materials/MVP_Flash.mat'
                    if transparent else 'Assets/IntercomMVP/Content/Environments/TutorialYard/Materials/Trim.mat')
        text = (ROOT/template).read_text()
        text = re.sub(r'^  m_Name: (?:MVP_Flash|Trim)$', '  m_Name: '+name, text, flags=re.M)
        text = re.sub(r'm_Texture: \{fileID: 2800000, guid: [0-9a-f]+, type: 3\}', 'm_Texture: {fileID: 0}', text)
        text = re.sub(r'(- _(?:BaseColor|Color): )\{[^}]+\}', lambda m:m[1]+rgba(color), text)
        if transparent:
            text = text.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - _SURFACE_TYPE_TRANSPARENT')
            text = text.replace('m_CustomRenderQueue: -1','m_CustomRenderQueue: 3000').replace('RenderType: Opaque','RenderType: Transparent')
            for key,value in {'_Surface':1,'_SrcBlend':5,'_DstBlend':10,'_SrcBlendAlpha':1,'_DstBlendAlpha':10,'_ZWrite':0,'_Cull':0}.items():
                text = re.sub(r'(- '+re.escape(key)+r': )[^\n]+',lambda m:m[1]+str(value),text)
        path.write_text('\n'.join(line.rstrip() for line in text.splitlines())+'\n')
    return metadata(path,2100000)

def ensure_assets():
    for folder in [ENEMY.parent,ENEMY,MATERIALS,PROFILE.parent]:
        folder.mkdir(parents=True,exist_ok=True); metadata(folder)
    for name in ['IntercomNpcArena']:
        script(name)
    metadata(script_path(ROOT, 'IntercomNpcSetup', editor=True))
    materials = {
        'bodyMaterial':ensure_material('UdarnikBody',(.72,.68,.55,1)),
        'detailMaterial':ensure_material('UdarnikDetail',(.12,.19,.20,1)),
        'telegraphMaterial':ensure_material('UdarnikTelegraph',(.95,.67,.22,.38),True)}
    profile = PROFILE
    if not profile.exists():
        text=YAML+'--- !u!114 &11400000\nMonoBehaviour:\n'+common(0)
        text+='  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        text+=f'  m_Script: {{fileID: 11500000, guid: {script("IntercomNpcDefinition")}, type: 3}}\n'
        text+='  m_Name: UdarnikCapsule\n  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.IntercomNpcDefinition\n'
        values={'maximumHealth':75,'damage':12,'attackRange':1.15,'attackHalfAngle':40,'windupSeconds':.45,
                'recoverySeconds':.65,'moveSpeed':2.25,'patrolSpeed':.9,'acceleration':10,'turnSpeed':420,
                'repathSeconds':.35,'detectionRadius':10,'loseTargetSeconds':2.5,'leashRadius':18,
                'patrolRadius':1.5,'idleSeconds':1.1}
        text+=''.join(f'  {key}: {value}\n' for key,value in values.items())
        text+=''.join(f'  {key}: {{fileID: 2100000, guid: {guid}, type: 2}}\n' for key,guid in materials.items())
        text+='  bodyColor: '+rgba((.68,.53,.28,1))+'\n'
        profile.write_text(text)
    return metadata(profile,11400000)

class Scene:
    def __init__(self,text):
        validate_scene_ids(text)
        pieces=re.split(r'(?=^--- !u!)',text,flags=re.M)
        self.header=pieces[0]
        self.blocks={int(re.match(r'--- !u!\d+ &(-?\d+)',p)[1]):p for p in pieces[1:]}
        self.ids=SceneIdAllocator(self.blocks,8800000000)
    def find_go(self,name):
        found=[i for i,t in self.blocks.items() if t.startswith('--- !u!1 ') and re.search(r'^  m_Name: '+re.escape(name)+r'$',t,re.M)]
        if len(found)>1:raise ValueError('Ambiguous GameObject '+name)
        return found[0] if found else None
    def component(self,owner,name):
        guid=script(name)
        found=[i for i,t in self.blocks.items() if t.startswith('--- !u!114 ') and f'm_GameObject: {{fileID: {owner}}}' in t and f'guid: {guid},' in t]
        if len(found)>1:raise ValueError('Duplicate '+name)
        return found[0] if found else None
    def transform(self,owner):
        return next(i for i,t in self.blocks.items() if t.startswith('--- !u!4 ') and f'm_GameObject: {{fileID: {owner}}}' in t)
    def add_component(self,owner,name,fields):
        existing=self.component(owner,name)
        if existing is not None:return existing
        ident=self.ids.allocate()
        self.blocks[owner]=self.blocks[owner].replace('  m_Layer:',f'  - component: {{fileID: {ident}}}\n  m_Layer:',1)
        self.blocks[ident]=f'--- !u!114 &{ident}\nMonoBehaviour:\n'+common(owner)
        self.blocks[ident]+='  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        self.blocks[ident]+=f'  m_Script: {{fileID: 11500000, guid: {script(name)}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.{name}\n'+fields
        return ident
    def add_object(self,name,parent=0,position=(0,0,0)):
        owner=self.find_go(name)
        if owner is not None:return owner,self.transform(owner)
        owner,transform=self.ids.allocate(),self.ids.allocate()
        self.blocks[owner]=f'--- !u!1 &{owner}\nGameObject:\n'+common(0).replace('  m_GameObject: {fileID: 0}\n','')
        self.blocks[owner]+=f'  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {transform}}}\n  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
        self.blocks[transform]=f'--- !u!4 &{transform}\nTransform:\n'+common(owner)
        self.blocks[transform]+='  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: '+vec(position)+'\n'
        self.blocks[transform]+='  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n'
        self.blocks[transform]+=f'  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
        if parent:
            if '  m_Children: []\n' in self.blocks[parent]:
                self.blocks[parent]=self.blocks[parent].replace('  m_Children: []\n',f'  m_Children:\n  - {{fileID: {transform}}}\n')
            else:self.blocks[parent]=self.blocks[parent].replace('  m_Father:',f'  - {{fileID: {transform}}}\n  m_Father:',1)
        else:
            roots=next(i for i,t in self.blocks.items() if t.startswith(f'--- !u!{ROOT_CLASS} '))
            self.blocks[roots]+=f'  - {{fileID: {transform}}}\n'
        return owner,transform
    def text(self):return self.header+''.join(self.blocks.values())

def install(text,profile_guid):
    scene=Scene(text);before=dict(scene.blocks)
    robot=scene.find_go('Robot');environment=scene.find_go('MVP_TestEnvironment')
    if robot is None or environment is None:raise ValueError('Expected Robot and MVP_TestEnvironment')
    motor=scene.component(robot,'IntercomPlayerMotor');input_id=scene.component(robot,'IntercomPlayerInput')
    shooter=scene.component(robot,'IntercomSimpleShooter');pose=scene.component(robot,'IntercomWeaponPose')
    assert all([motor,input_id,shooter,pose])
    health=scene.add_component(robot,'IntercomHealth','  maximumHealth: 100\n  team: 1\n  invulnerable: 0\n  postHitInvulnerability: 0.6\n  onDamaged:\n    m_PersistentCalls:\n      m_Calls: []\n  onDeath:\n    m_PersistentCalls:\n      m_Calls: []\n')
    vitality=scene.add_component(robot,'IntercomPlayerVitality',''.join(f'  {key}: {{fileID: {value}}}\n' for key,value in [('health',health),('motor',motor),('input',input_id),('shooter',shooter),('weaponPose',pose)]))
    if not re.search(r'^  vitality: ',scene.blocks[input_id],re.M):scene.blocks[input_id]+=f'  vitality: {{fileID: {vitality}}}\n'
    elif '  vitality: {fileID: 0}' in scene.blocks[input_id]:scene.blocks[input_id]=scene.blocks[input_id].replace('  vitality: {fileID: 0}',f'  vitality: {{fileID: {vitality}}}')
    arena,arena_tr=scene.add_object('MVP_NpcSandbox')
    nav=scene.add_component(arena,'IntercomNpcNavigation',f'  obstacleRoot: {{fileID: {scene.transform(environment)}}}\n  worldMin: {{x: -18, y: -16}}\n  worldSize: {{x: 36, y: 32}}\n  cellSize: 0.4\n  agentClearanceRadius: 0.4\n  floorHeight: 0\n  stepHeight: 0.18\n  agentHeight: 1.35\n  obstacleRefreshSeconds: 0.2\n  maximumSearchesPerFrame: 2\n  maximumGoalSnap: 1\n')
    markers=[scene.add_object('NPC Spawn '+str(i+1).zfill(2),arena_tr,p)[1] for i,p in enumerate(SPAWNS)]
    fields=f'  definition: {{fileID: 11400000, guid: {profile_guid}, type: 2}}\n  navigation: {{fileID: {nav}}}\n  playerHealth: {{fileID: {health}}}\n  playerMotor: {{fileID: {motor}}}\n  spawnPoints:\n'
    fields+=''.join(f'  - {{fileID: {point}}}\n' for point in markers)+'  maximumActors: 3\n'
    bootstrap=scene.add_component(arena,'IntercomNpcArena',fields)
    result=scene.text();counts=validate_scene_ids(result)
    allowed={robot,input_id,next(i for i,t in before.items() if t.startswith(f'--- !u!{ROOT_CLASS} '))}
    changed=[i for i,b in before.items() if scene.blocks[i]!=b]
    assert set(changed)<=allowed,changed
    return result,{'health':health,'vitality':vitality,'navigation':nav,'arena':bootstrap,'spawnTransforms':markers,'existingBlocksChanged':changed,'unrelatedBlocksPreserved':len(before)-len(changed),'localIds':counts}

def main():
    profile_guid=ensure_assets()
    previous=SCENE.read_text()
    result,report=install(previous,profile_guid)
    assert install(result,profile_guid)[0]==result,'Second install changed authored scene'
    SCENE.write_text(result)
    report.update(revision='npc-sandbox-v8',status='PASS',sceneSha256=hashlib.sha256(result.encode()).hexdigest(),repeatInstallIdempotent=True,
                  spawnMarkers=SPAWNS,profile=str(PROFILE.relative_to(ROOT)),
                  scope='Serialized component links and preservation; not Unity import or runtime physics.')
    (ROOT/'Validation/SceneNpcV8Review.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
