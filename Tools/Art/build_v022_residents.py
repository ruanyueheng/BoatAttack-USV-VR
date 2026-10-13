"""Run in fresh background Blender: preserved CC0 heads and authored modular clothing."""
from pathlib import Path
import json, math
import bpy, bmesh, numpy as np
from mathutils import Matrix, Vector, Quaternion
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'ArtSource/V019/Characters'; TEXTURES=ROOT/'ArtSource/V022/Textures'; CLOTHES=ROOT/'ArtSource/V022/Outfits'
OUT=ROOT/'Assets/Resources/FishingV022/Residents'; ART=ROOT/'ArtSource/V022'; PREVIEW=ROOT/'Docs/Images/V022'; ICONS=ROOT/'Assets/Resources/FishingV022/Thumbnails'
for folder in (OUT,ART,PREVIEW,ICONS):folder.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=480;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True
scene.view_settings.view_transform='AgX'
world=bpy.data.worlds.new('V022 studio');world.use_nodes=True;world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.35;scene.world=world
for name,pos,energy in [('key',(2,-3,4),650),('fill',(-2,-2,2),240),('rim',(1,2,3),400)]:
    data=bpy.data.lights.new('LGT-'+name,'AREA');data.energy=energy;data.size=3
    obj=bpy.data.objects.new('LGT-'+name,data);scene.collection.objects.link(obj);obj.location=pos
    obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
cam_data=bpy.data.cameras.new('CAM-resident');cam_data.type='ORTHO';cam_data.ortho_scale=1.98
camera=bpy.data.objects.new('CAM-resident',cam_data);scene.collection.objects.link(camera);scene.camera=camera
camera.location=(0,-4,.98);camera.rotation_euler=(Vector((0,0,.92))-camera.location).to_track_quat('-Z','Y').to_euler()
report=[];gallery=[]
def triangles(obj):return sum(len(p.vertices)-2 for p in obj.data.polygons)
def relaxed(rig):
    for side in ('l','r'):
        bone=rig.pose.bones['upperarm_'+side];matrix=rig.matrix_world@bone.matrix;pivot=matrix.translation.copy()
        sign=1 if (rig.matrix_world@bone.head).x>0 else -1
        rot=Quaternion(Vector((0,1,0)),sign*math.radians(68)).to_matrix().to_4x4()
        bone.matrix=rig.matrix_world.inverted()@Matrix.Translation(pivot)@rot@Matrix.Translation(-pivot)@matrix
        bpy.context.view_layer.update()
for id,sex,hair,outfit,variant,tint in [(0,'Female','Long','Peasant','',(.24,.12,.05)),(1,'Female','Buns','Ranger','',(.09,.13,.07)),(2,'Male','Buzzed','Peasant','_2',(.19,.12,.08)),(3,'Male','SimpleParted','Ranger','_3',(.38,.40,.43)),(5,'Male','Beard','Peasant','',(.56,.54,.48))]:
    before=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(BASE/('Superhero_'+sex+'_FullBody.fbx')))
    originals=[o for o in scene.objects if o not in before]
    body=next(o for o in originals if o.type=='MESH' and o.name.lower().startswith('superhero_'))
    eye=next(o for o in originals if o.type=='MESH' and 'Eyes' in o.name)
    points=[body.matrix_world@v.co for v in body.data.vertices];bottom=min(p.z for p in points);height=max(p.z for p in points)-bottom
    front=sum((eye.matrix_world@v.co).y for v in eye.data.vertices)/len(eye.data.vertices)
    canonical=Matrix.Rotation(math.pi if front>0 else 0,4,'Z')
    stubs={key:bpy.data.materials.new('MAT-'+key) for key in ['Skin','Eyes','Hair','Outfit','ArmSkin']}
    # The authored outfit includes the complete clothed body and hands. Keep the real head/neck only.
    remove=[]
    for p in body.data.polygons:
        weights={}
        for index in p.vertices:
            for g in body.data.vertices[index].groups:
                joint=body.vertex_groups[g.group].name;weights[joint]=weights.get(joint,0)+g.weight
        if (max(weights,key=weights.get) if weights else 'pelvis') not in {'Head','neck_01'}:remove.append(p.index)
    bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[bm.faces[i] for i in remove],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(body.data);bm.free()
    for obj in originals:
        if obj.type!='MESH':continue
        obj.data.materials.clear();obj.data.materials.append(stubs['Skin' if obj==body else 'Eyes' if obj==eye else 'Hair'])
        for p in obj.data.polygons:p.material_index=0
    before=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(BASE/('Hair_'+hair+'.fbx')))
    hair_objects=[o for o in scene.objects if o not in before]
    for obj in hair_objects:
        if obj.type!='MESH':continue
        obj.data.materials.clear();obj.data.materials.append(stubs['Hair'])
        for p in obj.data.polygons:p.material_index=0
    originals+=hair_objects
    before=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(CLOTHES/(sex+'_'+outfit+'.fbx')))
    clothes=[o for o in scene.objects if o not in before];relaxed(next(o for o in clothes if o.type=='ARMATURE'))
    excluded=[o for o in clothes if o.type=='MESH' and any(part in o.name for part in ['Head_Hood','Body_Belt_2','Acc_Pauldron'])]
    clothes=[o for o in clothes if o not in excluded]
    for obj in excluded:bpy.data.objects.remove(obj,do_unlink=True)
    for obj in clothes:
        if obj.type!='MESH':continue
        skin_slots={i for i,m in enumerate(obj.data.materials) if 'Regular' in m.name}
        indices=[1 if p.material_index in skin_slots else 0 for p in obj.data.polygons]
        obj.data.materials.clear();obj.data.materials.append(stubs['Outfit']);obj.data.materials.append(stubs['ArmSkin'])
        for p,index in zip(obj.data.polygons,indices):p.material_index=index
    originals+=clothes
    meshes=[];depsgraph=bpy.context.evaluated_depsgraph_get();adult=1.70 if sex=='Female' else 1.78
    for source in [o for o in originals if o.type=='MESH']:
        data=bpy.data.meshes.new_from_object(source.evaluated_get(depsgraph),depsgraph=depsgraph);data.transform(canonical@source.matrix_world)
        obj=bpy.data.objects.new('GEO-Resident'+str(id),data);scene.collection.objects.link(obj);meshes.append(obj)
        for v in data.vertices:v.co=(v.co-Vector((0,0,bottom)))*(adult/height)
    for obj in originals:bpy.data.objects.remove(obj,do_unlink=True)
    outfit_meshes=[o for o in meshes if all(m.name.startswith(('MAT-Outfit','MAT-ArmSkin')) for m in o.data.materials)]
    fixed=sum(triangles(o) for o in meshes if o not in outfit_meshes);clothing=sum(triangles(o) for o in outfit_meshes)
    ratio=min(1,(23000-fixed)/clothing)
    if ratio<1:
        for obj in outfit_meshes:
            bpy.context.view_layer.objects.active=obj;mod=obj.modifiers.new('Near clothing budget','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=mod.name)
    size=2048;albedo=np.ones((size,size,4),np.float32);normal=np.empty_like(albedo);normal[:]=(.5,.5,1,1)
    regions={'Skin':(0,0,1024),'Outfit':(0,1024,1024),'Hair':(1024,1024,1024),'Eyes':(1536,0,256),'ArmSkin':(1024,0,512)}
    skin=('T_Superhero_Female_Dark_BaseColor.png' if outfit=='Ranger' else 'T_Superhero_Female_Light_BaseColor.png') if sex=='Female' else 'T_Superhero_Male_Dark.png' if outfit=='Ranger' else 'T_Superhero_Male_Ligh.png'
    files={'Skin':(TEXTURES/skin,TEXTURES/('T_Superhero_'+sex+'_Normal.png')),'Hair':(TEXTURES/'T_Hair_2_BaseColor.png',TEXTURES/'T_Hair_2_Normal.png'),'Eyes':(TEXTURES/'T_Eye_Brown.png',TEXTURES/'T_Eye_Normal.png'),'Outfit':(CLOTHES/('T_'+outfit+variant+'_BaseColor.png'),CLOTHES/('T_'+outfit+'_Normal.png')),'ArmSkin':(CLOTHES/('T_Regular_'+sex+'_Dark_BaseColor.png'),CLOTHES/('T_Regular_'+sex+'_Normal.png'))}
    for key,(x,y,span) in regions.items():
        for pixels,filename,is_normal in [(albedo,files[key][0],False),(normal,files[key][1],True)]:
            img=bpy.data.images.load(str(filename),check_existing=False);img.colorspace_settings.name='Non-Color' if is_normal else 'sRGB';img.scale(span,span)
            data=np.empty(span*span*4,np.float32);img.pixels.foreach_get(data);patch=data.reshape(span,span,4)
            if key=='Hair' and not is_normal:patch[:,:,:3]*=tint
            patch[:,:,3]=1;pixels[y:y+span,x:x+span]=patch;bpy.data.images.remove(img)
    images=[]
    for suffix,pixels,space in [('Albedo',albedo,'sRGB'),('Normal',normal,'Non-Color')]:
        img=bpy.data.images.new('Resident'+str(id)+suffix,width=size,height=size,alpha=False);img.colorspace_settings.name=space;img.pixels.foreach_set(pixels.ravel())
        img.filepath_raw=str(OUT/('Resident'+str(id)+suffix+'.png'));img.file_format='PNG';img.save();images.append(img)
    mat=bpy.data.materials.new('MAT-Resident'+str(id));mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links
    shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Roughness'].default_value=.78
    tex=nodes.new('ShaderNodeTexImage');tex.image=images[0];links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    tex=nodes.new('ShaderNodeTexImage');tex.image=images[1];bump=nodes.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=.35
    links.new(tex.outputs['Color'],bump.inputs['Color']);links.new(bump.outputs['Normal'],shader.inputs['Normal'])
    for obj in meshes:
        uv=obj.data.uv_layers.active;assert uv is not None
        for p in obj.data.polygons:
            key=obj.data.materials[p.material_index].name.removeprefix('MAT-').split('.')[0];x,y,span=regions[key]
            for loop in p.loop_indices:
                old=uv.data[loop].uv.copy()
                if key=='Eyes':old=Vector((old.x%1,old.y%1))  # Source male eyes tile their image; atlas regions cannot repeat.
                assert -.01<=old.x<=1.01 and -.01<=old.y<=1.01,(id,key,tuple(old))
                uv.data[loop].uv=((x+2+old.x*(span-4))/size,(y+2+old.y*(span-4))/size)
            p.material_index=0;p.use_smooth=True
        obj.data.materials.clear();obj.data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();lod0=bpy.context.object;lod0.name='GEO-Resident'+str(id)+'_LOD0'
    lod1=lod0.copy();lod1.data=lod0.data.copy();scene.collection.objects.link(lod1);lod1.name='GEO-Resident'+str(id)+'_LOD1'
    bpy.context.view_layer.objects.active=lod1;mod=lod1.modifiers.new('Far resident','DECIMATE');mod.ratio=min(1,5500/triangles(lod1));mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    for level,obj in enumerate((lod0,lod1)):
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=str(OUT/('Resident'+str(id)+'_LOD'+str(level)+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
        assert triangles(obj)<28000 if level==0 else triangles(obj)<5800
        assert abs(min(v.co.z for v in obj.data.vertices))<.02
    lod1.hide_render=True;scene.render.filepath=str(PREVIEW/('Resident'+str(id)+'.png'));bpy.ops.render.render(write_still=True)
    bpy.data.images['Render Result'].save_render(str(ICONS/('Resident'+str(id)+'.png')))
    lod0.hide_render=True;gallery.extend((lod0,lod1));report.append({'id':id,'head':sex,'outfit':sex+'_'+outfit,'variant':variant,'height':adult,'lod0_triangles':triangles(lod0),'lod1_triangles':triangles(lod1),'materials_per_lod':1,'atlas':2048})
    print('V022_RESIDENT',id,report[-1],flush=True)
for obj in gallery:
    id=int(obj.name.split('Resident')[1][0]);obj.location.x=id*1.3;obj.hide_render=obj.name.endswith('LOD1');obj.hide_viewport=obj.name.endswith('LOD1')
bpy.ops.outliner.orphans_purge(do_recursive=True)
for image in bpy.data.images:
    if image.source=='FILE' and image.filepath and Path(image.filepath).exists():image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Residents-v022.blend'))
(ART/'residents-manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
