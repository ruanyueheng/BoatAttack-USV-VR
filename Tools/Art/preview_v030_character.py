"""Render three views of the adapted player in a separate background scene."""
from pathlib import Path
import bpy, math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'Docs/Images/V030';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/V030/9s-adapted.blend'))
scene=bpy.context.scene;scene.frame_set(1);bpy.context.view_layer.update()
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=640;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.film_transparent=False
scene.world=bpy.data.worlds.new('V030 preview');scene.world.use_nodes=True
background=scene.world.node_tree.nodes.new('ShaderNodeBackground');background.inputs['Color'].default_value=(.12,.16,.19,1);background.inputs['Strength'].default_value=.4
world_out=scene.world.node_tree.nodes.new('ShaderNodeOutputWorld');scene.world.node_tree.links.new(background.outputs[0],world_out.inputs['Surface'])
for name,loc,energy in [('Key',(2,-3,4),400),('Fill',(-3,-2,2),220),('Rim',(0,3,3),350)]:
    light=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(light);light.location=loc;light.data.energy=energy;light.data.size=3;light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('V030 preview camera',bpy.data.cameras.new('V030 preview camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.25
for name,position in [('character-front',(0,-4,1.0)),('character-side',(4,0,1.0)),('character-back',(0,4,1.0))]:
    cam.location=position;cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print('V030 PREVIEW PASS')
