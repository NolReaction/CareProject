"""Blender background preview of the exact generated game meshes (not a Unity screenshot).
Usage: blender --background --python Tools/RenderTutorialDistrict.py -- /path/render.json /path/out.png
Generate the JSON with BuildTutorialDistrict.py --render-json first.
"""
import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];data=json.loads(Path(args[0]).read_text());output=Path(args[1])
root=Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(output)
scene.render.film_transparent=False;scene.world.color=(.23,.28,.32)
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.40,.47,.55,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.48
scene.view_settings.view_transform='AgX';scene.view_settings.exposure=.35
materials={}
for name,(color,metal,smooth,texture) in data['materials'].items():
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;p=n.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=1-smooth
 if name=='Lamp':p.inputs['Emission Color'].default_value=(.5,.29,.08,1);p.inputs['Emission Strength'].default_value=.6
 if texture:
  img=n.new('ShaderNodeTexImage');img.image=bpy.data.images.load(str(root/'Assets/IntercomMVP/Art/PrototypeEnvironment/Surfaces'/(texture+'.png')));img.extension='REPEAT'
  mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=(*color,1);m.node_tree.links.new(img.outputs['Color'],mix.inputs[2]);m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
  if name in ['Asphalt','Backdrop','Concrete','Gravel']:
   uv=n.new('ShaderNodeTexCoord');scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value={'Asphalt':18,'Backdrop':18,'Concrete':2,'Gravel':5}[name];m.node_tree.links.new(uv.outputs['UV'],scale.inputs[0]);m.node_tree.links.new(scale.outputs[0],img.inputs['Vector'])
 materials[name]=m
# Unity is left-handed; mirror X and reverse triangle winding for Blender.
convert=lambda p:(-p[0],-p[2],p[1])
meshes={}
for name,mod in data['modules'].items():
 mesh=bpy.data.meshes.new(name);mesh.from_pydata([convert(v[:3]) for v in mod['vertices']],[],[list(reversed(t)) for t,mi in mod['faces']]);mesh.update()
 uv=mesh.uv_layers.new(name='UV0')
 for poly,(_,mi) in zip(mesh.polygons,mod['faces']):
  poly.material_index=mi
  for loop_index in poly.loop_indices:uv.data[loop_index].uv=mod['vertices'][mesh.loops[loop_index].vertex_index][6:8]
 for mat in mod['materials']:mesh.materials.append(materials[mat])
 meshes[name]=mesh

def create(name,mesh,pos,scale=(1,1,1),yaw=0):
 o=bpy.data.objects.new(name,mesh);scene.collection.objects.link(o);o.location=convert(pos);o.scale=(scale[0],scale[2],scale[1]);o.rotation_euler[2]=-math.radians(yaw);return o

def cube(name,pos,scale,material,yaw=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=convert(pos));o=bpy.context.object;o.name=name;o.scale=(scale[0],scale[2],scale[1]);o.rotation_euler[2]=-math.radians(yaw);o.data.materials.append(materials[material]);return o
for group in data['layout']['groups']:
 for block in group['blocks']:
  if not block.get('visual',True):continue
  pos=[a+b for a,b in zip(group['position'],block['position'])]
  if block.get('mesh'):create(block['name'],meshes[block['mesh']],pos,block['size'],block['yaw'])
  else:cube(block['name'],pos,block['size'],block['material'],block['yaw'])
for target in data['layout']['targets']:
 create(target['name'],meshes[target['mesh']],target['position'])
 create(target['name']+' stand',meshes['TrainingStand'],target['position'])
cube('Asphalt ground',(0,-.025,0),(36,.05,32),'Asphalt')
cube('Context ground',(0,-.11,0),(40,.12,36),'Backdrop')
# Use the project's actual robot in the preview, not an AI replacement.
try:
 before=set(bpy.data.objects)
 bpy.ops.import_scene.fbx(filepath=str(root/'Assets/IntercomMVP/Art/Robot/IntercomRobot.fbx'))
 imported=set(bpy.data.objects)-before
 for o in imported:
  if o.type=='ARMATURE' and o.animation_data:
   idle=next((a for a in bpy.data.actions if a.name.endswith('|Idle') or a.name=='Idle'),None)
   if idle:o.animation_data.action=idle
 scene.frame_set(1)
 anchor=bpy.data.objects.new('Actual project robot preview',None);scene.collection.objects.link(anchor)
 for o in imported:
  if o.parent not in imported:o.parent=anchor
 anchor.scale=(.8998325,)*3
 bpy.context.view_layer.update()
 verts=[o.matrix_world @ Vector(c) for o in imported if o.type=='MESH' for c in o.bound_box]
 lo=Vector((min(v.x for v in verts),min(v.y for v in verts),min(v.z for v in verts)));hi=Vector((max(v.x for v in verts),max(v.y for v in verts),max(v.z for v in verts)))
 anchor.location=(7-(lo.x+hi.x)/2,12-(lo.y+hi.y)/2,-lo.z+.04)
except Exception as e:print('Robot preview import:',e)
light=bpy.data.lights.new('Preview daylight','SUN');light.energy=2.4;light.color=(1,.96,.88);light.angle=.12;lo=bpy.data.objects.new('Preview daylight',light);scene.collection.objects.link(lo);lo.rotation_euler=(math.radians(34),math.radians(-22),math.radians(-32))
camera=bpy.data.cameras.new('Map overview');co=bpy.data.objects.new('Map overview',camera);scene.collection.objects.link(co);scene.camera=co;co.location=(25,36,36);look=Vector((0,0,.7));co.rotation_euler=(look-co.location).to_track_quat('-Z','Y').to_euler();camera.type='ORTHO';camera.ortho_scale=47;camera.lens=45
output.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output.with_suffix('.blend')))
bpy.ops.render.render(write_still=True)
print('Saved actual-mesh preview',output)
