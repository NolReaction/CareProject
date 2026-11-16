"""Render V21 attack and emergence poses from the actual editable source.

blender -b --python-exit-code 1 --python BlenderTools/RenderUdarnikPreview.py -- --out DIR
Only read the source; camera, lights and ground exist in this process only.
"""
import argparse, importlib.util, math, sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out',type=Path,default=ROOT/'Validation/Results/UdarnikArt/Preview')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Source/Enemies/Udarnik/Udarnik_Rigged.blend'))
spec=importlib.util.spec_from_file_location('anim',Path(__file__).with_name('AnimateUdarnik.py'))
anim=importlib.util.module_from_spec(spec);spec.loader.exec_module(anim);anim.setup()
rig=anim.rig;scene=bpy.context.scene
for obj in list(bpy.data.objects):
    if obj.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(obj,do_unlink=True)
world=bpy.data.worlds.new('ReviewWorld');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.13,.18,.19,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.65;scene.world=world
for name,position,power,size in [('ReviewKey',(-2,-3,4),450,4),('ReviewFill',(3,-1,2),180,3),('ReviewRim',(0,3,3),250,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=size
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=position
    obj.rotation_euler=(Vector((0,0,.65))-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('ReviewCamera');camera=bpy.data.objects.new('ReviewCamera',data)
scene.collection.objects.link(camera);data.type='ORTHO';data.ortho_scale=1.9;scene.camera=camera
camera.location=(1.5,-3,1.9);camera.rotation_euler=(Vector((0,0,.65))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=False
scene.render.resolution_x=360;scene.render.resolution_y=360;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
ground_material=bpy.data.materials.new('ReviewGround');ground_material.diffuse_color=(.17,.22,.23,1)
poses=[('Idle',.0),('Run',.1),('Run',.35),('HurtHeavy',.45),
       ('Windup',.55),('Strike',0.),('WindupHook',.55),('StrikeHook',0.),
       ('WindupSlam',.60),('StrikeSlam',0.),('EmergeGround',.32),('EmergeGround',.60),
       ('EmergeGround',.86),('EmergeHatch',.35),('EmergeHatch',.62),('EmergeHatch',.90)]
records=[]
for index,(name,t) in enumerate(poses):
    floor=None
    if name in {'EmergeGround','EmergeHatch'}:
        if name=='EmergeGround':
            vertices=[(-4,-4,0),(4,-4,0),(4,4,0),(-4,4,0)];faces=[(0,1,2,3)];start,end=.12,.86
        else:
            vertices=[];faces=[];start,end=.04,.90
            for radius in [.77,4]:vertices.extend((radius*math.cos(i*math.tau/64),radius*math.sin(i*math.tau/64),0) for i in range(64))
            faces=[(i,(i+1)%64,(i+1)%64+64,i+64) for i in range(64)]
        mesh=bpy.data.meshes.new('ReviewFloor');mesh.from_pydata(vertices,[],faces)
        floor=bpy.data.objects.new('ReviewFloor',mesh);scene.collection.objects.link(floor);mesh.materials.append(ground_material)
        rig.location.z=-1.48*(1-anim.window(t,start,end))
    else:rig.location.z=0
    action=bpy.data.actions[name];anim.bind_action(action)
    frame=action.frame_range[1]*t;scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
    filename=f'{index:02d}_{name}.png';scene.render.filepath=str(args.out/filename)
    bpy.ops.render.render(write_still=True);records.append({'filename':filename,'clip':name,'progress':t})
    if floor:
        mesh=floor.data;bpy.data.objects.remove(floor,do_unlink=True);bpy.data.meshes.remove(mesh)
import json
(args.out/'poses.json').write_text(json.dumps(records,indent=2)+'\n')
print('UDARNIK_PREVIEW_DONE',len(records),flush=True)
