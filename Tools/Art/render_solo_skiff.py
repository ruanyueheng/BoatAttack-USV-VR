"""Render the actual editable skiff source for asset inspection."""
from pathlib import Path
import bpy
from mathutils import Vector

root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/SoloSkiff/SoloSkiff.blend'))
assert 'SoloSkiff' in bpy.data.collections
for obj in bpy.data.objects:
    obj.hide_render=obj.name not in bpy.data.collections['SoloSkiff'].objects
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.12,.14,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.7
def unity(v):return Vector((-v[0],v[2],v[1]))
target=unity((0,.3,0))
bpy.ops.object.camera_add(location=unity((4.5,3.8,-5.5)))
camera=bpy.context.object;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=5.9;scene.camera=camera
for p,power,size in [((1,6,-3),1000,5),((-3,3,1),700,4)]:
    bpy.ops.object.light_add(type='AREA',location=unity(p))
    lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size
    lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
output=root/'Docs/Images/V016';output.mkdir(parents=True,exist_ok=True)
scene.render.filepath=str(output/'SoloSkiff.png')
bpy.ops.render.render(write_still=True)
assert (output/'SoloSkiff.png').stat().st_size>10000
print('SKIFF_PREVIEW_PASS')
