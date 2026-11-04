#!/usr/bin/env python3
"""Check authored menu-camera ownership/isolation and replaceable room content.

This inspects the real shipped Unity YAML, meshes and links. It does not render
Unity or substitute for Play Mode/device testing.
"""
from pathlib import Path
import json,math,re,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from AuthorNpcScene import Scene
from UnitySceneIds import validate_scene_ids
import BuildMenuHub as hub

def main():
    path=ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity'
    source=path.read_text();scene=Scene(source);checks=0
    def check(condition,label):
        nonlocal checks
        checks+=1
        if not condition:raise AssertionError(label)
    def field(block,key):
        match=re.search(r'^  '+re.escape(key)+r': (.*)$',block,re.M)
        check(match is not None,'missing '+key);return match[1]
    def local(block,key):return int(re.search(r'fileID: (-?\d+)',field(block,key))[1])
    def builtin(owner,kind):
        values=[i for i,t in scene.blocks.items() if t.startswith('--- !u!'+str(kind)+' ') and 'm_GameObject: {fileID: '+str(owner)+'}' in t]
        check(len(values)==1,'builtin component '+str(kind));return values[0]
    root=scene.find_go('MVP_MenuHub');check(root is not None,'menu hub absent')
    layer_text=(ROOT/'ProjectSettings/TagManager.asset').read_text().split('  layers:\n',1)[1].split('  m_SortingLayers:',1)[0].splitlines()
    check(layer_text[hub.LAYER].strip()=='- MenuPreview','menu layer unnamed')
    rt=scene.transform(root);rootpos=field(scene.blocks[rt],'m_LocalPosition')
    check([float(v) for v in re.findall(r'[xyz]: ([^,}]+)',rootpos)]==list(hub.ORIGIN),'hub isolation origin')
    mission_root=scene.find_go('MVP_NpcSandbox');frontend=scene.component(mission_root,'IntercomFrontEnd')
    # Actual imported art is optional in code, but deliberately wired in this shipped scene.
    from PIL import Image
    art=[]
    for key,name in [('homeDoodle','HomeDoodle'),('rescueDoodle','RescueDoodle')]:
        image_path=ROOT/'Assets/IntercomMVP/Content/Frontend/Artwork'/(name+'.png')
        meta=Path(str(image_path)+'.meta').read_text()
        guid=re.search(r'^guid: ([0-9a-f]{32})$',meta,re.M)[1]
        check(guid in field(scene.blocks[frontend],key),'illustration reference '+name)
        check('  alphaIsTransparency: 1' in meta,'illustration alpha importer')
        check('  nPOTScale: 0' in meta,'illustration importer stretches aspect')
        check('  maxTextureSize: 1024' in meta and '  isReadable: 0' in meta,'illustration mobile import bounds')
        with Image.open(image_path) as im:
            check(im.mode=='RGBA' and im.getchannel('A').getextrema()==(0,255),'genuine transparent art')
            check(im.getpixel((0,0))[3]==0 and im.getpixel((im.width-1,im.height-1))[3]==0,'opaque art corners')
            art.append({'name':name,'width':im.width,'height':im.height})
    rig=scene.component(root,'IntercomFrontEndRoomCamera');check(local(scene.blocks[frontend],'roomCamera')==rig,'frontend not linked to hub')
    camera=local(scene.blocks[rig],'roomCamera');game_camera=local(scene.blocks[rig],'gameplayCamera')
    check(camera!=game_camera,'shared game/menu camera')
    check(camera in scene.blocks and game_camera in scene.blocks,'camera reference missing')
    camera_go=local(scene.blocks[camera],'m_GameObject');check(field(scene.blocks[camera_go],'m_TagString')=='Untagged','menu camera claims Camera.main')
    urp=[t for t in scene.blocks.values() if 'UniversalAdditionalCameraData' in t and 'm_GameObject: {fileID: '+str(camera_go)+'}' in t]
    check(len(urp)==1,'menu URP camera settings missing')
    check(field(urp[0],'m_RenderPostProcessing')=='0' and field(urp[0],'m_RenderShadows')=='0','gameplay effects leaked to menu camera')
    hero=scene.find_go('Robot');player_input=scene.component(hero,'IntercomPlayerInput')
    portrait=(ROOT/'Assets/IntercomMVP/Content/Frontend/Textures/PistolPortrait.png.meta').read_text()
    portrait_guid=re.search(r'^guid: ([0-9a-f]{32})$',portrait,re.M)[1]
    check(portrait_guid in field(scene.blocks[player_input],'weaponHudPortrait'),'equipped weapon HUD icon not wired')
    check(field(scene.blocks[camera],'m_Enabled')=='0','menu camera must start disabled')
    def mask(block):return int(re.search(r'm_CullingMask:\n    serializedVersion: \d+\n    m_Bits: (\d+)',block)[1])
    check(mask(scene.blocks[camera])==1<<hub.LAYER,'menu camera sees gameplay layer')
    check(mask(scene.blocks[game_camera])&(1<<hub.LAYER)==0,'gameplay camera sees hub')
    check(float(field(scene.blocks[camera],'near clip plane'))>0,'invalid camera near clip')
    check(0<float(field(scene.blocks[camera],'far clip plane'))<70,'hub camera could reach yard')
    check(float(field(scene.blocks[camera],'orthographic size'))==3.9,'authored framing drift')
    check(field(scene.blocks[camera],'orthographic')=='1','hub not orthographic')
    anchor_ids=[]
    for key,x in [('home',0),('play',0),('robot',11),('armory',22)]:
        tr=local(scene.blocks[rig],key+'Anchor');check(tr in scene.blocks,'anchor missing')
        check(local(scene.blocks[tr],'m_Father')==rt,'anchor outside hub')
        xyz=[float(v) for v in re.findall(r'[xyz]: ([^,}]+)',field(scene.blocks[tr],'m_LocalPosition'))]
        expected,_=hub.camera_pose(x);check(all(abs(a-b)<.00001 for a,b in zip(xyz,expected)),'camera position '+key)
        q=[float(v) for v in re.findall(r'[xyzw]: ([^,}]+)',field(scene.blocks[tr],'m_LocalRotation'))]
        check(len(q)==4 and all(math.isfinite(v) for v in q) and abs(sum(v*v for v in q)-1)<.0001,'camera rotation '+key)
        anchor_ids.append(tr)
    check(len(set(anchor_ids))==4,'room anchors not independent')
    def belongs(tr):
        seen=set()
        while tr:
            check(tr not in seen,'cyclic hierarchy');seen.add(tr)
            if tr==rt:return True
            parent=re.search(r'^  m_Father: \{fileID: (-?\d+)\}',scene.blocks[tr],re.M)
            if parent is None:return False # Stripped prefab transforms outside this authored hub.
            tr=int(parent[1])
        return False
    members=[];renderers=0
    for go,t in scene.blocks.items():
        if not t.startswith('--- !u!1 '):continue
        tr=scene.transform(go)
        if belongs(tr):members.append(go)
    for go in members:
        check(not any(t.startswith(('--- !u!54 ','--- !u!65 ','--- !u!135 ','--- !u!136 ','--- !u!143 ','--- !u!64 ','--- !u!81 ','--- !u!82 ')) and 'm_GameObject: {fileID: '+str(go)+'}' in t for t in scene.blocks.values()),'hub has physics or audio listener/source')
        mesh=[t for t in scene.blocks.values() if t.startswith('--- !u!33 ') and 'm_GameObject: {fileID: '+str(go)+'}' in t]
        if mesh:
            renderers+=1;check(field(scene.blocks[go],'m_Layer')==str(hub.LAYER),'hub renderer outside isolated layer')
    check(renderers==16,'unexpected authored module instance count')
    for name,x,props in hub.ROOMS:
        check(scene.find_go('Menu Hub - '+name) is not None,'room missing '+name)
        for prop,module,pos,yaw in props:check(scene.find_go('Menu '+name+' - '+prop) is not None,'replaceable prop missing '+prop)
    files=list((ROOT/'Assets/IntercomMVP/Content/Frontend/Meshes').glob('*.mesh'))
    check(len(files)==11,'shared mesh count')
    triangles=0
    for path in files:
        text=path.read_text();vertices=int(re.search(r'm_VertexCount: (\d+)',text)[1]);indices=re.search(r'm_IndexBuffer: ([0-9a-f]+)',text)[1]
        check(0<vertices<65536,'mesh vertex bounds');check(len(indices)%12==0,'triangle index data');triangles+=len(indices)//12
    check(triangles<10000,'hub shared geometry budget')
    result={'status':'PASS','checks':checks,'rooms':len(hub.ROOMS),'moduleInstances':renderers,'sharedMeshes':len(files),'sharedTriangles':triangles,'illustrations':art,'isolatedLayer':hub.LAYER,'noPhysicsOrAudio':True,'localIds':validate_scene_ids(source),'scope':'Real serialized links/mesh buffers/isolation/transparent PNGs, no Unity rendering or Play Mode.'}
    (ROOT/'Validation/Reports/UiScene.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
if __name__=='__main__':main()
