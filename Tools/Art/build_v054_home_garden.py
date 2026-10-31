"""Repair the existing metre-scale cottage and replace mature-looking seedlings.

Run with Blender --background --python Tools/Art/build_v054_home_garden.py.
Only the two runtime FBXs and their scoped art records are written.
"""
from pathlib import Path
import importlib.util
import hashlib
import json
import bpy

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV035'
EVIDENCE = ROOT / 'Logs/V054/HomeGarden'
EVIDENCE.mkdir(parents=True, exist_ok=True)
spec = importlib.util.spec_from_file_location('v054_kit', ROOT / 'Tools/Art/build_v035_assets.py')
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)
kit.SCENE = 'V054 home and seedling repair'
kit.SOURCE = EVIDENCE
kit.setup()

sprout = kit.build('Turnip0')
sprout.hide_render = False
sprout_record = kit.REPORT[-1]
# Keep the earlier source-integrity regression aligned with this deliberate replacement.
manifest_path = ROOT / 'ArtSource/Feedback-20261026/P0Art-manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
for record in manifest['models']:
    if record['name'] == 'Turnip0':
        record.update(sprout_record)
manifest['seedlingRepairV054'] = 'Four low double-cotyledon sprouts; mature Turnip1/2 sources unchanged.'
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

# Reuse the existing source builder: floor, doorway and furnishing coordinates stay exact.
cottage = kit.build('CottageShell')
objects = [cottage] + list(cottage.children)
mesh = cottage.data
mesh.name = 'GEO-CottageShell'
bpy.context.view_layer.update()
painted = sum(poly.material_index == 1 for poly in mesh.polygons)
assert painted > 20, painted
mesh.calc_loop_triangles()
assert len(mesh.vertices) > 0
report = {'seedling': sprout_record, 'cottageTriangles': len(mesh.loop_triangles),
          'paintedWallFaces': painted, 'cottageMaterials': len(mesh.materials),
          'cottageSha256': hashlib.sha256((OUT / 'CottageShell.fbx').read_bytes()).hexdigest()}
(EVIDENCE / 'geometry.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE / 'home-garden-v054.blend'))
print('V054 HOME GARDEN', json.dumps(report))
