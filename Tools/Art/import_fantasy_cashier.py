"""Convert the user-supplied static character to posed, metre-scale Unity LODs.
Run with Blender --background --disable-autoexec ArtSource/FantasyGirl/FantasyGirl.blend
--python-exit-code 1 --python Tools/Art/import_fantasy_cashier.py. Never modifies source.
"""
import bpy, math, json, shutil
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FishingCashier/FantasyGirl'
TEX=ROOT/'ArtSource/FantasyGirl/Textures'
LOG=ROOT/'Logs/FantasyGirl'
OUT.mkdir(parents=True,exist_ok=True);LOG.mkdir(parents=True,exist_ok=True)
obj=bpy.data.objects.get('FantasyGirl')
assert obj is not None and obj.type=='MESH'
for o in list(bpy.data.objects):
    if o!=obj:bpy.data.objects.remove(o,do_unlink=True)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Mesh is Z-up, facing +X; preserve UVs. Relax arms in the source's unrigged T-pose.
coords=np.empty(len(obj.data.vertices)*3,dtype=np.float32)
obj.data.vertices.foreach_get('co',coords);coords=coords.reshape(-1,3)
side=np.sign(coords[:,1]);dist=np.abs(coords[:,1])
weight=np.clip((dist-.065)/.04,0,1);weight=weight*weight*(3-2*weight)
height=np.clip((coords[:,2]-.55)/.07,0,1);height=height*height*(3-2*height)
upper=np.clip((.85-coords[:,2])/.055,0,1);upper=upper*upper*(3-2*upper)
angle=-side*math.radians(60)*weight*height*upper
pivot_y=side*.08;pivot_z=.756
old_y=coords[:,1]-pivot_y;old_z=coords[:,2]-pivot_z
coords[:,1]=pivot_y+old_y*np.cos(angle)-old_z*np.sin(angle)
coords[:,2]=pivot_z+old_y*np.sin(angle)+old_z*np.cos(angle)
obj.data.vertices.foreach_set('co',coords.reshape(-1));obj.data.update()
# Feet at zero, standard height, front -Y in Blender (+Z in Unity).
height=float(coords[:,2].max()-coords[:,2].min());scale=1.72/height
coords[:,2]-=coords[:,2].min();coords*=scale
x=coords[:,0].copy();coords[:,0]=coords[:,1];coords[:,1]=-x
obj.data.vertices.foreach_set('co',coords.reshape(-1));obj.data.update()
for image in bpy.data.images:
    filename=image.name
    p=TEX/filename
    if p.is_file():image.filepath=str(p);image.reload()
# Keep normal/roughness/metallic data linear; original source used image colour defaults.
for mat in obj.data.materials:
    if not mat or not mat.use_nodes:continue
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image and 'basecolor' not in node.image.name:
            node.image.colorspace_settings.name='Non-Color'
for kind in ('basecolor','normal'):
    shutil.copyfile(TEX/f'FantasyGirl_01_{kind}.jpg',OUT/f'FantasyGirl_{kind}.jpg')
# URP metallic map: R=metalness, A=1-roughness. Blender reads these as non-colour.
metal=bpy.data.images['FantasyGirl_01_metallic.jpg'];rough=bpy.data.images['FantasyGirl_01_roughness.jpg']
assert list(metal.size)==list(rough.size)
m=np.empty(metal.size[0]*metal.size[1]*4,dtype=np.float32);r=np.empty_like(m)
metal.pixels.foreach_get(m);rough.pixels.foreach_get(r)
m=m.reshape(-1,4);r=r.reshape(-1,4);m[:,1]=m[:,0];m[:,2]=m[:,0];m[:,3]=1-r[:,0]
packed=bpy.data.images.new('Unity metallic smoothness',width=metal.size[0],height=metal.size[1],alpha=True)
packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(m.reshape(-1));packed.file_format='PNG';packed.filepath_raw=str(OUT/'FantasyGirl_metallic_smoothness.png');packed.save()
source_triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
report={'source_triangles':source_triangles,'height_metres':1.72,'rigged':False,'pose':'Static arms relaxed; no skeleton/animation fabricated','lods':[]}
models=[]
for index,target in enumerate((70000,22000)):
    lod=obj.copy();lod.data=obj.data.copy();bpy.context.collection.objects.link(lod);lod.name=f'FantasyGirl_LOD{index}'
    bpy.ops.object.select_all(action='DESELECT');lod.select_set(True);bpy.context.view_layer.objects.active=lod
    mod=lod.modifiers.new('Game mesh reduction','DECIMATE');mod.ratio=target/source_triangles;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Recalculate geometry normals after posing. Imported normal map preserves surface detail.
    if lod.data.has_custom_normals:bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for p in lod.data.polygons:p.use_smooth=True
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'FantasyGirl_LOD{index}.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
    report['lods'].append({'name':lod.name,'triangles':sum(len(p.vertices)-2 for p in lod.data.polygons),'vertices':len(lod.data.vertices)})
    models.append(lod)
bpy.data.objects.remove(obj,do_unlink=True);models[1].hide_render=True
# Render the actual reduced posed geometry with restored original textures.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(.20,.20,.20)
def area(name,pos,power,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(3,-4,4),400,4);area('Fill',(-3,-1,3),220,3);area('Rim',(1,3,3),300,2)
d=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO';d.ortho_scale=2.05
cam.location=(.4,-4,1.05);cam.rotation_euler=(Vector((0,0,.86))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(LOG/'textured-posed.png');bpy.ops.render.render(write_still=True)
(ROOT/'ArtSource/FantasyGirl/import-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('CASHIER EXPORT PASS',report)
