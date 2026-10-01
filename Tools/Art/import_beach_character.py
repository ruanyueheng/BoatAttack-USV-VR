"""Convert the user's CC0 Poly Pizza model to a Unity-importable FBX, preserving its rig/actions."""
from pathlib import Path
import bpy
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'ArtSource/Characters/BeachCharacter.glb'))
# Export only peaceful locomotion clips. No external Blender dependency at game runtime.
keep={'Idle','Walk','Run'}
for obj in bpy.data.objects:
    if obj.animation_data:
        for track in list(obj.animation_data.nla_tracks):
            obj.animation_data.nla_tracks.remove(track)
        obj.animation_data.action=None
for action in list(bpy.data.actions):
    if action.name.split('|')[-1] not in keep:
        bpy.data.actions.remove(action)
    else:
        action.name=action.name.split('|')[-1]
        action.use_fake_user=True
# Convert the source's centimetre rig to unit-scale metres *before* FBX export.
# Otherwise its 100x parent scale and inverse bind matrices produce a tiny skin
# despite apparently person-sized renderer bounds in Unity.
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
unit_scale=rig.scale.x
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
for obj in bpy.context.scene.objects:
    if obj.type=='MESH' and obj.name.startswith('Beach_'):
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
for action in bpy.data.actions:
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    if curve.data_path.startswith('pose.bones[') and curve.data_path.endswith('.location'):
                        for key in curve.keyframe_points:
                            key.co.y*=unit_scale;key.handle_left.y*=unit_scale;key.handle_right.y*=unit_scale
bpy.context.view_layer.update()
# 显式导出踝部锚点，避免 FBX 自动叶骨在单位换算后缩短 100 倍。
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for side in ('L','R'):
    shin=rig.data.edit_bones['LowerLeg.'+side]
    # glTF 导入器把末端骨的显示长度设为极小值；此角色上下腿等长。
    shin.tail=shin.head+(shin.tail-shin.head).normalized()*shin.parent.length
    ankle=rig.data.edit_bones.new('Ankle.'+side)
    ankle.head=shin.tail;ankle.tail=shin.tail+(shin.tail-shin.head).normalized()*.02
    ankle.parent=shin;ankle.use_connect=True;ankle.use_deform=False
bpy.ops.object.mode_set(mode='OBJECT')
# Export the actual avatar hierarchy only. The glTF importer creates hidden bone-widget
# meshes (e.g. Icosphere); exporting all objects would contaminate Unity's bounds.
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    if obj.type=='ARMATURE' or (obj.type=='MESH' and obj.name.startswith('Beach_')) or obj.name=='RootNode':
        obj.select_set(True)
out=ROOT/'Assets/Resources/FishingCharacter';out.mkdir(parents=True,exist_ok=True)
# 保留小腿末端标记，Unity 的双骨 IK 需要真实踝部位置而非独立 Foot 控制骨。
bpy.ops.export_scene.fbx(filepath=str(out/'BeachCharacter.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},apply_scale_options='FBX_SCALE_ALL',
    add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,
    bake_anim_simplify_factor=0.0,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
print('CHARACTER_EXPORT',[(a.name,tuple(a.frame_range)) for a in bpy.data.actions])
