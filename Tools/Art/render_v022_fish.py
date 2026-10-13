"""Render the real procedural fish meshes exported by FishingV022Assets.Build; no invented 2D glyphs."""
from pathlib import Path
import json
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
data=json.loads((ROOT/'ArtSource/V022/procedural-fish-snapshots.json').read_text(encoding='utf-8'))
assert len(data['fish'])==4
for fish in data['fish']:
    name=fish['name']
    if (ROOT/('ArtSource/Hunyuan/Fish/Originals/'+name+'.glb')).exists():
        continue  # Approved generated models keep the portrait produced by the shared Hunyuan importer.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    vertices=[(p['z'],p['x'],p['y']) for p in fish['vertices']]
    faces=[fish['triangles'][i:i+3] for i in range(0,len(fish['triangles']),3)]
    assert len(vertices)>100 and len(fish['colors'])==len(vertices)
    mesh=bpy.data.meshes.new('GEO-'+name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new('GEO-'+name,mesh);bpy.context.collection.objects.link(obj)
    colors=mesh.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    for face in mesh.polygons:
        face.use_smooth=True
        for loop in face.loop_indices:
            c=fish['colors'][mesh.loops[loop].vertex_index]
            colors.data[loop].color_srgb=(c['r'],c['g'],c['b'],1)
    mat=bpy.data.materials.new('MAT-'+name);mat.use_nodes=True
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value=.65;shader.inputs['Metallic'].default_value=0
    vertex=mat.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
    mat.node_tree.links.new(vertex.outputs['Color'],shader.inputs['Base Color']);mesh.materials.append(mat)
    length=max(p[0] for p in vertices)-min(p[0] for p in vertices)
    height=max(p[2] for p in vertices)-min(p[2] for p in vertices)
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=512;scene.render.resolution_y=288;scene.render.resolution_percentage=100;scene.render.film_transparent=True
    scene.view_settings.view_transform='Standard'
    world=bpy.data.worlds.new('Portrait studio');world.use_nodes=True
    world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.35;scene.world=world
    cam_data=bpy.data.cameras.new('CAM-'+name);cam_data.type='ORTHO';cam_data.ortho_scale=max(length,height*512/288)*1.25
    cam=bpy.data.objects.new('CAM-'+name,cam_data);scene.collection.objects.link(cam)
    cam.location=(0,-length*3,length*.12);cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
    for position,energy in [((-.4,-1,1),55),((.5,.5,.4),25)]:
        lamp_data=bpy.data.lights.new('LGT-portrait','AREA');lamp_data.energy=energy*length*length;lamp_data.size=length*2
        lamp=bpy.data.objects.new('LGT-portrait',lamp_data);scene.collection.objects.link(lamp)
        lamp.location=Vector(position)*length;lamp.rotation_euler=(-lamp.location).to_track_quat('-Z','Y').to_euler()
    output=ROOT/('Assets/Resources/FishingFish/'+name+'Thumbnail.png');scene.render.filepath=str(output)
    bpy.ops.render.render(write_still=True)
    assert output.stat().st_size>1000
    print('V022_FISH_PORTRAIT',name,len(vertices),len(faces),flush=True)
