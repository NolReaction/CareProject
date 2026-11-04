#!/usr/bin/env python3
"""Check that the shipped scene actually wires V15 menu, hatch and cosmetic data.
Serialized-data check only; does not execute Unity, PhysX or rendering.
"""
from pathlib import Path
import json,math,re,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import AuthorNpcScene as author
import ConfigureGameplay as setup
from UnitySceneIds import validate_scene_ids


def main():
    scene=author.Scene(setup.SCENE.read_text());checks=0
    def check(value,label):
        nonlocal checks
        checks+=1
        if not value:raise AssertionError(label)
    def field(block,key):
        m=re.search(r'^  '+re.escape(key)+r': (.*)$',block,re.M);check(m is not None,key+' missing');return m[1]
    def local(block,key):return int(re.search(r'fileID: (-?\d+)',field(block,key))[1])
    root=scene.find_go('MVP_NpcSandbox');hero=scene.find_go('Robot');director=scene.component(root,'IntercomMissionDirector');menu=scene.component(root,'IntercomFrontEnd')
    check(menu is not None,'front end not wired')
    for key,value in [('mission',director),('input',scene.component(hero,'IntercomPlayerInput')),('playerHealth',scene.component(hero,'IntercomHealth')),('playerMotor',scene.component(hero,'IntercomPlayerMotor'))]:check(local(scene.blocks[menu],key)==value,'menu '+key)
    for key,name in [('heroPortrait','RobotPortrait.png'),('weaponPortrait','PistolPortrait.png')]:
        path=ROOT/'Assets/IntercomMVP/Content/Frontend/Textures'/name
        check(path.is_file(),'portrait missing');check(setup.guid(path.relative_to(ROOT).as_posix()) in field(scene.blocks[menu],key),'portrait reference')
        check('alphaIsTransparency: 1' in Path(str(path)+'.meta').read_text(),'portrait alpha')
    check(local(scene.blocks[menu],'roomCamera')==0,'future room camera must remain unassigned in yard')
    points=re.search(r'^  authoredSpawnPoints:\n((?:  - \{fileID: \d+\}\n)+)',scene.blocks[director],re.M);check(points is not None,'authored points missing')
    ids=[int(v) for v in re.findall(r'fileID: (\d+)',points[1])];check(len(ids)==3 and len(set(ids))==3,'authored point count')
    observed=[]
    layout=json.loads((ROOT/'Assets/IntercomMVP/Content/Environments/TutorialYard/Layout/DistrictLayout.json').read_text())
    for name,position,kind in setup.POINTS:
        owner=scene.find_go(name);transform=scene.transform(owner);component=scene.component(owner,'IntercomEnemySpawnPoint');check(component in ids,'unregistered point')
        data=scene.blocks[component];check(int(field(data,'kind'))==kind,'point kind');check(float(field(data,'minimumDistance'))>=5.5,'point distance')
        check(float(field(data,'cooldownSeconds'))>=4,'point cooldown')
        actual=[float(x) for x in re.findall(r'[xyz]: ([^,}]+)',field(scene.blocks[transform],'m_LocalPosition'))];check(actual==list(position),'authored position')
        check(local(scene.blocks[transform],'m_Father')==scene.transform(root),'point owner')
        if kind==2:check(local(data,'hatchLid')!=0,'hatch hinge missing')
        warning=local(data,'warningVisual');warning_go=local(scene.blocks[warning],'m_GameObject');check(field(scene.blocks[warning_go],'m_IsActive')=='0','warning starts visible')
        for group in layout['groups']:
            for item in group['blocks']:
                if not item['collider']:continue
                size=[a*b for a,b in zip(item.get('collisionSize',[1,1,1]),item['size'])];center=[a*b for a,b in zip(item.get('collisionCenter',[0,0,0]),item['size'])]
                yaw=math.radians(item['yaw']);c,s=math.cos(yaw),math.sin(yaw)
                if group['position'][1]+item['position'][1]+center[1]+size[1]/2<=.18:continue
                x=group['position'][0]+item['position'][0]+c*center[0]+s*center[2];z=group['position'][2]+item['position'][2]-s*center[0]+c*center[2]
                hx=(abs(c)*size[0]+abs(s)*size[2])/2+.7;hz=(abs(s)*size[0]+abs(c)*size[2])/2+.7
                check(not(abs(actual[0]-x)<hx and abs(actual[2]-z)<hz),'hatch intersects '+item['name'])
        observed.append({'name':name,'position':actual,'kind':kind})
    for name in ['Kill50','Rescue']:
        data=(ROOT/'Assets/IntercomMVP/Content/Missions'/(name+'.asset')).read_text()
        check(field(data,'visibleSpawnEvery')=='3','visible cadence');check(float(field(data,'minimumVisibleSpawnDistance'))==5.5,'mission spawn distance');check(float(field(data,'visibleSpawnWarningSeconds'))==.35,'warning duration')
    enemy=(ROOT/'Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset').read_text();check(float(field(enemy,'moveSpeed'))==2.65,'enemy speed');check(float(field(enemy,'loseTargetSeconds'))==6,'zombie memory')
    weapon=(ROOT/'Assets/IntercomMVP/Content/Weapons/Pistols/Settings/DualPistols.asset').read_text();check(field(weapon,'casingEveryNthShot')=='2','casing cadence');check(float(field(weapon,'fireInterval'))==.12,'shot rate changed');check(float(field(weapon,'damagePerProjectile'))==15,'damage changed')
    counts={}
    for class_name in ['IntercomReactiveProp','IntercomTargetDummy']:
        count=0
        for go,block in list(scene.blocks.items()):
            if not block.startswith('--- !u!1 '):continue
            component=scene.component(go,class_name)
            if component is None:continue
            count+=1;data=scene.blocks[component]
            check(float(field(data,'hitDuration'))==.26,'hit time');check(0<float(field(data,'hitShapeAmount'))<=.3,'shape amount');check(0<float(field(data,'hitFlashStrength'))<=1,'flash amount');check(local(data,'visual')!=0,'no visual assigned')
        check(count==3,class_name+' count');counts[class_name]=count
    result={'status':'PASS','checks':checks,'frontend':menu,'authoredPoints':observed,'reactiveCounts':counts,'localIds':validate_scene_ids(setup.SCENE.read_text()),'scope':'Serialized current gameplay/visual setup and authored obstacle clearance. No Unity/PhysX execution.'}
    out=ROOT/'Validation/Reports/GameplayIntegration.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'status':'PASS','checks':checks,'authoredPoints':len(ids),'reactiveObjects':sum(counts.values())}))

if __name__=='__main__':main()
