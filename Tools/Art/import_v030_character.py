"""Bind the supplied 9S surface to the existing calibrated rig/locomotion in a fresh Blender process."""
from pathlib import Path
import bpy, json, shutil, zipfile
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]; ART=ROOT/'ArtSource/V030'; OUT=ROOT/'Assets/Resources/FishingCharacter'
if not (ART/'9s/gas gas gas.fbx').exists():
    with zipfile.ZipFile(ART/'9s-original.zip') as archive:
        for entry in archive.infolist():
            assert (ART/'9s'/entry.filename).resolve().is_relative_to((ART/'9s').resolve()),entry.filename
        archive.extractall(ART/'9s')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Resources/FishingCharacter/BeachCharacter.fbx'))
old_objects=set(bpy.data.objects); rig=next(o for o in old_objects if o.type=='ARMATURE')
rig.animation_data.action=None
for p in rig.pose.bones: p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
original_body=next(o for o in old_objects if o.type=='MESH' and o.name=='Beach_Body')
hand_names={b.name for b in rig.data.bones if b.name.startswith(('Wrist','Index','Middle','Ring','Pinky','Thumb'))}
original_weights=[{original_body.vertex_groups[g.group].name:g.weight for g in v.groups} for v in original_body.data.vertices]
hand_faces=[p for p in original_body.data.polygons if all(sum(w for n,w in original_weights[i].items() if n in hand_names)>.3 for i in p.vertices)]
bpy.ops.import_scene.fbx(filepath=str(ART/'9s/gas gas gas.fbx'))
new_objects=set(bpy.data.objects)-old_objects; source_rig=next(o for o in new_objects if o.type=='ARMATURE')
source=next(o for o in new_objects if o.type=='MESH')
bpy.context.view_layer.update()
evaluated=source.evaluated_get(bpy.context.evaluated_depsgraph_get()); mesh=evaluated.to_mesh()
points=[source.matrix_world@v.co for v in mesh.vertices]; weights=[[(source.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in source.data.vertices]
assert len(points)==len(weights)
mapping={'Hips':'Body','Spine':'Abdomen','Spine1':'Torso','Spine2':'Chest','Neck':'Neck','Head':'Head','HeadTop_End':'Head'}
for source_side,side in [('Left','.L'),('Right','.R')]:
    for a,b in [('Shoulder','Shoulder'),('Arm','UpperArm'),('ForeArm','LowerArm'),('Hand','Wrist'),('UpLeg','UpperLeg'),('Leg','LowerLeg'),('Foot','Foot'),('ToeBase','Foot'),('Toe_End','Foot')]: mapping[source_side+a]=b+side
    for digit in ['Thumb','Index','Ring']:
        for i in range(1,5): mapping[source_side+'Hand'+digit+str(i)]=digit+str(min(i,3) if digit=='Thumb' else min(i+1,4))+side
transforms={}; global_scale=1.12
for p in source_rig.pose.bones:
    target_name=mapping.get(p.name.split(':')[-1]);
    if not target_name: continue
    target=rig.data.bones[target_name]
    src=source_rig.matrix_world@p.head; src_tail=source_rig.matrix_world@p.tail
    dst=rig.matrix_world@target.head_local; dst_tail=rig.matrix_world@target.tail_local
    if p.name=='mixamorig:Hips':dst.z=1.01;dst_tail=dst+Vector((0,0,.12))
    key=p.name.split(':')[-1]
    if key.endswith('Leg') and not key.endswith('UpLeg'):
        side='.L' if key.startswith('Left') else '.R'
        thigh=rig.data.bones['UpperLeg'+side]
        dst_tail=dst+(dst_tail-dst).normalized()*(rig.matrix_world.to_3x3()@(thigh.tail_local-thigh.head_local)).length
    if any(key.endswith(k) for k in ['Foot','ToeBase','Toe_End']):
        side='.L' if key.startswith('Left') else '.R';prefix='Left' if side=='.L' else 'Right'
        foot=source_rig.pose.bones['mixamorig:'+prefix+'Foot'];src=source_rig.matrix_world@foot.head
        shin=rig.data.bones['LowerLeg'+side];thigh=rig.data.bones['UpperLeg'+side]
        dst=rig.matrix_world@shin.head_local+(rig.matrix_world.to_3x3()@(shin.tail_local-shin.head_local)).normalized()*(rig.matrix_world.to_3x3()@(thigh.tail_local-thigh.head_local)).length
        src_tail=src+Vector((0,-.1,0));dst_tail=dst+Vector((0,-.1,0))
    rotation=(src_tail-src).rotation_difference(dst_tail-dst)
    axis=(src_tail-src).normalized()
    axial=(dst_tail-dst).length/(src_tail-src).length if key.endswith(('UpLeg','Leg','Arm','ForeArm')) else global_scale
    transforms[p.name]=(target_name,src,dst,rotation,axis,axial)

converted=[]; mapped=[]
for point,groups in zip(points,weights):
    result=Vector(); total=0; targets={}
    for name,weight in groups:
        if name not in transforms: continue
        target,src,dst,rotation,axis,axial=transforms[name]
        offset=point-src;along=axis*offset.dot(axis)
        result+=(dst+rotation@((offset-along)*global_scale+along*axial))*weight; total+=weight
        targets[target]=targets.get(target,0)+weight
    assert total>.1
    converted.append(result/total); mapped.append(targets)

# Keep the project's body/head/legs contract and original animation paths.
categories={'Beach_Head':[], 'Beach_Body':[], 'Beach_Legs':[]}
materials=list(source.data.materials)
for polygon in source.data.polygons:
    mat=materials[polygon.material_index].name
    if 'gloves' in mat and sum(sum(w for n,w in weights[i] if 'Hand' in n) for i in polygon.vertices)/len(polygon.vertices)>.3:continue
    category='Beach_Head' if any(k in mat for k in ['hair','face','eyepatch']) else 'Beach_Legs' if any(k in mat for k in ['short','socks','shoes']) else 'Beach_Body'
    categories[category].append(polygon)
runtime_materials={}
for old_material in materials:
    name='9SHair' if 'hair' in old_material.name else '9SFace' if 'face' in old_material.name else '9SClothes'
    if name not in runtime_materials:
        material=bpy.data.materials.new(name); material.use_nodes=True
        bs=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        image=next(n.image for n in old_material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_d.png' in n.image.name)
        destination=OUT/(name+'Albedo.png'); shutil.copy2(bpy.path.abspath(image.filepath),destination)
        image=bpy.data.images.load(str(destination),check_existing=False)
        tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        bs.inputs['Roughness'].default_value=.7;runtime_materials[name]=material

output=[]
for category,faces in categories.items():
    used=sorted({i for f in faces for i in f.vertices}); remap={v:i for i,v in enumerate(used)}
    data=bpy.data.meshes.new(category); data.from_pydata([rig.matrix_world.inverted()@converted[i] for i in used],[],[[remap[i] for i in f.vertices] for f in faces]);data.update()
    obj=bpy.data.objects.new(category+'030',data);bpy.context.scene.collection.objects.link(obj);obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4)
    for mat in runtime_materials.values(): data.materials.append(mat)
    uv=data.uv_layers.new(name='UVMap')
    for dest,original in zip(data.polygons,faces):
        original_name=materials[original.material_index].name
        material_name='9SHair' if 'hair' in original_name else '9SFace' if 'face' in original_name else '9SClothes'
        dest.material_index=list(runtime_materials).index(material_name);dest.use_smooth=True
        for di,si in zip(dest.loop_indices,original.loop_indices): uv.data[di].uv=source.data.uv_layers.active.data[si].uv
    for b in rig.data.bones: obj.vertex_groups.new(name=b.name)
    for di,si in enumerate(used):
        for name,weight in mapped[si].items(): obj.vertex_groups[name].add([di],weight,'REPLACE')
    modifier=obj.modifiers.new('Calibrated game rig','ARMATURE');modifier.object=rig;output.append(obj)
# Reuse the proven five-finger surface inside the supplied sleeve cuffs. The
# supplied rig has only three finger chains and cannot reproduce the existing grasps.
body=next(o for o in output if o.name.startswith('Beach_Body'))
used=sorted({i for p in hand_faces for i in p.vertices});remap={v:i for i,v in enumerate(used)}
data=bpy.data.meshes.new('Calibrated glove surface');data.from_pydata([rig.matrix_world.inverted()@(original_body.matrix_world@original_body.data.vertices[i].co) for i in used],[],[[remap[i] for i in p.vertices] for p in hand_faces]);data.update()
hands=bpy.data.objects.new('Calibrated gloves',data);bpy.context.scene.collection.objects.link(hands);hands.parent=rig
material=bpy.data.materials.new('9SGloves');material.use_nodes=True
image=bpy.data.images.new('9SGlovesAlbedo',width=8,height=8);image.pixels=[v for i in range(64) for v in (.055,.065,.075,1)];image.filepath_raw=str(OUT/'9SGlovesAlbedo.png');image.file_format='PNG';image.save()
bs=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED');tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.8;data.materials.append(material)
data.uv_layers.new(name='UVMap')
for bone in rig.data.bones:hands.vertex_groups.new(name=bone.name)
for di,si in enumerate(used):
    for name,weight in original_weights[si].items():hands.vertex_groups[name].add([di],weight,'REPLACE')
for p in data.polygons:p.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);hands.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
evaluated.to_mesh_clear()
for obj in list(bpy.data.objects):
    if obj!=rig and obj not in output:bpy.data.objects.remove(obj,do_unlink=True)
for obj in output:obj.name=obj.name.replace('030','');obj.data.name=obj.name
rig.animation_data.action=next(a for a in bpy.data.actions if a.name.endswith('Idle'))
bpy.context.scene.frame_set(1)
for obj in bpy.context.scene.objects:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'9SCharacter.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for action in bpy.data.actions:
    if action.name.endswith(('Idle','Walk','Run')):action.use_fake_user=True
bpy.data.orphans_purge(do_recursive=True)
for image in bpy.data.images:
    if image.has_data and image.source=='FILE':image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'9s-adapted.blend'))
report={'source':'9s-original.zip / gas gas gas.fbx','surface_triangles':sum(len(p.vertices)-2 for o in output for p in o.data.polygons),'rig':'Existing CC0 Quaternius calibrated rig and Idle/Walk/Run','skins':{o.name:len(o.data.vertices) for o in output},'adaptation':'Supplied 9S surface and sleeve cuffs; proven CC0 five-finger hand surface reused as dark gloves for calibrated tool contact.'}
assert 15000<report['surface_triangles']<22000 and all(v>100 for v in report['skins'].values()),report
(ART/'character-import.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('V030 CHARACTER PASS',json.dumps(report),flush=True)
