"""Original hinged field guide, run in a fresh background Blender process."""
from pathlib import Path
import bpy
from mathutils import Vector
import math, json, hashlib

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FishingV031'
ART=ROOT/'ArtSource/V031'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
assert not bpy.data.filepath, 'Use --factory-startup; never modify an existing artist file'
# Only the unchanged startup scene belongs to this background helper.
for obj in list(scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
scene.unit_settings.system='METRIC'
mat=bpy.data.materials.new('MAT-V031 book vertex palette');mat.use_nodes=True
bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bsdf.inputs['Roughness'].default_value=.72
vertex=mat.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
mat.node_tree.links.new(vertex.outputs['Color'],bsdf.inputs['Base Color'])
TEAL=(.08,.25,.24);PAPER=(.93,.86,.69);GOLD=(.75,.53,.26);INK=(.17,.32,.30)
parts=[];models=[]
def U(p):return Vector((p[0],p[2],p[1]))
def paint(obj,color):
    attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    for c in attr.data:c.color_srgb=(*color,1)
    obj.data.color_attributes.active_color=attr;obj.data.materials.append(mat);parts.append(obj);return obj
def box(name,pos,size,color,bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1,location=U(pos));obj=bpy.context.object;obj.name='GEO-'+name
    obj.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Soft clothbound corners','BEVEL');mod.width=bevel;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    return paint(obj,color)
def tube(name,a,b,r,color):
    a,b=U(a),U(b);bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=(b-a).length,location=(a+b)*.5)
    obj=bpy.context.object;obj.name='GEO-'+name;obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return paint(obj,color)
def finish(name):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:p.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    path=OUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True)
    tris=sum(len(p.vertices)-2 for p in obj.data.polygons);assert 0<tris<12000
    models.append({'name':name,'triangles':tris,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()});parts.clear();return obj
# Body coordinates match the existing 0.48-scale two-hand grip.
box('back cloth cover',(0,-.014,0),(.44,.018,.52),TEAL,.006)
box('ivory deckled pages',(0,.012,0),(.412,.037,.49),PAPER,.004)
for i in range(7):box('individual page edge',(0,-.001+i*.004,.246),(.406,.0008,.0015),GOLD,0)
box('rounded spine',(-.218,.010,0),(.025,.064,.52),TEAL,.006)
box('terracotta bookmark',(.13,.029,-.253),(.024,.002,.075),(.67,.28,.16),0)
# Naturalist illustration on the exposed page: fish, leaf sprig and ruled observations.
outline=[(-.12,.03),(-.075,.075),(.015,.082),(.08,.03),(.015,-.015),(-.075,-.01),(-.12,.03)]
for a,b in zip(outline,outline[1:]):tube('naturalist fish silhouette',(a[0],.032,a[1]),(b[0],.032,b[1]),.0019,INK)
for sign in (-1,1):tube('fish tail',(.08,.032,.03),(.135,.032,.03+sign*.04),.0019,INK)
tube('fish tail edge',(.135,.032,-.01),(.135,.032,.07),.0019,INK)
box('fish eye',(-.079,.034,.037),(.006,.001,.006),INK,.001)
for j in range(5):tube('fish scales',(-.033+j*.019,.033,.006),(-.042+j*.019,.033,.056),.0007,GOLD)
for z in [-.12,-.10,-.08,.14,.16]:tube('observation line',(-.13,.033,z),(.13,.033,z),.0007,GOLD)
body=finish('FieldGuide')
# Cover and flyleaf are authored relative to the spine hinge, along local +X.
box('front cloth cover',(.218,0,0),(.44,.014,.52),TEAL,.006)
for x in [.025,.409]:box('gilt border',(x,.008,0),(.002,.001,.472),GOLD,0)
for z in [-.235,.235]:box('gilt border',(.217,.008,z),(.385,.001,.002),GOLD,0)
for sign in (-1,1):
    tube('compass diamond',(.218,.010,-.095),(.218+sign*.060,.010,0),.002,GOLD)
    tube('compass diamond',(.218+sign*.060,.010,0),(.218,.010,.095),.002,GOLD)
tube('compass needle',(.218,.010,-.12),(.218,.010,.12),.0014,GOLD)
cover=finish('GuideCover')
box('turning ivory flyleaf',(.204,0,0),(.40,.002,.482),PAPER,.0005)
for z in [-.14,-.12,-.10,.10,.12,.14]:tube('flyleaf notation',(.06,.002,z),(.34,.002,z),.0006,GOLD)
leaf=finish('GuideLeaf')
cover.location=U((-.215,.045,0));leaf.location=U((-.208,.036,0))
cover.rotation_euler.y=math.radians(-168);leaf.rotation_euler.y=math.radians(-151)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=960;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True
scene.view_settings.view_transform='Standard'
world=bpy.data.worlds.new('V031 neutral studio');world.use_nodes=True;scene.world=world
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.6
data=bpy.data.cameras.new('CAM-Guide');camera=bpy.data.objects.new('CAM-Guide',data);scene.collection.objects.link(camera);scene.camera=camera
camera.location=(-.40,-.78,.79);target=Vector((-.20,0,.06));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=1.1
for name,pos,energy in [('key',(-1,-2,3),160),('fill',(2,0,2),90)]:
    light=bpy.data.lights.new('LGT-'+name,'AREA');light.energy=energy;light.size=3;obj=bpy.data.objects.new('LGT-'+name,light);scene.collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(ART/'guide-open.png');bpy.ops.render.render(write_still=True)
cover.rotation_euler.y=0;leaf.rotation_euler.y=0
scene.render.filepath=str(OUT/'FieldGuideThumbnail.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'field-guide-v031.blend'))
(ART/'guide-models.json').write_text(json.dumps(models,indent=2),encoding='utf-8')
print('[V031-ART] PASS:',models,flush=True)

# Marine portrait follows the game's original procedural dolphin proportions.
for model in (body,cover,leaf):model.hide_render=True
def ell(name,pos,size,color):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=1,location=U(pos))
    obj=bpy.context.object;obj.name='GEO-'+name;obj.scale=(size[0],size[2],size[1])
    for face in obj.data.polygons:face.use_smooth=True
    return paint(obj,color)
gray=(.27,.48,.57)
ell('dolphin body',(0,0,0),(.27,.30,1.02),gray)
ell('dolphin belly',(0,-.16,.20),(.235,.11,.72),(.70,.80,.80))
ell('dolphin beak',(0,0,1.02),(.115,.10,.35),gray)
mesh=bpy.data.meshes.new('dolphin dorsal');mesh.from_pydata([U((0,.22,.13)),U((0,.68,-.21)),U((0,.22,-.54))],[],[(0,1,2),(2,1,0)])
obj=bpy.data.objects.new('GEO-dolphin dorsal fin',mesh);scene.collection.objects.link(obj);paint(obj,gray)
for side in (-1,1):
    obj=ell('dolphin flipper',(side*.30,-.14,.22),(.39,.035,.14),gray);obj.rotation_euler.z=side*math.radians(35)
    obj=ell('dolphin fluke',(side*.28,0,-.96),(.37,.035,.18),gray);obj.rotation_euler.z=-side*math.radians(18)
    ell('dolphin eye',(side*.205,.08,.64),(.033,.033,.033),(.055,.11,.15))
camera.location=(2.6,2.4,1.7);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.9
scene.render.resolution_x=384;scene.render.resolution_y=256
scene.render.filepath=str(OUT/'DolphinPortrait.png');bpy.ops.render.render(write_still=True)
image=bpy.data.images.load(str(OUT/'DolphinPortrait.png'),check_existing=False)
assert max(image.pixels[3::4])>.9,'Empty dolphin portrait'
print('[V031-ART] Dolphin portrait PASS',flush=True)

# The land tortoise has feet and a domed shell, rather than sea-turtle flippers.
for obj in list(scene.objects):
    if obj.type=='MESH':obj.hide_render=True
GREEN=(.34,.43,.22)
ell('land tortoise shell',(0,.24,0),(.32,.21,.42),GREEN)
for z in (-1,0,1):
    for x in (-1,0,1):ell('shell scute',(x*.16,.425-abs(x)*.045,z*.19),(.095,.024,.12),GOLD)
ell('tortoise head',(0,.19,.47),(.11,.10,.18),GREEN)
for x in (-1,1):
    for z in (-1,1):ell('tortoise walking foot',(x*.28,.09,z*.28),(.12,.07,.12),GREEN)
    ell('tortoise eye',(x*.08,.23,.53),(.023,.023,.023),(.055,.11,.15))
camera.location=(1.1,1.1,1.5);target=U((0,.20,.06));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.12
scene.render.filepath=str(OUT/'TortoisePortrait.png');bpy.ops.render.render(write_still=True)
print('[V031-ART] Land tortoise portrait PASS',flush=True)
