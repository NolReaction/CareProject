# Run this text in Blender Scripting. Export runs in a temporary background process.
import bpy,subprocess,tempfile,os
from pathlib import Path
WORKER = '"""Transient-only FBX conversion, reproducing the previous exported hierarchy."""\nimport bpy,os,json,sys,tempfile\nfrom mathutils import Matrix,Vector\nsource_path,target_path=sys.argv[sys.argv.index(\'--\')+1:]\nART=os.path.dirname(target_path)\nbpy.ops.wm.open_mainfile(filepath=source_path)\ns=bpy.context.scene;rig=bpy.data.objects[\'Intercom_Armature_Fixed\']\nif bpy.context.object and bpy.context.object.mode!=\'OBJECT\':bpy.ops.object.mode_set(mode=\'OBJECT\')\nrig.animation_data.action=bpy.data.actions[\'Shoot\'];s.frame_set(1);bpy.context.view_layer.update()\nmeshes=[o for o in s.objects if o.type==\'MESH\' and o.visible_get() and not o.hide_render]\nroots=[bpy.data.objects[n] for n in (\'Pistol_L\',\'Pistol_R\')]\nchildren=[o for o in s.objects if o.parent==rig and o.parent_type==\'BONE\']\nsaved={o:o.matrix_world.copy() for o in children}\nfor b in rig.data.bones:b.hide=False\nrig.data.bones[\'R_Hand\'].use_relative_parent=False;bpy.context.view_layer.update()\nfor o in children:\n o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_world=saved[o]\nbpy.context.view_layer.update()\nright_world={o:o.matrix_world.copy() for o in meshes if o.parent==rig and o.parent_type==\'BONE\' and o.parent_bone==\'R_Hand\'}\nbpy.data.objects[\'Pistol_R\'].matrix_world=rig.matrix_world@rig.pose.bones[\'R_Hand\'].matrix\nbpy.context.view_layer.update()\nfor o in meshes:\n if o.parent==rig and o.parent_type==\'BONE\' and o.parent_bone==\'R_Hand\':\n  m=right_world[o];o.parent=bpy.data.objects[\'Pistol_R\'];o.parent_type=\'OBJECT\';o.parent_bone=\'\';o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_world=m\nbpy.context.view_layer.update()\nroot=bpy.data.objects.new(\'IntercomRobot\',None);s.collection.objects.link(root)\noldworld=rig.matrix_world.copy();rig.parent=root;rig.matrix_parent_inverse=Matrix.Identity(4);rig.matrix_basis=oldworld\nroot.scale=(1.5325275659561157,)*3;root.location=(-.0014813084853813052,.03352617844939232,4.0384438904084163e-08)\nbpy.context.view_layer.update()\ntexture_dir=tempfile.mkdtemp(prefix=\'intercom_export_textures_\')\nfor image in bpy.data.images:\n if image.packed_file:\n  path=os.path.join(texture_dir,os.path.basename(image.filepath.replace(chr(92),\'/\')) or image.name)\n  with open(path,\'wb\') as stream:stream.write(bytes(image.packed_file.data))\n  image.filepath=path\nfor a in list(bpy.data.actions):\n if a.name not in (\'Idle\',\'Run\',\'IdleRelaxed\',\'RunRelaxed\',\'RaiseWeapons\',\'LowerWeapons\',\'Shoot\',\'RunBackward\',\'RunLeft\',\'RunRight\',\'RunBackwardRelaxed\',\'RunLeftRelaxed\',\'RunRightRelaxed\'):bpy.data.actions.remove(a)\nbpy.ops.object.select_all(action=\'DESELECT\')\nfor o in meshes+roots+[rig,root]:o.hide_set(False);o.hide_render=False;o.select_set(True)\nbpy.context.view_layer.objects.active=rig\ntemporary_fd,verified_path=tempfile.mkstemp(prefix=\'intercom_verified_\',suffix=\'.fbx\',dir=ART or \'.\');os.close(temporary_fd)\nbpy.ops.export_scene.fbx(filepath=verified_path,use_selection=True,object_types={\'ARMATURE\',\'MESH\',\'EMPTY\'},global_scale=1,apply_unit_scale=True,apply_scale_options=\'FBX_SCALE_UNITS\',axis_forward=\'-Z\',axis_up=\'Y\',use_mesh_modifiers=True,mesh_smooth_type=\'OFF\',use_tspace=True,add_leaf_bones=False,primary_bone_axis=\'Y\',secondary_bone_axis=\'X\',use_armature_deform_only=False,armature_nodetype=\'NULL\',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=.0625,bake_anim_simplify_factor=0,path_mode=\'COPY\',embed_textures=True,use_custom_props=False)\nfrom io_scene_fbx import parse_fbx\nparse_fbx.parse(verified_path)\nos.replace(verified_path,target_path);os.utime(target_path,None)\nprint(\'EXPORTED\',target_path,len(meshes),\'meshes\')\n'
class INTERCOM_OT_export_unity(bpy.types.Operator):
    bl_idname = "intercom.export_unity"
    bl_label = "Export 13 animation clips to Unity"
    filepath: bpy.props.StringProperty(subtype='FILE_PATH')
    filter_glob: bpy.props.StringProperty(default='*.fbx',options={'HIDDEN'})
    check_existing: bpy.props.BoolProperty(default=True,options={'HIDDEN'})
    def invoke(self,context,event):
        if not bpy.data.filepath:
            self.report({'ERROR'},'Save the .blend file first (Ctrl+S).');return {'CANCELLED'}
        self.filepath=bpy.path.abspath('//IntercomRobot.fbx')
        context.window_manager.fileselect_add(self);return {'RUNNING_MODAL'}
    def execute(self,context):
        if bpy.data.is_dirty:
            self.report({'ERROR'},'Save your changes first (Ctrl+S), then run export again.');return {'CANCELLED'}
        if not self.filepath.lower().endswith('.fbx'):self.filepath+='.fbx'
        with tempfile.TemporaryDirectory(prefix='intercom_unity_export_') as temp:
            script=Path(temp)/'export.py';script.write_text(WORKER,encoding='utf-8')
            result=subprocess.run([bpy.app.binary_path,'--background','--factory-startup','--disable-autoexec','--python',str(script),'--',bpy.data.filepath,bpy.path.abspath(self.filepath)],capture_output=True,text=True)
            if result.returncode!=0 or 'EXPORTED' not in result.stdout:
                print(result.stdout,result.stderr)
                self.report({'ERROR'},'Export failed. See Blender system console for the error.');return {'CANCELLED'}
        self.report({'INFO'},'Exported 13 animation clips. Unity will reimport the FBX.');return {'FINISHED'}
old=bpy.types.Operator.bl_rna_get_subclass_py('INTERCOM_OT_export_unity')
if old:bpy.utils.unregister_class(old)
bpy.utils.register_class(INTERCOM_OT_export_unity)
bpy.ops.intercom.export_unity('INVOKE_DEFAULT')
