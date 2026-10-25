"""Adapt the downloaded Hunyuan roast in the live Blender session; never edit its original GLB."""
from pathlib import Path
import bpy
import math
import json
import hashlib
from mathutils import Matrix, Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/V039/Hunyuan'
OUT=ROOT/'Assets/Resources/FishingV039'


def prepare():
    assert 'V039 Hunyuan roast production' not in bpy.data.scenes
    source=next(o for o in bpy.data.scenes['V039 Hunyuan grilled sea bream'].objects if o.type=='MESH')
    scene=bpy.data.scenes.new('V039 Hunyuan roast production');bpy.context.window.scene=scene
    obj=source.copy();obj.data=source.data.copy();obj.name='GEO-HunyuanGrilledFish';scene.collection.objects.link(obj)
    obj.data.transform(obj.matrix_world);obj.parent=None;obj.matrix_world=Matrix.Identity(4)
    points=[v.co for v in obj.data.vertices]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    assert hi.x-lo.x>max(hi.y-lo.y,hi.z-lo.z)
    turn=Matrix.Rotation(-math.pi/2,4,'X');scale=.68/(hi.x-lo.x)
    for v in obj.data.vertices:v.co=(turn@(v.co-(lo+hi)*.5))*scale
    bottom=min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:v.co.z-=bottom
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    modifier=obj.modifiers.new('PICO roast triangle budget','DECIMATE');modifier.ratio=6000/50000
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    material=source.data.materials[0].copy();material.name='MAT-V039 Hunyuan golden roasted skin';obj.data.materials.clear();obj.data.materials.append(material)
    bsdf=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    base=bsdf.inputs['Base Color'];assert base.is_linked and base.links[0].from_node.type=='TEX_IMAGE'
    albedo=base.links[0].from_node.image.copy();albedo.scale(2048,2048);OUT.mkdir(exist_ok=True)
    albedo.filepath_raw=str(OUT/'GrilledFishAlbedo.png');albedo.file_format='PNG';albedo.save();albedo.pack()
    base.links[0].from_node.image=albedo
    for node in list(material.node_tree.nodes):
        if node.type=='TEX_IMAGE' and node.image!=albedo:material.node_tree.nodes.remove(node)
    bsdf.inputs['Metallic'].default_value=0;bsdf.inputs['Roughness'].default_value=.48
    triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
    assert 4000<triangles<=6100 and len(obj.data.uv_layers)>0
    assert obj.data.validate() is False
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/'GrilledFish.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True)
    bpy.ops.export_scene.gltf(filepath=str(ART/'GrilledFish-game.glb'),use_selection=True,use_active_scene=True,export_format='GLB',export_animations=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART.parent/'v039-development.blend'))
    report={'provider':'Tencent Hunyuan3D website','model':'3.1','sourceTriangles':50000,'runtimeTriangles':triangles,'lengthMetres':.68,'sourceSha256':hashlib.sha256((ART/'GrilledSeaBream_Hunyuan_Source.glb').read_bytes()).hexdigest(),'runtimeFbxSha256':hashlib.sha256((OUT/'GrilledFish.fbx').read_bytes()).hexdigest(),'textureSize':list(albedo.size),'websiteGenerationCreditCost':1}
    (ART/'manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
