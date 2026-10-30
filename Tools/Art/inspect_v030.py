"""Read supplied assets in a fresh background Blender process; never execute archive code."""
from pathlib import Path
import bpy, json, zipfile, hashlib
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/V030'; ART.mkdir(parents=True,exist_ok=True)
source=Path('C:/Users/Lenovo/OneDrive/Desktop')
with zipfile.ZipFile(source/'9s.zip') as archive:
    for entry in archive.infolist():
        target=(ART/'9s'/entry.filename).resolve()
        assert target.is_relative_to((ART/'9s').resolve()), entry.filename
    archive.extractall(ART/'9s')
for original,name in [('提灯.glb','lantern-original.glb'),('9s.zip','9s-original.zip')]:
    data=(source/original).read_bytes(); (ART/name).write_bytes(data)
    assert hashlib.sha256(data).digest()==hashlib.sha256((ART/name).read_bytes()).digest()
report=[]
for name,path in [('lantern',ART/'lantern-original.glb'),('9s-glb',ART/'9s/73998847/glbfile.glb'),('9s-fbx',ART/'9s/gas gas gas.fbx'),('old',ROOT/'Assets/Resources/FishingCharacter/BeachCharacter.fbx')]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix=='.glb': bpy.ops.import_scene.gltf(filepath=str(path))
    else: bpy.ops.import_scene.fbx(filepath=str(path))
    objects=[]
    for obj in bpy.data.objects:
        item={'name':obj.name,'type':obj.type,'scale':list(obj.scale),'rotation':list(obj.rotation_euler)}
        if obj.type=='MESH':
            points=[obj.matrix_world@Vector(p) for p in obj.bound_box]
            item.update(vertices=len(obj.data.vertices),triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),min=[min(p[i] for p in points) for i in range(3)],max=[max(p[i] for p in points) for i in range(3)],materials=[m.name if m else None for m in obj.data.materials],groups=[g.name for g in obj.vertex_groups])
        if obj.type=='ARMATURE':
            item['bones']=[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(obj.matrix_world@b.head_local),'tail':list(obj.matrix_world@b.tail_local)} for b in obj.data.bones]
        objects.append(item)
    materials=[]
    for mat in bpy.data.materials:
        if not mat.use_nodes: continue
        materials.append({'name':mat.name,'images':[{'name':n.image.name,'size':list(n.image.size),'file':n.image.filepath} for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]})
    report.append({'asset':name,'objects':objects,'materials':materials,'actions':[{'name':a.name,'range':list(a.frame_range)} for a in bpy.data.actions]})
    print(name,[(o['name'],o['type'],o.get('vertices')) for o in objects],flush=True)
(ART/'inspection.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('INSPECTION PASS',flush=True)
