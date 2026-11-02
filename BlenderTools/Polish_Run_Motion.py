"""Polish the existing 8 Run clips while preserving repaired mechanical ankles.

Blender command: blender -b --python BlenderTools/Polish_Run_Motion.py -- input.blend output.blend
Input must be the reviewed baseline with corrected ankle pivots. Keep a backup:
arm gain is applied to input motion, so do not repeatedly apply to the output.
No rest-rig, geometry, weights, Idle or weapon-action edits; no new clips.
Calf rotates on a fixed-radius arc: flat stance gain 1.5, original swing amplitude.
Smooth reach limit prevents impossible extension of the short mechanical leg.
"""
import bpy,sys,math,json,hashlib
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:]
if len(args)!=2:raise RuntimeError('Specify baseline input.blend and distinct output.blend')
source=Path(args[0]).resolve();output=Path(args[1]).resolve()
if source==output:raise RuntimeError('Keep baseline input separate from output')
bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;rig=bpy.data.objects['Intercom_Armature_Fixed']
if rig.get('motion_polish_revision'):raise RuntimeError('This source is already polished. Use the unmodified reviewed baseline, not a polished output.')
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
runs=['Run','RunRelaxed','RunBackward','RunBackwardRelaxed','RunLeft','RunLeftRelaxed','RunRight','RunRightRelaxed'];frames=[1+i*.25 for i in range(65)];boots=['tripo_part_3_fixed','tripo_part_2_fixed'];sides=['L','R']
rest={b.name:b.matrix_local.copy() for b in rig.data.bones};originalRest={b.name:(b.parent.name if b.parent else None,tuple(x for row in b.matrix_local for x in row)) for b in rig.data.bones}
def frame(f):scene.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
def curves(a):return [f for l in a.layers for st in l.strips for bag in st.channelbags for f in bag.fcurves]
def signature(a):return [(f.data_path,f.array_index,[(tuple(k.co),k.interpolation) for k in f.keyframe_points]) for f in curves(a)]
beforeActions={a.name:signature(a) for a in bpy.data.actions}
def mesh_signature():
 return {o.name:hashlib.sha256(repr(([(tuple(v.co),[(g.group,g.weight) for g in v.groups]) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons])).encode()).hexdigest() for o in bpy.data.objects if o.type=='MESH'}
meshesBefore=mesh_signature();imagesBefore={i.name:hashlib.sha256(bytes(i.packed_file.data)).hexdigest() for i in bpy.data.images if i.packed_file}
def floor():
 lows=[]
 for n in boots:
  o=bpy.data.objects[n].evaluated_get(bpy.context.evaluated_depsgraph_get());m=o.to_mesh();lows.append(min((o.matrix_world@v.co).z for v in m.vertices));o.to_mesh_clear()
 return lows
baseline={}
for name in runs:
 rig.animation_data.action=bpy.data.actions[name];data=[]
 for f in frames:
  frame(f);data.append({'basis':{p.name:p.matrix_basis.copy() for p in rig.pose.bones},'floor':floor(),'ankleVectors':{side:rig.pose.bones[side+'_Foot'].head-rig.pose.bones[side+'_Calf'].head for side in sides}})
 baseline[name]=data
rig.animation_data.action=bpy.data.actions['Shoot'];frame(1);footNeutral={side:rig.pose.bones[side+'_Foot'].matrix.to_3x3().to_4x4() for side in sides}
def smooth(x):x=max(0.,min(1.,x));return x*x*(3.-2.*x)
def interp(keys,p):
 p=p%1
 for (a,x),(b,y) in zip(keys,keys[1:]):
  if a<=p<=b:return x+(y-x)*smooth((p-a)/(b-a))
 return keys[0][1]
def rot(axis,deg):return Matrix.Rotation(math.radians(deg),4,axis)
def foot_angles(p,kind):
 if kind=='Forward':
  pitch=interp([(0,-10),(.055,0),(.16,0),(.23,10),(.28,16),(.37,11),(.5,-3),(.68,-9),(.84,-14),(.93,-14),(1,-10)],p)
  toe=-min(12,max(0,pitch))*smooth((p-.13)/.07);bank=0
 elif kind=='Backward':
  pitch=interp([(0,8),(.055,0),(.17,0),(.28,-12),(.40,-10),(.60,-5),(.78,3),(.9,10),(1,8)],p)
  toe=-min(8,max(0,pitch));bank=0
 else:
  sign=1 if kind=='Left' else -1;q=p%.5
  bank=sign*interp([(0,3),(.055,0),(.17,0),(.28,-4),(.40,0),(.5,3),(1,3)],q)
  pitch=interp([(0,-5),(.055,0),(.17,0),(.28,6),(.38,-2),(.55,-8),(.78,-9),(.91,-7),(1,-5)],p)
  toe=-min(6,max(0,pitch))*smooth((p-.13)/.07)
 return pitch,bank,toe
# Hemisphere-aligned local quaternion averages retain the model's original shoulder axes.
def meanq(qs):
 first=qs[0];acc=Vector((0,0,0,0))
 for q in qs:
  v=Vector(tuple(q));acc+=v if q.dot(first)>=0 else -v
 answer=Quaternion(tuple(acc));answer.normalize();return answer
means={name:{side:meanq([d['basis'][side+'_Upperarm'].to_quaternion() for d in baseline[name]]) for side in sides} for name in runs}
reports={}
for name in runs:
 action=bpy.data.actions[name];rig.animation_data.action=action;kind='Backward' if 'Backward' in name else 'Left' if 'Left' in name else 'Right' if 'Right' in name else 'Forward';relaxed=name.endswith('Relaxed')
 direction=Vector((0,1,0) if kind in ['Forward','Backward'] else (1,0,0));ankleCenters={side:sum(d['ankleVectors'][side].dot(direction) for i,d in enumerate(baseline[name]) if .055<=((i/64+(0 if side=='L' else .5))%1)<=.17)/sum(1 for i in range(65) if .055<=((i/64+(0 if side=='L' else .5))%1)<=.17) for side in sides}
 changed=['Body','L_Calf','R_Calf','L_Foot','R_Foot','L_Toe','R_Toe']+([side+'_'+b for side in sides for b in ['Upperarm','Forearm_A','Forearm_B','Forearm_C']] if relaxed else [])
 # Capture input poses before changing curves so the authoring pass cannot feed back into itself.
 poses=[];minDelta=1e9;maxDelta=-1e9
 for i,f in enumerate(frames):
  frame(f)
  for n,m in baseline[name][i]['basis'].items():rig.pose.bones[n].matrix_basis=m
  bpy.context.view_layer.update();t=i/64
  for side,phase in [('L',0),('R',.5)]:
   p=(t+phase)%1;pitch,bank,toe=foot_angles(p,kind)
   calf=rig.pose.bones[side+'_Calf'];v=rig.pose.bones[side+'_Foot'].head-calf.head;target=v.copy();projection=v.dot(direction);gainWeight=(1 if p<=.17 else 1-smooth((p-.17)/.11) if p<.28 else 0 if p<=.88 else smooth((p-.88)/.12)) if kind in ['Left','Right'] else (1 if p<=.28 else 1-smooth((p-.28)/.14) if p<.42 else 0 if p<=.88 else smooth((p-.88)/.12));desiredProjection=projection+(projection-ankleCenters[side])*.5*gainWeight
   otherHorizontal=v.x if direction.y else v.y;reachable=math.sqrt(v.length_squared-otherHorizontal*otherHorizontal);start=.72*reachable;limit=.92*reachable
   if abs(desiredProjection)>start:desiredProjection=math.copysign(start+(limit-start)*(1-math.exp(-(abs(desiredProjection)-start)/(limit-start))),desiredProjection)
   target+=direction*(desiredProjection-projection);remaining=v.length_squared-target.x*target.x-target.y*target.y
   if remaining<=0:raise RuntimeError(f'Unreachable ankle at {name}:{f}:{side}: vector={tuple(v)}, target={tuple(target)}, center={ankleCenters[side]}, length={v.length}')
   target.z=-math.sqrt(remaining);oldloc=calf.location.copy();oldscale=calf.scale.copy();desired=v.rotation_difference(target).to_matrix().to_4x4()@calf.matrix;desired.translation=calf.head;calf.matrix=desired;calf.location=oldloc;calf.scale=oldscale;bpy.context.view_layer.update()
   foot=rig.pose.bones[side+'_Foot'];oldloc=foot.location.copy();oldscale=foot.scale.copy()
   desired=rot('Y',bank)@rot('X',pitch)@footNeutral[side];desired.translation=foot.head;foot.matrix=desired;foot.location=oldloc;foot.scale=oldscale;bpy.context.view_layer.update()
   toeBone=rig.pose.bones[side+'_Toe'];th=rest[side+'_Toe'].translation;deform=foot.matrix@rest[side+'_Foot'].inverted();desired=deform@Matrix.Translation(th)@rot('X',toe)@Matrix.Translation(-th)@rest[side+'_Toe'];oldloc=toeBone.location.copy();oldscale=toeBone.scale.copy();toeBone.matrix=desired;toeBone.location=oldloc;toeBone.scale=oldscale;bpy.context.view_layer.update()
   if relaxed:
    n=side+'_Upperarm';q=baseline[name][i]['basis'][n].to_quaternion();mean=means[name][side];delta=mean.inverted()@q
    if delta.w<0:delta.negate()
    rig.pose.bones[n].rotation_mode='QUATERNION';rig.pose.bones[n].rotation_quaternion=mean@Quaternion(delta.axis,delta.angle*2.4)
    assert abs(rig.pose.bones[n].rotation_quaternion.magnitude-1)<1e-5
    assert (mean.inverted()@rig.pose.bones[n].rotation_quaternion).angle<math.radians(16), (name,f,side)
    for j,bone in enumerate(['Forearm_A','Forearm_B','Forearm_C']):
     n=side+'_'+bone;bonePose=rig.pose.bones[n];base=baseline[name][i]['basis'][n].to_quaternion();axis=rest[n].to_3x3().inverted()@Vector((1,0,0));axis.normalize();angle=[2.2,1.5,.8][j]*math.sin(2*math.pi*(p-.06-.035*j))
     bonePose.rotation_mode='QUATERNION';bonePose.rotation_quaternion=Quaternion(axis,math.radians(angle))@base
    bpy.context.view_layer.update()
  delta=min(baseline[name][i]['floor'])-min(floor());body=rig.pose.bones['Body'];m=body.matrix.copy();m.translation.z+=delta;body.matrix=m;bpy.context.view_layer.update();minDelta=min(minDelta,delta);maxDelta=max(maxDelta,delta)
  poses.append({n:rig.pose.bones[n].matrix_basis.copy() for n in changed})
 # Rewrite only owned channels. All arm positions/scales and all leg locations/scales are retained.
 paths={f'pose.bones["{n}"].rotation_quaternion' for n in changed if n!='Body'}|{'pose.bones["Body"].location'}
 for l in action.layers:
  for st in l.strips:
   for bag in st.channelbags:
    for fc in list(bag.fcurves):
     if fc.data_path in paths:bag.fcurves.remove(fc)
 prev={}
 for f,pose in zip(frames,poses):
  frame(f)
  for n,m in pose.items():
   b=rig.pose.bones[n];b.matrix_basis=m
   if n=='Body':b.keyframe_insert(data_path='location',frame=f,group=n)
   else:
    b.rotation_mode='QUATERNION';q=b.rotation_quaternion.copy()
    if n in prev and q.dot(prev[n])<0:q.negate()
    b.rotation_quaternion=q;prev[n]=q.copy();b.keyframe_insert(data_path='rotation_quaternion',frame=f,group=n)
 for fc in curves(action):
  if fc.data_path in paths:
   for k in fc.keyframe_points:k.interpolation='LINEAR'
   if not any(m.type=='CYCLES' for m in fc.modifiers):fc.modifiers.new('CYCLES')
 unchangedBefore=[x for x in beforeActions[name] if x[0] not in paths];unchangedAfter=[x for x in signature(action) if x[0] not in paths];assert unchangedBefore==unchangedAfter,name
 reports[name]={'kind':kind,'flatStanceAnkleAmplitudeGain':1.5,'swingGain':1.0,'maximumHorizontalReachFraction':.92,'relaxedArmGain':2.4 if relaxed else 1,'maximumToeFlexDegrees':12 if kind=='Forward' else 8 if kind=='Backward' else 6,'bodyFloorCorrectionSourceUnits':[minDelta,maxDelta],'editedChannels':sorted(paths)}
assert {b.name:(b.parent.name if b.parent else None,tuple(x for row in b.matrix_local for x in row)) for b in rig.data.bones}==originalRest
assert meshesBefore==mesh_signature()
assert imagesBefore=={i.name:hashlib.sha256(bytes(i.packed_file.data)).hexdigest() for i in bpy.data.images if i.packed_file}
assert all(signature(bpy.data.actions[n])==sig for n,sig in beforeActions.items() if n not in runs)
rig['motion_polish_revision']='2026-11-grounded-v1'
rig.animation_data.action=bpy.data.actions['RunRelaxed'];frame(1);scene.frame_start=1;scene.frame_end=17;output.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(output),relative_remap=False)
report={'sourceInputSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'sourceOutputSha256':hashlib.sha256(output.read_bytes()).hexdigest(),'restRigUnchanged':True,'geometryTopologyWeightsUnchanged':True,'allPackedImagesUnchanged':True,'packedImageCount':len(imagesBefore),'nonRunActionsExactlyPreserved':[n for n in beforeActions if n not in runs],'rigBoneCount':len(rig.data.bones),'supportWindows':{'left':[0,.28],'right':[.5,.78]},'actions':reports,'scope':'Authoring invariants only. Visual, deformed-geometry and FBX roundtrip validation required separately.'}
(ROOT/'Validation/MotionPolishAuthoring.json').write_text(json.dumps(report,indent=2)+'\n');print('MOTION_POLISH_AUTHORED',json.dumps({n:d['bodyFloorCorrectionSourceUnits'] for n,d in reports.items()}))
