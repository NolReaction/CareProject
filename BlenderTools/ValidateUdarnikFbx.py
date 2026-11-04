"""Compare actual exported FBX animation and static chunks against the source.
Run Blender in background. This checks Blender's FBX roundtrip, not Unity.
"""
import bpy,json,math,argparse,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=ROOT/'Validation/Results/UdarnikArt')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []);args.out.mkdir(parents=True,exist_ok=True)
CLIPS={'Idle':1.60,'Walk':.80,'Windup':.30,'Strike':.16,'Recover':.48,'Hurt':.22,'Death':.18,'Emerge':.68,'EmergeGround':.95,'EmergeHatch':1.05}
checks=0

def check(ok,detail):
 global checks
 checks+=1
 if not ok:raise AssertionError(detail)

def bind(rig,action):
 rig.animation_data_create();rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]

def bbox(meshes):
 deps=bpy.context.evaluated_depsgraph_get();coords=[]
 for obj in meshes:
  evaluated=obj.evaluated_get(deps);coords.extend(evaluated.matrix_world@v.co for v in evaluated.data.vertices)
 return [min(p[i] for p in coords) for i in range(3)]+[max(p[i] for p in coords) for i in range(3)]

def sample(rig,action,t,meshes):
 bind(rig,action);frame=action.frame_range[0]+t*(action.frame_range[1]-action.frame_range[0]);bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();return bbox(meshes)

bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Source/Enemies/Udarnik/Udarnik_Rigged.blend'))
rig=bpy.data.objects['UdarnikRig'];meshes=[o for o in bpy.data.collections['01 Editable Robot Parts'].objects if o.type=='MESH'];reference={}
for name in CLIPS:
 reference[name]=[sample(rig,bpy.data.actions[name],i/10,meshes) for i in range(11)]
source_triangles=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
source_vertices=sum(len(o.data.vertices) for o in meshes)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/IntercomMVP/Content/Enemies/Udarnik/Model/Udarnik.fbx'),use_anim=True,automatic_bone_orientation=False)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
check(len(meshes)==1 and len(rigs)==1,'one skin and one rig');rig=rigs[0];mesh=meshes[0]
# Blender infers connected edit-bones from FBX joint positions. That local
# Blender-only flag suppresses exported location curves, notably Pelvis bob.
# FBX/Unity joints have no connected-bone constraint. Remove the inferred flags
# for a faithful matrix/skin roundtrip while retaining every rest transform.
inferred_connections=[bone.name for bone in rig.data.bones if bone.use_connect]
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:bone.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
check(mesh.name=='UdarnikBody','stable game mesh name');check(len(rig.data.bones)==14,'14 bones')
check(len(mesh.data.materials)==1,'one game material');check(len(mesh.data.uv_layers)==1,'one UV atlas')
triangles=sum(len(p.vertices)-2 for p in mesh.data.polygons);check(triangles==source_triangles,('triangles',triangles,source_triangles))
actions={a.name.rsplit('|',1)[-1]:a for a in bpy.data.actions};check(set(actions)==set(CLIPS),'ten exact actions')
records=[];max_difference=0.
for name,duration in CLIPS.items():
 action=actions[name];actual=(action.frame_range[1]-action.frame_range[0])/bpy.context.scene.render.fps;check(abs(actual-duration)<1e-5,(name,actual,duration))
 for i in range(11):
  bounds=sample(rig,action,i/10,meshes);difference=max(abs(x-y) for x,y in zip(bounds,reference[name][i]));max_difference=max(max_difference,difference);check(difference<.00015,(name,i,'evaluated bounds',difference))
 records.append({'name':name,'durationSeconds':actual,'frames':list(action.frame_range)})
deform={b.name for b in rig.data.bones if b.use_deform};groups={g.index:g.name for g in mesh.vertex_groups}
for v in mesh.data.vertices:
 weight=sum(g.weight for g in v.groups if groups[g.group] in deform);check(abs(weight-1)<1e-5,('weight',v.index,weight))
game={'mesh':mesh.name,'vertices':len(mesh.data.vertices),'sourceVertices':source_vertices,'triangles':triangles,'bones':list(rig.data.bones.keys()),'materials':len(mesh.data.materials),'blenderImporterInferredConnectionsCleared':inferred_connections,'clips':records,'maximumSourceVsRoundtripBoundsErrorMetres':max_difference}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/IntercomMVP/Content/Enemies/Udarnik/Model/UdarnikBreakup.fbx'),use_anim=False)
chunks=[o for o in bpy.context.scene.objects if o.type=='MESH'];check(len(chunks)==6,'six breakup meshes');check(not any(o.type=='ARMATURE' for o in bpy.context.scene.objects),'breakup no rig');check(len(bpy.data.actions)==0,'breakup no animation')
expected={'Breakup_Helmet','Breakup_Body','Breakup_GloveL','Breakup_GloveR','Breakup_BootL','Breakup_BootR'};check({o.name for o in chunks}==expected,'chunk names')
chunk_records=[];materials=set()
for obj in chunks:
 check(len(obj.data.materials)==1,('chunk material',obj.name));materials.add(obj.data.materials[0])
 center=Vector(tuple((min(v.co[i] for v in obj.data.vertices)+max(v.co[i] for v in obj.data.vertices))*.5 for i in range(3)));check(center.length<.00001,('centered pivot',obj.name,list(center)))
 chunk_records.append({'name':obj.name,'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'pivotBlender':list(obj.matrix_world.translation),'localCenterErrorMetres':center.length})
check(len(materials)==1,'all chunks share material');check(sum(c['triangles'] for c in chunk_records)==source_triangles,'chunks preserve total geometry')
report={'status':'PASS','checks':checks,'game':game,'breakup':chunk_records,'scope':'Blender FBX roundtrip against evaluated source at 11 poses per action; not native Unity import/Animator.'}
(args.out/'FbxRoundtrip.json').write_text(json.dumps(report,indent=2)+'\n');print('FBX_ROUNDTRIP_PASS',checks,'max_error',max_difference,flush=True)
