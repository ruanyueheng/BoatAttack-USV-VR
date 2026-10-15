"""Export only the supplied pick head/wood handle; bake source materials for URP.
Run Blender --background ArtSource/UserPickaxe/pickaxe_free_asset.blend --python this file.
Does not save or edit the source .blend. Dimensions and contact points are recorded.
"""
import bpy
import json
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FishingUserTools';OUT.mkdir(parents=True,exist_ok=True)
LOG=ROOT/'Logs/UserPickaxe';LOG.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
source=[bpy.data.objects[n] for n in ('Circle','Circle.001')]
wood=source[1]
wood_points=np.array([tuple(wood.matrix_world@v.co) for v in wood.data.vertices])
head_points=np.array([tuple(source[0].matrix_world@v.co) for v in source[0].data.vertices])
_,vectors=np.linalg.eigh(np.cov(wood_points.T));up=vectors[:,-1]
if np.dot(up,head_points.mean(axis=0)-wood_points.mean(axis=0))<0:up=-up
_,vectors=np.linalg.eigh(np.cov(head_points.T));right=vectors[:,-1]
right-=up*np.dot(right,up);right/=np.linalg.norm(right)
if right[0]<0:right=-right
depth=np.cross(up,right)
basis=np.array([right,depth,up])
all_points=np.concatenate((head_points,wood_points))
local=all_points@basis.T
scale=.58/(local[:,2].max()-local[:,2].min())
# Grip at the lower third of the real wooden handle, not at the metal head.
w=wood_points@basis.T
grip_z=w[:,2].min()+(w[:,2].max()-w[:,2].min())*.28
low=w[w[:,2]<w[:,2].min()+.06].mean(axis=0)
high=w[w[:,2]>w[:,2].max()-.06].mean(axis=0)
grip=low*.72+high*.28;grip[2]=grip_z
assert np.isfinite(grip).all(),grip
copies=[];head_vertices=[]
deps=bpy.context.evaluated_depsgraph_get()
for original in source:
    mesh=bpy.data.meshes.new_from_object(original.evaluated_get(deps),depsgraph=deps)
    obj=bpy.data.objects.new(original.name+'_export',mesh);scene.collection.objects.link(obj)
    for vertex in mesh.vertices:
        point=np.array(tuple(original.matrix_world@vertex.co))
        vertex.co=Vector(tuple((basis@point-grip)*scale))
        if original==source[0]:head_vertices.append(tuple(vertex.co))
    obj.data.materials.clear()
    for mat in original.data.materials:obj.data.materials.append(mat.copy())
    copies.append(obj)
for obj in list(bpy.data.objects):
    if obj not in copies:bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for obj in copies:obj.select_set(True)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join()
obj=copies[0];obj.name='UserPickaxe'
old_uv=obj.data.uv_layers.active;old_uv.name='SourceUV'
# Keep packed wood UVs while a separate, non-overlapping atlas is generated.
for mat in obj.data.materials:
    tree=mat.node_tree
    uvnode=tree.nodes.new('ShaderNodeUVMap');uvnode.uv_map='SourceUV'
    for node in list(tree.nodes):
        if node.type=='TEX_COORD':
            for link in list(node.outputs['UV'].links):tree.links.new(uvnode.outputs['UV'],link.to_socket)
atlas_uv=obj.data.uv_layers.new(name='UnityAtlas');obj.data.uv_layers.active=atlas_uv
atlas_uv.active_render=True
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15192,island_margin=.018)
bpy.ops.object.mode_set(mode='OBJECT')
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.device='CPU'
scene.render.bake.margin=8;scene.render.bake.use_clear=True
saved=[]
for mat in obj.data.materials:
    tree=mat.node_tree;bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    output=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL')
    saved.append((mat,bsdf,output))

def bake_emit(name,socket_name):
    image=bpy.data.images.new(name,width=1024,height=1024,alpha=True)
    image.colorspace_settings.name='sRGB' if socket_name=='Base Color' else 'Non-Color'
    for mat,bsdf,output in saved:
        tree=mat.node_tree;emission=tree.nodes.new('ShaderNodeEmission');value=bsdf.inputs[socket_name]
        if value.is_linked:tree.links.new(value.links[0].from_socket,emission.inputs['Color'])
        else:
            val=value.default_value
            emission.inputs['Color'].default_value=tuple(val) if socket_name=='Base Color' else (val,val,val,1)
        tree.links.new(emission.outputs[0],output.inputs['Surface'])
        target=tree.nodes.new('ShaderNodeTexImage');target.image=image;tree.nodes.active=target
    bpy.ops.object.bake(type='EMIT')
    return image

albedo=bake_emit('PickaxeAlbedo','Base Color')
albedo.filepath_raw=str(OUT/'PickaxeAlbedo.png');albedo.file_format='PNG';albedo.save()
metallic=bake_emit('PickaxeMetallic','Metallic')
roughness=bake_emit('PickaxeRoughness','Roughness')
m=np.array(metallic.pixels[:]);r=np.array(roughness.pixels[:]);packed=m.copy()
packed[3::4]=1-r[0::4]
mask=bpy.data.images.new('PickaxeMetallicSmoothness',width=1024,height=1024,alpha=True)
mask.colorspace_settings.name='Non-Color';mask.pixels=packed.tolist()
mask.filepath_raw=str(OUT/'PickaxeMetallicSmoothness.png');mask.file_format='PNG';mask.save()
for mat,bsdf,output in saved:mat.node_tree.links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
normal=bpy.data.images.new('PickaxeNormal',width=1024,height=1024,alpha=True)
normal.colorspace_settings.name='Non-Color'
for mat,bsdf,output in saved:
    target=mat.node_tree.nodes.new('ShaderNodeTexImage');target.image=normal;mat.node_tree.nodes.active=target
scene.render.bake.normal_space='TANGENT';bpy.ops.object.bake(type='NORMAL')
normal.filepath_raw=str(OUT/'PickaxeNormal.png');normal.file_format='PNG';normal.save()

# Use one atlas material/submesh at runtime. Actual source colour remains available
# as a vertex layer too; this is a fallback, not replacement procedural geometry.
mat=bpy.data.materials.new('UserPickaxeAtlas');mat.use_nodes=True
tree=mat.node_tree;tree.nodes.clear();bsdf=tree.nodes.new('ShaderNodeBsdfPrincipled')
output=tree.nodes.new('ShaderNodeOutputMaterial');tree.links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
tex=tree.nodes.new('ShaderNodeTexImage');tex.image=albedo;tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
tex=tree.nodes.new('ShaderNodeTexImage');tex.image=metallic;tree.links.new(tex.outputs['Color'],bsdf.inputs['Metallic'])
tex=tree.nodes.new('ShaderNodeTexImage');tex.image=roughness;tree.links.new(tex.outputs['Color'],bsdf.inputs['Roughness'])
tex=tree.nodes.new('ShaderNodeTexImage');tex.image=normal;n=tree.nodes.new('ShaderNodeNormalMap')
tree.links.new(tex.outputs['Color'],n.inputs['Color']);tree.links.new(n.outputs['Normal'],bsdf.inputs['Normal'])
obj.data.materials.clear();obj.data.materials.append(mat)
for face in obj.data.polygons:face.material_index=0
# Preserve the atlas coordinates across layer deletion (Blender's active-layer
# wrapper can refer to the old slot after a UV layer is removed).
atlas_coords=[tuple(d.uv) for d in obj.data.uv_layers['UnityAtlas'].data]
assert max(p[0] for p in atlas_coords)-min(p[0] for p in atlas_coords)>.1
print('ATLAS UV RANGE',min(p[0] for p in atlas_coords),max(p[0] for p in atlas_coords),flush=True)
obj.data.uv_layers.remove(obj.data.uv_layers['SourceUV'])
obj.data.uv_layers.active_index=0
for d,coords in zip(obj.data.uv_layers[0].data,atlas_coords):d.uv=coords
attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
pixels=albedo.pixels[:];uv=obj.data.uv_layers.active
for loop in obj.data.loops:
    u,v=uv.data[loop.index].uv;x=max(0,min(1023,int(u*1024)));y=max(0,min(1023,int(v*1024)))
    attr.data[loop.index].color=tuple(pixels[(y*1024+x)*4:(y*1024+x)*4+4])

bpy.ops.export_scene.fbx(filepath=str(OUT/'Pickaxe.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
head=np.array(head_vertices);tip=head[np.argmax(head[:,0])]
report={'source':'ArtSource/UserPickaxe/pickaxe_free_asset.blend','included':['Circle','Circle.001'],'excluded':'display planes, camera and lights','height_m':.58,'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'unity_grip':[.034,0,0],'unity_tip':[float(tip[0]),float(tip[2]),float(-tip[1])],'unity_bounds_min':[float(local[:,0].min()*scale-grip[0]*scale),float(local[:,2].min()*scale-grip[2]*scale),float(-local[:,1].max()*scale+grip[1]*scale)],'unity_bounds_max':[float(local[:,0].max()*scale-grip[0]*scale),float(local[:,2].max()*scale-grip[2]*scale),float(-local[:,1].min()*scale+grip[1]*scale)]}
(ROOT/'ArtSource/UserPickaxe/import-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

# Native preview of the exported model and baked source atlas.
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.world.color=(.15,.15,.15)
center=Vector((0,0,.16));camera=bpy.data.objects.new('PreviewCamera',bpy.data.cameras.new('PreviewCamera'));scene.collection.objects.link(camera)
camera.location=center+Vector((1,-2,.9));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=.82;scene.camera=camera
for name,pos,power,size in [('Key',(1,-2,3),300,3),('Fill',(-2,-1,1),150,3),('Rim',(0,2,2),250,2)]:
    light=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(light);light.location=center+Vector(pos)
    light.data.energy=power;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'PickaxeThumbnail.png');bpy.ops.render.render(write_still=True)
print('USER PICKAXE EXPORT PASS',json.dumps(report),flush=True)
