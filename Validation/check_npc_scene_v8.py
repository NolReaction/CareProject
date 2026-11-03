#!/usr/bin/env python3
"""Validate authored V8 NPC/player links and repeat-safe scene installation.

Optional --baseline-scene proves all unrelated serialized V7 blocks survived.
No Unity Editor or runtime physics is executed by this validator.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import InstallNpcDemo as install
from UnitySceneIds import validate_scene_ids

def field(block,key):
    match=re.search(r'^  '+re.escape(key)+r': (.*)$',block,re.M)
    assert match,key
    return match[1]

def local(block,key):
    return int(re.fullmatch(r'\{fileID: (-?\d+)\}',field(block,key))[1])

def value(block,key):return float(field(block,key))

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-scene',type=Path)
    args=parser.parse_args()
    scene_path=ROOT/'Assets/IntercomMVP/Scenes/IntercomMVP.unity'
    text=scene_path.read_text();scene=install.Scene(text)
    robot=scene.find_go('Robot');arena_go=scene.find_go('MVP_NpcSandbox')
    assert robot and arena_go
    health=scene.component(robot,'IntercomHealth');vitality=scene.component(robot,'IntercomPlayerVitality')
    input_id=scene.component(robot,'IntercomPlayerInput');motor=scene.component(robot,'IntercomPlayerMotor')
    shooter=scene.component(robot,'IntercomSimpleShooter');pose=scene.component(robot,'IntercomWeaponPose')
    nav=scene.component(arena_go,'IntercomNpcNavigation');arena=scene.component(arena_go,'IntercomNpcArena')
    assert all([health,vitality,input_id,motor,shooter,pose,nav,arena])
    for key,expected in [('health',health),('motor',motor),('input',input_id),('shooter',shooter),('weaponPose',pose)]:
        assert local(scene.blocks[vitality],key)==expected,key
    assert local(scene.blocks[input_id],'vitality')==vitality
    assert value(scene.blocks[health],'maximumHealth')>0
    assert value(scene.blocks[health],'team')==1
    assert 0<=value(scene.blocks[health],'postHitInvulnerability')<=2
    environment=scene.find_go('MVP_TestEnvironment')
    assert local(scene.blocks[nav],'obstacleRoot')==scene.transform(environment)
    assert .25<=value(scene.blocks[nav],'cellSize')<=1
    assert value(scene.blocks[nav],'agentClearanceRadius')>=.4
    assert 1<=value(scene.blocks[nav],'maximumSearchesPerFrame')<=8
    for key,expected in [('playerHealth',health),('playerMotor',motor),('navigation',nav)]:
        assert local(scene.blocks[arena],key)==expected
    points=re.search(r'^  spawnPoints:\n((?:  - \{fileID: \d+\}\n)+)',scene.blocks[arena],re.M)
    assert points
    point_ids=[int(x) for x in re.findall(r'fileID: (\d+)',points[1])]
    assert len(point_ids)==3 and len(set(point_ids))==3
    assert value(scene.blocks[arena],'maximumActors')==3
    positions=[]
    for ident in point_ids:
        block=scene.blocks[ident]
        assert block.startswith('--- !u!4 ')
        assert local(block,'m_Father')==scene.transform(arena_go)
        numbers=[float(x) for x in re.findall(r'[xyz]: ([^,}]+)',field(block,'m_LocalPosition'))]
        assert len(numbers)==3
        positions.append(numbers)

    # Spawn points must themselves be outside inflated static footprints; runtime
    # projection additionally snaps them to the navigation grid's free centres.
    layout=json.loads((ROOT/'Assets/IntercomMVP/World/DistrictLayout.json').read_text())
    obstacle_count=0
    for group in layout['groups']:
        for block in group['blocks']:
            if not block['collider']:continue
            scale=block['size'];size=[a*b for a,b in zip(block.get('collisionSize',[1,1,1]),scale)]
            center=[a*b for a,b in zip(block.get('collisionCenter',[0,0,0]),scale)]
            yaw=math.radians(block['yaw']);c,s=math.cos(yaw),math.sin(yaw)
            x=group['position'][0]+block['position'][0]+c*center[0]+s*center[2]
            z=group['position'][2]+block['position'][2]-s*center[0]+c*center[2]
            y=group['position'][1]+block['position'][1]+center[1]
            if y+size[1]/2<=.18:continue
            obstacle_count+=1
            hx=(abs(c)*size[0]+abs(s)*size[2])/2+.4
            hz=(abs(s)*size[0]+abs(c)*size[2])/2+.4
            for p in positions:
                assert not (abs(p[0]-x)<hx and abs(p[2]-z)<hz),(p,block['name'])

    profile=ROOT/'Assets/IntercomMVP/Game/Enemies/Udarnik/UdarnikCapsule.asset'
    profile_text=profile.read_text()
    profile_guid=install.metadata(profile,11400000)
    assert f'guid: {profile_guid},' in field(scene.blocks[arena],'definition')
    parameters={key:value(profile_text,key) for key in ['maximumHealth','damage','moveSpeed','attackRange','attackHalfAngle','windupSeconds','recoverySeconds','detectionRadius','leashRadius']}
    assert all(math.isfinite(x) and x>0 for x in parameters.values())
    assert parameters['attackHalfAngle']<=85
    materials=[]
    for name,field_name in [('UdarnikBody','bodyMaterial'),('UdarnikDetail','detailMaterial'),('UdarnikTelegraph','telegraphMaterial')]:
        path=profile.parent/(name+'.mat');guid=install.metadata(path,2100000)
        assert f'guid: {guid},' in field(profile_text,field_name)
        assert 'm_Shader: {fileID: 4800000, guid:' in path.read_text()
        materials.append(str(path.relative_to(ROOT)))
    telegraph=(profile.parent/'UdarnikTelegraph.mat').read_text()
    assert '_SURFACE_TYPE_TRANSPARENT' in telegraph and '_ZWrite: 0' in telegraph
    assert '_SrcBlend: 5' in telegraph and '_DstBlend: 10' in telegraph
    repeated,_=install.install(text,profile_guid)
    assert repeated==text,'Repeat installer changed existing scene tuning'
    report_path=ROOT/'Validation/SceneNpcV8Review.json'
    report=json.loads(report_path.read_text()) if report_path.exists() else {}
    report.update(revision='npc-sandbox-v8',status='PASS',sceneSha256=hashlib.sha256(scene_path.read_bytes()).hexdigest(),
                  repeatInstallIdempotent=True,localIds=validate_scene_ids(text),spawnMarkers=positions,
                  spawnPointsOutsideInflatedObstacles=True,checkedObstacleCount=obstacle_count,
                  heroHealth=value(scene.blocks[health],'maximumHealth'),heroInvulnerability=value(scene.blocks[health],'postHitInvulnerability'),
                  npcParameters=parameters,sharedMaterials=materials,
                  contracts={'explicitNavObstacleRoot':True,'explicitPlayerAndResetRefs':True,'inputVitalityRef':True,'boundedActors':3},
                  scope='Serialized component links, asset dependencies, safe spawn footprints and installer idempotence. No Unity runtime/PhysX execution.')
    if args.baseline_scene:
        baseline=install.Scene(args.baseline_scene.read_text())
        changed=[i for i,b in baseline.blocks.items() if scene.blocks.get(i)!=b]
        assert set(changed)=={robot,input_id,9223372036854775807},changed
        assert not set(baseline.blocks)-set(scene.blocks)
        report['existingBlocksChanged']=changed
        report['unrelatedBlocksPreserved']=len(baseline.blocks)-len(changed)
        report['baselineSceneSha256']=hashlib.sha256(args.baseline_scene.read_bytes()).hexdigest()
    report_path.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({k:report[k] for k in ['status','localIds','spawnPointsOutsideInflatedObstacles','npcParameters']},indent=2))

if __name__=='__main__':main()
