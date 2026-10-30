"""不启动 Unity，生成使用 CC0 素材的成年海岸店员。

Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python
Tools/Art/import_cc0_cashier.py -- --output Logs/CashierSource/Generated

输入为选中的 Quaternius Standard 原始文件，来源记录见
ArtSource/Characters/Cashier/SOURCE.txt。免费源模型是 Superhero Female，
不是 Regular Female 或 Teen。输出为穿工作装的静态角色，不替换玩家骨架。
预览灯光和地面不会进入导出的 FBX。
"""
from pathlib import Path
import argparse
import json
import math
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Characters/Cashier"
parser = argparse.ArgumentParser()
parser.add_argument("--output", default="Logs/CashierSource/Generated")
parser.add_argument("--no-preview", action="store_true")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
OUT = (ROOT / args.output).resolve()
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW = ROOT / "Logs/CashierSource"
PREVIEW.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

PALETTE = {
    "Shirt": (0.72, 0.82, 0.80, 1),
    "Trousers": (0.035, 0.085, 0.115, 1),
    "Apron": (0.045, 0.30, 0.32, 1),
    "Pocket": (0.10, 0.39, 0.39, 1),
    "Strap": (0.035, 0.14, 0.16, 1),
    "Boots": (0.025, 0.04, 0.045, 1),
    "Brass": (0.76, 0.55, 0.25, 1),
}
stubs = {}
for name in ["Skin", "Eyes", "Hair", *PALETTE]:
    stubs[name] = bpy.data.materials.new("Cashier_" + name)

bpy.ops.import_scene.fbx(filepath=str(SOURCE / "Superhero_Female_FullBody.fbx"))
base_meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
rig = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
body = next(o for o in base_meshes if o.name == "Superhero_Female")
eye = next(o for o in base_meshes if o.name == "Eyes")
eye_y = sum((eye.matrix_world @ v.co).y for v in eye.data.vertices) / len(eye.data.vertices)
front_sign = 1 if eye_y > 0 else -1
canonical = Matrix.Rotation(math.pi if front_sign > 0 else 0, 4, "Z")
rest_points = [body.matrix_world @ v.co for v in body.data.vertices]
rest_min = min(v.z for v in rest_points)
rest_height = max(v.z for v in rest_points) - rest_min

# 在真实人物表面生成不透明工作衬衣与长裤，不使用裸露底模、
# 方块肢体或第二个玩家模型；手、脸和颈部保留皮肤。
body.data.materials.clear()
body_keys = ["Skin", "Shirt", "Trousers", "Boots"]
for key in body_keys:
    body.data.materials.append(stubs[key])
for poly in body.data.polygons:
    weights = {}
    for index in poly.vertices:
        for group in body.data.vertices[index].groups:
            name = body.vertex_groups[group.group].name
            weights[name] = weights.get(name, 0) + group.weight
    joint = max(weights, key=weights.get) if weights else "pelvis"
    height = sum((rest_points[i].z - rest_min) / rest_height for i in poly.vertices) / len(poly.vertices)
    hand = joint.startswith(("hand_", "index_", "middle_", "ring_", "pinky_", "thumb_"))
    if hand or joint in {"Head", "neck_01"} or height > 0.855:
        key = "Skin"
    elif height < 0.065:
        key = "Boots"
    elif height < 0.56 or joint.startswith(("thigh_", "calf_", "foot_", "ball_")):
        key = "Trousers"
    else:
        key = "Shirt"
    poly.material_index = body_keys.index(key)
for obj in base_meshes:
    if obj == body:
        continue
    obj.data.materials.clear()
    obj.data.materials.append(stubs["Eyes" if obj == eye else "Hair"])
    for poly in obj.data.polygons:
        poly.material_index = 0

# 烘焙前先摆好源骨架：双臂下垂，肘部略向前弯曲。
# 这是制作好的静态姿势，不宣称具备待机或交易动画。
def rotate_bone_world(name, axis, radians):
    bone = rig.pose.bones[name]
    world = rig.matrix_world @ bone.matrix
    pivot = world.translation.copy()
    rotation = Quaternion(Vector(axis), radians).to_matrix().to_4x4()
    bone.matrix = rig.matrix_world.inverted() @ Matrix.Translation(pivot) @ rotation @ Matrix.Translation(-pivot) @ world
    bpy.context.view_layer.update()

for side in ("l", "r"):
    x = (rig.matrix_world @ rig.pose.bones["upperarm_" + side].head).x
    sign = 1 if x > 0 else -1
    rotate_bone_world("upperarm_" + side, (0, 1, 0), sign * math.radians(67))
    rotate_bone_world("lowerarm_" + side, (0, 0, 1), sign * front_sign * math.radians(10))

before = set(bpy.context.scene.objects)
bpy.ops.import_scene.fbx(filepath=str(SOURCE / "Hair_Long.fbx"))
hair_meshes = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"]
assert len(hair_meshes) == 1, "Expected the selected shoulder-length hairstyle only"
for obj in hair_meshes:
    obj.data.materials.clear()
    obj.data.materials.append(stubs["Hair"])
    for poly in obj.data.polygons:
        poly.material_index = 0

# 只烘焙选中的授权网格，移除运行时不需要的骨架、动画及相机。
# 头发和身体保留原有 UV 与拓扑。
source_meshes = base_meshes + hair_meshes
depsgraph = bpy.context.evaluated_depsgraph_get()
meshes = []
for source in source_meshes:
    data = bpy.data.meshes.new_from_object(source.evaluated_get(depsgraph), depsgraph=depsgraph)
    data.transform(canonical @ source.matrix_world)
    obj = bpy.data.objects.new("Clerk_" + source.name, data)
    bpy.context.collection.objects.link(obj)
    meshes.append(obj)
for obj in list(bpy.context.scene.objects):
    if obj not in meshes:
        bpy.data.objects.remove(obj, do_unlink=True)
points = [v.co for obj in meshes for v in obj.data.vertices]
feet = min(p.z for p in points)
scale = 1.72 / (max(p.z for p in points) - feet)
for obj in meshes:
    for vertex in obj.data.vertices:
        vertex.co = Vector((vertex.co.x * scale * 0.96, vertex.co.y * scale, (vertex.co.z - feet) * scale))
        # 略微减小超级英雄躯干的厚度，保留成年人的头身与肢体比例。
        if "Superhero_Female" in obj.name and 1.0 < vertex.co.z < 1.43:
            blend = math.sin((vertex.co.z - 1.0) / 0.43 * math.pi)
            vertex.co.y *= 1 - 0.08 * blend
    obj.data.update()
body = next(o for o in meshes if "Superhero_Female" in o.name)
vertex_materials = [set() for _ in body.data.vertices]
for polygon in body.data.polygons:
    material_name = body.data.materials[polygon.material_index].name
    for index in polygon.vertices:
        vertex_materials[index].add(material_name)
for vertex, touching in zip(body.data.vertices, vertex_materials):
    if touching and all(n.endswith(("Shirt", "Trousers")) for n in touching):
        vertex.co += vertex.normal * 0.007
body.data.update()
body_points = [v.co.copy() for v in body.data.vertices]

def front_at(z):
    sample = [v.y for v in body_points if abs(v.z - z) < 0.05 and abs(v.x) < 0.21]
    return min(sample) if sample else -0.13

def fabric(name, vertices, faces, key, thickness=0.003):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.materials.append(stubs[key])
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for p in data.polygons:
        p.use_smooth = True
    if thickness:
        bpy.context.view_layer.objects.active = obj
        solid = obj.modifiers.new("Cloth thickness", "SOLIDIFY")
        solid.thickness = thickness
        bpy.ops.object.modifier_apply(modifier=solid.name)
    meshes.append(obj)
    return obj

def panel(name, levels, key, offset):
    vertices = []
    segments = 12
    for z, half_width in levels:
        for i in range(segments + 1):
            u = i / segments * 2 - 1
            vertices.append((u * half_width, front_at(z) - offset + 0.022 * u * u, z))
    faces = []
    for row in range(len(levels) - 1):
        for col in range(segments):
            a = row * (segments + 1) + col
            faces.append((a, a + 1, a + segments + 2, a + segments + 1))
    return fabric(name, vertices, faces, key)

panel("Original teal work apron", [(0.58, .235), (.73, .225), (.88, .21), (1.02, .19), (1.13, .165), (1.23, .135), (1.33, .095)], "Apron", .025)
panel("Apron practical pocket", [(.86, .11), (.99, .12)], "Pocket", .036)
for sign in (-1, 1):
    centres = [(sign * .078, front_at(1.32) - .031, 1.32), (sign * .105, front_at(1.375) - .013, 1.375), (sign * .13, 0, 1.405), (sign * .14, .07, 1.375), (sign * .15, .115, 1.07)]
    verts = [(x + dx, y, z) for x, y, z in centres for dx in (-.015, .015)]
    fabric("Apron shoulder strap", verts, [(i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2) for i in range(len(centres) - 1)], "Strap")
# 用整齐的圆领包边遮住源网格颈部的材质分界。
collar = []
for radius_x, radius_y, z in [(.069, .065, 1.468), (.088, .082, 1.43)]:
    for i in range(48):
        angle = i * math.tau / 48
        collar.append((radius_x * math.cos(angle), .013 + radius_y * math.sin(angle), z + .009 * math.sin(angle)))
fabric("Original shirt collar", collar, [(i, (i + 1) % 48, (i + 1) % 48 + 48, i + 48) for i in range(48)], "Shirt")
# 包头工作鞋贴合真实源脚型，不把裸露脚趾简单涂黑。
# 鞋壳取自脚部凸包，不使用方块鞋。
for sign in (-1, 1):
    vertices = [p for p in body_points if p.z < .13 and p.x * sign > .02]
    centre = sum(vertices, Vector()) / len(vertices)
    bm = bmesh.new()
    for p in vertices:
        bm.verts.new((centre.x + (p.x - centre.x) * 1.08, centre.y + (p.y - centre.y) * 1.05, p.z + .003))
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    data = bpy.data.meshes.new("Source-shaped work shoe")
    bm.to_mesh(data)
    bm.free()
    data.materials.append(stubs["Boots"])
    shoe = bpy.data.objects.new("Original closed work shoe", data)
    bpy.context.collection.objects.link(shoe)
    bpy.context.view_layer.objects.active = shoe
    bevel = shoe.modifiers.new("Soft shoe edge", "BEVEL")
    bevel.width = .003
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    meshes.append(shoe)
# 制作原创鱼形员工徽章，不引入外部字体或标志素材。
badge_y = front_at(1.245) - .043
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(0, badge_y, 1.245))
badge = bpy.context.object
badge.name = "Original brass fish badge"
badge.scale = (.026, .0035, .011)
badge.data.materials.append(stubs["Brass"])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
meshes.append(badge)
fabric("Fish badge tail", [(-.020, badge_y, 1.245), (-.038, badge_y, 1.258), (-.038, badge_y, 1.232)], [(0, 1, 2)], "Brass", .002)

# 共用一组 2k 颜色/法线图集，让每级 LOD 只需一次不透明 URP 绘制，
# 不为皮肤、眼睛、头发及衣服分别增加多份材质。
SIZE = 2048
albedo = np.ones((SIZE, SIZE, 4), dtype=np.float32)
normal = np.empty_like(albedo)
normal[:] = (.5, .5, 1, 1)
regions = {"Skin": (0, 0, 1024), "Hair": (1024, 1024, 1024), "Eyes": (0, 1024, 256)}
image_files = {
    "Skin": ("T_Superhero_Female_Light_BaseColor.png", "T_Superhero_Female_Normal.png"),
    "Hair": ("T_Hair_2_BaseColor.png", "T_Hair_2_Normal.png"),
    "Eyes": ("T_Eye_Brown.png", "T_Eye_Normal.png"),
}
for key, (x, y, size) in regions.items():
    for target, filename, is_normal in [(albedo, image_files[key][0], False), (normal, image_files[key][1], True)]:
        image = bpy.data.images.load(str(SOURCE / "Textures" / filename), check_existing=False)
        image.colorspace_settings.name = "Non-Color" if is_normal else "sRGB"
        image.scale(size, size)
        pixels = np.empty(size * size * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        target[y:y + size, x:x + size] = pixels.reshape(size, size, 4)
        if key == "Hair" and not is_normal:
            # 源头发图集为便于染色的中性色；选中发型与原眉毛
            # 统一染成自然暖棕色。
            target[y:y + size, x:x + size, :3] *= (.22, .11, .055)
        target[y:y + size, x:x + size, 3] = 1
        bpy.data.images.remove(image)
patches = {}
for i, (key, colour) in enumerate(PALETTE.items()):
    x = 320 + (i % 5) * 128
    y = 1088 + (i // 5) * 128
    albedo[y:y + 96, x:x + 96] = colour
    patches[key] = (x + 48, y + 48)
atlas_images = []
for name, pixels, space in [("ClerkBaseColor", albedo, "sRGB"), ("ClerkNormal", normal, "Non-Color")]:
    image = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=False)
    image.colorspace_settings.name = space
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(OUT / (name + ".png"))
    image.file_format = "PNG"
    image.save()
    atlas_images.append(image)

atlas_mat = bpy.data.materials.new("CoastalClerk")
atlas_mat.use_nodes = True
nodes = atlas_mat.node_tree.nodes
links = atlas_mat.node_tree.links
shader = nodes.get("Principled BSDF")
shader.inputs["Roughness"].default_value = .72
colour = nodes.new("ShaderNodeTexImage")
colour.image = atlas_images[0]
links.new(colour.outputs["Color"], shader.inputs["Base Color"])
tex_normal = nodes.new("ShaderNodeTexImage")
tex_normal.image = atlas_images[1]
map_normal = nodes.new("ShaderNodeNormalMap")
map_normal.inputs["Strength"].default_value = .45
links.new(tex_normal.outputs["Color"], map_normal.inputs["Color"])
links.new(map_normal.outputs["Normal"], shader.inputs["Normal"])
for obj in meshes:
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVMap")
    for polygon in obj.data.polygons:
        key = obj.data.materials[polygon.material_index].name.removeprefix("Cashier_")
        for corner, loop in enumerate(polygon.loop_indices):
            if key in regions:
                x, y, size = regions[key]
                old = uv.data[loop].uv.copy()
                assert -.01 <= old.x <= 1.01 and -.01 <= old.y <= 1.01, "Unexpected source UV outside its texture"
                uv.data[loop].uv = ((x + 2 + old.x * (size - 4)) / SIZE, (y + 2 + old.y * (size - 4)) / SIZE)
            else:
                x, y = patches[key]
                angle = corner * 2 * math.pi / max(3, len(polygon.loop_indices))
                uv.data[loop].uv = ((x + 20 * math.cos(angle)) / SIZE, (y + 20 * math.sin(angle)) / SIZE)
        polygon.material_index = 0
    obj.data.materials.clear()
    obj.data.materials.append(atlas_mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True

bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
lod0 = bpy.context.object
lod0.name = "CoastalClerk_LOD0"
lod0.data.materials.clear()
lod0.data.materials.append(atlas_mat)
for poly in lod0.data.polygons:
    poly.material_index = 0
lod1 = lod0.copy()
lod1.data = lod0.data.copy()
bpy.context.collection.objects.link(lod1)
lod1.name = "CoastalClerk_LOD1"
triangles = lambda o: sum(len(p.vertices) - 2 for p in o.data.polygons)
bpy.context.view_layer.objects.active = lod1
reduction = lod1.modifiers.new("Far static cashier LOD", "DECIMATE")
reduction.ratio = min(1, 9500 / triangles(lod1))
reduction.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=reduction.name)
report = {
    "source_character": "Quaternius Universal Base Characters Standard / Superhero_Female_FullBody",
    "source_character_variant": "Adult Superhero Female; not Regular Female or Teen",
    "source_body_triangles": 12812,
    "source_hair": "Hair_Long",
    "source_hair_triangles": 2906,
    "licence": "CC0-1.0",
    "height_metres": 1.72,
    "pose": "Static standing, arms relaxed using source rig before baking; no runtime skeleton or animation",
    "modifications": "Torso depth softened 8%, width 4%; original opaque shirt, trousers, boots, teal apron/straps/pocket and fish badge; shared albedo/normal atlas",
    "materials_per_active_lod": 1,
    "atlas_size": 2048,
    "lods": [],
}
for obj in (lod0, lod1):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(OUT / (obj.name + ".fbx")), use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
        add_leaf_bones=False, bake_anim=False, path_mode="STRIP", use_mesh_modifiers=True)
    bounds = [obj.matrix_world @ v.co for v in obj.data.vertices]
    report["lods"].append({"name": obj.name, "triangles": triangles(obj), "vertices": len(obj.data.vertices),
        "height": max(v.z for v in bounds) - min(v.z for v in bounds), "feet_z": min(v.z for v in bounds)})
assert report["lods"][0]["triangles"] < 25000 and report["lods"][1]["triangles"] < 12000
assert all(1.69 < lod["height"] < 1.75 and abs(lod["feet_z"]) < .02 for lod in report["lods"])
(SOURCE / "import-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("CASHIER_EXPORT", json.dumps(report, ensure_ascii=False))

if not args.no_preview:
    lod1.hide_render = True
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 800
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("Preview world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.22, .22, .22, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .4
    scene.view_settings.view_transform = "AgX"
    for name, location, energy, size in [("Preview key", (2.5, -3.4, 4), 650, 4), ("Preview fill", (-2, -1, 2.5), 260, 3), ("Preview rim", (0, 2.5, 3), 500, 3)]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0, 0, 1)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("Preview camera")
    camera = bpy.data.objects.new("Preview camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    for name, position, aim, ortho in [("cashier-front.png", (0, -4, 1.0), (0, 0, .90), 1.96), ("cashier-face.png", (.12, -3, 1.5), (0, 0, 1.42), .72)]:
        camera.location = position
        camera.rotation_euler = (Vector(aim) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera_data.ortho_scale = ortho
        scene.render.filepath = str(PREVIEW / name)
        bpy.ops.render.render(write_still=True)
