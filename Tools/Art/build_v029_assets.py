"""Import the four supplied GLBs without changing originals; build a solid whole roast fish.
Run only in a fresh background Blender process. Runtime uses the project's vertex palette.
"""
from pathlib import Path
import bpy, math, json, hashlib
import numpy as np
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/V029'; OUT=ROOT/'Assets/Resources/FishingV029'
ART.mkdir(parents=True,exist_ok=True); OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True
scene.render.resolution_x=scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.film_transparent=True
world=bpy.data.worlds.new('V029 studio');world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.4;scene.world=world
palette=bpy.data.materials.new('Vertex colours');palette.use_nodes=True
bs=next(n for n in palette.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.62
vc=palette.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';palette.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color'])
cam=bpy.data.objects.new('V029 camera',bpy.data.cameras.new('V029 camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO'
for name,p,e in [('key',(2,-3,4),400),('fill',(-3,-1,2),200),('rim',(0,3,3),300)]:
    l=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(l);l.location=p;l.data.energy=e;l.data.size=3;l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
report=[];models=[]
def export(obj,name):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.name=name;obj.data.materials.clear();obj.data.materials.append(palette)
    for f in obj.data.polygons:f.material_index=0
    obj.data.color_attributes.active_color=obj.data.color_attributes.get('Color')
    triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
    assert 50<triangles<22000,(name,triangles)
    coords=np.array([v.co[:] for v in obj.data.vertices]);lo=coords.min(axis=0);hi=coords.max(axis=0)
    assert np.isfinite(coords).all() and min(hi-lo)>.025,(name,lo,hi)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_space_transform=True)
    bpy.ops.export_scene.gltf(filepath=str(ART/(name+'.glb')),use_selection=True,export_format='GLB',export_animations=False)
    for m in models:m.hide_render=True
    center=Vector((lo+hi)*.5);cam.location=center+Vector((1.4,2,1.1)).normalized()*3
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=float(max(hi-lo))*1.35
    scene.render.filepath=str(OUT/(name+'Thumbnail.png'));bpy.ops.render.render(write_still=True)
    obj.hide_render=True;models.append(obj)
    report.append({'name':name,'triangles':triangles,'unity_size':[float(hi[0]-lo[0]),float(hi[2]-lo[2]),float(hi[1]-lo[1])], 'sha256':hashlib.sha256((OUT/(name+'.fbx')).read_bytes()).hexdigest()})
    print('[V029-ART]',report[-1],flush=True)

for name,size in [('Rabbit',.68),('Turtle',.85),('Fox',1.15),('Seagull',1.05)]:
    dst=ART/(name.lower()+'-original.glb');assert dst.is_file(),dst
    before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(dst))
    parts=[o for o in bpy.data.objects if o not in before and o.type=='MESH'];assert parts,name
    for obj in parts:
        bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Mobile geometry budget','DECIMATE');mod.ratio=min(1,3500/max(1,sum(len(p.vertices)-2 for p in obj.data.polygons)))
        bpy.ops.object.modifier_apply(modifier=mod.name)
        attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER');images={}
        for mat in obj.data.materials:
            if mat and mat.use_nodes:
                shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
                tex=shader.inputs['Base Color'].links[0].from_node if shader and shader.inputs['Base Color'].is_linked else None
                if tex and tex.type=='TEX_IMAGE':
                    im=tex.image;px=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(px);images[mat.name]=(im.size[:],px)
                    runtime=im.copy();runtime.scale(1024,1024);runtime.filepath_raw=str(OUT/(name+'Albedo.png'));runtime.file_format='PNG';runtime.save()
        uv=obj.data.uv_layers.active
        for face in obj.data.polygons:
            mat=obj.data.materials[face.material_index];im=images.get(mat.name) if mat else None
            for i in face.loop_indices:
                color=mat.diffuse_color if mat else (.5,.5,.5,1)
                if im and uv:
                    (w,h),px=im;u,v=uv.data[i].uv;offset=(min(h-1,max(0,int(v*h)))*w+min(w-1,max(0,int(u*w))))*4;color=px[offset:offset+4]
                attr.data[i].color=tuple(color)
        matrix=obj.matrix_world.copy();obj.parent=None;obj.matrix_world=Matrix.Identity(4)
        for vertex in obj.data.vertices:vertex.co=matrix@vertex.co
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=bpy.context.object
    # Supplied GLBs use Z as depth-negative in glTF; Blender import restores world Z-up.
    coords=np.array([v.co[:] for v in obj.data.vertices]);lo=coords.min(axis=0);hi=coords.max(axis=0)
    scale=size/max(hi-lo);center=(lo+hi)*.5
    for v in obj.data.vertices:v.co=(v.co-Vector((center[0],center[1],lo[2])))*scale
    angle=math.pi/2 if name=='Seagull' else math.pi
    turn=Matrix.Rotation(angle,4,'Z')
    for vertex in obj.data.vertices:vertex.co=turn@vertex.co
    export(obj,name)

parts=[]
def ellipsoid(name,position,scale,color):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=position);o=bpy.context.object;o.name=name;o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    a=o.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    for c in a.data:c.color_srgb=(*color,1)
    parts.append(o);return o
# Real volume: plump body, rounded head, paired fins, forked tail, char scores on both sides.
ellipsoid('Golden skin',(0,0,.085),(.27,.095,.085),(.58,.28,.085))
ellipsoid('Rounded head',(.23,0,.085),(.09,.08,.075),(.43,.22,.08))
for side in (-1,1):
    ellipsoid('Forked tail',(-.31,side*.055,.085),(.11,.055,.026),(.46,.19,.055))
    ellipsoid('Pectoral fin',(.025,side*.09,.075),(.10,.035,.022),(.55,.24,.06))
    ellipsoid('Cooked eye',(.267,side*.063,.116),(.018,.009,.018),(.12,.09,.05))
    for i in range(4):
        o=ellipsoid('Seared score',(-.14+i*.085,side*.071,.13),(.009,.018,.044),(.22,.105,.035));o.rotation_euler.x=side*.6
ellipsoid('Raised dorsal fin',(-.045,0,.17),(.13,.018,.038),(.46,.19,.055))
for x,y in [(-.05,-.15),(.10,.145)]:
    ellipsoid('Lemon rind',(x,y,.035),(.057,.057,.020),(.93,.68,.12))
    ellipsoid('Lemon flesh',(x,y,.05),(.046,.046,.007),(.98,.87,.38))
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();export(bpy.context.object,'GrilledFish')
(ART/'manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'v029-assets.blend'))
print('V029 ASSETS PASS',flush=True)
