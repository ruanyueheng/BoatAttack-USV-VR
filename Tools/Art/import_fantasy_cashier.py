"""Convert the user-supplied static character to posed, metre-scale Unity LODs.
Run with Blender --background --disable-autoexec ArtSource/FantasyGirl/FantasyGirl.blend
--python-exit-code 1 --python Tools/Art/import_fantasy_cashier.py. Never modifies source.
"""
import bpy, math, json, shutil, sys
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
# Mesh is Z-up, facing +X. Pose the original vertices; retain UVs and fingers.
coords=np.empty(len(obj.data.vertices)*3,dtype=np.float32)
obj.data.vertices.foreach_get('co',coords);coords=coords.reshape(-1,3)
def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1)
    return t*t*(3-2*t)

original=coords.copy()
pose_landmarks=[]
for side in (-1,1):
    # Joint axes follow the centre of the original T-pose flesh, not its lower
    # silhouette. A pivot below the deltoid swung the shoulder cap outward.
    shoulder=Vector((.005,side*.085,.790))
    target_shoulder=Vector((.005,side*.085,.770))
    elbow=Vector((.005,side*.190,.768))
    wrist=Vector((.012,side*.284,.742))
    # Slight asymmetry lets one hand rest on the other instead of occupying it.
    target_wrist=Vector((.092 if side==1 else .087,side*.025,.637 if side==1 else .622))
    upper_length=(elbow-shoulder).length
    lower_length=(wrist-elbow).length
    direction=(target_wrist-target_shoulder).normalized()
    distance=(target_wrist-target_shoulder).length
    assert distance < upper_length+lower_length
    along=(upper_length**2-lower_length**2+distance**2)/(2*distance)
    # Elbows stay beside/slightly behind the torso. The forearms turn forward
    # from those joints, instead of following one continuous circular arc.
    outward=Vector((-.35,side,0))
    outward=(outward-direction*outward.dot(direction)).normalized()
    target_elbow=target_shoulder+direction*along+outward*math.sqrt(max(0,upper_length**2-along**2))
    upper_rotation=(elbow-shoulder).rotation_difference(target_elbow-target_shoulder).to_matrix()
    lower_rotation=(wrist-elbow).rotation_difference(target_wrist-target_elbow).to_matrix()
    # Palms lie across the lower abdomen, fingers angled gently down and inward.
    hand_direction=Vector((.012,-side*.070,-.023)).normalized()
    hand_rotation=Vector((0,side,0)).rotation_difference(hand_direction).to_matrix()
    def transformed(rotation,source_pivot,target_pivot):
        return (original-np.array(source_pivot))@np.array(rotation).T+np.array(target_pivot)
    upper_coords=transformed(upper_rotation,shoulder,target_shoulder)
    lower_coords=transformed(lower_rotation,elbow,target_elbow)
    hand_coords=transformed(hand_rotation,wrist,target_wrist)
    lateral=side*original[:,1]
    # Keep both limb segments rigid; blend only within a small joint crease.
    # The previous broad blend rounded ~8 cm of sleeve into a rubber loop.
    elbow_blend=smooth(.187,.195,lateral)[:,None]
    wrist_blend=smooth(.281,.290,lateral)[:,None]
    posed=upper_coords*(1-elbow_blend)+lower_coords*elbow_blend
    posed=posed*(1-wrist_blend)+hand_coords*wrist_blend
    sleeve_floor=.665-.105*smooth(.125,.245,lateral)
    weight=smooth(.080,.100,lateral)*smooth(sleeve_floor,sleeve_floor+.028,original[:,2])*(1-smooth(.807,.850,original[:,2]))
    coords+=((posed-original)*weight[:,None]).astype(np.float32)
    elbow_flex=math.degrees((target_elbow-target_shoulder).angle(target_wrist-target_elbow))
    assert 55 < elbow_flex < 100, 'Forearm must visibly articulate at the elbow'
    pose_landmarks.append({'side':side,'source_shoulder':list(shoulder),'shoulder':list(target_shoulder),'elbow':list(target_elbow),'wrist':list(target_wrist),'elbow_flex_degrees':elbow_flex})
obj.data.vertices.foreach_set('co',coords.reshape(-1));obj.data.update()
# Feet at zero, standard height, front -Y in Blender (+Z in Unity).
height=float(coords[:,2].max()-coords[:,2].min());scale=1.98/height
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
report={'source_triangles':source_triangles,'height_metres':1.98,'rigged':False,'pose':'Hands overlapped at lower abdomen; anatomical shoulder centres, lowered upper arms and localized elbow creases','source_space_landmarks':pose_landmarks,'lods':[]}
models=[]
for index,target in enumerate((70000,22000)):
    lod=obj.copy();lod.data=obj.data.copy();bpy.context.collection.objects.link(lod);lod.name=f'FantasyGirl_LOD{index}'
    bpy.ops.object.select_all(action='DESELECT');lod.select_set(True);bpy.context.view_layer.objects.active=lod
    mod=lod.modifiers.new('Game mesh reduction','DECIMATE');mod.ratio=target/source_triangles;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Recalculate geometry normals after posing. Imported normal map preserves surface detail.
    if lod.data.has_custom_normals:bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for p in lod.data.polygons:p.use_smooth=True
    if '--preview-only' not in sys.argv:
        bpy.ops.export_scene.fbx(filepath=str(OUT/f'FantasyGirl_LOD{index}.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
    report['lods'].append({'name':lod.name,'triangles':sum(len(p.vertices)-2 for p in lod.data.polygons),'vertices':len(lod.data.vertices)})
    models.append(lod)
bpy.data.objects.remove(obj,do_unlink=True);models[1].hide_render=True
# Render the actual reduced posed geometry with restored original textures.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=800;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world.color=(.20,.20,.20)
def area(name,pos,power,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(3,-4,4),400,4);area('Fill',(-3,-1,3),220,3);area('Rim',(1,3,3),300,2)
d=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO';d.ortho_scale=2.25
cam.location=(0,-4,1.12);cam.rotation_euler=(Vector((0,0,.99))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(LOG/'textured-posed.png');bpy.ops.render.render(write_still=True)
d.ortho_scale=1.25
for label,position in [('joint-front',(0,-4,1.40)),('joint-side',(3,-2,1.40))]:
    cam.location=position;cam.rotation_euler=(Vector((0,0,1.40))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(LOG/(label+'.png'));bpy.ops.render.render(write_still=True)
(ROOT/'ArtSource/FantasyGirl/import-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('CASHIER EXPORT PASS',report)
