"""Validate the frozen normalized runtime sole profile against another source.
Blender --background --python this.py -- SOURCE.blend RUNTIME_PROFILE.json OUT.json [samples=129]
Only OUT.json is written. Calibrates from the candidate Idle pose like runtime.
"""
import bpy,sys,json,hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np
args=sys.argv[sys.argv.index('--')+1:];src=Path(args[0]);profilePath=Path(args[1]);out=Path(args[2]);samples=int(args[3]) if len(args)>3 else 129;N=1.3790181109932065
profile=json.loads(profilePath.read_text());before=hashlib.sha256(src.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(src));rig=bpy.data.objects['Intercom_Armature_Fixed'];scene=bpy.context.scene
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
boots={s:bpy.data.objects[n] for s,n in [('L','tripo_part_3_fixed'),('R','tripo_part_2_fixed')]};clips=['Run','RunBackward','RunLeft','RunRight','RunRelaxed','RunBackwardRelaxed','RunLeftRelaxed','RunRightRelaxed']
def frame(f):scene.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
def bone(n):return rig.matrix_world@rig.pose.bones[n].matrix

def vertices(obj):
    ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();v=np.empty(len(mesh.vertices)*3);mesh.vertices.foreach_get('co',v);m=np.array(ev.matrix_world);vs=v.reshape((-1,3))@m[:3,:3].T+m[:3,3];ev.to_mesh_clear();return vs

def transform(m,p):return p@m[:3,:3].T+m[:3,3]
def stats(v):return {'samples':len(v),'minimumErrorMetres':min(v),'maximumErrorMetres':max(v),'meanAbsoluteErrorMetres':sum(abs(x) for x in v)/len(v)}
rig.animation_data.action=bpy.data.actions['Idle'];frame(1);up=Vector((0,0,1));lateral=bone('L_Foot').translation-bone('R_Foot').translation;lateral-=up*lateral.dot(up);lateral.normalize();arrays={};weightValidation={};maxIdleError=0
for side,obj in boots.items():
    F=bone(side+'_Foot');T=bone(side+'_Toe');length=(F.translation-bone(side+'_Calf').translation).length;forward=T.translation-F.translation;forward-=up*forward.dot(up);forward-=lateral*forward.dot(lateral);forward.normalize()
    localF=[];localT=[];weights=[]
    for p in profile['points'][side]:
        def world(v):return F.translation+length*(lateral*v[0]+up*v[1]+forward*v[2])
        localF.append(list(F.inverted()@world(p['footPosition'])));localT.append(list(T.inverted()@world(p['toePosition'])));weights.append(p['toeWeight'])
    indices=np.array(profile['vertexIndices'][side]);lf=np.array(localF);lt=np.array(localT);w=np.array(weights)[:,None];arrays[side]=(lf,lt,w,indices)
    actual=vertices(obj);pred=transform(np.array(F),lf)*(1-w)+transform(np.array(T),lt)*w;maxIdleError=max(maxIdleError,float(np.linalg.norm(pred-actual[indices],axis=1).max())*N)
    groups={g.index:g.name for g in obj.vertex_groups};extra=set();maxSum=0;maxW=0
    for index,want in zip(indices,weights):
        influence={groups[g.group]:float(g.weight) for g in obj.data.vertices[int(index)].groups if g.weight>1e-8};maxSum=max(maxSum,abs(sum(influence.values())-1));extra.update(n for n in influence if n not in [side+'_Foot',side+'_Toe']);maxW=max(maxW,abs(influence.get(side+'_Toe',0)-want))
    weightValidation[side]={'onlyFootAndToe':not extra,'maximumWeightSumError':maxSum,'maximumStoredToeWeightError':maxW}
keys=['all','newStance0To40','flat08To30'];aggregate={k:[] for k in keys};perClip={};worst={};maxReplica=0;failures=[]
for name in clips:
    a=bpy.data.actions[name];rig.animation_data.action=a;start,end=map(float,a.frame_range);clip={side:{k:[] for k in keys} for side in boots}
    for i in range(samples):
        phase=i/(samples-1);frame(start+(end-start)*phase)
        for side,obj in boots.items():
            actual=vertices(obj);lf,lt,w,indices=arrays[side];pred=transform(np.array(bone(side+'_Foot')),lf)*(1-w)+transform(np.array(bone(side+'_Toe')),lt)*w;maxReplica=max(maxReplica,float(np.linalg.norm(pred-actual[indices],axis=1).max())*N)
            error=(float(pred[:,2].min())-float(actual[:,2].min()))*N;local=(phase-(.5 if side=='R' else 0))%1;active=['all']
            if local<.4:active.append('newStance0To40')
            if .08<=local<=.3:active.append('flat08To30')
            for key in active:
                aggregate[key].append(error);clip[side][key].append(error)
                if key not in worst or abs(error)>abs(worst[key]['errorMetres']):worst[key]={'clip':name,'phase':phase,'side':side,'errorMetres':error}
            if abs(error)>.0005:failures.append({'clip':name,'phase':phase,'side':side,'cloudMinimumErrorMetres':error})
    perClip[name]={side:{k:stats(v) for k,v in data.items()} for side,data in clip.items()};print('PROFILE_CHECK',name,'maxReplica',maxReplica,flush=True)
report={'source':str(src),'sourceSha256Before':before,'sourceSha256After':hashlib.sha256(src.read_bytes()).hexdigest(),'runtimeProfileSha256':hashlib.sha256(profilePath.read_bytes()).hexdigest(),'samplesPerClip':samples,'scope':'Frozen runtime anatomical profile calibrated in this source Idle, then actual deformed vertices at each Run sample. No Unity execution.','weightValidation':weightValidation,'maximumIdleVertexErrorMetres':maxIdleError,'maximumSelectedVertexReplicaErrorMetres':maxReplica,'aggregate':{k:stats(v) for k,v in aggregate.items()},'worst':worst,'exceedsHalfMillimetre':failures,'clips':perClip};report['status']='ok' if not failures and maxReplica<.0005 and before==report['sourceSha256After'] else 'failed';out.write_text(json.dumps(report,indent=2)+'\n');print('PROFILE_CHECK_DONE',report['status'],len(failures),json.dumps(report['aggregate']),flush=True)
