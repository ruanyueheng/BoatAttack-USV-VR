"""Render representative frames of the reused source actions and verify the FBX export."""
from pathlib import Path
import json
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Feedback-20261026/Animations'
bpy.ops.wm.open_mainfile(filepath=str(ART/'same-rig-interactions.blend'))
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=='ARMATURE')
meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('Beach_')]
for obj in scene.objects:
    if obj.type=='MESH':obj.hide_render=obj not in meshes
points=[]
for obj in meshes:points.extend(obj.matrix_world@Vector(corner) for corner in obj.bound_box)
lower=Vector(tuple(min(p[i] for p in points) for i in range(3)))
upper=Vector(tuple(max(p[i] for p in points) for i in range(3)))
centre=(lower+upper)/2
camera_data=bpy.data.cameras.new('CAM-feedback-action-QA')
camera=bpy.data.objects.new('CAM-feedback-action-QA',camera_data);scene.collection.objects.link(camera)
camera.location=centre+Vector((3,-6,1.6))
camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=(upper-lower).z*1.6;scene.camera=camera
for name,location,power,size in [('key',(3,-4,5),650,4),('fill',(-3,-2,3),400,4),('rim',(1,3,4),650,3)]:
    light_data=bpy.data.lights.new('LGT-action-'+name,'AREA');light_data.energy=power;light_data.shape='DISK';light_data.size=size
    light=bpy.data.objects.new('LGT-action-'+name,light_data);scene.collection.objects.link(light)
    light.location=centre+Vector(location);light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler()
if scene.world is None:scene.world=bpy.data.worlds.new('MAT-action-QA-world')
scene.world.color=(.12,.14,.18)
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=360;scene.render.resolution_y=520;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
records=[]
for action in list(bpy.data.actions):
    rig.animation_data.action=action
    if len(action.slots):rig.animation_data.action_slot=action.slots[0]
    start,end=action.frame_range
    for phase in [0,.5,.95]:
        scene.frame_set(round(start+(end-start)*phase));bpy.context.view_layer.update()
        path=ART/(action.name+'-'+str(phase)+'.png');scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True)
        records.append({'action':action.name,'phase':phase,'image':path.name,'body':list(rig.pose.bones['Body'].head)})
assert len(records)==9
# Re-import only the exported rig into this owned process and verify all action channels survived.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ART/'InteractionAnimations.fbx'))
actions={a.name.split('|')[-1] for a in bpy.data.actions}
assert {'Interact','HitRecieve','Punch_Right'}<=actions,actions
assert not any(o.type=='MESH' for o in bpy.context.scene.objects)
action_rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');action_rig.name='AnimationSource'
imported_actions=list(bpy.data.actions)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Resources/FishingCharacter/9SCharacter.fbx'))
avatar=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE' and o!=action_rig)
avatar.animation_data_clear();avatar.animation_data_create()
base=(avatar.matrix_world@avatar.pose.bones['Head'].head-avatar.matrix_world@avatar.pose.bones['Ankle.R'].head).z
compatibility=[]
for action in imported_actions:
    avatar.animation_data.action=action;avatar.animation_data.action_slot=action.slots[0]
    first,last=action.frame_range
    for phase in [0,.5,.95]:
        bpy.context.scene.frame_set(round(first+(last-first)*phase));bpy.context.view_layer.update()
        height=(avatar.matrix_world@avatar.pose.bones['Head'].head-avatar.matrix_world@avatar.pose.bones['Ankle.R'].head).z
        assert height>base*.8,(action.name,phase,height,base)
        compatibility.append({'action':action.name,'phase':phase,'headHeightRatio':height/base})
(ART/'validation.json').write_text(json.dumps({'frames':records,'exportedActions':sorted(actions),'exportedMeshes':0,'actual9SCompatibility':compatibility},indent=2)+'\n',encoding='utf-8')
print('ACTION_QA_PASS',len(records),sorted(actions))
