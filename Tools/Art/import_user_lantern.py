"""Preserve supplied lantern geometry/UV/PBR; add a glass emission submesh.
Read-only for the source .blend. Outputs normalized runtime FBX/maps/thumbnail.
"""
import bpy
import json
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/Resources/FishingUserTools';OUT.mkdir(parents=True,exist_ok=True)
source=bpy.data.objects['node_0'];scene=bpy.context.scene
material=source.data.materials[0];textures=[n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
colour_src=next(i for i in textures if i.name=='texture_pbr_20250901')
mask_src=next(i for i in textures if 'metallic' in i.name)
normal_src=next(i for i in textures if 'normal' in i.name)
def save_copy(image,name,size,linear=False):
    image=image.copy();image.name=name;image.colorspace_settings.name='Non-Color' if linear else 'sRGB';image.scale(size,size)
    image.filepath_raw=str(OUT/(name+'.png'));image.file_format='PNG';image.save();return image
colour=save_copy(colour_src,'LanternAlbedo',2048)
normal=save_copy(normal_src,'LanternNormal',1024,True)
mask=mask_src.copy();mask.colorspace_settings.name='Non-Color';mask.scale(1024,1024)
data=np.empty(1024*1024*4,dtype=np.float32);mask.pixels.foreach_get(data)
packed=np.zeros_like(data);packed[0::4]=data[2::4];packed[3::4]=1-data[1::4]
metal=bpy.data.images.new('LanternMetallicSmoothness',width=1024,height=1024,alpha=True);metal.colorspace_settings.name='Non-Color';metal.pixels.foreach_set(packed)
metal.filepath_raw=str(OUT/'LanternMetallicSmoothness.png');metal.file_format='PNG';metal.save()
mesh=source.data.copy();obj=bpy.data.objects.new('SuppliedLantern',mesh);scene.collection.objects.link(obj)
points=np.array([tuple(source.matrix_world@v.co) for v in mesh.vertices]);lo=points.min(axis=0);hi=points.max(axis=0)
body=points[points[:,2]<.65];body_center=(body.min(axis=0)+body.max(axis=0))*.5
scale=.40/(hi[2]-lo[2]);base=np.array([body_center[0],body_center[1],lo[2]])
for v,p in zip(mesh.vertices,points):v.co=Vector(tuple((p-base)*scale))
for original in list(bpy.data.objects):
    if original!=obj:bpy.data.objects.remove(original,do_unlink=True)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
def atlas_material(name,emission=0):
    mat=bpy.data.materials.new(name);mat.use_nodes=True;tree=mat.node_tree;tree.nodes.clear()
    bsdf=tree.nodes.new('ShaderNodeBsdfPrincipled');output=tree.nodes.new('ShaderNodeOutputMaterial');tree.links.new(bsdf.outputs[0],output.inputs['Surface'])
    tex=tree.nodes.new('ShaderNodeTexImage');tex.image=colour;tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    tex=tree.nodes.new('ShaderNodeTexImage');tex.image=mask;separate=tree.nodes.new('ShaderNodeSeparateColor');tree.links.new(tex.outputs['Color'],separate.inputs[0])
    tree.links.new(separate.outputs['Blue'],bsdf.inputs['Metallic']);tree.links.new(separate.outputs['Green'],bsdf.inputs['Roughness'])
    tex=tree.nodes.new('ShaderNodeTexImage');tex.image=normal;node=tree.nodes.new('ShaderNodeNormalMap');tree.links.new(tex.outputs['Color'],node.inputs['Color']);tree.links.new(node.outputs[0],bsdf.inputs['Normal'])
    bsdf.inputs['Emission Color'].default_value=(1,.54,.16,1);bsdf.inputs['Emission Strength'].default_value=emission
    return mat
mesh.materials.clear();mesh.materials.append(atlas_material('LanternFrame'));mesh.materials.append(atlas_material('LanternPaperLight',.8))
pixels=np.empty(2048*2048*4,dtype=np.float32);colour.pixels.foreach_get(pixels);uv=mesh.uv_layers.active
glow_faces=0
for face in mesh.polygons:
    center=sum((mesh.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)
    u,v=sum((uv.data[i].uv for i in face.loop_indices),Vector((0,0)))/len(face.loop_indices)
    x=max(0,min(2047,int(u*2048)));y=max(0,min(2047,int(v*2048)));r,g,b,_=pixels[(y*2048+x)*4:(y*2048+x)*4+4]
    # The four inset paper panes are geometric rectangles. A brightness-only
    # threshold left triangular patches in the source texture, so identify each
    # pane by its inset span while excluding corner posts and horizontal rails.
    glass=.043<center.z<.242 and ((abs(center.x)<.055 and abs(center.y)>.04) or (abs(center.y)<.048 and abs(center.x)>.04))
    face.material_index=1 if glass else 0;glow_faces+=glass
assert glow_faces>100,glow_faces
modifier=obj.modifiers.new('Runtime geometry budget','DECIMATE');modifier.ratio=.24
bpy.ops.object.modifier_apply(modifier=modifier.name)
assert sum(len(p.vertices)-2 for p in obj.data.polygons)<13000
# Colour fallback shares the actual source UV and texture, not an invented palette.
uv=obj.data.uv_layers.active;attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
for loop in obj.data.loops:
    u,v=uv.data[loop.index].uv;x=max(0,min(2047,int(u*2048)));y=max(0,min(2047,int(v*2048)))
    attr.data[loop.index].color=tuple(pixels[(y*2048+x)*4:(y*2048+x)*4+4])
grip_original=np.array([.25,body_center[1],.94]);grip=(grip_original-base)*scale
grip_unity=[float(grip[0]),float(grip[2]+.014),float(-grip[1]-.017)]
emitter=[0,.14,0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Lantern.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
report={'source':'ArtSource/UserLantern/lantern.blend','source_triangles':50000,'export_triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'height_m':.40,'unity_grip':grip_unity,'unity_light':emitter,'glowing_triangles':sum(len(p.vertices)-2 for p in obj.data.polygons if p.material_index==1),'submeshes':['LanternFrame','LanternPaperLight'],'note':'Only supplied lantern; no match or unrelated ak-47 imported.'}
(ROOT/'ArtSource/UserLantern/import-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100;scene.render.film_transparent=True
world=bpy.data.worlds.new('PreviewWorld');world.use_nodes=True;world.node_tree.nodes.clear();bg=world.node_tree.nodes.new('ShaderNodeBackground');bg.inputs['Color'].default_value=(.25,.25,.25,1);out=world.node_tree.nodes.new('ShaderNodeOutputWorld');world.node_tree.links.new(bg.outputs[0],out.inputs['Surface']);scene.world=world
center=Vector((.035,0,.20));camera=bpy.data.objects.new('Preview',bpy.data.cameras.new('Preview'));scene.collection.objects.link(camera);camera.location=center+Vector((.5,-1.5,.5));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.54;scene.camera=camera
for name,offset,power in [('Key',(1,-2,2),100),('Fill',(-2,-1,1),60),('Rim',(0,2,2),80)]:
    light=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(light);light.location=center+Vector(offset);light.data.energy=power;light.data.size=2;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'LanternThumbnail.png');bpy.ops.render.render(write_still=True)
print('USER LANTERN EXPORT PASS',json.dumps(report),flush=True)
