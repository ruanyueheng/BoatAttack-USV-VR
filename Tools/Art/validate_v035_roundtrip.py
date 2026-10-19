"""Run inside Blender after exporting the owned V035 gallery."""
import bpy,json,math
from mathutils import Vector
ns=bpy.app.driver_namespace['v035'];original=bpy.context.scene
before_objects=set(bpy.data.objects);before_meshes=set(bpy.data.meshes);before_materials=set(bpy.data.materials);before_images=set(bpy.data.images)
scene=bpy.data.scenes.new('V035 export roundtrip validation');bpy.context.window.scene=scene
try:
    bpy.ops.import_scene.gltf(filepath=str(ns['SOURCE']/'coastal-craftsmanship-kit.glb'))
    imported=[o for o in scene.objects if o.type=='MESH'];assert len(imported)==len(ns['MODELS'])
    error=0;triangles=0
    def bounds(obj):
        points=[obj.matrix_world@Vector(p) for p in obj.bound_box]
        return [min(p[a] for p in points) for a in range(3)]+[max(p[a] for p in points) for a in range(3)]
    bpy.context.view_layer.update()
    for name,source in ns['MODELS'].items():
        candidates=[o for o in imported if o.name==source.name or o.name.startswith(source.name+'.')]
        assert len(candidates)==1,(name,[o.name for o in candidates])
        target=candidates[0];delta=max(abs(a-b) for a,b in zip(bounds(source),bounds(target)));error=max(error,delta)
        assert delta<.002,(name,delta)
        assert target.data.uv_layers and target.data.materials
        assert all(math.isfinite(v) for vertex in target.data.vertices for v in vertex.co)
        triangles+=sum(len(p.vertices)-2 for p in target.data.polygons)
    assert triangles==sum(r['triangles'] for r in ns['REPORT'])
    report={'passed':True,'models':len(imported),'triangles':triangles,'maxWorldBoundsErrorM':error,'uvs':True,'materials':True,'finiteVertices':True}
    (ns['SOURCE']/'roundtrip-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
finally:
    bpy.context.window.scene=original
    for obj in set(bpy.data.objects)-before_objects:bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(scene)
    for collection,before in [(bpy.data.meshes,before_meshes),(bpy.data.materials,before_materials),(bpy.data.images,before_images)]:
        for item in set(collection)-before:
            if item.users==0:collection.remove(item)
