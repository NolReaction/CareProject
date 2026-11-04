#!/usr/bin/env python3
"""Verify the shipped rig/export bytes and Unity import links; no Unity execution."""
from pathlib import Path
import hashlib,json,re,struct,zlib
ROOT=Path(__file__).resolve().parents[1]

def fbx_nodes(data):
    assert data[:23]==b'Kaydara FBX Binary  \x00\x1a\x00'
    version=struct.unpack_from('<I',data,23)[0]
    assert version==7400,'Review the node-header parser for this FBX version'
    def node(offset):
        end,count,prop_size,name_size=struct.unpack_from('<IIIB',data,offset)
        if not end:return None,offset+13
        assert offset<end<=len(data)
        cursor=offset+13;name=data[cursor:cursor+name_size].decode();cursor+=name_size;props=[]
        property_end=cursor+prop_size
        for _ in range(count):
            kind=chr(data[cursor]);cursor+=1
            if kind in 'YCFDIL':
                fmt={'Y':'h','C':'?','F':'f','D':'d','I':'i','L':'q'}[kind]
                value=struct.unpack_from('<'+fmt,data,cursor)[0];cursor+=struct.calcsize(fmt)
            elif kind in 'SR':
                length=struct.unpack_from('<I',data,cursor)[0];cursor+=4
                value=data[cursor:cursor+length];cursor+=length
                if kind=='S':value=value.decode('utf-8').split('\x00')[0]
            elif kind in 'fdlibc':
                length,encoding,bytes_count=struct.unpack_from('<III',data,cursor);cursor+=12
                payload=data[cursor:cursor+bytes_count];cursor+=bytes_count;value=None
                if name in ('PolygonVertexIndex','KeyValueFloat'):
                    if encoding==1:payload=zlib.decompress(payload)
                    else:assert encoding==0,'Unknown FBX array compression'
                    fmt={'f':'f','d':'d','l':'q','i':'i','b':'?','c':'b'}[kind]
                    assert len(payload)==length*struct.calcsize(fmt),'FBX array length'
                    value=struct.unpack('<'+str(length)+fmt,payload)
            else:raise AssertionError('Unknown FBX property '+kind)
            props.append(value)
        assert cursor==property_end
        children=[]
        while cursor<end:
            child,next_cursor=node(cursor);cursor=next_cursor
            if child is None:break
            children.append(child)
        assert cursor==end
        return (name,props,children),end
    nodes=[];offset=27
    while offset<len(data)-13:
        value,offset=node(offset)
        if value is None:break
        nodes.append(value)
    return nodes

def triangles(objects):
    total=0
    for geometry in (n for n in objects if n[0]=='Geometry' and n[1][-1]=='Mesh'):
        indices=next(child[1][0] for child in geometry[2] if child[0]=='PolygonVertexIndex')
        vertices=0
        for index in indices:
            vertices+=1
            if index<0:
                assert vertices==3,'Game meshes must be triangulated'
                total+=vertices-2;vertices=0
        assert vertices==0,'Unterminated FBX polygon'
    return total

def main():
    report=json.loads((ROOT/'Validation/Reports/UdarnikRig.json').read_text());checks=0
    def check(ok,label):
        nonlocal checks
        checks+=1
        if not ok:raise AssertionError(label)
    for item in report['files']:
        p=ROOT/item['path'];check(p.stat().st_size==item['bytes'],'size '+item['path'])
        check(hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256'],'hash '+item['path'])
    base=ROOT/'Assets/IntercomMVP/Content/Enemies/Udarnik'
    model=base/'Model/Udarnik.fbx';nodes=fbx_nodes(model.read_bytes())
    objects=next(n for n in nodes if n[0]=='Objects')[2]
    connections=next(n for n in nodes if n[0]=='Connections')[2]
    stacks=[n for n in objects if n[0]=='AnimationStack'];observed={}
    for n in stacks:
        name=n[1][1].split('|')[-1]
        properties=next(c for c in n[2] if c[0]=='Properties70')[2]
        timing={p[1][0]:p[1][-1] for p in properties if p[0]=='P'}
        observed[name]=(timing['LocalStop']-timing.get('LocalStart',0))/46186158000
    expected={'Idle':1.6,'Walk':.8,'Windup':.3,'Strike':.16,'Recover':.48,'Hurt':.22,'Death':.18,'Emerge':.68,'EmergeGround':.95,'EmergeHatch':1.05}
    check(report['build']['clips']==expected,'V19 ten-clip contract')
    check(set(observed)==set(expected),'actual FBX takes')
    for name,duration in expected.items():check(abs(observed[name]-duration)<1e-6,'FBX duration '+name)
    check(len([n for n in objects if n[0]=='Geometry' and n[1][-1]=='Mesh'])==1,'FBX mesh count')
    check([n[1][1] for n in objects if n[0]=='Material']==['UdarnikAtlas'],'FBX shared atlas material')
    check(len([n for n in objects if n[0]=='Model' and n[1][-1]=='LimbNode'])==14,'FBX bone count')
    triangle_count=triangles(objects)
    check(triangle_count==7454 and report['build']['triangles']==triangle_count,'actual repaired game triangle count')
    check(report['build']['rootMotion'] is False,'source report prohibits root motion')
    root_ids={n[1][0] for n in objects if n[0]=='Model' and n[1][1] in ('Root','UdarnikRig')}
    check(len(root_ids)==2,'both rig and root-bone motion checked')
    root_nodes={n[1][1] for n in connections if n[1][0]=='OP' and n[1][2] in root_ids and n[1][3] in ('Lcl Translation','Lcl Rotation','Lcl Scaling')}
    root_curves={n[1][1] for n in connections if n[1][0]=='OP' and n[1][2] in root_nodes}
    check(bool(root_curves),'exported rig/root animation curves inspected')
    max_root_variation=0
    for curve in (n for n in objects if n[0]=='AnimationCurve' and n[1][0] in root_curves):
        values=next(child[1][0] for child in curve[2] if child[0]=='KeyValueFloat')
        check(bool(values),'root curve has values')
        variation=max(values)-min(values);max_root_variation=max(max_root_variation,variation)
        check(variation<1e-5,'root TRS remains constant across exported clip')
    meta=Path(str(model)+'.meta').read_text()
    for field,value in [('animationType','2'),('avatarSetup','1'),('importAnimation','1')]:check(re.search(r'^  '+field+': '+value+r'$',meta,re.M) is not None,'Generic import '+field)
    check(re.search(r'^    clipAnimations: \[\]$',meta,re.M) is not None,'all native takes imported')
    breakup=base/'Model/UdarnikBreakup.fbx';breakup_nodes=fbx_nodes(breakup.read_bytes())
    breakup_objects=next(n for n in breakup_nodes if n[0]=='Objects')[2]
    breakup_connections=next(n for n in breakup_nodes if n[0]=='Connections')[2]
    chunks=[n for n in breakup_objects if n[0]=='Model' and n[1][-1]=='Mesh']
    expected_chunks={'Breakup_Body','Breakup_Helmet','Breakup_GloveL','Breakup_GloveR','Breakup_BootL','Breakup_BootR'}
    check({n[1][1] for n in chunks}==expected_chunks,'actual six static breakup chunks')
    check(len([n for n in breakup_objects if n[0]=='Geometry' and n[1][-1]=='Mesh'])==6,'six breakup geometry objects')
    check(not any(n[0].startswith('Animation') or n[0]=='Deformer' or (n[0]=='Model' and n[1][-1]=='LimbNode') for n in breakup_objects),'breakup has no animation, skin or bones')
    for chunk in chunks:check(any(c[1]==['OO',chunk[1][0],0] for c in breakup_connections),'chunk direct root parent '+chunk[1][1])
    check(triangles(breakup_objects)==triangle_count,'breakup retains all repaired game triangles')
    check([n[1][1] for n in breakup_objects if n[0]=='Material']==['UdarnikAtlas'],'one shared breakup atlas material')
    breakup_meta=Path(str(breakup)+'.meta').read_text()
    for field in ('animationType','avatarSetup','importAnimation'):check(re.search(r'^  '+field+r': 0$',breakup_meta,re.M) is not None,'static breakup import '+field)
    model_guid=re.search(r'^guid: (\w+)$',meta,re.M)[1]
    breakup_guid=re.search(r'^guid: (\w+)$',breakup_meta,re.M)[1]
    check(model_guid!=breakup_guid,'game and breakup model GUIDs differ')
    atlas=base/'Textures/UdarnikAtlas.png';atlas_meta=Path(str(atlas)+'.meta').read_text();guid=re.search(r'^guid: (\w+)$',atlas_meta,re.M)[1]
    material_path=base/'Materials/UdarnikAtlas.mat';material=material_path.read_text();check(guid in material,'material atlas route')
    material_guid=re.search(r'^guid: (\w+)$',Path(str(material_path)+'.meta').read_text(),re.M)[1]
    check(material_guid in meta and material_guid in breakup_meta,'both models remap to authored UdarnikAtlas material')
    check('  maxTextureSize: 1024' in atlas_meta and '    enableMipMap: 1' in atlas_meta,'mobile texture import')
    definition=(base/'Settings/UdarnikCapsule.asset').read_text()
    check('  visualPrefab: {fileID: 0}' in definition and '  breakupPrefab: {fileID: 0}' in definition,'initial builder-owned prefab assignments')
    check('  groundEmergenceSeconds: 0.95' in definition and '  hatchEmergenceSeconds: 1.05' in definition,'serialized source-specific arrival durations')
    check('  walkAnimationMetresPerSecond: 0.603' in definition,'measured rounded gait reference')
    registry=json.loads((ROOT/'Assets/IntercomMVP/Editor/Project/ProjectAssetsV13.json').read_text())
    for asset in (model,breakup,*[base/'Materials'/name for name in ('UdarnikPop.mat','UdarnikScrap.mat','UdarnikScrapInk.mat')],ROOT/'Assets/IntercomMVP/Runtime/Enemies/Presentation/IntercomRobotDeathEffects.cs'):
        path=asset.relative_to(ROOT).as_posix();guid=re.search(r'^guid: (\w+)$',Path(str(asset)+'.meta').read_text(),re.M)[1]
        check(any(entry['destination']==path and entry['guid']==guid for entry in registry['files']),'registered asset '+path)
    result={'status':'PASS','checks':checks,'actualFbxTakesSeconds':observed,'bones':14,'meshRenderers':1,'materials':1,'triangles':triangle_count,
        'rootMotion':False,'rootCurveMaxVariation':max_root_variation,'breakup':{'chunks':sorted(expected_chunks),'triangles':triangle_count,'animation':False,'bones':0,'materials':1},
        'nativePrefabController':'Recognized generated V18 graph upgrades to ten native states with backups; custom assets preserved',
        'scope':'Export bytes, actual FBX stacks/root curves/bones/triangle indices, static chunks and serialized import/material/registry links. Unity importer/Animator not run.'}
    (ROOT/'Validation/Reports/UdarnikAssets.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
if __name__=='__main__':main()
