"""Read-only extreme upper-body yaw clearance on actual evaluated source skin."""
import bpy,sys,json,math,hashlib
from pathlib import Path
from mathutils import Vector,Quaternion
from mathutils.bvhtree import BVHTree
src,dst=map(Path,sys.argv[sys.argv.index('--')+1:][:2]);bpy.ops.wm.open_mainfile(filepath=str(src));r=bpy.data.objects['Intercom_Armature_Fixed'];scene=bpy.context.scene
names=['tripo_part_0_fixed','tripo_part_3_fixed','tripo_part_2_fixed'];objects={n:bpy.data.objects[n] for n in names};polys={n:[tuple(p.vertices) for p in o.data.polygons] for n,o in objects.items()};hits=[];count=0
for action in [a for a in bpy.data.actions if a.name.startswith('Run')]:
 r.animation_data.action=action
 if action.slots:r.animation_data.action_slot=action.slots[0]
 for i in range(16):
  f=1+i;scene.frame_set(f);bpy.context.view_layer.update();base=r.pose.bones['Torso'].matrix.copy()
  for yaw in [-42,42]:
   m=(Quaternion((0,0,1),math.radians(yaw)).to_matrix()@base.to_3x3()).to_4x4();m.translation=base.translation;r.pose.bones['Torso'].matrix=m;bpy.context.view_layer.update()
   verts={};bounds={}
   for n,o in objects.items():
    e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=e.to_mesh();v=[e.matrix_world@p.co for p in mesh.vertices];verts[n]=v;bounds[n]=[(min(p[a] for p in v),max(p[a] for p in v)) for a in range(3)];e.to_mesh_clear()
   body=names[0]
   for boot in names[1:]:
    if all(bounds[boot][a][1]>=bounds[body][a][0] and bounds[body][a][1]>=bounds[boot][a][0] for a in range(3)):
     contact=BVHTree.FromPolygons(verts[boot],polys[boot]).overlap(BVHTree.FromPolygons(verts[body],polys[body]))
     if contact:hits.append(dict(action=action.name,frame=f,additionalYaw=yaw,boot=boot,pairs=len(contact)))
   count+=1;r.pose.bones['Torso'].matrix=base;bpy.context.view_layer.update()
result=dict(status='PASS' if not hits else 'REVIEW',sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest(),samples=count,additionalTorsoYawDegrees=[-42,42],intersections=hits,scope='Actual Blender evaluated source skin with extreme additional upper-body yaw; does not include every procedural Unity combination')
dst.write_text(json.dumps(result,indent=2)+'\n');print('WAIST_CLEARANCE',result['status'],count,'samples',len(hits),'hits',flush=True)
