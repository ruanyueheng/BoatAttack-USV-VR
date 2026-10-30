from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
for filename in ['BeachCharacter.fbx','9SCharacter.fbx']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Resources/FishingCharacter'/filename))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    actions=[a for a in bpy.data.actions if a.name.endswith('Idle')];rig.animation_data.action=actions[0];bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
    print('RIGCHECK',filename,'rig',list(rig.scale),'arm',((rig.matrix_world@rig.pose.bones['UpperArm.L'].head)-(rig.matrix_world@rig.pose.bones['LowerArm.L'].head)).length,flush=True)
    for name in ['Body','UpperArm.L','LowerArm.L','Wrist.L','UpperLeg.L','Ankle.L']:
        p=rig.pose.bones[name];print('BONE',name,'scale',list(p.scale),'position',list(rig.matrix_world@p.head),'rest',list(rig.matrix_world@p.bone.head_local),flush=True)
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh();points=[o.matrix_world@v.co for v in m.vertices]
        print('SKINCHECK',o.name,'scale',list(o.scale),'parent',o.parent.name if o.parent else None,'local',list(o.matrix_local[0]),'bounds',list(m.vertices[0].co),'min',[min(p[i] for p in points) for i in range(3)],'max',[max(p[i] for p in points) for i in range(3)],flush=True);e.to_mesh_clear()
