"""Re-import the portable model in a fresh Blender process and verify exported geometry."""
from pathlib import Path
import bpy,json
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'ArtSource/V027/Coastal-home-v027.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(meshes)==3,[o.name for o in meshes]
triangles=0
for obj in meshes:
    obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    assert len(obj.data.color_attributes)>0,obj.name
    assert all(len(p.vertices)>=3 for p in obj.data.polygons)
assert 20000<triangles<65000,triangles
report={'passed':True,'meshes':[o.name for o in meshes],'triangles':triangles,'vertex_colours':True,'portable_size':(root/'ArtSource/V027/Coastal-home-v027.glb').stat().st_size}
(root/'ArtSource/V027/export-validation.json').write_text(json.dumps(report,indent=2))
print('V027 export re-import PASS',report)
