"""Render one EEVEE thumbnail per runtime FBX for a visual art audit (read-only on Assets/).

Usage: blender --background --factory-startup --python Tools/Art/audit_model_thumbs.py -- <out_dir> [subfolder_filter]
Writes <out_dir>/<index>.png plus index.json mapping index -> asset path.
"""
import bpy, sys, json, math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
args = sys.argv[sys.argv.index('--') + 1:]
OUT = Path(args[0]); OUT.mkdir(parents=True, exist_ok=True)
FILTER = args[1] if len(args) > 1 else ''
files = sorted(p for p in (ROOT / 'Assets/Resources').rglob('*.fbx') if FILTER in p.as_posix())


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.render.resolution_x = s.render.resolution_y = 384
    s.render.film_transparent = False
    s.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('W'); s.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (.55, .6, .66, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.2
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(35)))
    bpy.context.object.data.energy = 3.5


index = {}
for i, f in enumerate(files):
    setup()
    try:
        bpy.ops.import_scene.fbx(filepath=str(f))
    except Exception as e:
        index[i] = {'path': f.relative_to(ROOT).as_posix(), 'error': str(e)}; continue
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        index[i] = {'path': f.relative_to(ROOT).as_posix(), 'error': 'no mesh'}; continue
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector(map(min, *[p.to_tuple() for p in pts])) if len(pts) > 1 else pts[0]
    hi = Vector(map(max, *[p.to_tuple() for p in pts])) if len(pts) > 1 else pts[0]
    centre, span = (lo + hi) / 2, max((hi - lo).length, 1e-4)
    d = Vector((1.0, -1.25, .8)).normalized()
    cam = bpy.data.objects.new('C', bpy.data.cameras.new('C'))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = centre + d * span * 1.35
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_start, cam.data.clip_end = span * .01, span * 20
    bpy.context.scene.camera = cam
    bpy.context.scene.render.filepath = str(OUT / f'{i:03d}.png')
    bpy.ops.render.render(write_still=True)
    index[i] = {'path': f.relative_to(ROOT).as_posix(), 'tris': sum(len(o.data.polygons) for o in meshes),
                'size_m': [round(v, 3) for v in (hi - lo)]}

(OUT / 'index.json').write_text(json.dumps(index, indent=1, ensure_ascii=False), encoding='utf-8')
