"""Import only the supplied Tripo asset in a fresh background Blender process."""
from pathlib import Path
import bpy, bmesh, sys, json, math, hashlib, struct
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/V032';OUT=ROOT/'Assets/Resources/FishingV032'
ART.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
assert not bpy.data.filepath, 'Requires a new --factory-startup process'
for obj in list(bpy.context.scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
source=ART/'OriginalCoastalArch.glb'
bpy.ops.import_scene.gltf(filepath=str(source))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(objects)==1
obj=objects[0];obj.name='GEO-CoastalArch'
before=sum(len(p.vertices)-2 for p in obj.data.polygons)
assert before==467496
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world.color=(.18,.22,.26)
bounds=[obj.matrix_world@Vector(c) for c in obj.bound_box]
center=sum(bounds,Vector())/8;span=max((max(v[i] for v in bounds)-min(v[i] for v in bounds)) for i in range(3))
bpy.ops.object.camera_add(location=center+Vector((1.45,-2.5,1.05))*span)
camera=bpy.context.object;camera.name='CAM-ArchPreview';camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=span*1.4;scene.camera=camera
for name,offset,power,size in [('Key',(-2,-3,4),600,4),('Fill',(3,-1,2),300,3)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(offset)*span)
    light=bpy.context.object;light.name='LGT-'+name;light.data.energy=power*span**2;light.data.shape='DISK';light.data.size=size*span
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
if '--optimize' not in sys.argv:
    scene.render.filepath=str(ART/'arch-original-preview.png');bpy.ops.render.render(write_still=True)
    print('[V032-ART] INSPECT',before,list(obj.dimensions),flush=True)
else:
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    mesh=bmesh.new();mesh.from_mesh(obj.data);bmesh.ops.remove_doubles(mesh,verts=list(mesh.verts),dist=.00001);mesh.to_mesh(obj.data);mesh.free()
    modifier=obj.modifiers.new('Game budget 20k triangles','DECIMATE');modifier.ratio=20000/before
    modifier.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=modifier.name)
    after=sum(len(p.vertices)-2 for p in obj.data.polygons)
    print('[V032-ART] DECIMATE',before,after,flush=True)
    assert 15000<after<22000,after
    for item in bpy.context.selected_objects:item.select_set(False)
    obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/'CoastalArch.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True)
    bpy.ops.export_scene.gltf(filepath=str(ART/'coastal-arch-game.glb'),use_selection=True,export_format='GLB',export_animations=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART/'coastal-arch-v032.blend'))
    scene.render.filepath=str(ART/'arch-game-preview.png');bpy.ops.render.render(write_still=True)
    report={'sourceTriangles':before,'gameTriangles':after,'dimensions':list(obj.dimensions),'originalSha256':hashlib.sha256(source.read_bytes()).hexdigest()}
    (ART/'arch-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('[V032-ART] PASS',report,flush=True)
