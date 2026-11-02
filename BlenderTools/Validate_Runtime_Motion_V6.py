"""Read-only mesh verification of measured production-runtime pose traces.

Blender 5.2+: blender -b --python this.py -- model.blend report.json trace1.json ...
Traces are produced by Validation/RunningContactTests, not Unity playback.
"""
import bpy, sys, json, hashlib, math
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix
from mathutils.bvhtree import BVHTree

args=sys.argv[sys.argv.index('--')+1:]
source, destination=map(Path,args[:2])
bpy.ops.wm.open_mainfile(filepath=str(source))
rig=bpy.data.objects['Intercom_Armature_Fixed']
scene=bpy.context.scene
objects={n:bpy.data.objects[n] for n in ['tripo_part_0_fixed','tripo_part_3_fixed','tripo_part_2_fixed']}
polygons={n:[tuple(p.vertices) for p in o.data.polygons] for n,o in objects.items()}
conversion=Quaternion((1,0,0),math.pi/2)
reports=[]
for filename in args[2:]:
    trace=json.loads(Path(filename).read_text())
    norm=trace['scale']
    collisions=[]
    minimum=1e6
    max_torso_offset=0
    for index,row in enumerate(trace['frames']):
        phase=row['phase']%1
        clip=trace['clip'].split('+')[0]
        rig.animation_data.action=bpy.data.actions[clip]
        frame=1+phase*16
        scene.frame_set(int(frame),subframe=frame-int(frame))
        # Remove world travel: it has no effect on self-intersection or floor gap.
        origin=Vector(row['root'])
        for name,pose in zip(trace['bones'],row['bones']):
            p=Vector(pose['position'])-origin
            p=Vector((p.x,-p.z,p.y))/norm
            q=pose['rotation']
            q=conversion@Quaternion((q[3],q[0],q[1],q[2]))
            rig.pose.bones[name].matrix=Matrix.LocRotScale(p,q,Vector(pose['scale']))
            bpy.context.view_layer.update()
        max_torso_offset=max(max_torso_offset,(rig.pose.bones['Torso'].head-rig.pose.bones['Body'].head).length*norm)
        trees={}
        bounds={}
        for name,obj in objects.items():
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh=evaluated.to_mesh()
            vertices=[evaluated.matrix_world@v.co for v in mesh.vertices]
            if name!='tripo_part_0_fixed': minimum=min(minimum,min(v.z for v in vertices)*norm)
            bounds[name]=[(min(v[a] for v in vertices),max(v[a] for v in vertices)) for a in range(3)]
            trees[name]=BVHTree.FromPolygons(vertices,polygons[name])
            evaluated.to_mesh_clear()
        for boot in ['tripo_part_3_fixed','tripo_part_2_fixed']:
            body='tripo_part_0_fixed'
            if all(bounds[boot][a][1]>=bounds[body][a][0] and bounds[body][a][1]>=bounds[boot][a][0] for a in range(3)):
                hits=trees[boot].overlap(trees[body])
                if hits:collisions.append({'sample':index,'phase':phase,'boot':boot,'pairs':len(hits)})
    report={'clip':trace['clip'],'speed':trace['speed'],'fps':trace['fps'],'frames':len(trace['frames']),
            'minimumBootFloorMeters':minimum,'maximumTorsoSuspensionMeters':max_torso_offset,'bodyBootIntersections':collisions}
    reports.append(report)
    print('V6_MESH',json.dumps(report),flush=True)
result={'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'blender':bpy.app.version_string,
        'scope':'Blender evaluated original meshes driven by production C# algorithm traces at 60 Hz. Independent skinning/self-intersection check; not Unity Animator/PhysX/rendering.',
        'bodyMeshSkin':'tripo_part_0_fixed: 2276 vertices weighted entirely to Torso',
        'traces':reports,'status':'PASS' if all(not r['bodyBootIntersections'] and r['minimumBootFloorMeters']>=-.001 for r in reports) else 'REVIEW'}
destination.write_text(json.dumps(result,indent=2)+'\n')
print('V6_MESH_STATUS',result['status'],flush=True)
