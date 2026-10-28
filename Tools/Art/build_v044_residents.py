"""Run in fresh background Blender. v044 residents: the v022 CC0 heads/outfits, but kept skinned on the shared
Quaternius 65-bone skeleton so Unity can drive them as Humanoids with clips cut from the CC0 Universal Animation Library 1/2. Budget per resident: LOD0 <=15k triangles, LOD1 ~4k, one 1024 atlas.
blender --background --factory-startup --python-exit-code 1 --python Tools/Art/build_v044_residents.py"""
from pathlib import Path
import json, math
import bpy, bmesh, numpy as np
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'ArtSource/V019/Characters'; TEXTURES=ROOT/'ArtSource/V022/Textures'; CLOTHES=ROOT/'ArtSource/V022/Outfits'
OUT=ROOT/'Assets/Resources/FishingV044/Residents'; ART=ROOT/'ArtSource/V044/Residents'
for folder in (OUT,ART):folder.mkdir(parents=True,exist_ok=True)
def tris(obj):return sum(len(p.vertices)-2 for p in obj.data.polygons)
def imported(path):
    before=set(bpy.context.scene.objects);bpy.ops.import_scene.fbx(filepath=str(path));return [o for o in bpy.context.scene.objects if o not in before]
def heads(rig):return {b.name:(rig.matrix_world@b.head_local) for b in rig.data.bones}
report=[]
for id,sex,hair,outfit,variant,tint in [(0,'Female','Long','Peasant','',(.24,.12,.05)),(1,'Female','Buns','Ranger','',(.09,.13,.07)),(2,'Male','Buzzed','Peasant','_2',(.19,.12,.08)),(3,'Male','SimpleParted','Ranger','_3',(.38,.40,.43)),(5,'Male','Beard','Peasant','',(.56,.54,.48)),(6,'Female','Buns','Peasant','_2',(.30,.16,.08))]:
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene
    parts=imported(BASE/('Superhero_'+sex+'_FullBody.fbx'));rig=next(o for o in parts if o.type=='ARMATURE')
    body=next(o for o in parts if o.type=='MESH' and o.name.lower().startswith('superhero_'))
    eye=next(o for o in parts if o.type=='MESH' and 'Eyes' in o.name)
    points=[body.matrix_world@v.co for v in body.data.vertices];bottom=min(p.z for p in points);height=max(p.z for p in points)-bottom
    stubs={k:bpy.data.materials.new('MAT-'+k) for k in ['Skin','Eyes','Hair','Outfit','ArmSkin']}
    # Outfit covers body and hands; keep only head/neck faces of the base body (same rule as v022).
    remove=[]
    for p in body.data.polygons:
        w={}
        for i in p.vertices:
            for g in body.data.vertices[i].groups:j=body.vertex_groups[g.group].name;w[j]=w.get(j,0)+g.weight
        if (max(w,key=w.get) if w else 'pelvis') not in {'Head','neck_01'}:remove.append(p.index)
    bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[bm.faces[i] for i in remove],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(body.data);bm.free()
    def stub(objs,key):
        for o in objs:
            if o.type!='MESH':continue
            o.data.materials.clear();o.data.materials.append(stubs[key])
            for p in o.data.polygons:p.material_index=0
    for o in parts:
        if o.type=='MESH':stub([o],'Skin' if o==body else 'Eyes' if o==eye else 'Hair')
    extra=imported(BASE/('Hair_'+hair+'.fbx'));stub(extra,'Hair')
    clothes=imported(CLOTHES/(sex+'_'+outfit+'.fbx'))
    for o in [o for o in clothes if o.type=='MESH' and any(k in o.name for k in ['Head_Hood','Body_Belt_2','Acc_Pauldron'])]:
        clothes.remove(o);bpy.data.objects.remove(o,do_unlink=True)
    for o in clothes:
        if o.type!='MESH':continue
        skin={i for i,m in enumerate(o.data.materials) if 'Regular' in m.name};idx=[1 if p.material_index in skin else 0 for p in o.data.polygons]
        o.data.materials.clear();o.data.materials.append(stubs['Outfit']);o.data.materials.append(stubs['ArmSkin'])
        for p,i in zip(o.data.polygons,idx):p.material_index=i
    # All packs share the skeleton; rebind every mesh to the body rig after proving the rest poses agree.
    master=heads(rig)
    for o in extra+clothes:
        if o.type!='ARMATURE':continue
        drift=max((heads(o)[k]-v).length for k,v in master.items() if k in heads(o));print("rest drift",id,o.name,round(drift,4));assert drift<.05,(id,o.name,drift)  # meshes keep their world verts, drift only shifts pivots
    meshes=[o for o in parts+extra+clothes if o.type=='MESH']
    for o in meshes:
        world=o.matrix_world.copy();o.parent=rig;o.matrix_world=world
        for m in o.modifiers:
            if m.type=='ARMATURE':m.object=rig
    for o in extra+clothes:
        if o.type=='ARMATURE':bpy.data.objects.remove(o,do_unlink=True)
    # Atlas (1024): same region layout as v022 at half scale.
    size=1024;albedo=np.ones((size,size,4),np.float32);normal=np.empty_like(albedo);normal[:]=(.5,.5,1,1)
    regions={'Skin':(0,0,512),'Outfit':(0,512,512),'Hair':(512,512,512),'Eyes':(768,0,128),'ArmSkin':(512,0,256)}
    skin=('T_Superhero_Female_Dark_BaseColor.png' if outfit=='Ranger' else 'T_Superhero_Female_Light_BaseColor.png') if sex=='Female' else 'T_Superhero_Male_Dark.png' if outfit=='Ranger' else 'T_Superhero_Male_Ligh.png'
    files={'Skin':(TEXTURES/skin,TEXTURES/('T_Superhero_'+sex+'_Normal.png')),'Hair':(TEXTURES/'T_Hair_2_BaseColor.png',TEXTURES/'T_Hair_2_Normal.png'),'Eyes':(TEXTURES/'T_Eye_Brown.png',TEXTURES/'T_Eye_Normal.png'),'Outfit':(CLOTHES/('T_'+outfit+variant+'_BaseColor.png'),CLOTHES/('T_'+outfit+'_Normal.png')),'ArmSkin':(CLOTHES/('T_Regular_'+sex+'_Dark_BaseColor.png'),CLOTHES/('T_Regular_'+sex+'_Normal.png'))}
    for key,(x,y,span) in regions.items():
        for pixels,filename,isn in [(albedo,files[key][0],False),(normal,files[key][1],True)]:
            img=bpy.data.images.load(str(filename),check_existing=False);img.colorspace_settings.name='Non-Color' if isn else 'sRGB';img.scale(span,span)
            data=np.empty(span*span*4,np.float32);img.pixels.foreach_get(data);patch=data.reshape(span,span,4)
            if key=='Hair' and not isn:patch[:,:,:3]*=tint
            patch[:,:,3]=1;pixels[y:y+span,x:x+span]=patch;bpy.data.images.remove(img)
    for suffix,pixels,space in [('Albedo',albedo,'sRGB'),('Normal',normal,'Non-Color')]:
        img=bpy.data.images.new('Resident'+str(id)+suffix,width=size,height=size,alpha=False);img.colorspace_settings.name=space;img.pixels.foreach_set(pixels.ravel())
        img.filepath_raw=str(OUT/('Resident'+str(id)+suffix+'.png'));img.file_format='PNG';img.save()
    mat=bpy.data.materials.new('Resident'+str(id))
    for o in meshes:
        uv=o.data.uv_layers.active;assert uv is not None
        for p in o.data.polygons:
            key=o.data.materials[p.material_index].name.removeprefix('MAT-').split('.')[0];x,y,span=regions[key]
            for loop in p.loop_indices:
                old=uv.data[loop].uv.copy()
                if key=='Eyes':old=Vector((old.x%1,old.y%1))
                assert -.01<=old.x<=1.01 and -.01<=old.y<=1.01,(id,key,tuple(old))
                uv.data[loop].uv=((x+2+old.x*(span-4))/size,(y+2+old.y*(span-4))/size)
            p.material_index=0;p.use_smooth=True
        o.data.materials.clear();o.data.materials.append(mat)
    outfit_tris=sum(tris(o) for o in meshes if o in clothes);fixed=sum(tris(o) for o in meshes)-outfit_tris
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();lod0=bpy.context.object;lod0.name='Resident'+str(id)+'_LOD0'
    def decimate(o,target):
        if tris(o)<=target:return
        for m in list(o.modifiers):o.modifiers.remove(m)  # armature is re-added after the topology change
        bpy.context.view_layer.objects.active=o;d=o.modifiers.new('budget','DECIMATE');d.ratio=target/tris(o);d.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=d.name);o.modifiers.new('Armature','ARMATURE').object=rig
    decimate(lod0,14500)
    lod1=lod0.copy();lod1.data=lod0.data.copy();scene.collection.objects.link(lod1);lod1.name='Resident'+str(id)+'_LOD1';decimate(lod1,4000)
    assert tris(lod0)<=15000 and tris(lod1)<=4200,(tris(lod0),tris(lod1))
    # Feet on the floor at the adult height used before; facing handled by the Unity humanoid avatar.
    rig.scale=Vector((1,1,1))*((1.70 if sex=='Female' else 1.78)/height)
    # Unity's auto humanoid avatar reads the male rest pose as facing the other way; males keep the source yaw.
    rig.rotation_euler.z=math.pi if sex=='Female' else 0
    bpy.ops.object.select_all(action='DESELECT')
    for o in (rig,lod0,lod1):o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/('Resident'+str(id)+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
                             add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_armature_deform_only=True,primary_bone_axis='Y',secondary_bone_axis='X')
    report.append({'id':id,'lod0_triangles':tris(lod0),'lod1_triangles':tris(lod1),'atlas':size,'height':1.70 if sex=='Female' else 1.78,'bottom':bottom})
    print('V044_RESIDENT',report[-1],flush=True)
# Animation-only FBX: the UAL takes residents use (names kept; FishingV044ResidentImporter maps them), in-place versions.
UAL=ROOT/'ArtSource/V044/AI/quaternius/2026-10-28'
USED={'Idle_Loop','Idle_Talking_Loop','Fixing_Kneeling','PickUp_Table','Interact','Farm_Watering','Farm_PlantSeed','Farm_Harvest',
      'Idle_FoldArms_Loop','Idle_Lantern_Loop','Yes','Consume','Chest_Open','Walk_Loop'}
bpy.ops.wm.read_factory_settings(use_empty=True)
for glb in [UAL/'ual/Universal Animation Library[Standard]/Unreal-Godot/UAL1_Standard.glb',UAL/'ual2/Universal Animation Library 2[Standard]/Unreal-Godot/UAL2_Standard.glb']:
    bpy.ops.import_scene.gltf(filepath=str(glb))
keep=[o for o in bpy.context.scene.objects if o.type=='ARMATURE'][0]
for o in list(bpy.context.scene.objects):
    if o!=keep:bpy.data.objects.remove(o,do_unlink=True)
found=set()
for a in list(bpy.data.actions):
    if a.name in USED and a.name not in found:found.add(a.name)
    else:bpy.data.actions.remove(a)
assert found==USED,USED-found
keep.animation_data_clear();ad=keep.animation_data_create()
for a in bpy.data.actions:  # one NLA strip per clip; the FBX exporter writes each strip as its own take
    t=ad.nla_tracks.new();t.name=a.name;s=t.strips.new(a.name,int(a.frame_range[0]),a);s.name=a.name
    if a.slots:s.action_slot=a.slots[0]
keep.select_set(True);bpy.context.view_layer.objects.active=keep
bpy.ops.export_scene.fbx(filepath=str(OUT/'ResidentClips.fbx'),use_selection=True,object_types={'ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,
                         bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=.5,
                         use_armature_deform_only=False,primary_bone_axis='Y',secondary_bone_axis='X')
(ART/'residents-manifest.json').write_text(json.dumps({'residents':report},indent=2)+'\n',encoding='utf-8')
print('V044_RESIDENTS_DONE',flush=True)
