"""Read-only actual evaluated-mesh gait QA, including half-key frames and held weapons.
blender -b --python BlenderTools/Validate_Grounded_Run_V3.py -- model.blend report.json
"""
import bpy,sys,math,json,hashlib,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
args=sys.argv[sys.argv.index('--')+1:];src,dest=map(Path,args);bpy.ops.wm.open_mainfile(filepath=str(src));r=bpy.data.objects['Intercom_Armature_Fixed'];s=bpy.context.scene;NORM=1.3790181109932065;TOP=3.3
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
boots={'L':'tripo_part_3_fixed','R':'tripo_part_2_fixed'};body='tripo_part_0_fixed';meshes={o.name:o for o in s.objects if o.type=='MESH' and o.visible_get() and not o.hide_render};polys={n:[list(p.vertices) for p in o.data.polygons] for n,o in meshes.items()};result={'sourceSha256':hashlib.sha256(src.read_bytes()).hexdigest(),'scope':'Actual Blender evaluated meshes at half-source-key frames; no Unity solver. Non-boot floor sampled every half frame.','worldScale':NORM,'topSpeed':TOP,'actions':{},'collisions':[]}
def sample(n):
 o=meshes[n].evaluated_get(bpy.context.evaluated_depsgraph_get());m=o.to_mesh();v=np.empty(len(m.vertices)*3);m.vertices.foreach_get('co',v);v=v.reshape(-1,3);a=np.array(o.matrix_world);v=v@a[:3,:3].T+a[:3,3];o.to_mesh_clear();return v
for stem,D in [('Run',(0,-1,0)),('RunBackward',(0,1,0)),('RunLeft',(1,0,0)),('RunRight',(-1,0,0))]:
 for suffix in ['', 'Relaxed']:
  name=stem+suffix;physicalTop=2.475 if stem in ['RunLeft','RunRight'] else TOP;r.animation_data.action=bpy.data.actions[name];direction=np.array(D);last={};vels=[];stance=[[],[]];rows=[];bothAir=0;minBoot=1e9;maxSwing=0;nonBootMin={n:1e9 for n in meshes if n not in boots.values()};bodyZ=[];maxAnkleSpeed=0;maxFootAngleSpeed=0;lastAnkle={};lastRotation={};maxCross=0
  for i in range(257):
   f=1+i*.0625;p=i/256;s.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update();vs={n:sample(n) for n in list(boots.values())+[body]};row={'phase':p,'feet':{}};bodyZ.append(r.pose.bones['Body'].head.z*NORM)
   for si,(side,obj) in enumerate(boots.items()):
    v=vs[obj];low=float(v[:,2].min());minBoot=min(minBoot,low*NORM);local=(p+(0 if side=='L' else .5))%1;speed=None
    if side in last and .11<=local<=.21:
     near=v[:,2]<low+.0025/NORM;speed=-float(np.mean((((v-last[side])/(.0625/24)*NORM)@direction)[near]));vels.append(speed)
    if local<=.32:stance[si].append(low*NORM)
    else:maxSwing=max(maxSwing,low*NORM)
    ankle=np.array(r.pose.bones[side+'_Foot'].head);q=r.pose.bones[side+'_Foot'].matrix.to_quaternion();dt=.0625/24/TOP
    if side in lastAnkle:
     maxAnkleSpeed=max(maxAnkleSpeed,float(np.linalg.norm((ankle-lastAnkle[side])*NORM/dt+direction*physicalTop)));ang=(lastRotation[side].inverted()@q).angle;ang=min(ang,2*math.pi-ang);maxFootAngleSpeed=max(maxFootAngleSpeed,math.degrees(ang)/dt)
    lastAnkle[side]=ankle;lastRotation[side]=q;last[side]=v;row['feet'][side]={'phase':local,'lowestMetres':low*NORM,'flatSupportSpeed':speed}
   if all(v['lowestMetres']>.01 for v in row['feet'].values()):bothAir+=1
   for a,b in [(boots['L'],boots['R']),(boots['L'],body),(boots['R'],body)]:
    va,vb=vs[a],vs[b]
    if np.all(va.max(0)>=vb.min(0)) and np.all(vb.max(0)>=va.min(0)):
     ta=BVHTree.FromPolygons([Vector(x) for x in va],polys[a]);tb=BVHTree.FromPolygons([Vector(x) for x in vb],polys[b]);hits=ta.overlap(tb)
     if hits:result['collisions'].append({'clip':name,'frame':f,'objects':[a,b],'trianglePairs':len(hits)})
   if i%8==0:
    for n in nonBootMin:
     v=vs.get(n)
     if v is None:v=sample(n)
     nonBootMin[n]=min(nonBootMin[n],float(v[:,2].min())*NORM)
   rows.append(row)
  report={'samples':257,'physicalTopSpeed':physicalTop,'referenceSpeed':(.975 if stem in ['RunLeft','RunRight'] else 1.3),'bodyVerticalRangeMetres':max(bodyZ)-min(bodyZ),'minimumBootFloorMetres':minBoot,'maximumSwingFloorMetres':maxSwing,'stanceFloorMetres':{'L':[min(stance[0]),max(stance[0])],'R':[min(stance[1]),max(stance[1])]},'bothBootsOver1cmPercent':bothAir/257*100,'flatSupportSpeedMean':float(np.mean(vels)),'flatSupportSpeedRange':[min(vels),max(vels)],'maximumAnkleWorldSpeedAtTopSpeed':maxAnkleSpeed,'maximumFootDegreesPerSecondAtTopSpeed':maxFootAngleSpeed,'minimumNonBootMeshFloors':nonBootMin,'rows':rows};result['actions'][name]=report;print('V12_QA',name,{k:v for k,v in report.items() if k not in ['rows','minimumNonBootMeshFloors']},'nonbootmin',min(nonBootMin.items(),key=lambda p:p[1]),'hits',len(result['collisions']),flush=True)
result['status']='ok' if not result['collisions'] and min(x['minimumBootFloorMetres'] for x in result['actions'].values())>-.001 and min(v for x in result['actions'].values() for v in x['minimumNonBootMeshFloors'].values())>0 else 'needs-review';dest.write_text(json.dumps(result,indent=2)+'\n');print('V12_QA_DONE',result['status'],flush=True)
