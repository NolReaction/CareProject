"""Build mechanical actions and exports while keeping editable source parts separate.

Run inside Blender5.2+, or use PrepareUdarnik.py for the complete Original.glb pipeline.
For a geometry review: blender -b --python BlenderTools/AnimateUdarnik.py -- --rest path.blend
"""
import argparse,json,math
from pathlib import Path
import bpy
from mathutils import Vector,Matrix,Quaternion

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Source/Enemies/Udarnik'
MODEL=ROOT/'Assets/IntercomMVP/Content/Enemies/Udarnik/Model'
FPS=100
CLIPS={'Idle':1.60,'Walk':.80,'Windup':.30,'Strike':.16,'Recover':.48,'Hurt':.22,
       'Death':.18,'Emerge':.68,'EmergeGround':.95,'EmergeHatch':1.05}
WALK_DUTY=.58
WALK_STANCE_TRAVEL=.28
WALK_REFERENCE=WALK_STANCE_TRAVEL/(WALK_DUTY*CLIPS['Walk'])
FLOOR=.003
rig=None
parts=[]
REST={}
DIRECTIONS={}
FOOT_VERTICES={}
ik_clamps=0
arm_reach_clamps=0

def smooth(t):
    t=max(0.,min(1.,t));return t*t*(3.-2.*t)
def mix(a,b,t):return a+(b-a)*t
def window(t,a,b):return smooth((t-a)/(b-a))

def source_parts():
    collection=bpy.data.collections.get('01 Editable Robot Parts') or bpy.data.collections.get('EditableParts')
    values=list(collection.all_objects) if collection else list(bpy.context.scene.objects)
    return [o for o in values if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]

def setup():
    global rig,parts,REST,DIRECTIONS,FOOT_VERTICES
    rig=bpy.data.objects.get('UdarnikRig')
    if rig is None:raise RuntimeError('Expected UdarnikRig from the geometry repair stage')
    parts=source_parts()
    if not parts:raise RuntimeError('Expected editable mechanically weighted meshes')
    REST={name:b.matrix_local.copy() for name,b in rig.data.bones.items()}
    DIRECTIONS={name:(b.tail_local-b.head_local).copy() for name,b in rig.data.bones.items()}
    for p in rig.pose.bones:p.rotation_mode='QUATERNION'
    FOOT_VERTICES={}
    inverse=rig.matrix_world.inverted()
    for side,partids in [('L',[4,11,13]),('R',[5,10])]:
        coords=[]
        for obj in parts:
            indexes={g.index for g in obj.vertex_groups if g.name in ['Part.'+str(i) for i in partids]}
            by_name=obj.name in {'Boot.'+side,'Foot.'+side}
            matrix=inverse@obj.matrix_world
            for v in obj.data.vertices:
                if by_name or any(g.group in indexes and g.weight>.1 for g in v.groups):coords.append(matrix@v.co)
        if not coords:raise RuntimeError('No sole vertices for '+side)
        FOOT_VERTICES[side]=coords

def rotate(name,x=0.,y=0.,z=0.):
    q=Quaternion((0,0,1),math.radians(z))@Quaternion((0,1,0),math.radians(y))@Quaternion((1,0,0),math.radians(x))
    rest=REST[name].to_quaternion()
    rig.pose.bones[name].rotation_quaternion=rest.inverted()@q@rest

def segment(name,head,tail):
    q=DIRECTIONS[name].rotation_difference(tail-head)
    m=q.to_matrix().to_4x4()@REST[name].to_quaternion().to_matrix().to_4x4()
    m.translation=head;rig.pose.bones[name].matrix=m

def leg(side,target,pelvis,pitch=0.):
    global ik_clamps
    hip=rig.data.bones['Thigh.'+side].head_local+pelvis
    rest_ankle=rig.data.bones['Foot.'+side].head_local
    ankle=rest_ankle+target
    q=Quaternion((1,0,0),math.radians(pitch))
    minimum=min((q@(v-rest_ankle)).z+ankle.z for v in FOOT_VERTICES[side])
    ankle.z+=max(0.,FLOOR-minimum)
    a=DIRECTIONS['Thigh.'+side].length;b=DIRECTIONS['Shin.'+side].length
    v=ankle-hip;distance=v.length
    if distance>a+b-.00002:
        ik_clamps+=1;distance=a+b-.00002
    distance=max(abs(a-b)+.00002,distance)
    direction=v.normalized();ankle=hip+direction*distance
    along=(a*a-b*b+distance*distance)/(2*distance)
    altitude=max(0.,a*a-along*along)**.5
    pole=Vector((0,-1,0));bend=pole-direction*direction.dot(pole)
    if bend.length<.0001:bend=Vector((1,0,0))
    knee=hip+direction*along+bend.normalized()*altitude
    segment('Thigh.'+side,hip,knee);bpy.context.view_layer.update()
    segment('Shin.'+side,knee,ankle);bpy.context.view_layer.update()
    m=q.to_matrix().to_4x4()@REST['Foot.'+side].to_quaternion().to_matrix().to_4x4()
    m.translation=ankle;rig.pose.bones['Foot.'+side].matrix=m

def planted_hand(side,target,weight):
    """A planted fist pushes the body up; only rigid joint rotations change."""
    global arm_reach_clamps
    upper=rig.pose.bones['UpperArm.'+side];fore=rig.pose.bones['Forearm.'+side]
    base_upper=upper.rotation_quaternion.copy();base_fore=fore.rotation_quaternion.copy()
    base_upper_location=upper.location.copy();base_fore_location=fore.location.copy()
    shoulder=upper.head.copy();a=DIRECTIONS[upper.name].length;b=DIRECTIONS[fore.name].length
    delta=target-shoulder;distance=delta.length
    if distance>a+b-.00005:arm_reach_clamps+=1;distance=a+b-.00005
    distance=max(abs(a-b)+.00005,distance);direction=delta.normalized();target=shoulder+direction*distance
    along=(a*a-b*b+distance*distance)/(2*distance);height=max(0.,a*a-along*along)**.5
    pole=Vector((.8 if side=='L' else -.8,-1,0));bend=pole-direction*direction.dot(pole)
    if bend.length<.0001:bend=Vector((1,0,0))
    elbow=shoulder+direction*along+bend.normalized()*height
    segment(upper.name,shoulder,elbow);bpy.context.view_layer.update();segment(fore.name,elbow,target);bpy.context.view_layer.update()
    upper.rotation_quaternion=base_upper.slerp(upper.rotation_quaternion,weight)
    fore.rotation_quaternion=base_fore.slerp(fore.rotation_quaternion,weight)
    upper.location=base_upper_location.lerp(upper.location,weight);fore.location=base_fore_location.lerp(fore.location,weight)

def gait(phase):
    phase=phase%1.
    if phase<WALK_DUTY:
        s=phase/WALK_DUTY
        y=mix(-WALK_STANCE_TRAVEL*.5,WALK_STANCE_TRAVEL*.5,s)
        pitch=-4*(1-smooth(s/.12))+8*smooth((s-.85)/.15)
        return Vector((0,y,0)),pitch,True
    s=(phase-WALK_DUTY)/(1-WALK_DUTY)
    # Match the planted backward sole velocity at both ends. The airborne
    # foot changes direction between them rather than snapping at touchdown.
    y0=WALK_STANCE_TRAVEL*.5;y1=-y0
    tangent=WALK_REFERENCE*CLIPS['Walk']*(1-WALK_DUTY)
    y=(2*s*s*s-3*s*s+1)*y0+(s*s*s-2*s*s+s)*tangent+(-2*s*s*s+3*s*s)*y1+(s*s*s-s*s)*tangent
    lift=math.sin(math.pi*s)**2*.072
    pitch=mix(7,-4,smooth(s))
    return Vector((0,y,lift)),pitch,False

def pose(kind,t):
    for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
    pelvis=Vector((0,0,0));body=(0.,0.,0.);head=(0.,0.,0.)
    arms={'L':[-27.,-48.,0.],'R':[-30.,-52.,0.]}
    ankles={'L':Vector((0,0,0)),'R':Vector((0,0,0))};pitches={'L':0.,'R':0.}
    if kind=='Idle':
        wave=math.sin(t*math.tau);other=math.sin(t*math.tau+.5)
        body=(wave*1.7,other*.55,wave*1.9);head=(-wave*.8,0,-wave*1.2)
        arms['L'][0]+=wave*2.4;arms['L'][1]-=other*2
        arms['R'][0]-=wave*2.;arms['R'][1]+=other*1.8
    elif kind=='Walk':
        phase=t*math.tau
        pelvis=Vector((math.sin(phase)*.017,0,-.055+.006*math.cos(phase*2)))
        body=(9.+1.4*math.sin(phase*2),math.sin(phase)*1.6,math.sin(phase)*4.2)
        head=(-math.sin(phase*2)*1.3,0,-body[2]*.70)
        for side,offset,sign in [('L',0,1),('R',.5,-1)]:
            ankles[side],pitches[side],_=gait(t+offset)
            c=math.cos(phase)*sign
            arms[side]=[-18+c*25,-34-c*9,sign*(4+c*2)]
    elif kind in {'Windup','Strike','Recover'}:
        if kind=='Windup':
            coil=window(t,0,.60);punch=window(t,.74,1.)
            body=(mix(-7*coil,18,punch),0,mix(-15*coil,13,punch))
            head=(mix(2*coil,-3.5,punch),0,mix(5*coil,-6,punch))
            pelvis=Vector((0,-.025*punch,-.028*coil*(1-punch)))
            arms['L']=[mix(mix(-27,17,coil),-96,punch),mix(mix(-48,-98,coil),-3,punch),mix(9*coil,-6,punch)]
            arms['R']=[mix(-30,-39,coil),mix(-52,-64,coil),-5*coil]
        else:
            release=0. if kind=='Strike' else smooth(t)
            pulse=math.sin(math.pi*t) if kind=='Strike' else 0.
            body=(mix(18,0,release)+pulse*2,0,mix(13,0,release))
            head=(mix(-3.5,0,release),0,mix(-6,0,release))
            pelvis=Vector((0,mix(-.025,0,release),0))
            arms['L']=[mix(-96,-27,release)-pulse*3,mix(-3,-48,release),mix(-6,0,release)]
            arms['R']=[mix(-39,-30,release),mix(-64,-52,release),mix(-5,0,release)]
    elif kind=='Hurt':
        q=math.sin(math.pi*t)*(1-.35*t)
        body=(-15*q,-3*q,7*q);head=(4*q,0,-7*q)
        arms={'L':[-27+19*q,-48-22*q,4*q],'R':[-30+14*q,-52-17*q,-5*q]}
        pelvis=Vector((0,.012*q,-.012*q))
    elif kind in {'Emerge','EmergeGround'}:
        pull=window(t,.22,.68);heave=math.sin(math.pi*max(0.,min(1.,(t-.66)/.22))) if .66<t<.88 else 0.
        settle=window(t,.84,1.)
        body=(mix(28,0,pull)-heave*5,0,math.sin(t*math.pi)*6*(1-settle))
        head=(mix(-3.5,0,pull),0,-body[2]*.5)
        pelvis=Vector((0,-.022*(1-settle),mix(-.074,0,pull)+heave*.010))
        for side,sign in [('L',1),('R',-1)]:
            dig=math.sin(math.tau*min(t/.27,1.))*max(0.,1-t/.32)
            arms[side]=[mix(-13-9*dig*sign,-27 if side=='L' else -30,pull)-heave*20,
                        mix(-16-10*dig*sign,-48 if side=='L' else -52,pull)-heave*8,sign*7*(1-settle)]
            lift=(1-pull)*(.035 if side=='L' else .055)
            step=math.sin(math.pi*max(0.,min(1.,(t-(.52 if side=='L' else .66))/.22))) if (.52 if side=='L' else .66)<t<(.74 if side=='L' else .88) else 0.
            ankles[side]=Vector((0,-sign*.018*(1-settle),lift+step*.040));pitches[side]=step*-5
    elif kind=='EmergeHatch':
        unfold=window(t,.64,.92);climb=window(t,.28,.76);settle=window(t,.88,1.)
        body=(mix(23,0,unfold),0,mix(-11,0,unfold))
        head=(mix(3.5,0,unfold),0,mix(6,0,unfold))
        pelvis=Vector((0,.012*(1-unfold),mix(-.065,0,unfold)))
        for side,sign in [('L',1),('R',-1)]:
            arms[side]=[mix(-40,-27 if side=='L' else -30,unfold),mix(-103,-48 if side=='L' else -52,unfold),sign*mix(26,0,unfold)]
            step=math.sin(math.pi*max(0.,min(1.,(t-(.38 if side=='L' else .57))/.31))) if (.38 if side=='L' else .57)<t<(.69 if side=='L' else .88) else 0.
            ankles[side]=Vector((0,-.025*(1-settle)*sign,step*.085+(1-climb)*(.045 if side=='L' else .025)))
            pitches[side]=-5*step
    elif kind=='Death':
        compress=math.sin(math.pi*min(t/.35,1.)) if t<.35 else 0.
        pop=window(t,.28,.78)
        body=(-11*pop,0,8*pop);head=(3.5*pop,0,-6*pop)
        pelvis=Vector((0,.015*pop,-.020*compress))
        arms={'L':[mix(-27,13,pop),mix(-48,-12,pop),12*pop],
              'R':[mix(-30,17,pop),mix(-52,-16,pop),-12*pop]}
    else:raise ValueError(kind)
    rig.pose.bones['Pelvis'].location=REST['Pelvis'].to_quaternion().inverted()@pelvis
    bpy.context.view_layer.update()
    for side in ['L','R']:leg(side,ankles[side],pelvis,pitches[side])
    rotate('Body',*body);rotate('Head',*head)
    for side,sign in [('L',1),('R',-1)]:
        upper,forearm,tuck=arms[side]
        rotate('UpperArm.'+side,upper,tuck,sign*2.)
        rotate('Forearm.'+side,forearm,0,sign*1.)
    bpy.context.view_layer.update()
    if kind in {'Emerge','EmergeGround'} and t<.86:
        # Match the runtime's monotonic wrapper rise. Hands are the first
        # parts to break the ground plane, then hold a low ledge while the
        # body pushes upward. Both feet remain inside the authored rig.
        lift=window(t,.12,.86);wrapper_y=-1.48*(1-lift)
        weight=1-window(t,.57,.86)
        for side,sign in [('L',1),('R',-1)]:
            planted_hand(side,Vector((sign*.47,-.31,min(1.48,-wrapper_y+.13))),weight)
        bpy.context.view_layer.update()

def build_actions():
    global ik_clamps,arm_reach_clamps
    setup();ik_clamps=0;arm_reach_clamps=0
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    scene=bpy.context.scene;scene.render.fps=FPS;scene.render.fps_base=1.
    rig.animation_data_create()
    for name,duration in CLIPS.items():
        action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
        end=round(duration*FPS)
        frames=list(range(end+1))
        for frame in frames:
            pose(name,frame/end)
            for bone in rig.pose.bones:
                bone.keyframe_insert('location',frame=frame,group=bone.name)
                bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
        for layer in action.layers:
            for strip in layer.strips:
                if strip.type=='KEYFRAME':
                    for bag in strip.channelbags:
                        for curve in bag.fcurves:
                            for point in curve.keyframe_points:point.interpolation='LINEAR'
        action['duration_seconds']=duration
        action['purpose']='Compatibility ground arrival' if name=='Emerge' else name
    rig.animation_data.action=bpy.data.actions['Idle']
    if rig.animation_data.action.slots:rig.animation_data.action_slot=rig.animation_data.action.slots[0]
    scene.frame_start=0;scene.frame_end=160;scene.frame_set(0)
    return {'clips':CLIPS,'fps':FPS,'walkStanceTravel':WALK_STANCE_TRAVEL,'walkStanceDuty':WALK_DUTY,
            'walkReferenceMetresPerSecond':WALK_REFERENCE,'ikClampedTargetsDuringAuthoring':ik_clamps,
            'earlyEmergenceArmReachClamps':arm_reach_clamps}

def frame_workspace():
    bpy.context.view_layer.objects.active=rig
    for obj in bpy.context.selected_objects:obj.select_set(False)
    rig.select_set(True)
    if bpy.context.object.mode!='POSE':bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    rig.show_in_front=True;rig.data.display_type='STICK'
    rig['editing_help']='Select UdarnikRig in Pose Mode. Actions menu: Idle, Walk, attacks and ground/hatch emergence. 01 Editable Robot Parts contains separate metal meshes.'
    center=Vector((0,0,.67));rotation=(Vector((1.8,-3.7,1.9))-center).to_track_quat('Z','Y')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_floor=True
                space.region_3d.view_location=center;space.region_3d.view_distance=2.15;space.region_3d.view_rotation=rotation
                space.clip_start=.01;space.clip_end=200.
            elif area.type=='DOPESHEET_EDITOR':
                space=area.spaces.active;space.mode='ACTION';space.dopesheet.show_only_selected=False
                space.show_region_ui=False
                region=next((r for r in area.regions if r.type=='WINDOW'),None)
                if region and screen==bpy.context.screen:
                    try:
                        with bpy.context.temp_override(area=area,region=region):bpy.ops.action.view_all()
                    except (RuntimeError,TypeError):pass

def export_game(path):
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    scene=bpy.context.scene;collection=bpy.data.collections.new('_TemporaryGameExport');scene.collection.children.link(collection)
    copies=[]
    for src in parts:
        obj=src.copy();obj.data=src.data.copy();collection.objects.link(obj);obj.hide_set(False);obj.hide_render=False;copies.append(obj)
    for obj in bpy.context.selected_objects:obj.select_set(False)
    for obj in copies:obj.select_set(True)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join()
    game=bpy.context.object;game.name='UdarnikBody';game.data.name='UdarnikBody'
    for obj in bpy.context.selected_objects:obj.select_set(False)
    rig.select_set(True);game.select_set(True);bpy.context.view_layer.objects.active=rig
    path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,use_armature_deform_only=True,
        bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,
        bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='RELATIVE',embed_textures=False)
    mesh=game.data;bpy.data.objects.remove(game,do_unlink=True)
    if mesh.users==0:bpy.data.meshes.remove(mesh)
    bpy.data.collections.remove(collection)

def export_breakup(path,groups):
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    action=rig.animation_data.action;rig.animation_data.action=None
    for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
    collection=bpy.data.collections.new('_TemporaryBreakupExport');bpy.context.scene.collection.children.link(collection)
    chunks=[];records=[];lookup={o.name:o for o in parts}
    for name,names in groups.items():
        copies=[]
        for source_name in names:
            src=lookup[source_name]
            data=bpy.data.meshes.new_from_object(src.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)
            obj=bpy.data.objects.new('_ChunkSource',data);collection.objects.link(obj);obj.matrix_world=src.matrix_world.copy();copies.append(obj)
        for obj in bpy.context.selected_objects:obj.select_set(False)
        for obj in copies:obj.select_set(True)
        bpy.context.view_layer.objects.active=copies[0]
        if len(copies)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name='Breakup_'+name;obj.data.name=obj.name
        coords=[obj.matrix_world@v.co for v in obj.data.vertices]
        center=Vector(tuple((min(v[i] for v in coords)+max(v[i] for v in coords))*.5 for i in range(3)))
        matrix=obj.matrix_world.copy()
        for vertex in obj.data.vertices:vertex.co=matrix@vertex.co-center
        obj.matrix_world=Matrix.Translation(center)
        for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
        chunks.append(obj);records.append({'name':obj.name,'parts':names,'pivotBlender':list(center),
            'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons)})
    for obj in bpy.context.selected_objects:obj.select_set(False)
    for obj in chunks:obj.select_set(True)
    bpy.context.view_layer.objects.active=chunks[0]
    path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,path_mode='RELATIVE',embed_textures=False)
    for obj in chunks:
        mesh=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
        if mesh.users==0:bpy.data.meshes.remove(mesh)
    bpy.data.collections.remove(collection)
    rig.animation_data.action=action
    if action and action.slots:rig.animation_data.action_slot=action.slots[0]
    bpy.context.scene.frame_set(0)
    return records

def save_and_export(report,review_dir,breakup=None):
    SOURCE.mkdir(parents=True,exist_ok=True);MODEL.mkdir(parents=True,exist_ok=True)
    for obj in list(bpy.data.objects):
        if obj.type in {'CAMERA','LIGHT'} and obj.name.startswith(('Review','Key','Fill','Rim')):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.context.preferences.filepaths.save_version=0
    chunks=export_breakup(MODEL/'UdarnikBreakup.fbx',breakup) if breakup else []
    # Save the actual editable source before creating the temporary one-mesh game export.
    bpy.data.orphans_purge(do_recursive=True)
    frame_workspace();bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Udarnik_Rigged.blend'))
    export_game(MODEL/'Udarnik.fbx')
    frame_workspace()
    report.update({'status':'PREPARED','editableMeshObjects':[o.name for o in parts],'gameMeshObjects':1,
        'sourceRig':'UdarnikRig','sourcePackedImages':[im.name for im in bpy.data.images if im.packed_file],
        'rootMotion':False,'boneScaleCurves':False,'actions':list(CLIPS),'breakupChunks':chunks})
    review_dir.mkdir(parents=True,exist_ok=True);(review_dir/'AnimationBuild.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2),flush=True)

def main():
    import sys
    parser=argparse.ArgumentParser();parser.add_argument('--rest',type=Path);parser.add_argument('--review-dir',type=Path,default=ROOT/'Validation/Results/UdarnikArt');parser.add_argument('--probe',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    geometry=None
    if args.rest:
        bpy.ops.wm.open_mainfile(filepath=str(args.rest.resolve()))
        candidate=args.rest.parent/'RepairGeometry.json'
        if candidate.exists():geometry=json.loads(candidate.read_text())
    else:
        import importlib.util
        path=Path(__file__).with_name('RepairUdarnik.py');spec=importlib.util.spec_from_file_location('udarnik_geometry',path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);geometry=module.build_rest()['report']
    report=build_actions()
    if args.probe:
        args.review_dir.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(args.review_dir/'AnimationProbe.blend'))
        (args.review_dir/'AnimationBuild.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report),flush=True)
    else:save_and_export(report,args.review_dir,geometry.get('breakupGroups') if geometry else None)
if __name__=='__main__':main()
