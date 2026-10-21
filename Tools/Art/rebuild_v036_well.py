"""Regenerate only the two source assets changed by the V036 deep-well fix."""
import importlib.util
from pathlib import Path
import bpy
script=Path(__file__).with_name('build_v035_assets.py')
spec=importlib.util.spec_from_file_location('well_source',script)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
module.SOURCE=module.ROOT/'ArtSource/V036/Well';module.SOURCE.mkdir(parents=True,exist_ok=True)
module.setup()
for name in ['HomeLand','GardenFixtures']:module.build(name)
for obj in bpy.context.scene.objects:obj.select_set(obj.type=='MESH')
bpy.ops.export_scene.gltf(filepath=str(module.SOURCE/'deep-well-and-land.glb'),export_format='GLB',use_selection=True)
bpy.ops.wm.save_as_mainfile(filepath=str(module.SOURCE/'deep-well-v036.blend'))
print('V036 DEEP WELL export PASS')
