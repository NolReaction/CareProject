"""Build the editable mechanical Udarnik directly from the untouched supplied GLB.

The supplied face belongs to the torso mesh while the helmet is a separate part.
V19 makes a real head assembly, closes cut surfaces and adds a nested neck joint.
Game export may join temporary copies; the source objects remain separate.
"""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector,Matrix
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
SEMANTIC={0:'BodyShell',3:'Helmet',1:'UpperArm.L',2:'UpperArm.R',7:'Glove.L',6:'Glove.R',9:'Leg.L',8:'Leg.R',4:'Boot.L',5:'Boot.R',13:'Boot.L',11:'Boot.L',10:'Boot.R'}
BONE={0:'Body',3:'Head',1:'UpperArm.L',2:'UpperArm.R',7:'Forearm.L',6:'Forearm.R',4:'Foot.L',5:'Foot.R',13:'Foot.L',11:'Foot.L',10:'Foot.R'}

def _rigid(obj,bone):
    for group in list(obj.vertex_groups):
        if not group.name.startswith('Part.'):obj.vertex_groups.remove(group)
    g=obj.vertex_groups.new(name=bone);g.add(list(range(len(obj.data.vertices))),1,'REPLACE')

def _clean_and_cap(obj,dark_uv,plane=None,upper=False):
    bm=bmesh.new();bm.from_mesh(obj.data)
    before=len(bm.verts);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000003)
    welded=before-len(bm.verts)
    if plane is not None:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000002,
          plane_co=(0,0,plane),plane_no=(0,0,1),clear_inner=upper,clear_outer=not upper)
    # Close each component's open ends, never bridge distinct robot parts.
    boundary=[e for e in bm.edges if e.is_boundary]
    created=bmesh.ops.holes_fill(bm,edges=boundary,sides=0).get('faces',[]) if boundary else []
    # The source boot ankle is a strongly non-planar loop. Blender's ngon filler
    # legitimately refuses it; close that loop as a small mechanical cap fan.
    remaining={e for e in bm.edges if e.is_boundary}
    while remaining:
        first=next(iter(remaining));loop=[first.verts[0]];edge=first;current=first.verts[1];remaining.remove(first)
        while current!=loop[0]:
            loop.append(current);choices=[e for e in current.link_edges if e in remaining]
            if len(choices)!=1:break
            edge=choices[0];remaining.remove(edge);current=edge.other_vert(current)
        if current==loop[0] and len(loop)>=3:
            center=bm.verts.new(sum((v.co for v in loop),Vector())/len(loop))
            deform=bm.verts.layers.deform.verify()
            sums={}
            for v in loop:
                for group,weight in v[deform].items():sums[group]=sums.get(group,0.)+weight/len(loop)
            for group,weight in sums.items():center[deform][group]=weight
            for i,v in enumerate(loop):
                try:created.append(bm.faces.new((v,loop[(i+1)%len(loop)],center)))
                except ValueError:pass
    uv=bm.loops.layers.uv.active
    if uv:
        for face in created:
            for loop in face.loops:loop[uv].uv=dark_uv
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    boundary_after=sum(e.is_boundary for e in bm.edges)
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    for p in obj.data.polygons:p.use_smooth=True
    return {'weldedCoincidentUvVertices':welded,'openEdgesBeforeCap':len(boundary),'capFaces':len(created),'openEdgesAfterCap':boundary_after}

def _joint(name,center,radii,rig,material,dark_uv,light_uv,collection):
    # A closed, overlapping ball-joint sleeve. It follows Body; the rigid head turns around it.
    verts=[];faces=[];sides=32;rings=12
    for j in range(rings+1):
        v=-math.pi/2+math.pi*j/rings
        for i in range(sides):
            u=math.tau*i/sides
            verts.append((center.x+radii.x*math.cos(v)*math.cos(u),center.y+radii.y*math.cos(v)*math.sin(u),center.z+radii.z*math.sin(v)))
    for j in range(rings):
        for i in range(sides):
            a=j*sides+i;b=j*sides+(i+1)%sides;c=(j+1)*sides+(i+1)%sides;d=(j+1)*sides+i
            faces.append((a,b,c,d))
    base_faces=len(faces)
    # Three soft metal ribs make the closed joint visibly mechanical when the head tilts.
    for zoff in [-.027,0.,.027]:
        factor=math.sqrt(max(0,1-(zoff/radii.z)**2));first=len(verts);segments=24;tube_sides=4;tube=.006
        for i in range(segments):
            a=math.tau*i/segments
            for j in range(tube_sides):
                b=math.tau*j/tube_sides;offset=tube*math.cos(b)
                verts.append((center.x+(radii.x*factor+offset)*math.cos(a),center.y+(radii.y*factor+offset)*math.sin(a),center.z+zoff+tube*math.sin(b)))
        for i in range(segments):
            for j in range(tube_sides):
                faces.append((first+i*tube_sides+j,first+((i+1)%segments)*tube_sides+j,first+((i+1)%segments)*tube_sides+(j+1)%tube_sides,first+i*tube_sides+(j+1)%tube_sides))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);obj.data.materials.append(material)
    uv=obj.data.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        for loop in face.loop_indices:uv.data[loop].uv=dark_uv if face.index<base_faces else light_uv
    _clean_and_cap(obj,dark_uv);_rigid(obj,'Body');obj.parent=rig
    mod=obj.modifiers.new('MechanicalSkin','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=True
    obj['repair_role']='Closed overlapping mechanical neck joint; no head/body seam welding.'
    return obj

def build_rest(project_root=None):
    root=Path(project_root) if project_root else ROOT
    original=root/'Source/Enemies/Udarnik/Original.glb'
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    bpy.ops.import_scene.gltf(filepath=str(original))
    raw=sorted([o for o in bpy.data.objects if o.type=='MESH'],key=lambda o:int(o.name.rsplit('_',1)[1]))
    height=max((o.matrix_world@v.co).z for o in raw for v in o.data.vertices);scale=1.3/height
    for o in raw:
        matrix=o.matrix_world.copy()
        for v in o.data.vertices:v.co=(matrix@v.co)*scale
        o.matrix_world=Matrix.Identity(4)
    atlas_path=root/'Assets/IntercomMVP/Content/Enemies/Udarnik/Textures/UdarnikAtlas.png';atlas_path.parent.mkdir(parents=True,exist_ok=True)
    cell=512;pad=4;size=2048;pixels=np.ones((size,size,4),dtype=np.float32)
    for index,o in enumerate(raw):
        image=next(n.image for n in o.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
        image.scale(cell-2*pad,cell-2*pad);a=np.empty((cell-2*pad)**2*4,np.float32);image.pixels.foreach_get(a)
        a=np.pad(a.reshape(cell-2*pad,cell-2*pad,4),((pad,pad),(pad,pad),(0,0)),mode='edge');x,y=index%4,index//4;pixels[y*cell:(y+1)*cell,x*cell:(x+1)*cell]=a
        for loop in o.data.uv_layers.active.data:
            uv=loop.uv.copy();loop.uv=((x*cell+pad+uv.x*(cell-2*pad))/size,(y*cell+pad+uv.y*(cell-2*pad))/size)
        o['source_part']=int(o.name.rsplit('_',1)[1])
    atlas=bpy.data.images.new('UdarnikAtlas',width=size,height=size,alpha=False);atlas.colorspace_settings.name='sRGB';atlas.pixels.foreach_set(pixels.ravel());atlas.filepath_raw=str(atlas_path);atlas.file_format='PNG';atlas.save();atlas.pack()
    # Reuse a dark grey texel from the existing painted glove stripe. No extra material or repaint.
    distance=np.sum((pixels[:,:,:3]-np.array([.17,.18,.18],dtype=np.float32))**2,axis=2)
    iy,ix=np.unravel_index(np.argmin(distance),distance.shape);dark_uv=((ix+.5)/size,(iy+.5)/size)
    light_distance=np.sum((pixels[:,:,:3]-np.array([.32,.34,.35],dtype=np.float32))**2,axis=2);ly,lx=np.unravel_index(np.argmin(light_distance),light_distance.shape);light_uv=((lx+.5)/size,(ly+.5)/size)
    mat=bpy.data.materials.new('UdarnikAtlas');mat.use_nodes=True;shader=mat.node_tree.nodes['Principled BSDF'];shader.inputs['Roughness'].default_value=.82;shader.inputs['Metallic'].default_value=0
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas;mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    arm=bpy.data.armatures.new('UdarnikRig');rig=bpy.data.objects.new('UdarnikRig',arm);bpy.context.scene.collection.objects.link(rig)
    for o in bpy.context.selected_objects:o.select_set(False)
    rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    bones={}
    def bone(name,head,tail,parent=None):
        b=arm.edit_bones.new(name);b.head=Vector(head)*scale;b.tail=Vector(tail)*scale
        if parent:b.parent=bones[parent]
        bones[name]=b
    neck_z=.870
    bone('Root',(0,0,0),(0,0,.10));bone('Pelvis',(0,0,.385),(0,0,.47),'Root');bone('Body',(0,0,.385),(0,0,.77),'Pelvis')
    bone('Head',(0,.035,neck_z/scale),(0,.035,.95),'Body')
    for side,sign in [('L',1),('R',-1)]:
        bone('UpperArm.'+side,(sign*.201,-.015,.67),(sign*.330,-.020,.480),'Body')
        bone('Forearm.'+side,(sign*.330,-.020,.480),(sign*.414,-.070,.300),'UpperArm.'+side)
        bone('Thigh.'+side,(sign*.120,.015,.385),(sign*.153,-.005,.255),'Pelvis')
        bone('Shin.'+side,(sign*.153,-.005,.255),(sign*.176,.030,.158),'Thigh.'+side)
        bone('Foot.'+side,(sign*.176,.030,.158),(sign*.176,-.115,.105),'Shin.'+side)
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
    collection=bpy.data.collections.new('01 Editable Robot Parts');bpy.context.scene.collection.children.link(collection)
    grouped={}
    for o in raw:
        part=o['source_part'];name=SEMANTIC[part]
        # Remove the imported parent while preserving the already baked world geometry.
        o.parent=None;o.matrix_world=Matrix.Identity(4)
        for oldcol in list(o.users_collection):oldcol.objects.unlink(o)
        collection.objects.link(o)
        o.data.materials.clear();o.data.materials.append(mat)
        g=o.vertex_groups.new(name='Part.'+str(part));g.add(list(range(len(o.data.vertices))),1,'REPLACE')
        if part in BONE:_rigid(o,BONE[part])
        else:
            side='L' if part==9 else 'R';upper=o.vertex_groups.new(name='Thigh.'+side);lower=o.vertex_groups.new(name='Shin.'+side)
            for v in o.data.vertices:
                w=max(0,min(1,(v.co.z/scale-.242)/.027));w=w*w*(3-2*w)
                if w:upper.add([v.index],w,'REPLACE')
                if w<1:lower.add([v.index],1-w,'REPLACE')
        grouped.setdefault(name,[]).append(o)
    parts=[];stats={}
    for name,objects in grouped.items():
        for o in bpy.context.selected_objects:o.select_set(False)
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1:bpy.ops.object.join()
        obj=bpy.context.view_layer.objects.active;obj.name=name;obj.data.name=name
        if name=='BodyShell':
            head=obj.copy();head.data=obj.data.copy();collection.objects.link(head);head.name='HeadCore';head.data.name='HeadCore'
            stats['HeadCore']=_clean_and_cap(head,dark_uv,neck_z,True);_rigid(head,'Head');parts.append(head)
            stats[name]=_clean_and_cap(obj,dark_uv,neck_z,False);_rigid(obj,'Body')
        else:stats[name]=_clean_and_cap(obj,dark_uv)
        parts.append(obj)
    for obj in parts:
        obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4)
        mod=obj.modifiers.new('MechanicalSkin','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=True
    # Find actual cut-ring footprint; the ball overlaps both capped shells.
    body=next(o for o in parts if o.name=='BodyShell');cut=[v.co for v in body.data.vertices if abs(v.co.z-neck_z)<.00001]
    low=Vector(tuple(min(v[i] for v in cut) for i in range(3)));high=Vector(tuple(max(v[i] for v in cut) for i in range(3)))
    center=(low+high)*.5;center.z=neck_z;radius=(high-low)*.5
    joint=_joint('NeckJoint',center,Vector((radius.x*.985,radius.y*.985,.105)),rig,mat,dark_uv,light_uv,collection);parts.append(joint)
    for o in list(bpy.data.objects):
        if o.type=='EMPTY':bpy.data.objects.remove(o,do_unlink=True)
    for p in rig.pose.bones:p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
    rig['repair']='V19: face+helmet share rigid Head; torso split at capped mechanical neck. Editable parts remain separate.'
    breakup={
      'Helmet':['Helmet'],'Body':['BodyShell','HeadCore','NeckJoint'],
      'GloveL':['UpperArm.L','Glove.L'],'GloveR':['UpperArm.R','Glove.R'],
      'BootL':['Leg.L','Boot.L'],'BootR':['Leg.R','Boot.R']}
    report={'source':'Source/Enemies/Udarnik/Original.glb','heightMeters':1.3,'neckPlane':neck_z,'headPivot':list(arm.bones['Head'].head_local),'parts':[o.name for o in parts],
      'boneNames':list(arm.bones.keys()),'atlas':'Assets/IntercomMVP/Content/Enemies/Udarnik/Textures/UdarnikAtlas.png',
      'repair':'Original face was Body while helmet was Head. Upper face is now a rigid HeadCore, both neck cut surfaces are capped and an overlapping mechanical joint closes the pivot.',
      'weldingPolicy':'Coincident UV-split vertices within each original component only; separate articulated components are never welded together.',
      'partTopology':stats,'breakupGroups':breakup,'darkAtlasUv':dark_uv,'neckFootprintMin':list(low),'neckFootprintMax':list(high),
      'triangles':sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),'vertices':sum(len(o.data.vertices) for o in parts)}
    bpy.context.scene.render.fps=100;bpy.context.scene.render.fps_base=1
    bpy.context.preferences.filepaths.save_version=0
    for o in bpy.context.selected_objects:o.select_set(False)
    rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.data.orphans_purge(do_recursive=True)
    bpy.context.view_layer.update()
    return {'rig':rig,'parts':parts,'atlas':atlas,'material':mat,'report':report,'breakup':breakup}

if __name__=='__main__':
    import argparse,sys
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',required=True,type=Path,help='Explicit intermediate .blend destination; normal builds use PrepareUdarnik.py.')
    target=parser.parse_args(args).output.resolve();target.parent.mkdir(parents=True,exist_ok=True)
    result=build_rest()
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    target.with_suffix('.json').write_text(json.dumps(result['report'],indent=2)+'\n')
    print(json.dumps(result['report'],indent=2),flush=True)
