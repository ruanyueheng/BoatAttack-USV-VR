"""Import the licensed Poly Haven hanging fixture into an owned Blender scene."""
from pathlib import Path
import json
import sys

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'ArtSource/V036'
ORIGINAL=SOURCE/'Originals/BrassDiya'
OUT=ROOT/'Assets/Resources/FishingV036'
TEX=OUT/'LanternTextures'
if '--textures' in sys.argv:
    from PIL import Image
    import numpy as np
    TEX.mkdir(parents=True,exist_ok=True)
    maps=ORIGINAL/'textures'
    color=Image.open(maps/'brass_diya_lantern_diff_1k.jpg').convert('RGB')
    alpha=Image.open(maps/'brass_diya_lantern_opacity_1k.png').convert('L')
    rgba=color.convert('RGBA');rgba.putalpha(alpha);rgba.save(TEX/'LanternAlbedo.png')
    arm=np.array(Image.open(maps/'brass_diya_lantern_arm_1k.jpg').convert('RGB'))
    metal=np.zeros((1024,1024,4),np.uint8);metal[:,:,0]=arm[:,:,2];metal[:,:,3]=255-arm[:,:,1]
    Image.fromarray(metal).save(TEX/'LanternMetallicSmoothness.png')
    Image.open(maps/'brass_diya_lantern_nor_gl_1k.jpg').save(TEX/'LanternNormal.png')
    Image.open(maps/'brass_diya_lantern_emissive_1k.png').save(TEX/'LanternEmission.png')
    assert np.count_nonzero(np.array(alpha)<128)>1000 and np.count_nonzero(metal[:,:,0])>1000
    print('V036-LANTERN texture repack PASS');sys.exit(0)
import bpy
from mathutils import Vector
scene=bpy.data.scenes.new('V036 Poly Haven hanging lantern')
bpy.context.window.scene=scene
bpy.ops.import_scene.gltf(filepath=str(ORIGINAL/'brass_diya_lantern_1k.gltf'))
objects=[o for o in scene.objects if o.type=='MESH']
points=[o.matrix_world@Vector(v) for o in objects for v in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
for o in objects:o.location-=offset
for mat in {m for o in objects for m in o.data.materials}:
    mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links
    bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(TEX/'LanternAlbedo.png'),check_existing=True)
    links.new(texture.outputs['Color'],bsdf.inputs['Base Color'])
    links.new(texture.outputs['Alpha'],bsdf.inputs['Alpha'])
    if 'flame' in mat.name:
        emission=nodes.new('ShaderNodeTexImage');emission.image=bpy.data.images.load(str(TEX/'LanternEmission.png'),check_existing=True)
        links.new(emission.outputs['Color'],bsdf.inputs['Emission Color']);bsdf.inputs['Emission Strength'].default_value=4
    mat.surface_render_method='DITHERED'
for o in bpy.context.selected_objects:o.select_set(False)
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'HangingLantern.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',path_mode='AUTO')
bpy.ops.export_scene.gltf(filepath=str(SOURCE/'hanging-lantern.glb'),export_format='GLB',use_selection=True)
tris=sum(len(o.data.loop_triangles) for o in objects)
for o in objects:o.data.calc_loop_triangles()
tris=sum(len(o.data.loop_triangles) for o in objects)
assert len(objects)==3 and 10000<tris<17000 and .30<hi.z-lo.z<.65
scene.world=bpy.data.worlds.new('V036 lantern studio');scene.world.use_nodes=True
background=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
background.inputs['Color'].default_value=(.15,.21,.28,1)
background.inputs['Strength'].default_value=.7
focus=Vector((0,0,(hi.z-lo.z)*.5))
for name,pos,power,size in [('key',(0,-.7,.8),65,.6),('rim',(.5,.4,.65),80,.5),('fill',(-.5,-.1,.3),25,.6)]:
    data=bpy.data.lights.new('LGT-'+name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    light=bpy.data.objects.new('LGT-'+name,data);scene.collection.objects.link(light);light.location=pos;light.rotation_euler=(focus-light.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('CAM-lantern',bpy.data.cameras.new('V036 camera'));scene.collection.objects.link(camera)
camera.location=(.75,-1.1,.55);camera.rotation_euler=(focus-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=(hi.z-lo.z)*1.3;scene.camera=camera
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=640;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.film_transparent=True
scene.render.filepath=str(SOURCE/'hanging-lantern-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'hanging-lantern-v036.blend'))
bpy.ops.render.render(write_still=True)
report={'source':'https://polyhaven.com/a/brass_diya_lantern','author':'Bhargav Kubal','license':'CC0','objects':len(objects),'triangles':tris,'metres':list(hi-lo),'engine':'Blender 5.2 EEVEE','textureMax':1024}
(SOURCE/'lantern-import.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('V036-LANTERN PASS',json.dumps(report))
