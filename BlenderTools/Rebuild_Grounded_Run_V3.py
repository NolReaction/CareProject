"""Rebuild 8 Run lower-body actions from a reviewed V2 source; preserve other actions/rest/skin.
Usage: blender -b --python BlenderTools/Rebuild_Grounded_Run_V3.py -- baseline.blend candidate.blend
Requires distinct paths. Does not stack the earlier arm-polish pass. Review geometry before publishing/export.
"""
import bpy,sys,math,json,hashlib,numpy as np
from pathlib import Path
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1];args=sys.argv[sys.argv.index('--')+1:];src,out=map(lambda x:Path(x).resolve(),args[:2]);preflightOnly='--preflight-only' in args
if src==out:raise RuntimeError('Input and output must differ')
bpy.ops.wm.open_mainfile(filepath=str(src));r=bpy.data.objects['Intercom_Armature_Fixed'];s=bpy.context.scene
if r.get('grounded_run_revision'):raise RuntimeError('Already rebuilt; use reviewed V2 baseline')
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
NORM=1.3790181109932065;REF=1.;PERIOD=16/24;STRIDE=REF/NORM*PERIOD;SUPPORT=.40;OUTWARD=.025/NORM;FLAT_START=.12;FLAT_END=.28;MID=(FLAT_START+FLAT_END)/2;GROUND=.001/NORM
names=['Run','RunRelaxed','RunBackward','RunBackwardRelaxed','RunLeft','RunLeftRelaxed','RunRight','RunRightRelaxed'];frames=[1+i*.125 for i in range(129)];sides=['L','R'];rest={b.name:b.matrix_local.copy() for b in r.data.bones}
def frame(f):s.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
def curves(a):return [f for l in a.layers for st in l.strips for bag in st.channelbags for f in bag.fcurves]
def sig(a):return [(f.data_path,f.array_index,[(tuple(k.co),k.interpolation) for k in f.keyframe_points]) for f in curves(a)]
oldActions={a.name:sig(a) for a in bpy.data.actions};oldRest={b.name:(b.parent.name if b.parent else None,tuple(x for row in b.matrix_local for x in row)) for b in r.data.bones};images={i.name:hashlib.sha256(bytes(i.packed_file.data)).hexdigest() for i in bpy.data.images if i.packed_file}
def meshSig():return {o.name:hashlib.sha256(repr(([(tuple(v.co),[(g.group,g.weight) for g in v.groups]) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons])).encode()).hexdigest() for o in bpy.data.objects if o.type=='MESH'}
meshes=meshSig();cache={}
for name in names:
 r.animation_data.action=bpy.data.actions[name];cache[name]=[]
 for f in frames:frame(f);cache[name].append({p.name:p.matrix_basis.copy() for p in r.pose.bones})
r.animation_data.action=bpy.data.actions['Shoot'];frame(1);bodyRef=r.pose.bones['Body'].matrix.copy();neutral={side:r.pose.bones[side+'_Foot'].matrix.to_3x3().to_4x4() for side in sides};hip={side:r.pose.bones[side+'_Calf'].head.copy() for side in sides};calfQ={side:r.pose.bones[side+'_Calf'].matrix.to_quaternion() for side in sides};link={side:r.pose.bones[side+'_Foot'].head-hip[side] for side in sides};hipOffset={side:hip[side].z-bodyRef.translation.z for side in sides};skin={}
for side,objname in [('L','tripo_part_3_fixed'),('R','tripo_part_2_fixed')]:
 o=bpy.data.objects[objname];v=np.array([list(o.matrix_world@v.co)+[1] for v in o.data.vertices]);w=np.array([sum(g.weight for g in v.groups if o.vertex_groups[g.group].name==side+'_Toe') for v in o.data.vertices]);skin[side]=(v,w)
bodyObj=bpy.data.objects['tripo_part_0_fixed'];bo=bodyObj.evaluated_get(bpy.context.evaluated_depsgraph_get());bm=bo.to_mesh();bodyNeutral=np.array([bo.matrix_world@v.co for v in bm.vertices]);bo.to_mesh_clear();bootPolys={side:[list(p.vertices) for p in bpy.data.objects[n].data.polygons] for side,n in [('L','tripo_part_3_fixed'),('R','tripo_part_2_fixed')]};bodyPolys=[list(p.vertices) for p in bodyObj.data.polygons]
def hermite(a,b,va,vb,ma,mb,p):
 u=(p-a)/(b-a);return (2*u**3-3*u*u+1)*va+(u**3-2*u*u+u)*(b-a)*ma+(-2*u**3+3*u*u)*vb+(u**3-u*u)*(b-a)*mb
def profile(keys,p):
 p=p%1
 for (a,va,ma),(b,vb,mb) in zip(keys,keys[1:]):
  if a<=p<=b:return hermite(a,b,va,vb,ma,mb,p)
 return keys[0][1]
def angles(p,kind):
 if kind=='Forward':
  pitch=profile([(0,-18,150),(.12,0,0),(.28,0,0),(.40,18,150),(.50,2,-180),(.65,-8,0),(.85,-14,0),(.93,-18,0),(1,-18,150)],p);bank=0;toe=profile([(0,0,0),(.28,0,0),(.40,-10,-80),(.50,-3,80),(.60,0,0),(1,0,0)],p)
 elif kind=='Backward':
  pitch=profile([(0,16,-133.3333),(.12,0,0),(.28,0,0),(.40,-18,-150),(.55,-8,0),(.78,-6,0),(.93,12,80),(1,16,-133.3333)],p);bank=0;toe=profile([(0,-6,50),(.12,0,0),(.75,0,0),(.90,-3,-30),(1,-6,50)],p)
 else:
  sign=1 if kind=='Left' else -1;bank=sign*profile([(0,-16,133.3333),(.12,0,0),(.28,0,0),(.40,16,133.3333),(.55,2,-100),(.70,-6,0),(.90,-16,0),(1,-16,133.3333)],p);pitch=profile([(0,0,0),(.40,0,0),(.60,-5,0),(.85,-5,0),(1,0,0)],p);toe=0
 return pitch,bank,toe
memo={}
def shape(side,p,kind):
 phase=p%1;pitch,bank,toe=angles(phase,kind)
 key=(side,round(pitch,8),round(bank,8),round(toe,8))
 if key in memo:return memo[key]
 fm=Matrix.Rotation(math.radians(bank),4,'Y')@Matrix.Rotation(math.radians(pitch),4,'X')@neutral[side];fd=fm@rest[side+'_Foot'].inverted();th=rest[side+'_Toe'].translation;td=fd@Matrix.Translation(th)@Matrix.Rotation(math.radians(toe),4,'X')@Matrix.Translation(-th);v,w=skin[side];a=v@np.array(fd).T;b=v@np.array(td).T;vs=a*(1-w[:,None])+b*w[:,None];ans=(fm,td@rest[side+'_Toe'],vs[:,:3],toe);memo[key]=ans;return ans
def bodyShift(t):return Vector((OUTWARD*math.cos(2*math.pi*(t-MID)),0,0))
def globalPhase(side,p):return (p-(0 if side=='L' else .5))%1
def hipXY(side,t):return Vector((hip[side].x,hip[side].y,0))+bodyShift(t)
results={};poses={};GLOBAL_REACH=.95
for kind,D in [('Forward',(0,-1,0)),('Backward',(0,1,0)),('Left',(1,0,0)),('Right',(-1,0,0))]:
 OUTWARD=(.055 if kind in ['Left','Right'] else .035 if kind=='Backward' else .025)/NORM
 STRIDE=(.75 if kind in ['Left','Right'] else 1.)/NORM*PERIOD
 D=Vector(D);entry={side:int(np.argmin(shape(side,0,kind)[2][:,2])) for side in sides};exitid={side:int(np.argmin(shape(side,SUPPORT,kind)[2][:,2])) for side in sides};flat={side:shape(side,.20,kind)[2] for side in sides};centers={side:Vector((hip[side].x+(OUTWARD if side=='L' else -OUTWARD),hip[side].y,0))+Vector((link[side].x,link[side].y,0))-D*link[side].dot(D) for side in sides}
 def flatXY(side,p):return centers[side]-D*STRIDE*(p-MID)
 def stanceXY(side,p):
  if p<FLAT_START:
   idx=entry[side];v=flatXY(side,FLAT_START)+Vector(flat[side][idx])-D*STRIDE*(p-FLAT_START)-Vector(shape(side,p,kind)[2][idx])
  elif p>FLAT_END:
   idx=exitid[side];v=flatXY(side,FLAT_END)+Vector(flat[side][idx])-D*STRIDE*(p-FLAT_END)-Vector(shape(side,p,kind)[2][idx])
  else:v=flatXY(side,p)
  v.z=0;return v
 def supportHeight(side,p):
  xy=stanceXY(side,p);v=xy-hipXY(side,globalPhase(side,p));length=link[side].length;h2=v.x*v.x+v.y*v.y
  if h2>=length*length*GLOBAL_REACH**2:raise RuntimeError(f'Support unreachable {kind} {side} {p}: {math.sqrt(h2)/length}')
  return GROUND-float(shape(side,p,kind)[2][:,2].min())+math.sqrt(length*length-h2)-hipOffset[side]
 def bodyHeight(t):
  pL=t%1;pR=(t+.5)%1
  if pL<=SUPPORT:return supportHeight('L',pL)
  if pR<=SUPPORT:return supportHeight('R',pR)
  prev='L' if pL<.5 else 'R';nxt='R' if prev=='L' else 'L';p=pL if prev=='L' else pR;u=(p-SUPPORT)/(.5-SUPPORT);a=supportHeight(prev,SUPPORT);b=supportHeight(nxt,0);return a+(b-a)*(u*u*(3-2*u))+.002/NORM*math.sin(math.pi*u)**2
 targetErrors=[0.]
 # Continuous spherical swing. Height sets the radius; angle follows a
 # deliberate outward arc with matched boundary angular velocities.
 swingPolar={}
 for side in sides:
  eps=1e-4
  def vAt(p):return stanceXY(side,p)-hipXY(side,globalPhase(side,p))
  va=vAt(SUPPORT);vb=vAt(0);da=(va-vAt(SUPPORT-eps))/eps;db=(vAt(eps)-vb)/eps;a0=math.atan2(va.y,va.x);a1=math.atan2(vb.y,vb.x);short=(a1-a0+math.pi)%(2*math.pi)-math.pi;prefer=Vector((1 if side=='L' else -1,0,0));options=[short,short+2*math.pi,short-2*math.pi];delta=max(options,key=lambda z:Vector((math.cos(a0+z*.5),math.sin(a0+z*.5),0)).dot(prefer)-.03*abs(z));assert abs(delta)<math.pi*1.25,(kind,side,delta)
  speed0=(va.x*da.y-va.y*da.x)/max(1e-8,va.length_squared);speed1=(vb.x*db.y-vb.y*db.x)/max(1e-8,vb.length_squared);swingPolar[side]=(a0,a0+delta,speed0,speed1)
 data=[];minfloor=1e9;maxreach=0;outside=0;minbody=1e9;maxbody=-1e9
 for i in range(129):
  t=i/128;bz=bodyHeight(t);shift=bodyShift(t);minbody=min(minbody,bz);maxbody=max(maxbody,bz);row={'bodyZ':bz,'bodyShift':shift,'feet':{}}
  for side,phase in [('L',0),('R',.5)]:
   p=(t+phase)%1;fm,tm,vs,toe=shape(side,p,kind);length=link[side].length;hz=bz+hipOffset[side]
   if p<=SUPPORT:
    xy=stanceXY(side,p);v=xy-hipXY(side,t);h2=v.x*v.x+v.y*v.y
    if h2>=length*length*GLOBAL_REACH**2:raise RuntimeError(f'Stance unreachable {kind} {side} {p}')
    v.z=-math.sqrt(length*length-h2)
   else:
    u=(p-SUPPORT)/(1-SUPPORT);targetFloor=(.001+.008*math.sin(math.pi*u)**2)/NORM;vertical=targetFloor-float(vs[:,2].min())-hz
    vertical=max(vertical,-length*.98)
    if not -length<vertical<-.02:raise RuntimeError(f'Swing height unreachable {kind} {side} {p}: {vertical}')
    radius=math.sqrt(length*length-vertical*vertical);a0,a1,d0,d1=swingPolar[side];angle=hermite(SUPPORT,1,a0,a1,d0,d1,p);v=Vector((radius*math.cos(angle),radius*math.sin(angle),vertical));h2=radius*radius;outside=max(outside,abs(v.x if abs(D.y)>.5 else v.y))
   if h2>=length*length*GLOBAL_REACH**2:raise RuntimeError(f'Swing reach excessive {kind} {side} {p}: {math.sqrt(h2)/length}')
   hxy=hipXY(side,t);ankle=Vector((hxy.x,hxy.y,hz))+v;low=ankle.z+float(vs[:,2].min());minfloor=min(minfloor,low);maxreach=max(maxreach,math.sqrt(h2)/length);row['feet'][side]={'vector':v,'ankle':ankle,'foot':fm,'toe':tm,'toeAngle':toe,'minimum':low}
  data.append(row)
 results[kind]={'minimumFloorMetres':minfloor*NORM,'bodyRangeMetres':(maxbody-minbody)*NORM,'bodyRange':[minbody,maxbody],'maxHorizontalReachFraction':maxreach,'maximumCircumductionMetres':outside*NORM,'referenceSpeed':(.75 if kind in ['Left','Right'] else 1.),'physicalTopSpeed':(1.575 if kind in ['Left','Right'] else 2.1),'maximumToeFlexDegrees':max(abs(q['toeAngle']) for row in data for q in row['feet'].values()),'swingArcDegrees':{side:math.degrees(v[1]-v[0]) for side,v in swingPolar.items()},'bodyLateralPeakToPeakMetres':2*OUTWARD*NORM};poses[kind]=data;print('V3_CANDIDATE',kind,results[kind],flush=True)
assert all(v['minimumFloorMetres']>=-.0001 and v['bodyRangeMetres']<.08 and v['maxHorizontalReachFraction']<.95 for v in results.values()),results
preflightHits=[]
for kind,data in poses.items():
 for i,row in enumerate(data):
  vs={side:shape(side,(i/128+(0 if side=='L' else .5))%1,kind)[2]+np.array(row['feet'][side]['ankle']) for side in sides};vs['Body']=bodyNeutral+np.array(row['bodyShift'])+np.array([0,0,row['bodyZ']-bodyRef.translation.z])
  for a,b in [('L','R'),('L','Body'),('R','Body')]:
   va,vb=vs[a],vs[b]
   if np.all(va.max(0)>=vb.min(0)) and np.all(vb.max(0)>=va.min(0)):
    ta=BVHTree.FromPolygons([Vector(v) for v in va],bodyPolys if a=='Body' else bootPolys[a]);tb=BVHTree.FromPolygons([Vector(v) for v in vb],bodyPolys if b=='Body' else bootPolys[b]);hits=ta.overlap(tb)
    if hits:preflightHits.append({'direction':kind,'phase':i/128,'objects':[a,b],'trianglePairs':len(hits)})
print('V3_PREFLIGHT_COLLISIONS',len(preflightHits),flush=True)
out.with_suffix('.preflight.json').write_text(json.dumps({'directions':results,'collisions':preflightHits},indent=2)+'\n')
if preflightOnly:sys.exit(0)
if preflightHits:raise RuntimeError('Candidate has mechanical boot collisions; inspect preflight report before authoring')
changed=['Body','L_Calf','R_Calf','L_Foot','R_Foot','L_Toe','R_Toe'];paths={f'pose.bones["{n}"].rotation_quaternion' for n in changed if n!='Body'}|{'pose.bones["Body"].location'}
for name in names:
 kind='Backward' if 'Backward' in name else 'Left' if 'Left' in name else 'Right' if 'Right' in name else 'Forward';a=bpy.data.actions[name];r.animation_data.action=a;newposes=[]
 for i,f in enumerate(frames):
  frame(f)
  for n,m in cache[name][i].items():r.pose.bones[n].matrix_basis=m
  bpy.context.view_layer.update();row=poses[kind][i];body=r.pose.bones['Body'];m=bodyRef.copy();m.translation+=row['bodyShift'];m.translation.z=row['bodyZ'];body.matrix=m;bpy.context.view_layer.update()
  for side in sides:
   info=row['feet'][side];c=r.pose.bones[side+'_Calf'];m=(link[side].rotation_difference(info['vector'])@calfQ[side]).to_matrix().to_4x4();m.translation=c.head;oldloc=c.location.copy();oldscale=c.scale.copy();c.matrix=m;c.location=oldloc;c.scale=oldscale;bpy.context.view_layer.update()
   ft=r.pose.bones[side+'_Foot'];m=info['foot'].copy();m.translation=ft.head;oldloc=ft.location.copy();oldscale=ft.scale.copy();ft.matrix=m;ft.location=oldloc;ft.scale=oldscale;bpy.context.view_layer.update()
   toe=r.pose.bones[side+'_Toe'];m=info['toe'].copy();m.translation+=ft.head;oldloc=toe.location.copy();oldscale=toe.scale.copy();toe.matrix=m;toe.location=oldloc;toe.scale=oldscale;bpy.context.view_layer.update()
  newposes.append({n:r.pose.bones[n].matrix_basis.copy() for n in changed})
 for l in a.layers:
  for st in l.strips:
   for bag in st.channelbags:
    for fc in list(bag.fcurves):
     if fc.data_path in paths:bag.fcurves.remove(fc)
 prev={}
 for f,pose in zip(frames,newposes):
  frame(f)
  for n,m in pose.items():
   b=r.pose.bones[n];b.matrix_basis=m
   if n=='Body':b.keyframe_insert(data_path='location',frame=f,group=n)
   else:
    q=b.rotation_quaternion.copy()
    if n in prev and q.dot(prev[n])<0:q.negate()
    b.rotation_quaternion=q;prev[n]=q.copy();b.keyframe_insert(data_path='rotation_quaternion',frame=f,group=n)
 for fc in curves(a):
  if fc.data_path in paths:
   for k in fc.keyframe_points:k.interpolation='LINEAR'
   if not any(m.type=='CYCLES' for m in fc.modifiers):fc.modifiers.new('CYCLES')
 assert [x for x in oldActions[name] if x[0] not in paths]==[x for x in sig(a) if x[0] not in paths]
assert all(sig(bpy.data.actions[n])==a for n,a in oldActions.items() if n not in names)
assert oldRest=={b.name:(b.parent.name if b.parent else None,tuple(x for row in b.matrix_local for x in row)) for b in r.data.bones}
assert meshes==meshSig();assert images=={i.name:hashlib.sha256(bytes(i.packed_file.data)).hexdigest() for i in bpy.data.images if i.packed_file}
# Portable output: package all 28 original textures, byte for byte.
textureDir=ROOT/'Assets/IntercomMVP/Art/Robot/Textures';textureFiles={p.name:p for p in textureDir.iterdir() if p.is_file() and p.suffix.lower() in ['.png','.jpg','.jpeg']};packedOriginals={}
for image in bpy.data.images:
 basename=Path(image.filepath.replace('\\','/')).name;original=textureFiles.get(basename) or textureFiles.get(image.name)
 if original is None:continue
 payload=original.read_bytes()
 if not image.packed_file:image.filepath=str(original);image.reload();image.pack()
 assert bytes(image.packed_file.data)==payload;image.filepath='//../../Assets/IntercomMVP/Art/Robot/Textures/'+original.name;packedOriginals[original.name]=hashlib.sha256(payload).hexdigest()
assert len(packedOriginals)==28,'Expected all 28 original maps'
r['grounded_run_revision']='v3-grounded-1';r.animation_data.action=bpy.data.actions['RunRelaxed'];frame(1);s.frame_start=1;s.frame_end=17;bpy.ops.wm.save_as_mainfile(filepath=str(out),relative_remap=False)
d={'status':'authored-awaiting-validation','inputSha256':hashlib.sha256(src.read_bytes()).hexdigest(),'outputSha256':hashlib.sha256(out.read_bytes()).hexdigest(),'supportWindow':[0,SUPPORT],'flatWindow':[FLAT_START,FLAT_END],'referenceSpeed':REF,'packedOriginalTextures':packedOriginals,'nonRunActionsUnchanged':True,'restGeometryWeightsImagesUnchanged':True,'directions':results};out.with_suffix('.authoring.json').write_text(json.dumps(d,indent=2)+'\n');print('V3_AUTHORED',out,flush=True)
