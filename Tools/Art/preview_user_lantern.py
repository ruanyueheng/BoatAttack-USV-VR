"""Native read-only source preview; no .blend writes."""
import bpy
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2];out=root/'Logs/UserLantern';out.mkdir(parents=True,exist_ok=True)
objects=[o for o in bpy.data.objects if o.type=='MESH'];points=[o.matrix_world@Vector(p) for o in objects for p in o.bound_box]
lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)]);center=(lo+hi)*.5
for o in list(bpy.data.objects):
    if o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100;scene.render.film_transparent=True
world=bpy.data.worlds.new('Neutral preview world');world.use_nodes=True;world.node_tree.nodes.clear()
background=world.node_tree.nodes.new('ShaderNodeBackground');background.inputs['Color'].default_value=(.25,.25,.25,1)
output=world.node_tree.nodes.new('ShaderNodeOutputWorld');world.node_tree.links.new(background.outputs[0],output.inputs['Surface']);scene.world=world
camera=bpy.data.objects.new('Preview',bpy.data.cameras.new('Preview'));scene.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=1.30;scene.camera=camera
for name,offset,power in [('Key',(1,-2,2),300),('Fill',(-2,-1,1),180),('Rim',(0,2,2),250)]:
    light=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(light);light.location=center+Vector(offset);light.data.energy=power;light.data.size=2;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
for name,offset in [('source-front',(1,-3,1)),('source-side',(-3,-1,1))]:
    camera.location=center+Vector(offset);camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
