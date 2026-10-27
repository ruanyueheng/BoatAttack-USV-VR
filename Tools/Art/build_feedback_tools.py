"""Four compact work tools and the existing CC0 chest with its original hinged cover."""
from pathlib import Path
import json, math
import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Feedback-20261026/Tools'
OUT=ART/'Exports'
OUT.mkdir(parents=True,exist_ok=True)
survival=ART/'Sources/Kenney-Survival'
pirate=ART/'Sources/Kenney-Pirate'

def reset():bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name,color):
    value=bpy.data.materials.new('MAT-'+name);value.diffuse_color=(*color,1);value.use_nodes=True
    value.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    return value
def cube(name,p,s,material,bevel=.005):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);obj=bpy.context.object;obj.name='GEO-'+name;obj.dimensions=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);obj.data.materials.append(material)
    if bevel:
        modifier=obj.modifiers.new('Rounded working edges','BEVEL');modifier.width=bevel;modifier.segments=2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj
def shaft(name,a,b,r,material):
    a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=(b-a).length,location=(a+b)/2)
    obj=bpy.context.object;obj.name='GEO-'+name;obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();obj.data.materials.append(material);return obj
def bake_colors(obj):
    mesh=obj.data;color=mesh.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    uv=mesh.uv_layers.active
    pixels={}
    for polygon in mesh.polygons:
        material=mesh.materials[polygon.material_index] if mesh.materials else None
        base=tuple(material.diffuse_color) if material else (1,1,1,1)
        image=None
        if material and material.use_nodes:
            node=next((n for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None)
            if node:image=node.image
            bsdf=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
            if bsdf:base=tuple(bsdf.inputs['Base Color'].default_value)
        if image and image not in pixels:pixels[image]=list(image.pixels)
        for loop in polygon.loop_indices:
            rgba=base
            if image and uv and image.size[0] and len(pixels[image]):
                u,v=uv.data[loop].uv;w,h=image.size;x=min(w-1,max(0,int(u*w)));y=min(h-1,max(0,int(v*h)))
                rgba=pixels[image][(y*w+x)*4:(y*w+x)*4+4]
            color.data[loop].color=rgba
        polygon.material_index=0
    mesh.materials.clear()
    material=bpy.data.materials.get('MAT-vertex-preview')
    if material is None:
        material=bpy.data.materials.new('MAT-vertex-preview');material.use_nodes=True
        vertex=material.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
        material.node_tree.links.new(vertex.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    mesh.materials.append(material)
def export(name,join=True,height=None):
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    if height:
        points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices];low=min(p.z for p in points);span=max(p.z for p in points)-low
        for obj in meshes:
            transform=obj.matrix_world.copy()
            for vertex in obj.data.vertices:vertex.co=(transform@vertex.co-Vector((0,0,low)))*(height/span)
            if name=='Shovel':
                for vertex in obj.data.vertices:vertex.co.z-=.2
            obj.matrix_world.identity()
    for obj in meshes:
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.triangulate(bm,faces=list(bm.faces))
        collapsed=[face for face in bm.faces if face.calc_area()<=1e-12]
        if collapsed:bmesh.ops.delete(bm,geom=collapsed,context='FACES_ONLY')
        bm.to_mesh(obj.data);bm.free();obj.data.update();bake_colors(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    if join:
        if len(meshes)>1:bpy.ops.object.join()
        meshes=[bpy.context.object];meshes[0].name='GEO-'+name
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    bpy.ops.wm.save_as_mainfile(filepath=str(ART/(name+'.blend')))
    triangles=sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in meshes)
    assert triangles<4500,(name,triangles)
    return {'name':name,'triangles':triangles,'grip':[0,.1,0],'headDirection':[0,1,0],'unit':'metres','vertexColors':True,'file':(OUT/(name+'.fbx')).relative_to(ROOT).as_posix()}

records=[]
for name,height in [('Axe',.85),('Hammer',.45)]:
    reset();bpy.ops.import_scene.gltf(filepath=str(survival/('tool-'+name.lower()+'.glb')))
    record=export(name,height=height);record.update(source='Kenney Survival Kit 2.0 / tool-'+name.lower(),license='CC0-1.0',height=height);records.append(record)
reset();bpy.ops.import_scene.gltf(filepath=str(pirate/'tool-shovel.glb'))
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':obj.matrix_world=Matrix.Rotation(math.pi,4,'Y')@obj.matrix_world
record=export('Shovel',height=1.05);record.update(source='Kenney Pirate Kit 2.1 / tool-shovel',license='CC0-1.0',height=1.05);records.append(record)
reset();wood=mat('warm ash wood',(.48,.28,.13));steel=mat('worked blue steel',(.24,.32,.35))
shaft('Rake long handle',(0,0,0),(0,0,1.02),.022,wood)
shaft('Rake steel collar',(0,0,.94),(0,0,1.03),.029,steel)
shaft('Rake crossbar',(-.22,0,1.04),(.22,0,1.04),.018,steel)
for i in range(9):
    x=-.20+i*.05;shaft('Rake curved tooth '+str(i),(x,0,1.04),(x,-.14,1.04),.009,steel)
    shaft('Rake working tip '+str(i),(x,-.14,1.04),(x,-.18,1.00),.009,steel)
record=export('Rake');record.update(source='Original model created for the requested garden tool',license='Project original',height=1.1);records.append(record)
reset();wood=mat('oiled beech handle',(.52,.30,.13));steel=mat('saw steel',(.52,.60,.62));dark=mat('handle fasteners',(.16,.21,.23))
cube('Saw handle left',(-.065,0,.10),(.035,.045,.19),wood,.012)
cube('Saw handle right',(.065,0,.10),(.035,.045,.19),wood,.012)
cube('Saw handle lower',(0,0,.02),(.14,.045,.04),wood,.01)
cube('Saw handle upper',(0,0,.18),(.14,.045,.045),wood,.01)
profile=[(-.085,.18),(-.07,.64),(.06,.64)]
for i in range(15):
    z=.64-i*.03;profile.extend([(.105,z),(.085,z-.015)])
profile.append((.085,.18))
verts=[(x,y,z) for y in [-.004,.004] for x,z in profile];n=len(profile)
faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
mesh=bpy.data.meshes.new('Saw toothed blade');mesh.from_pydata(verts,[],faces);mesh.update()
blade=bpy.data.objects.new('GEO-Saw continuous toothed blade',mesh);bpy.context.scene.collection.objects.link(blade);mesh.materials.append(steel)
for x in [-.055,.055]:shaft('Saw handle rivet',(x,-.027,.17),(x,.027,.17),.008,dark)
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':obj.location.x+=.065
record=export('Saw');record.update(source='Original model created for the requested sawmill interaction',license='Project original',height=.64,toothDirection=[1,0,0]);records.append(record)
reset();bpy.ops.import_scene.gltf(filepath=str(pirate/'chest.glb'))
for obj in bpy.context.scene.objects:
    obj.animation_data_clear()
    if obj.name=='lid':obj.rotation_mode='QUATERNION';obj.rotation_quaternion=(1,0,0,0)
record=export('HomeChest',join=False);record.update(source='Kenney Pirate Kit 2.1 / chest',license='CC0-1.0',lidNode='lid',hinge='Original lid origin; local X rotation',grip=None);records.append(record)
(ART/'manifest.json').write_text(json.dumps({'assets':records},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('FEEDBACK_TOOLS_PASS',json.dumps(records))
