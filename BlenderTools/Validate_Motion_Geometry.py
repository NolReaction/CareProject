import bpy,sys,json,math,hashlib
from pathlib import Path
from mathutils import Matrix,Vector
args=sys.argv[sys.argv.index('--')+1:];src=Path(args[0]);dest=Path(args[1]);bpy.ops.wm.open_mainfile(filepath=str(src));r=bpy.data.objects['Intercom_Armature_Fixed'];s=bpy.context.scene;NORM=1.3790181109932065
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
rest={b.name:b.matrix_local.copy() for b in r.data.bones};boots={side:bpy.data.objects[name] for side,name in [('L','tripo_part_3_fixed'),('R','tripo_part_2_fixed')]};edges={side:[(e.vertices[:],(o.data.vertices[e.vertices[0]].co-o.data.vertices[e.vertices[1]].co).length) for e in o.data.edges] for side,o in boots.items()};result={'sourceSha256':hashlib.sha256(src.read_bytes()).hexdigest(),'worldScale':NORM,'actions':{},'runtimeStress':{},'scope':'Deformed source geometry, shared ankle-point invariant, loop and transform stress. Does not execute Unity.'}
def frame(f):s.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
def joint():
 return max(((r.pose.bones[side+'_Calf'].matrix@rest[side+'_Calf'].inverted()@rest[side+'_Foot'].translation)-(r.pose.bones[side+'_Foot'].matrix@rest[side+'_Foot'].inverted()@rest[side+'_Foot'].translation)).length for side in ['L','R'])
for a in sorted(bpy.data.actions,key=lambda a:a.name):
 r.animation_data.action=a;end=float(a.frame_range[1]);n=round((end-1)*4)+1;minfloor=1e9;minratio=1;maxratio=1;jointerr=0;locerr=0;scaleerr=0;arms={side:[] for side in ['L','R']}
 for i in range(n):
  f=1+i*.25;frame(f);jointerr=max(jointerr,joint())
  for side in ['L','R']:
   arms[side].append(r.pose.bones[side+'_Upperarm'].rotation_quaternion.copy())
   for suffix in ['Calf','Foot','Toe']:
    b=r.pose.bones[side+'_'+suffix];locerr=max(locerr,b.location.length);scaleerr=max(scaleerr,max(abs(v-1) for v in b.scale))
   o=boots[side].evaluated_get(bpy.context.evaluated_depsgraph_get());m=o.to_mesh();vs=[o.matrix_world@v.co for v in m.vertices];minfloor=min(minfloor,min(v.z for v in vs))
   for (x,y),length in edges[side]:
    if length>1e-6:
     ratio=(vs[x]-vs[y]).length/length;minratio=min(minratio,ratio);maxratio=max(maxratio,ratio)
   o.to_mesh_clear()
 frame(1);start={p.name:p.matrix.copy() for p in r.pose.bones};frame(end);loop=max(max(abs(x-y) for row1,row2 in zip(start[p.name],p.matrix) for x,y in zip(row1,row2)) for p in r.pose.bones)
 arange={side:max(math.degrees((a.inverted()@b).angle if (a.inverted()@b).angle<math.pi else 2*math.pi-(a.inverted()@b).angle) for a in qs for b in qs) for side,qs in arms.items()}
 result['actions'][a.name]={'samples':n,'maximumAnkleAttachmentErrorMetres':jointerr*NORM,'minimumBootFloorMetres':minfloor*NORM,'bootEdgeLengthRatio':[minratio,maxratio],'maximumLegLocalTranslation':locerr,'maximumLegScaleError':scaleerr,'loopMatrixError':loop if a.name.startswith('Run') else None,'shoulderRotationRangeDegrees':arange}
 assert jointerr<1e-5,(a.name,jointerr)
 if a.name.startswith('Run'):assert locerr<1e-5 and scaleerr<1e-5,(a.name,locerr,scaleerr)
 if a.name.startswith('Run'):assert loop<1e-5,(a.name,loop)
 print('MOTION_CHECK',a.name,jointerr,minfloor,minratio,maxratio,flush=True)
count=0;mx=0
for a in sorted(bpy.data.actions,key=lambda a:a.name):
 r.animation_data.action=a
 for f in [1,(1+float(a.frame_range[1]))*.5,float(a.frame_range[1])]:
  for axis in ['X','Y','Z']:
   for angle in [-28,28]:
    frame(f)
    for side in ['L','R']:
     c=r.pose.bones[side+'_Calf'];ft=r.pose.bones[side+'_Foot'];old=ft.matrix.copy();pivot=c.head.copy();c.matrix=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(angle),4,axis)@Matrix.Translation(-pivot)@c.matrix;bpy.context.view_layer.update();m=old.to_3x3().to_4x4();m.translation=ft.head;ft.matrix=m;bpy.context.view_layer.update();mx=max(mx,joint());count+=1
result['runtimeStress']={'samples':count,'degrees':[-28,28],'axes':['X','Y','Z'],'maximumAnkleAttachmentErrorMetres':mx*NORM,'mode':'Calf perturbed, authored Foot world orientation restored'};assert mx<1e-5;result['status']='ok';dest.write_text(json.dumps(result,indent=2)+'\n');print('MOTION_ALL_PASS',mx,count,flush=True)
