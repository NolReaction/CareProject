"""Read-only Blender evaluation of source poses and Unity FindSole markers.
Run Blender --background --python this.py -- INPUT.blend OUTPUT.json.
No .blend, FBX, or project files are saved.
"""
import bpy, sys, math, hashlib, json
from pathlib import Path
from mathutils import Vector, Matrix
import numpy as np

args=sys.argv[sys.argv.index('--')+1:]
src=Path(args[0]);out=Path(args[1]);worldScale=1.3790181109932065
before=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src))
scene=bpy.context.scene;rig=bpy.data.objects['Intercom_Armature_Fixed']
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
names=['Body','Torso','L_Calf','R_Calf','L_Foot','R_Foot','L_Toe','R_Toe']
parents=[names.index(rig.pose.bones[n].parent.name) if rig.pose.bones[n].parent and rig.pose.bones[n].parent.name in names else -1 for n in names]
boots={side:bpy.data.objects[mesh] for side,mesh in [('L','tripo_part_3_fixed'),('R','tripo_part_2_fixed')]}
clips=['Run','RunBackward','RunLeft','RunRight','RunRelaxed','RunBackwardRelaxed','RunLeftRelaxed','RunRightRelaxed']
def f(x):return float(format(float(x),'.7g'))
def vec(v):return [f(x) for x in v]
def pose_frame(frame):
    scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
def world_matrices():return {name:rig.matrix_world@rig.pose.bones[name].matrix for name in names}
def mesh_vertices(obj):
    e=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh()
    flat=np.empty(len(m.vertices)*3);m.vertices.foreach_get('co',flat)
    mat=np.array(e.matrix_world);v=flat.reshape((-1,3))@mat[:3,:3].T+mat[:3,3]
    e.to_mesh_clear();return v

def calibrate(vertices,foot,toe):
    # Source Z corresponds to Unity Y; source forward is -Y.
    low=vertices.min(axis=0);high=vertices.max(axis=0)
    centre=np.array([(low[0]+high[0])*.5,(low[1]+high[1])*.5,low[2]])
    along=-vertices[:,1];back=float(along.min());front=float(along.max())
    bottom=vertices[:,2]<=low[2]+.025/worldScale
    heelPoints=vertices[bottom & (along<=back+(front-back)*.28)]
    toePoints=vertices[bottom & (along>=back+(front-back)*.72)]
    if len(heelPoints):heel=heelPoints.mean(axis=0);heel[2]=heelPoints[:,2].min()
    else:heel=centre+np.array([0,(front-back)*.25,0])
    if len(toePoints):tip=toePoints.mean(axis=0);tip[2]=toePoints[:,2].min()
    else:tip=centre-np.array([0,(front-back)*.25,0])
    return {'centreLocal':vec(foot.inverted()@Vector(centre)),
            'heelLocal':vec(foot.inverted()@Vector(heel)),
            'toeLocal':vec(toe.inverted()@Vector(tip)),
            'centreWorld':vec(centre),'heelWorld':vec(heel),'toeWorld':vec(tip),
            'heelVertexCount':len(heelPoints),'toeVertexCount':len(toePoints),
            'meshVertexCount':len(vertices)}

rig.animation_data.action=bpy.data.actions['Idle'];pose_frame(1)
idleMatrices=world_matrices();calibration={}
for side in ['L','R']:
    calibration[side]=calibrate(mesh_vertices(boots[side]),idleMatrices[side+'_Foot'],idleMatrices[side+'_Toe'])

maxDecomposeError=0.0;maxSelectedMinError=0.0

def sole_cloud(vertices):
    # Deterministic hull samples: support extrema for upright and bounded tilted
    # soles, then evenly fill from points <=20mm above the evaluated minimum.
    # Always retain the exact lowest source mesh vertex.
    selected={int(np.argmin(vertices[:,2]))}
    for tilt,count in [(.5,16),(1.0,8)]:
        for i in range(count):
            a=2*math.pi*i/count
            direction=np.array([math.sin(a)*tilt,math.cos(a)*tilt,1.0])
            selected.add(int(np.argmin(vertices@direction)))
    near=np.flatnonzero(vertices[:,2]<=float(vertices[:,2].min())+.02/worldScale)
    if len(selected)>24:
        lowest=int(np.argmin(vertices[:,2]));others=sorted(selected-{lowest});selected={lowest,*[others[round(i*(len(others)-1)/22)] for i in range(23)]}
    if len(selected)<24 and len(near):
        for i in range(48):
            selected.add(int(near[round(i*(len(near)-1)/47)]))
            if len(selected)>=24:break
    rows=[[i,*vec(vertices[i])] for i in sorted(selected)]
    global maxSelectedMinError
    maxSelectedMinError=max(maxSelectedMinError,abs(min(row[3] for row in rows)-float(vertices[:,2].min()))*worldScale)
    return rows

def sample(phase):
    global maxDecomposeError
    matrices=world_matrices();trs=[]
    for name in names:
        mat=matrices[name];p,q,s=mat.decompose();trs.append(vec(p)+vec(q)+vec(s))
        rebuilt=Matrix.LocRotScale(p,q,s)
        maxDecomposeError=max(maxDecomposeError,max(abs(mat[r][c]-rebuilt[r][c]) for r in range(4) for c in range(4)))
    feet=[]
    for side in ['L','R']:
        vertices=mesh_vertices(boots[side]);c=calibration[side]
        centre=matrices[side+'_Foot']@Vector(c['centreLocal'])
        heel=matrices[side+'_Foot']@Vector(c['heelLocal'])
        tip=matrices[side+'_Toe']@Vector(c['toeLocal'])
        feet.append([f(vertices[:,2].min()),vec(centre),vec(heel),vec(tip),sole_cloud(vertices)])
    return [f(phase),trs,feet]

result={'schema':'intercom-source-runtime-fixture-v1','source':str(src),'sourceSha256Before':before,
        'worldScale':worldScale,'coordinateSystem':'Blender source Z-up, forward -Y. Quaternion order WXYZ. Bone TRS and cloud points in unscaled source world coordinates.',
        'unityConversion':'For the existing pure test convention: worldPositionMetres=(x,z,-y)*worldScale; worldRotation=RotX(-90deg)*sourceWorldQuaternion. L/R names need not match Unity +X/right: use anatomical calf separation. For physical Unity import (-Z visual forward), mirror conversion and facing together; do not guess signs.',
        'bones':names,'parents':parents,'bootOrder':['L','R'],'boneTRSOrder':['px','py','pz','qw','qx','qy','qz','sx','sy','sz'],
        'poseOrder':['normalizedPhase','boneWorldTRS','feet'],
        'footOrder':['actualFullMeshMinimumZ','centreWorld','heelWorld','toeWorld','cloud'],
        'cloudPointOrder':['meshVertexIndex','x','y','z'],'cloudMaxVertices':24,
        'calibration':calibration,'fps':scene.render.fps,'idle':sample(0),'clips':{},
        'scope':'Read-only source evaluation; exact Unity FindSole algorithm reproduced in source coordinates using 25mm world band. Cloud is a compact subset, not full mesh collision proof.'}
for name in clips:
    action=bpy.data.actions[name];rig.animation_data.action=action
    start,end=map(float,action.frame_range);rows=[]
    for i in range(129):
        p=i/128;pose_frame(start+(end-start)*p);rows.append(sample(p))
    result['clips'][name]={'frameRange':[start,end],'samples':rows}
    print('FIXTURE',name,'samples',len(rows),flush=True)
result['sourceSha256After']=hashlib.sha256(src.read_bytes()).hexdigest()
result['sourceUnchanged']=result['sourceSha256After']==before
result['maximumWorldTRSDecompositionMatrixError']=maxDecomposeError
result['maximumRoundedCloudMinimumErrorMetres']=maxSelectedMinError
assert result['sourceUnchanged']
out.write_text(json.dumps(result,separators=(',',':'))+'\n')
print('FIXTURE_DONE',str(out),'bytes',out.stat().st_size,'trsError',maxDecomposeError,'minimumRoundError',maxSelectedMinError,flush=True)
