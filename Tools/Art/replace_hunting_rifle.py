"""Inspect/convert the user-supplied GLB without requiring a runtime glTF package.

Run with Blender --background --factory-startup --python this_file -- --inspect.
All source changes are made on imported copies; never save over the supplied GLB.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/HuntingRifle/NewAK.glb'
OUT = ROOT / 'Assets/Resources/FishingHunting'
LOG = ROOT / 'Logs/NewAK'


def save_texture(source, name, non_color=False):
    image = source.copy()
    image.name = name
    image.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    image.scale(2048, 2048)
    image.file_format = 'PNG'
    image.filepath_raw = str(OUT / (name + '.png'))
    image.save()
    return image


def export(obj):
    # Source: muzzle -X, top +Z. Keep proportions; align -Y in Blender
    # to +Z in Unity. The pistol-grip centre is the new origin.
    length = max(v.co.x for v in obj.data.vertices) - min(v.co.x for v in obj.data.vertices)
    scale = .66 / length
    grip = Vector((.08, -.015, .165))
    for vertex in obj.data.vertices:
        p = (vertex.co - grip) * scale
        vertex.co = Vector((-p.y, p.x, p.z))
    obj.data.update()
    obj.name = obj.data.name = 'HuntingRifle'
    OUT.mkdir(parents=True, exist_ok=True)
    images = {i.name: i for i in bpy.data.images if i.type == 'IMAGE'}
    save_texture(images['texture_pbr_20250901'], 'Diffuse')
    save_texture(images['texture_pbr_20250901_normal'], 'RifleNormal', True)
    orm = images['texture_pbr_20250901_metallic-texture_pbr_20250901_roughness'].copy()
    orm.colorspace_settings.name = 'Non-Color'
    orm.scale(2048, 2048)
    pixels = np.empty(2048 * 2048 * 4, dtype=np.float32)
    orm.pixels.foreach_get(pixels)
    pixels = pixels.reshape(-1, 4)
    packed = np.ones_like(pixels)
    packed[:, :3] = pixels[:, 2:3]
    packed[:, 3] = 1 - pixels[:, 1]
    image = bpy.data.images.new('RifleMetallicSmoothness', width=2048, height=2048, alpha=True)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(packed.ravel())
    image.file_format = 'PNG'
    image.filepath_raw = str(OUT / 'RifleMetallicSmoothness.png')
    image.save()
    # Materials are assigned by the Unity builder; FBX carries geometry/UVs only.
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / 'Rifle.fbx'), use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
        path_mode='STRIP', use_mesh_modifiers=True,
    )
    outputs = ['Rifle.fbx', 'Diffuse.png', 'RifleNormal.png', 'RifleMetallicSmoothness.png']
    manifest = {
        'source': 'NewAK.glb', 'sourceSha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'sourceVertices': len(obj.data.vertices), 'triangles': len(obj.data.polygons),
        'sourceGripOrigin': list(grip), 'uniformScale': scale, 'lengthMeters': .66,
        'unityForward': '+Z', 'unityUp': '+Y',
        'unityRightPalm': [.022, 0, 0], 'unityLeftPalm': [0, .043, .184],
        'unityMuzzle': [-.0045, .0695, .416],
        'textureSize': 2048, 'metallicSmoothnessPacking': 'RGB=glTF B, A=1-glTF G; linear',
        'outputs': {n: hashlib.sha256((OUT / n).read_bytes()).hexdigest() for n in outputs},
    }
    (SOURCE.parent / 'NewAK-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    render(obj, 'converted-model', (1, 1.6, .6), .8)
    print('NEW AK EXPORT PASS', json.dumps(manifest))


def render(obj, name, direction, ortho=1.6):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.world.color = (.15, .15, .15)
    scene.view_settings.view_transform = 'AgX'
    center = sum((obj.matrix_world @ Vector(c) for c in obj.bound_box), Vector()) / 8
    camera_data = bpy.data.cameras.new('Preview')
    camera = bpy.data.objects.new('Preview', camera_data)
    scene.collection.objects.link(camera)
    camera.location = center + Vector(direction).normalized() * 3
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = ortho
    scene.camera = camera
    lamps = []
    for position, energy, size in [((1, -2, 3), 220, 3), ((-2, 2, 2), 160, 3)]:
        data = bpy.data.lights.new('Preview light', 'AREA')
        data.energy = energy
        data.shape = 'DISK'
        data.size = size
        lamp = bpy.data.objects.new('Preview light', data)
        scene.collection.objects.link(lamp)
        lamp.location = center + Vector(position)
        lamp.rotation_euler = (center - lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lamps.append(lamp)
    scene.render.filepath = str(LOG / (name + '.png'))
    bpy.ops.render.render(write_still=True)
    for item in lamps + [camera]:
        bpy.data.objects.remove(item, do_unlink=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--inspect', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    LOG.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(meshes) == 1, 'Review multi-mesh sources before exporting'
    obj = meshes[0]
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    print('NEW AK SOURCE', json.dumps({
        'vertices': len(obj.data.vertices), 'faces': len(obj.data.polygons),
        'bounds': [list(v) for v in obj.bound_box],
        'materials': [m.name for m in obj.data.materials],
        'images': [(i.name, list(i.size)) for i in bpy.data.images if i.type == 'IMAGE'],
        'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    }))
    if args.inspect:
        render(obj, 'source-side', (0, -1, 0), 1.4)
        render(obj, 'source-top', (0, 0, 1), 1.4)
        render(obj, 'source-perspective', (1, -2, 1), 1.4)
        return
    export(obj)


main()
