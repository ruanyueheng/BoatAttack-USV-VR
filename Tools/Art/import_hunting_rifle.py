"""Export the supplied Blender rifle as a compact, UV-preserving Unity static mesh."""
import bpy
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
obj=bpy.data.objects['ak-47']
for other in list(bpy.data.objects):
    if other!=obj:bpy.data.objects.remove(other,do_unlink=True)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Source muzzle points -Y, Z is up. The grip centre becomes origin; final length 66 cm.
scale=.66/.8798075
for vertex in obj.data.vertices:
    vertex.co=(vertex.co-Vector((0,.13,-.064)))*scale
    vertex.co.x*=1.5
obj.data.update();obj.name='HuntingRifle'
for mat in obj.data.materials:
    if mat and mat.use_nodes:
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:node.image.filepath=str(root/'ArtSource/HuntingRifle/diffuse.png')
out=root/'Assets/Resources/FishingHunting/Rifle.fbx'
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
print('HUNTING EXPORT PASS: vertices',len(obj.data.vertices),'bounds',list(obj.dimensions))
