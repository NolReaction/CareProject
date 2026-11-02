"""Read-only Blender pose study for V6 runtime gestures.
Run Blender 5.2.3:
  blender -b Source/Robot/intercom-armed.blend -t 4 -P BlenderTools/Preview_Character_Gestures_V6.py
Only writes PNG previews under Validation/Results; never saves or exports the model.
Mirrors runtime world-axis rotations in Blender's native Z-up, forward -Y space.
This is a source-mesh pose inspection, not a Unity Animator/render test.
"""
import bpy,math,json,hashlib
from mathutils import Vector,Quaternion
from pathlib import Path
project=Path(__file__).resolve().parents[1]
out=project/'Validation/Results/CharacterGesturesV6';out.mkdir(parents=True,exist_ok=True)
r=bpy.data.objects['Intercom_Armature_Fixed'];scene=bpy.context.scene
for o in list(bpy.data.objects):
 if o.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(o,do_unlink=True)
# Preserve source meshes, modifiers and materials; only add temporary staging.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012));ground=bpy.context.object;ground.name='Preview floor'
m=bpy.data.materials.new('Preview floor');m.diffuse_color=(.12,.15,.16,1);ground.data.materials.append(m)
world=bpy.data.worlds.new('Preview world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.26,.29,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;scene.world=world
for name,loc,power,size in [('Key',(-3,-4,5),900,4),('Fill',(4,-1,3),500,3),('Rim',(1,4,4),700,3)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('PreviewCamera');camera=bpy.data.objects.new('PreviewCamera',data);scene.collection.objects.link(camera);camera.location=(1.8,-3,1.75);camera.rotation_euler=(Vector((0,-.03,.57))-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=1.6;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.render.film_transparent=False
forward=Vector((0,-1,0));right=Vector((1,0,0));up=Vector((0,0,1))
def rot(b,angle,axis):
 p=r.pose.bones[b];oldloc=p.location.copy();oldscale=p.scale.copy();m=p.matrix.copy();m=(Quaternion(axis,math.radians(angle)).to_matrix()@m.to_3x3()).to_4x4();m.translation=p.head;p.matrix=m;p.location=oldloc;p.scale=oldscale;bpy.context.view_layer.update()

def muzzle(side):
 pistol=bpy.data.objects['Pistol_'+side];vertices=[];triangles=[]
 def descendant(o):
  if side=='R' and o.parent==r and o.parent_bone=='R_Hand':return True
  while o.parent:
   if o.parent==pistol:return True
   o=o.parent
  return False
 for o in bpy.data.objects:
  if o.type!='MESH' or not descendant(o):continue
  mesh=o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh();mesh.calc_loop_triangles();offset=len(vertices)
  vertices.extend(o.matrix_world@v.co for v in mesh.vertices);triangles.extend(tuple(offset+i for i in t.vertices) for t in mesh.loop_triangles)
  o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
 projections=[v.dot(forward) for v in vertices];lo,hi=min(projections),max(projections);threshold=hi-(hi-lo)*.02;total=0;normal=Vector();centre=Vector()
 for indices in triangles:
  a,b,c=[vertices[i] for i in indices]
  if min(a.dot(forward),b.dot(forward),c.dot(forward))<threshold:continue
  cross=(b-a).cross(c-a);area=cross.length
  if area<1e-10:continue
  n=cross/area;f=n.dot(forward)
  if abs(f)<.75:continue
  if f<0:n=-n
  total+=area;normal+=n*area;centre+=(a+b+c)/3*area
 assert total>0,side
 return normal.normalized(),centre/total

def align_guns():
 directions={side:muzzle(side)[0] for side in ['L','R']}
 planar={side:Vector((d.x,d.y,0)).normalized() for side,d in directions.items()}
 common=(planar['L']+planar['R']).normalized();angles={}
 for side in ['L','R']:
  d=planar[side];angle=math.degrees(math.atan2(d.cross(common).z,d.dot(common)));angles[side]=angle
  rot(side+'_Hand',max(-25,min(25,angle)),up)
 after={side:Quaternion(up,math.radians(max(-25,min(25,angles[side]))))@directions[side] for side in ['L','R']}
 print('AIM_ALIGNMENT',json.dumps({'before':{k:list(v) for k,v in directions.items()},'yawCorrection':angles,'after':{k:list(v) for k,v in after.items()}}))

def pose(name,action,reload=0,wall=0,latch=0,inspect=0,align=False):
 r.animation_data.action=bpy.data.actions[action]
 if r.animation_data.action.slots:r.animation_data.action_slot=r.animation_data.action.slots[0]
 scene.frame_set(2);scene.frame_set(1);bpy.context.view_layer.update()
 basis={p.name:p.matrix_basis.copy() for p in r.pose.bones}
 r.animation_data.action=None
 for p in r.pose.bones:p.matrix_basis=basis[p.name]
 bpy.context.view_layer.update()
 before={p.name:(tuple(p.location),tuple(p.scale)) for p in r.pose.bones}
 for side,sign in [('L',1),('R',-1)]:
  rot(side+'_Upperarm',-38*reload+18*wall,right);rot(side+'_Upperarm',sign*(8*reload-5*wall),up)
  rot(side+'_Hand',-18*reload+5*latch+3*inspect,right);rot(side+'_Hand',sign*(8*reload-6*latch+1.5*inspect),forward)
 if align:align_guns()
 after={p.name:(tuple(p.location),tuple(p.scale)) for p in r.pose.bones}
 assert before==after,(name,'localpositions/scales changed')
 before_render={p.name:p.matrix.copy() for p in r.pose.bones}
 scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
 assert max(abs(a-b) for p in r.pose.bones for rowA,rowB in zip(before_render[p.name],p.matrix) for a,b in zip(rowA,rowB))<1e-6
 print('POSE_OK',name)
pose('01_aim','Idle')
pose('02_reload','Idle',reload=1,latch=0)
pose('03_latch','Idle',reload=1,latch=1)
pose('04_relaxed','IdleRelaxed')
pose('05_wall','IdleRelaxed',wall=1)
pose('06_inspect','IdleRelaxed',inspect=1)
pose('07_manual_reload','IdleRelaxed',reload=1,latch=1)
pose('08_aligned','Idle',align=True)
