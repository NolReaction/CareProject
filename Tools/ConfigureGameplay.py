#!/usr/bin/env python3
"""Wire the current front end and authored arrivals into the tutorial scene.

Explicit opt-in: python Tools/ConfigureGameplay.py --apply-scene
Existing authored transforms, assignments and component tuning are preserved.
The shipped scene is already configured; normal project opening needs no script.
"""
from pathlib import Path
import argparse,re,json,uuid
import AuthorNpcScene as author
import BuildTutorialDistrict as district
import BuildSpawnHatches as hatches
from UnitySceneIds import validate_scene_ids

ROOT=Path(__file__).resolve().parents[1]
SCENE=ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'
MESHES=ROOT/'Assets/IntercomMVP/Content/Enemies/Spawns/Meshes'
POINTS=[('Spawn Hatch - Central Yard',(4,0,-2),2),('Spawn Hatch - Workshop Lane',(-8,0,-5),2),('Ground Arrival - North Yard',(1,0,6),1)]

def guid(path):
    return re.search(r'^guid: ([0-9a-f]{32})$',Path(str(ROOT/path)+'.meta').read_text(),re.M)[1]

def local(ident):return '{fileID: '+str(ident)+'}'

def asset(path,fileid):return '{fileID: '+str(fileid)+', guid: '+guid(path)+', type: 2}'

def fill_if_empty(scene,ident,key,value):
    pattern=r'^  '+re.escape(key)+r': ([^\n]*)$';m=re.search(pattern,scene.blocks[ident],re.M)
    if not m:scene.blocks[ident]+='  '+key+': '+value+'\n'
    elif m[1]=='{fileID: 0}':scene.blocks[ident]=re.sub(pattern,'  '+key+': '+value,scene.blocks[ident],flags=re.M)

def add_renderer(scene,go,meshname,materials,active=True):
    # Reuse the shipped engine-version-specific renderer serializer shape.
    existing=[i for i,t in scene.blocks.items() if t.startswith('--- !u!23 ') and f'm_GameObject: {{fileID: {go}}}' in t]
    if existing:return existing[0]
    mf,mr=scene.ids.allocate(),scene.ids.allocate()
    scene.blocks[go]=scene.blocks[go].replace('  m_Layer:',f'  - component: {{fileID: {mf}}}\n  - component: {{fileID: {mr}}}\n  m_Layer:',1)
    scene.blocks[mf]=f'--- !u!33 &{mf}\nMeshFilter:\n'+author.common(go)+'  m_Mesh: '+asset('Assets/IntercomMVP/Content/Enemies/Spawns/Meshes/'+meshname+'.mesh',4300000)+'\n'
    template=next(t for t in scene.blocks.values() if t.startswith('--- !u!23 '))
    template=re.sub(r'^--- !u!23 &(-?\d+)',f'--- !u!23 &{mr}',template)
    template=re.sub(r'm_GameObject: \{fileID: -?\d+\}',f'm_GameObject: {{fileID: {go}}}',template)
    template=re.sub(r'm_CastShadows: \d+','m_CastShadows: 0',template)
    refs=[]
    for name in materials:
        path='Assets/IntercomMVP/Content/Enemies/Udarnik/Materials/UdarnikTelegraph.mat' if name=='Warning' else 'Assets/IntercomMVP/Content/Environments/TutorialYard/Materials/'+name+'.mat'
        refs.append('  - '+asset(path,2100000)+'\n')
    template=re.sub(r'  m_Materials:\n(?:  - .*\n)+','  m_Materials:\n'+''.join(refs),template)
    scene.blocks[mr]=template
    if not active:scene.blocks[go]=scene.blocks[go].replace('  m_IsActive: 1\n','  m_IsActive: 0\n')
    return mr

def configure(text,meshdata):
    scene=author.Scene(text);root=scene.find_go('MVP_NpcSandbox');hero=scene.find_go('Robot')
    if root is None or hero is None:raise ValueError('Expected configured tutorial mission and hero')
    root_tr=scene.transform(root);director=scene.component(root,'IntercomMissionDirector');input_id=scene.component(hero,'IntercomPlayerInput')
    health=scene.component(hero,'IntercomHealth');motor=scene.component(hero,'IntercomPlayerMotor');assert all([director,input_id,health,motor])
    fields=''.join('  '+key+': '+local(value)+'\n' for key,value in [('mission',director),('input',input_id),('playerHealth',health),('playerMotor',motor)])
    fields+='  weapon: '+asset('Assets/IntercomMVP/Content/Weapons/Pistols/Settings/DualPistols.asset',11400000)+'\n  gameTitle: ЗАБОТА\n'
    fields+='  heroPortrait: '+asset('Assets/IntercomMVP/Content/Frontend/Textures/RobotPortrait.png',2800000)+'\n'
    fields+='  weaponPortrait: '+asset('Assets/IntercomMVP/Content/Frontend/Textures/PistolPortrait.png',2800000)+'\n  roomCamera: {fileID: 0}\n'
    frontend=scene.add_component(root,'IntercomFrontEnd',fields)
    for key,value in [('mission',local(director)),('input',local(input_id)),('playerHealth',local(health)),('playerMotor',local(motor)),('weapon',asset('Assets/IntercomMVP/Content/Weapons/Pistols/Settings/DualPistols.asset',11400000)),('heroPortrait',asset('Assets/IntercomMVP/Content/Frontend/Textures/RobotPortrait.png',2800000)),('weaponPortrait',asset('Assets/IntercomMVP/Content/Frontend/Textures/PistolPortrait.png',2800000))]:fill_if_empty(scene,frontend,key,value)
    arrivals=[]
    for name,position,kind in POINTS:
        point,point_tr=scene.add_object(name,root_tr,position)
        warning_go,warning_tr=scene.add_object(name+' warning',point_tr)
        add_renderer(scene,warning_go,'SpawnWarningRing',meshdata['SpawnWarningRing']['materials'],False)
        hinge_tr=0
        if kind==2:
            rim,_=scene.add_object(name+' rim',point_tr);add_renderer(scene,rim,'HatchRim',meshdata['HatchRim']['materials'])
            hinge,hinge_tr=scene.add_object(name+' hinge',point_tr,(0,0,-.5))
            lid,_=scene.add_object(name+' lid',hinge_tr);add_renderer(scene,lid,'HatchLid',meshdata['HatchLid']['materials'])
        fields=f'  kind: {kind}\n  minimumDistance: 5.5\n  cooldownSeconds: 4\n  hatchLid: '+local(hinge_tr)+'\n  warningVisual: '+local(warning_tr)+'\n'
        point_id=scene.add_component(point,'IntercomEnemySpawnPoint',fields);arrivals.append(point_id)
        fill_if_empty(scene,point_id,'hatchLid',local(hinge_tr));fill_if_empty(scene,point_id,'warningVisual',local(warning_tr))
    block=scene.blocks[director]
    if not re.search(r'^  authoredSpawnPoints:',block,re.M):scene.blocks[director]+='  authoredSpawnPoints:\n'+''.join('  - '+local(i)+'\n' for i in arrivals)
    elif '  authoredSpawnPoints: []\n' in block:scene.blocks[director]=block.replace('  authoredSpawnPoints: []\n','  authoredSpawnPoints:\n'+''.join('  - '+local(i)+'\n' for i in arrivals))
    # Persist the visible hit settings on the authored scenery as well as class defaults.
    for go, block in list(scene.blocks.items()):
        if not block.startswith('--- !u!1 '):continue
        for class_name,amount,strength in [('IntercomReactiveProp',.16,.48),('IntercomTargetDummy',.20,.58)]:
            component=scene.component(go,class_name)
            if component is None:continue
            for key,value in [('hitDuration','.26'),('hitShapeAmount',str(amount)),('hitFlashStrength',str(strength)),('hitTint','{r: .94, g: .25, b: .16, a: 1}')]:fill_if_empty(scene,component,key,value)
    output=scene.text();validate_scene_ids(output)
    return output,{'frontend':frontend,'authoredSpawnComponents':arrivals,'points':[{'name':n,'position':p,'kind':k} for n,p,k in POINTS]}

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--apply-scene',action='store_true');args=parser.parse_args()
    meshdata=hatches.generate()
    if not args.apply_scene:return
    text=SCENE.read_text();updated,report=configure(text,meshdata)
    assert configure(updated,meshdata)[0]==updated,'Scene setup was not idempotent'
    SCENE.write_text(updated)
    out=ROOT/'Validation/Reports/GameplayScene.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps({'status':'PASS','idempotent':True,**report},indent=2)+'\n')
    print(json.dumps(report,indent=2))
if __name__=='__main__':main()
