"""Read-only source pose preview. Blender5.2+: -- model.blend output-directory."""
import bpy,sys,math
from pathlib import Path
from mathutils import Vector
src,out=map(Path,sys.argv[sys.argv.index('--')+1:][:2]);out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(src));scene=bpy.context.scene;rig=bpy.data.objects['Intercom_Armature_Fixed']
for o in list(bpy.data.objects):
 if o.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012));floor=bpy.context.object;floor.name='_V12_PREVIEW_FLOOR'
mat=bpy.data.materials.new('PreviewFloor');mat.diffuse_color=(.10,.13,.14,1);floor.data.materials.append(mat)
world=bpy.data.worlds.new('PreviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.26,.29,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;scene.world=world
for name,loc,power,size in [('Key',(-3,-4,5),700,4),('Fill',(4,-1,3),400,3),('Rim',(1,4,4),650,3)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=loc;obj.rotation_euler=(Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('PreviewCamera');camera=bpy.data.objects.new('PreviewCamera',data);scene.collection.objects.link(camera);camera.location=(1.8,-3,1.75);camera.rotation_euler=(Vector((0,-.03,.52))-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=1.65;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True;scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.render.film_transparent=False
for action in ['Archive_V3_RunRelaxed','RunRelaxed']:
 rig.animation_data.action=bpy.data.actions[action]
 if rig.animation_data.action.slots:rig.animation_data.action_slot=rig.animation_data.action.slots[0]
 for i,frame in enumerate([1,5,9,13]):
  scene.frame_set(frame);bpy.context.view_layer.update();scene.render.filepath=str(out/(action+'_'+str(i)+'.png'));bpy.ops.render.render(write_still=True)
print('V12_PREVIEW_DONE',flush=True)
