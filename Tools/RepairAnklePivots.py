"""Rebase ankle pivots to the measured connector seating point inside each cuff.
Run in Blender: blender -b --python Tools/RepairAnklePivots.py
Optional -- input.blend output.blend; defaults to Source/Robot/intercom-armed.blend.
Preserves mesh vertices/weights, bone axes, hierarchy and all rotation curves.
Run-only vertical Body correction preserves the source lowest-sole height envelope.
"""
import bpy,json,sys,hashlib,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
source=Path(args[0]).resolve() if args else ROOT/'Source/Robot/intercom-armed.blend';output=Path(args[1]).resolve() if len(args)>1 else source
bpy.ops.wm.open_mainfile(filepath=str(source));r=bpy.data.objects['Intercom_Armature_Fixed'];s=bpy.context.scene
pivots={'L':Vector((.12347579002380371,.01223486103117466,.19351394474506378)),'R':Vector((-.1199374720454216,.011939220130443573,.19917550683021545))}
clips=['Idle','IdleRelaxed','RaiseWeapons','LowerWeapons','Shoot','Run','RunRelaxed','RunBackward','RunLeft','RunRight','RunBackwardRelaxed','RunLeftRelaxed','RunRightRelaxed']
def frame(f):s.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
def curves(a):return [f for l in a.layers for st in l.strips for bag in st.channelbags for f in bag.fcurves]
def actionsig():return {a.name:[(f.data_path,f.array_index,[(list(k.co),k.interpolation) for k in f.keyframe_points]) for f in curves(a)] for a in bpy.data.actions}
def shape():return {o.name:([(tuple(v.co),[(g.group,g.weight) for g in v.groups]) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons]) for o in bpy.data.objects if o.type=='MESH'}
def bootfloor():
 low=1e9
 for mesh in ['tripo_part_3_fixed','tripo_part_2_fixed']:
  o=bpy.data.objects[mesh].evaluated_get(bpy.context.evaluated_depsgraph_get());m=o.to_mesh();low=min(low,min((o.matrix_world@v.co).z for v in m.vertices));o.to_mesh_clear()
 return low
def sample():
 result={}
 for name in clips:
  a=bpy.data.actions[name];r.animation_data.action=a;lo,hi=map(float,a.frame_range);mx={'L':0,'R':0};lowest=1e9;count=round((hi-lo)*2)+1
  for i in range(count):
   frame(lo+i*.5)
   for side,q in pivots.items():
    c=r.pose.bones[side+'_Calf'].matrix@r.data.bones[side+'_Calf'].matrix_local.inverted();f=r.pose.bones[side+'_Foot'].matrix@r.data.bones[side+'_Foot'].matrix_local.inverted();mx[side]=max(mx[side],(c@q-f@q).length)
   for mesh in ['tripo_part_3_fixed','tripo_part_2_fixed']:
    o=bpy.data.objects[mesh].evaluated_get(bpy.context.evaluated_depsgraph_get());m=o.to_mesh();lowest=min(lowest,min((o.matrix_world@v.co).z for v in m.vertices));o.to_mesh_clear()
  result[name]={'samples':count,'maxCommonJointPointDivergenceSourceUnits':mx,'minBootFloorSourceUnits':lowest}
 return result
beforeActions=actionsig();beforeShape=shape();before=sample();originalFloors={}
for n in clips:
 if not n.startswith('Run'):continue
 r.animation_data.action=bpy.data.actions[n];originalFloors[n]=[]
 for i in range(65):frame(1+i*.25);originalFloors[n].append(bootfloor())
old={n:list(r.data.bones[n+'_Foot'].head_local) for n in pivots}
bpy.context.view_layer.objects.active=r;r.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for side,q in pivots.items():
 b=r.data.edit_bones[side+'_Foot'];delta=q-b.head;b.head+=delta;b.tail+=delta
bpy.ops.object.mode_set(mode='OBJECT');bpy.context.view_layer.update()
assert shape()==beforeShape and actionsig()==beforeActions
rebased=sample();corrections={}
for n,targets in originalFloors.items():
 r.animation_data.action=bpy.data.actions[n];values=[]
 for i,target in enumerate(targets):
  f=1+i*.25;frame(f);delta=target-bootfloor();m=r.pose.bones['Body'].matrix.copy();m.translation.z+=delta;values.append((f,delta,m))
 corrections[n]={'minSourceUnits':min(v[1] for v in values),'maxSourceUnits':max(v[1] for v in values)}
 if max(abs(v[1]) for v in values)>1e-6:
  for f,delta,m in values:
   frame(f);r.pose.bones['Body'].matrix=m;r.pose.bones['Body'].keyframe_insert(data_path='location',frame=f,group='Body')
  for fc in curves(bpy.data.actions[n]):
   if fc.data_path=='pose.bones["Body"].location':
    for k in fc.keyframe_points:k.interpolation='LINEAR'
now=actionsig()
for n,oldAction in beforeActions.items():
 if n.startswith('Run'):
  assert [x for x in oldAction if x[0]!='pose.bones["Body"].location']==[x for x in now[n] if x[0]!='pose.bones["Body"].location']
 else:assert now[n]==oldAction
floorErrors={}
for n,targets in originalFloors.items():
 r.animation_data.action=bpy.data.actions[n];err=0;low=1e9
 for i in range(129):
  frame(1+i*.125);v=bootfloor();low=min(low,v)
  if i%2==0:err=max(err,abs(v-targets[i//2]))
 assert err<1e-5 and low>-.002,(n,err,low)
 floorErrors[n]={'maxQuarterFrameEnvelopeErrorSourceUnits':err,'minimumAtEighthFramesSourceUnits':low}
after=sample()
for clip,d in after.items():assert max(d['maxCommonJointPointDivergenceSourceUnits'].values())<1e-5,(clip,d)
report={'change':'Translate only L_Foot/R_Foot rest head and tail to connector seating centers while preserving axes, hierarchy, geometry, skin weights and all rotation/scale curves; bounded Run Body vertical correction preserves floor envelope.','pivotSelection':'Centroid of original lower connector vertices within 0.018 source units of lowest point; inside boot cuff. Connector meshes tripo_part_9_fixed/10_fixed weighted to Calf.','oldFootPivots':old,'newFootPivots':{n:list(v) for n,v in pivots.items()},'allRotationAndScaleCurvesUnchanged':True,'onlyRunBodyVerticalLocationChanged':True,'bodyCorrections':corrections,'floorEnvelopeValidation':floorErrors,'allMeshVerticesTopologyWeightsUnchanged':True,'worldScale':1.3790181109932065,'before':before,'after':after,'scope':'Source Blender half-frame sampling, not Unity Play Mode.'}
r.animation_data.action=bpy.data.actions['RunRelaxed'];frame(1);bpy.ops.wm.save_as_mainfile(filepath=str(output),relative_remap=False);report['sourceSha256']=hashlib.sha256(output.read_bytes()).hexdigest();(ROOT/'Validation/AnklePivotRepair.json').write_text(json.dumps(report,indent=2)+'\n')
print('ANKLE_REBASED',json.dumps({n:{'oldFloor':before[n]['minBootFloorSourceUnits'],'newFloor':v['minBootFloorSourceUnits'],'maxJointError':max(v['maxCommonJointPointDivergenceSourceUnits'].values())} for n,v in after.items()}))
