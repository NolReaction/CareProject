"""Actual Blender skin/action/contact review; load Udarnik_Rigged.blend first."""
import bpy,math,json,argparse,sys,importlib.util
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=ROOT/'Validation/Results/UdarnikArt');parser.add_argument('--render',action='store_true');parser.add_argument('--render-world',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []);args.out.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('anim',Path(__file__).with_name('AnimateUdarnik.py'));anim=importlib.util.module_from_spec(spec);spec.loader.exec_module(anim);anim.setup()
rig=anim.rig;parts=anim.parts;scene=bpy.context.scene;deform={b.name for b in rig.data.bones if b.use_deform}
checks=0
def check(ok,detail):
 global checks
 checks+=1
 if not ok:raise AssertionError(detail)
vertices=0
for obj in parts:
 names={g.index:g.name for g in obj.vertex_groups}
 for vertex in obj.data.vertices:
  weight=sum(g.weight for g in vertex.groups if names[g.group] in deform)
  check(abs(weight-1)<.00001,(obj.name,vertex.index,'weight',weight));vertices+=1
check(set(bpy.data.actions)=={bpy.data.actions[n] for n in anim.CLIPS},'action set')
samples=[];contact_errors=[];slips=[];previous={};root_rest=rig.data.bones['Root'].matrix_local.copy()
for name,duration in anim.CLIPS.items():
 action=bpy.data.actions[name];rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]
 check(abs((action.frame_range[1]-action.frame_range[0])/scene.render.fps-duration)<1e-6,(name,'duration'))
 previous={}
 for i in range(101):
  t=i/100;frame=action.frame_range[0]+t*(action.frame_range[1]-action.frame_range[0]);scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
  scale=max(abs(x-1) for bone in rig.pose.bones for x in bone.matrix.to_scale());check(scale<.0001,(name,t,'bone stretch',scale))
  root_error=max(abs(rig.pose.bones['Root'].matrix[a][b]-root_rest[a][b]) for a in range(4) for b in range(4));check(root_error<.00001,(name,t,'root motion',root_error))
  for side in ['L','R']:
   upper_arm=rig.pose.bones['UpperArm.'+side];forearm=rig.pose.bones['Forearm.'+side]
   check((upper_arm.tail-forearm.head).length<.00002,(name,t,side,'elbow detached'))
   hip=rig.pose.bones['Thigh.'+side];shin=rig.pose.bones['Shin.'+side];foot=rig.pose.bones['Foot.'+side]
   check((hip.tail-shin.head).length<.00002,(name,t,side,'knee detached'))
   check((shin.tail-foot.head).length<.00002,(name,t,side,'ankle detached'))
   boot=next(o for o in parts if o.name=='Boot.'+side) if any(o.name=='Boot.'+side for o in parts) else parts[0]
   evaluated=boot.evaluated_get(bpy.context.evaluated_depsgraph_get());coords=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
   if name=='Walk':
    phase=(t+(0 if side=='L' else .5))%1.;stance=phase/anim.WALK_DUTY
    if phase<anim.WALK_DUTY:
     error=abs(min(v.z for v in coords)-anim.FLOOR);contact_errors.append(error);check(error<.00015,(name,t,side,'sole floor',error))
    if .16<stance<.82:
     # Mid-support is flat. The whole rigid sole should stay in the world while the body travels.
     center=sum(coords,Vector())/len(coords);world_y=center.y-anim.WALK_REFERENCE*t*duration
     if side in previous and abs(previous[side][0]-(t-.01))<1e-6:
      slip=abs(world_y-previous[side][1])/(duration*.01);slips.append(slip);check(slip<.0012,(name,t,side,'planted slip',slip))
     previous[side]=(t,world_y)
    else:previous.pop(side,None)
  if i%10==0:
   coords=[]
   for obj in parts:
    eval_obj=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());coords.extend(eval_obj.matrix_world@v.co for v in eval_obj.data.vertices)
   samples.append({'clip':name,'t':t,'minZ':min(v.z for v in coords),'maxZ':max(v.z for v in coords),'maxWidth':max(v.x for v in coords)-min(v.x for v in coords),'boneScaleError':scale})
report={'status':'PASS','checks':checks,'editableMeshes':len(parts),'weightedVertices':vertices,'bones':len(rig.data.bones),'clips':anim.CLIPS,
 'walkReferenceMetresPerSecond':anim.WALK_REFERENCE,'maximumPlantedSoleFloorError':max(contact_errors or [0]),'maximumMidStanceWorldSlipMetresPerSecond':max(slips or [0]),
 'scope':'Actual Blender evaluated skin, complete weight sums, 101samples peraction, stationaryRoot, bonecontacts and planted soles; not Unity playback.', 'samples':samples}
(args.out/'AnimationPoseQA.json').write_text(json.dumps(report,indent=2)+'\n');print('ACTION_QA_PASS',checks,flush=True)
if args.render or args.render_world:
 for obj in list(bpy.data.objects):
  if obj.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(obj,do_unlink=True)
 world=bpy.data.worlds.new('ReviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.14,.18,.19,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;scene.world=world
 for name,pos,power,size in [('ReviewKey',(-2,-3,4),450,4),('ReviewFill',(3,-1,2),180,3),('ReviewRim',(0,3,3),250,3)]:
  data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=size;obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(Vector((0,0,.65))-obj.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('ReviewCamera');camera=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(camera);data.type='ORTHO';data.ortho_scale=1.82;scene.camera=camera;camera.location=(1.5,-3,1.9);camera.rotation_euler=(Vector((0,0,.65))-camera.location).to_track_quat('-Z','Y').to_euler()
 scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True;scene.render.resolution_x=480;scene.render.resolution_y=480;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
 poses=[('Idle',0),('Walk',.10),('Walk',.30),('Walk',.60),('Walk',.80),('Windup',.6),('Windup',1),('Strike',.5),('Recover',.55),('Hurt',.5),('EmergeGround',.15),('EmergeGround',.55),('EmergeGround',.85),('EmergeHatch',.15),('EmergeHatch',.55),('EmergeHatch',.85),('Death',.7)]
 for name,t in (poses if args.render else []):
  action=bpy.data.actions[name];rig.animation_data.action=action
  if action.slots:rig.animation_data.action_slot=action.slots[0]
  frame=action.frame_range[1]*t;scene.frame_set(int(frame),subframe=frame-int(frame));scene.render.filepath=str(args.out/('Pose_'+name+'_'+str(t).replace('.','_')+'.png'));bpy.ops.render.render(write_still=True)
 print('POSE_RENDERS_DONE',len(poses) if args.render else 0,flush=True)
 if args.render_world:
  ground_material=bpy.data.materials.new('ReviewGround');ground_material.diffuse_color=(.17,.22,.23,1)
  for name,start,end in [('EmergeGround',.12,.86),('EmergeHatch',.04,.90)]:
   if name=='EmergeGround':
    vertices=[(-4,-4,0),(4,-4,0),(4,4,0),(-4,4,0)];faces=[(0,1,2,3)]
   else:
    vertices=[];faces=[]
    for radius in [.77,4]:
     vertices.extend((radius*math.cos(i*math.tau/64),radius*math.sin(i*math.tau/64),0) for i in range(64))
    faces=[(i,(i+1)%64,(i+1)%64+64,i+64) for i in range(64)]
   data=bpy.data.meshes.new('ReviewFloor');data.from_pydata(vertices,[],faces);floor=bpy.data.objects.new('ReviewFloor',data);scene.collection.objects.link(floor);data.materials.append(ground_material)
   for t in [.30,.55,.85]:
    action=bpy.data.actions[name];rig.animation_data.action=action
    if action.slots:rig.animation_data.action_slot=action.slots[0]
    frame=action.frame_range[1]*t;scene.frame_set(int(frame),subframe=frame-int(frame));rig.location.z=-1.48*(1-anim.window(t,start,end));bpy.context.view_layer.update()
    scene.render.filepath=str(args.out/('World_'+name+'_'+str(t).replace('.','_')+'.png'));bpy.ops.render.render(write_still=True)
   bpy.data.objects.remove(floor,do_unlink=True);bpy.data.meshes.remove(data)
  rig.location.z=0
  print('WRAPPER_RENDERS_DONE',6,flush=True)
