"""Reuse the original CC0 character's same-rig actions without replacing the supplied 9S surface."""
from pathlib import Path
import json
import bpy

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Feedback-20261026/Animations'
ART.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'ArtSource/Characters/BeachCharacter.glb'))
keep={'Interact','Punch_Right','HitRecieve'}
for obj in bpy.data.objects:
    if obj.animation_data:
        for track in list(obj.animation_data.nla_tracks):obj.animation_data.nla_tracks.remove(track)
        obj.animation_data.action=None
for action in list(bpy.data.actions):
    if action.name.split('|')[-1] not in keep:bpy.data.actions.remove(action)
    else:action.name=action.name.split('|')[-1];action.use_fake_user=True
rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
unit_scale=rig.scale.x
for bone in rig.pose.bones:bone.matrix_basis.identity()
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
assert {a.name for a in bpy.data.actions}==keep
source_objects=set(bpy.context.scene.objects);source_rig=rig;source_rig.name='SOURCE-original-character'
source_actions=list(bpy.data.actions)
source_rest={bone.name:source_rig.matrix_world@bone.matrix_local for bone in source_rig.data.bones}
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Resources/FishingCharacter/BeachCharacter.fbx'))
rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE' and obj not in source_objects)
rig.animation_data_clear();rig.animation_data_create()
target_objects=set(bpy.context.scene.objects)-source_objects
target_rest={bone.name:rig.matrix_world@bone.matrix_local for bone in rig.data.bones}
generated=[]
for source_action in source_actions:
    name=source_action.name;source_rig.animation_data.action=source_action
    source_rig.animation_data.action_slot=source_action.slots[0]
    action=bpy.data.actions.new('BAKED-'+name);rig.animation_data.action=action
    for frame in range(int(source_action.frame_range[0]),int(source_action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        poses={name:source_rig.matrix_world@bone.matrix for name,bone in source_rig.pose.bones.items()}
        target_poses={name:rig.matrix_world.inverted()@pose@source_rest[name].inverted()@target_rest[name] for name,pose in poses.items() if name in target_rest}
        for bone in rig.pose.bones:
            if bone.name not in target_poses:continue
            if bone.parent:
                bone.matrix_basis=bone.bone.convert_local_to_pose(target_poses[bone.name],bone.bone.matrix_local,
                    parent_matrix=target_poses.get(bone.parent.name,bone.parent.matrix),parent_matrix_local=bone.parent.bone.matrix_local,invert=True)
            else:bone.matrix_basis=bone.bone.convert_local_to_pose(target_poses[bone.name],bone.bone.matrix_local,invert=True)
            bone.keyframe_insert('location',frame=frame,group=bone.name)
            bone.keyframe_insert('rotation_quaternion' if bone.rotation_mode=='QUATERNION' else 'rotation_euler',frame=frame,group=bone.name)
            bone.keyframe_insert('scale',frame=frame,group=bone.name)
    action.use_fake_user=True;generated.append((action,name))
source_rig.animation_data.action=None
for obj in source_objects:bpy.data.objects.remove(obj,do_unlink=True)
rig.animation_data.action=None
for action in list(bpy.data.actions):
    if action not in [item[0] for item in generated]:bpy.data.actions.remove(action)
for action,name in generated:action.name=name
bpy.ops.object.select_all(action='DESELECT')
for obj in target_objects:
    if obj.type in {'ARMATURE','EMPTY'}:obj.select_set(True)
out=ART/'InteractionAnimations.fbx'
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'ARMATURE','EMPTY'},apply_scale_options='FBX_SCALE_ALL',
    add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,
    bake_anim_simplify_factor=0.0,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'same-rig-interactions.blend'))
report={'source':'ArtSource/Characters/BeachCharacter.glb','targetRig':'Assets/Resources/FishingCharacter/BeachCharacter.fbx','retarget':'per-frame world pose delta on the same calibrated bone names','license':'CC0-1.0; see ArtSource/Characters/ATTRIBUTION.md',
        'actions':{a.name:list(a.frame_range) for a in bpy.data.actions},'boneCount':len(rig.data.bones),
        'output':out.relative_to(ROOT).as_posix(),'bytes':out.stat().st_size,'geometryExported':False}
(ART/'manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('FEEDBACK_INTERACTIONS',json.dumps(report))
