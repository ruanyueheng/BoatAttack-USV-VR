"""Clean a Hunyuan/Tripo GLB into a project asset: join, decimate, scale, orient, rebake basecolor, export FBX + Albedo.
Usage: blender --background --factory-startup --python-exit-code 1 --python import_v044_hunyuan.py -- <glb> <name>
Profiles below set the Resources folder, metres, triangle budget and texture size per asset.
GLBs are untrusted downloads: only geometry and images are read, nothing is executed.
"""
from pathlib import Path
import math, sys
import bpy, bmesh
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / 'Assets/Resources'
SHOTS = ROOT / 'Logs/V044/models'
# name: (folder, size metres, measured axis, triangle budget, texture px, portal aperture cut, Z turn deg so the head faces +Y (Unity +Z), waterline m above keel or None=centre)
PROFILES = {
    'PortalFrame0': ('FishingV044', 3.7, 'Z', 6000, 512, True, 0, None), 'PortalFrame2': ('FishingV044', 3.65, 'Z', 6000, 512, True, 0, None),
    'Whale': ('FishingV044/Sea', 12.0, 'Y', 8000, 1024, False, 180, None), 'Dolphin': ('FishingV044/Sea', 2.2, 'Y', 3000, 512, False, 180, None),
    'GhostShip': ('FishingV044/Sea', 16.0, 'Y', 8000, 1024, False, -90, 1.6),
}
R_IN, SHOULDER, JAMB = .975, 2.12, .995  # portal contract (FishingPortalVisual, vfx)

glb, name = sys.argv[sys.argv.index('--') + 1:][:2]
folder, metres, axis, budget, px, portal, turn, waterline = PROFILES[name]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.object; obj.name = 'GEO-' + name
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM'); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in bpy.context.scene.objects if o != obj]: bpy.data.objects.remove(o)

# keep the source material as the bake source
src_mat = obj.data.materials[0] if obj.data.materials else None

# scale so the measured axis spans `metres`, then sit on the ground (portal) or centre at waterline (sea)
obj.data.transform(Matrix.Rotation(math.radians(turn), 4, 'Z')); obj.data.update(); bpy.context.view_layer.update()
dims = obj.dimensions; i = 'XYZ'.index(axis)
obj.data.transform(Matrix.Scale(metres / dims[i], 4)); obj.data.update()
pts = [v.co for v in obj.data.vertices]
lo = Vector([min(p[k] for p in pts) for k in range(3)]); hi = Vector([max(p[k] for p in pts) for k in range(3)])
centre = (lo + hi) / 2
obj.data.transform(Matrix.Translation((-centre.x, -centre.y, -lo.z if portal else -(lo.z + waterline) if waterline else -centre.z))); obj.data.update()

if portal:  # carve the exact aperture: vertical jambs, semicircular head, deep enough to pass through
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, SHOULDER / 2 - .5)); cut = bpy.context.object
    cut.scale = (JAMB * 2, 4, SHOULDER + 1); bpy.ops.object.transform_apply(scale=True)
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=R_IN, depth=4, location=(0, 0, SHOULDER), rotation=(math.pi / 2, 0, 0)); arc = bpy.context.object
    for c in (cut, arc):
        m = obj.modifiers.new('aperture', 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = c; m.solver = 'EXACT'
        bpy.context.view_layer.objects.active = obj; bpy.ops.object.modifier_apply(modifier=m.name); bpy.data.objects.remove(c)

tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
if tris > budget:
    m = obj.modifiers.new('budget', 'DECIMATE'); m.ratio = budget * .95 / tris; m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj; bpy.ops.object.modifier_apply(modifier=m.name)
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
assert tris <= budget, (name, tris)

# rebake basecolor onto fresh UVs at the target size
old_uv = obj.data.uv_layers.active.name if obj.data.uv_layers else None
new = obj.data.uv_layers.new(name='PaintUV'); obj.data.uv_layers.active = new
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.004); bpy.ops.object.mode_set(mode='OBJECT')
img = bpy.data.images.new(name + 'Albedo', px, px)
if src_mat:
    t = src_mat.node_tree; tex = t.nodes.new('ShaderNodeTexImage'); tex.image = img; t.nodes.active = tex
    for n in t.nodes:  # sample the source texture through its original UV map
        if n.type == 'TEX_IMAGE' and n is not tex and old_uv:
            uvn = t.nodes.new('ShaderNodeUVMap'); uvn.uv_map = old_uv; t.links.new(uvn.outputs['UV'], n.inputs['Vector'])
    s = bpy.context.scene; s.render.engine = 'CYCLES'; s.cycles.samples = 1; s.render.bake.margin = 4
    s.cycles.bake_type = 'DIFFUSE'; s.render.bake.use_pass_direct = False; s.render.bake.use_pass_indirect = False
    obj.select_set(True); bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
out = RES / folder; out.mkdir(parents=True, exist_ok=True)
img.filepath_raw = str(out / (name + 'Albedo.png')); img.file_format = 'PNG'; img.save()
for layer in [l for l in obj.data.uv_layers if l.name != 'PaintUV']: obj.data.uv_layers.remove(layer)
# vertex colours for runtime fallbacks and the palette shader's colour count checks
col = obj.data.color_attributes.new('Color', 'BYTE_COLOR', 'CORNER')
pix = list(img.pixels); uv = obj.data.uv_layers['PaintUV']
for li, loop in enumerate(uv.data):
    x = min(px - 1, int(loop.uv[0] * px)); y = min(px - 1, int(loop.uv[1] * px)); k = (y * px + x) * 4
    col.data[li].color = (pix[k], pix[k + 1], pix[k + 2], 0)
obj.data.materials.clear(); mat = bpy.data.materials.new('MAT-vertex-preview'); obj.data.materials.append(mat)
mat.use_nodes = True; tn = mat.node_tree.nodes.new('ShaderNodeTexImage'); tn.image = img
mat.node_tree.links.new(tn.outputs['Color'], mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / (name + '.fbx')), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                         bake_space_transform=True, add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='STRIP')
if name == 'Whale':  # tail fluke as a child pivoting at the peduncle, for sea-events' swim/dive animation
    cut = -metres * .36
    bm = bmesh.new(); bm.from_mesh(obj.data)
    tail = [f for f in bm.faces if all(v.co.y < cut for v in f.verts)]
    zc = sum(v.co.z for f in tail for v in f.verts) / max(1, sum(len(f.verts) for f in tail))
    fl = bmesh.new(); fl_uv = fl.loops.layers.uv.new('PaintUV'); fl_col = fl.loops.layers.color.new('Color')
    uv0 = bm.loops.layers.uv['PaintUV']; col0 = bm.loops.layers.color['Color']; vmap = {}
    for f in tail:
        vs = [vmap.setdefault(v, fl.verts.new(v.co - Vector((0, cut, zc)))) for v in f.verts]
        nf = fl.faces.new(vs)
        for a, b in zip(f.loops, nf.loops): b[fl_uv].uv = a[uv0].uv; b[fl_col] = a[col0]
    # body keeps a 0.5 m sleeve past the cut so the swinging fluke never opens a seam
    bmesh.ops.delete(bm, geom=[f for f in tail if any(v.co.y < cut - .5 for v in f.verts)], context='FACES')
    for v in bm.verts:  # thicken the 0.5 m sleeve slightly so the fluke root stays inside it while swinging
        if cut - .5 <= v.co.y <= cut + .1: k = 1.06; v.co.x *= k; v.co.z = zc + (v.co.z - zc) * k
    bm.to_mesh(obj.data); bm.free()
    me = bpy.data.meshes.new('Fluke'); fl.to_mesh(me); fl.free(); me.materials.append(mat)
    fluke = bpy.data.objects.new('Fluke', me); bpy.context.scene.collection.objects.link(fluke); fluke.parent = obj; fluke.location = (0, cut, zc)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); fluke.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(out / (name + '.fbx')), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='STRIP')
print('HUNYUAN_IMPORT', name, tris, [round(v, 3) for v in obj.dimensions])
