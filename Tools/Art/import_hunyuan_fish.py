"""Blender --background --factory-startup --python-exit-code 1 --python Tools/Art/import_hunyuan_fish.py [-- --inspect].

Preserves the supplied GLBs; exports metre-scale, UV-preserving fish and albedo only.
Source previews establish orientation before the runtime exports are accepted.
"""
from pathlib import Path
import hashlib
import json
import math
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Hunyuan/Fish/Originals"
OUT = ROOT / "Assets/Resources/FishingFish"
PREVIEW = ROOT / "Logs/Hunyuan"
INSPECT = "--inspect" in sys.argv
LENGTHS = {"Mackerel": .29, "SeaBream": .27, "Sardine": .17, "Grouper": .35,
           "Butterfly": .20, "Tuna": .38, "Lanternfish": .18, "Lionfish": .26,
           "Coral": .24, "Dart": .32, "Puffer": .21, "Sailfish": .82}
# Source side previews determine which end is the head (positive X in the supplied GLB).
REVERSE = {"Grouper", "Sardine", "Tuna"}
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
report = []
for name, length in LENGTHS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = SOURCE / (name + ".glb")
    if not source.exists() and name in {"Coral", "Dart", "Puffer", "Sailfish"}:
        continue  # Optional V022 replacements; keep the established fish until approved sources arrive.
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    assert len(meshes) == 1, (name, "Expected one source mesh")
    fish = meshes[0]
    fish.data.transform(fish.matrix_world)
    fish.parent = None
    fish.matrix_world = Matrix.Identity(4)
    points = [v.co for v in fish.data.vertices]
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    assert high.x-low.x > max(high.y-low.y, high.z-low.z), (name, "Length axis")
    centre = (low + high) / 2
    scale = length / (high.x-low.x)
    for v in fish.data.vertices:
        v.co = (v.co-centre) * scale
    if name in REVERSE:
        fish.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
    before = sum(len(p.vertices)-2 for p in fish.data.polygons)
    if not INSPECT:
        mesh = bmesh.new()
        mesh.from_mesh(fish.data)
        bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=length*1e-5)
        mesh.to_mesh(fish.data)
        mesh.free()
        decimate = fish.modifiers.new("PICO triangle budget", "DECIMATE")
        welded = sum(len(p.vertices)-2 for p in fish.data.polygons)
        decimate.ratio = min(1, 3500 / welded)
        bpy.context.view_layer.objects.active = fish
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    triangles = sum(len(p.vertices)-2 for p in fish.data.polygons)
    assert INSPECT or triangles <= 3600, (name, triangles)
    material = fish.data.materials[0]
    base = material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"]
    assert base.is_linked and base.links[0].from_node.type == "TEX_IMAGE", name
    albedo = base.links[0].from_node.image
    if not INSPECT:
        albedo.scale(1024, 1024)
        albedo.filepath_raw = str(OUT / (name + "Albedo.png"))
        albedo.file_format = "PNG"
        albedo.save()
        # Keep only colour; the large normal/ORM maps do not enter the mobile runtime.
        for node in list(material.node_tree.nodes):
            if node.type == "TEX_IMAGE" and node.image != albedo:
                material.node_tree.nodes.remove(node)
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Roughness"].default_value = .65
        bsdf.inputs["Metallic"].default_value = 0
    # A consistent side view is also the exact thumbnail used by the journal.
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 512 if INSPECT else 256
    scene.render.resolution_y = 288 if INSPECT else 144
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.world = bpy.data.worlds.new("Preview world")
    scene.world.color = (.28, .28, .28)
    scene.view_settings.view_transform = "Standard"
    bpy.ops.object.camera_add(location=(0, -length*3, length*.12))
    camera = bpy.context.object
    camera.rotation_euler = (-camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = length*1.22
    scene.camera = camera
    for position, energy in [((-.4, -1, 1), 55), ((.5, .5, .4), 25)]:
        bpy.ops.object.light_add(type="AREA", location=Vector(position)*length)
        lamp = bpy.context.object
        lamp.data.energy = energy*length*length
        lamp.data.shape = "DISK"
        lamp.data.size = length*2
        lamp.rotation_euler = (-lamp.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str((PREVIEW if INSPECT else OUT) / (name + ("Source.png" if INSPECT else "Thumbnail.png")))
    bpy.ops.render.render(write_still=True)
    if not INSPECT:
        # Blender -Y becomes Unity +Z; all fish use the established +Z-head convention.
        fish.data.transform(Matrix.Rotation(-math.pi/2, 4, "Z"))
        bpy.ops.object.select_all(action="DESELECT")
        fish.select_set(True)
        bpy.context.view_layer.objects.active = fish
        fish.name = name
        bpy.ops.export_scene.fbx(filepath=str(OUT / (name + ".fbx")), use_selection=True,
            object_types={"MESH"}, bake_anim=False, add_leaf_bones=False,
            axis_forward="-Z", axis_up="Y", path_mode="STRIP")
    report.append({"species": name, "source": "Originals/"+source.name,
        "sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "sourceTriangles": before, "runtimeTriangles": triangles,
        "lengthMeters": length, "texturePixels": 1024})
    print("[HUNYUAN]", name, before, "->", triangles, flush=True)
(PREVIEW / "source-inspection.json" if INSPECT else ROOT / "ArtSource/Hunyuan/Fish/manifest.json").write_text(
    json.dumps(report, indent=2), encoding="utf-8")
