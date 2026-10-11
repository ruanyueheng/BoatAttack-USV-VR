"""Selected CC0 assets and original nautical props, in Unity metres. Run in a fresh background Blender.
The shared vertex palette removes texture/material dependencies from the small runtime models.
"""
from pathlib import Path
import bpy, math, json, hashlib
from mathutils import Vector, Matrix, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV019'
SOURCE = ROOT / 'ArtSource/V019'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
REPORT, GALLERY = [], []
mat = bpy.data.materials.new('MAT-V019-vertex-palette')
mat.use_nodes = True
bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
vertex = mat.node_tree.nodes.new('ShaderNodeVertexColor'); vertex.layer_name = 'Color'
mat.node_tree.links.new(vertex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = .65

def U(p): return Vector((p[0], -p[2], p[1]))

def paint(obj, color):
    for attribute in list(obj.data.color_attributes): obj.data.color_attributes.remove(attribute)
    attr = obj.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
    for item in attr.data: item.color_srgb = (*color[:3], 1)
    obj.data.color_attributes.active_color = attr
    obj.data.materials.clear(); obj.data.materials.append(mat)
    return obj

def export(name, objects, source):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join(); obj = bpy.context.object; obj.name = 'GEO-' + name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    points = [v.co for v in obj.data.vertices]
    low = min(v.z for v in points)
    for v in obj.data.vertices: v.co.z -= low
    obj.data.materials.clear(); obj.data.materials.append(mat)
    for p in obj.data.polygons: p.material_index = 0
    bpy.ops.export_scene.fbx(filepath=str(OUT / (name + '.fbx')), use_selection=True,
        object_types={'MESH'}, bake_anim=False, axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='FACE')
    assert (OUT / (name + '.fbx')).stat().st_size > 200
    triangles = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    assert triangles < 18000, (name, triangles)
    REPORT.append(dict(name=name, triangles=triangles, dimensions=list(obj.dimensions),
        source=source, sha256=hashlib.sha256((OUT / (name + '.fbx')).read_bytes()).hexdigest()))
    obj.location = Vector((len(GALLERY) % 8 * 5, len(GALLERY) // 8 * 5, 0))
    GALLERY.append(obj)
    print('V019_ASSET', name, triangles, flush=True)

selected = {
    'pirate-kit': ['bottle', 'bottle-large', 'crate-bottles', 'barrel', 'chest', 'tool-shovel',
        'tool-paddle', 'ship-wreck', 'mast-ropes', 'cannon-mobile', 'tower-watch', 'flag-pennant'],
    'food-kit': ['apple', 'banana', 'bread', 'fish', 'bag', 'bottle-oil', 'mug', 'pot', 'cheese', 'whole-ham'],
    'nature-kit': ['tree_oak', 'tree_pineTallA', 'tree_palmDetailedTall', 'tent_detailedOpen',
        'mushroom_redGroup', 'flower_purpleA', 'plant_bush', 'campfire_logs', 'stump_old',
        'stone_tallC', 'cliff_blockCave_rock']
}
for pack, names in selected.items():
    for name in names:
        paths = list((SOURCE / 'Kenney' / pack).glob('**/' + name + '.glb'))
        if not paths: paths = list((SOURCE / 'Kenney' / pack).glob('**/' + name + '.fbx'))
        assert paths, (pack, name)
        path = paths[0]; before = set(bpy.data.objects)
        if path.suffix == '.glb': bpy.ops.import_scene.gltf(filepath=str(path))
        else: bpy.ops.import_scene.fbx(filepath=str(path))
        imported = [o for o in bpy.data.objects if o not in before]
        meshes = [o for o in imported if o.type == 'MESH']
        image_pixels = {}
        for obj in meshes:
            colors = []
            for material in obj.data.materials:
                node = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if material and material.use_nodes else None
                tex = next((n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), None) if node else None
                rgb = node.inputs['Base Color'].default_value[:] if node else (material.diffuse_color[:] if material else (.6, .6, .6, 1))
                if pack=='nature-kit' and material:
                    if 'leafsGreen' in material.name:rgb=(.16,.42,.27,1)
                    elif 'woodBark' in material.name:rgb=(.34,.22,.13,1)
                colors.append((rgb, tex))
            for previous in list(obj.data.color_attributes):obj.data.color_attributes.remove(previous)
            attr = obj.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
            uv = obj.data.uv_layers.active
            for face in obj.data.polygons:
                rgb, tex = colors[face.material_index] if colors else ((.6, .6, .6, 1), None)
                if tex and tex.name not in image_pixels: image_pixels[tex.name] = list(tex.pixels)
                for i in face.loop_indices:
                    value = rgb
                    if tex and uv and tex.size[0]:
                        u, v = uv.data[i].uv; w, h = tex.size[:]
                        x = max(0, min(w-1, int(u * w))); y = max(0, min(h-1, int(v * h)))
                        value = image_pixels[tex.name][(y*w+x)*4:(y*w+x)*4+4]
                    attr.data[i].color_srgb = (*value[:3], 1)
            obj.data.color_attributes.active_color = attr
            obj.data.materials.clear(); obj.data.materials.append(mat)
            for p in obj.data.polygons: p.material_index = 0
        imported_names = [o.name for o in imported]
        export(name, meshes, str(path.relative_to(ROOT)))
        for imported_name in imported_names:
            obj = bpy.data.objects.get(imported_name)
            if obj is not None and obj not in GALLERY: bpy.data.objects.remove(obj, do_unlink=True)

PARTS = []
def cube(name, point, size, color, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(point)); o = bpy.context.object
    o.name = 'GEO-' + name; o.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = o.modifiers.new('Rounded working edges', 'BEVEL'); mod.width=bevel; mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    paint(o, color); PARTS.append(o); return o

def tube(name, start, end, radius, color, sides=16):
    a,b = U(start),U(end)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length, location=(a+b)*.5)
    o=bpy.context.object; o.name='GEO-'+name; o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    paint(o,color); PARTS.append(o); return o

def ring(name, point, radius, thickness, color, vertical=False):
    bpy.ops.mesh.primitive_torus_add(major_segments=32, minor_segments=6, location=U(point), major_radius=radius, minor_radius=thickness)
    o=bpy.context.object; o.name='GEO-'+name
    if vertical: o.rotation_euler.x=math.pi/2
    paint(o,color); PARTS.append(o); return o

BRASS=(.78,.52,.21); TEAL=(.07,.30,.32); DARK=(.045,.09,.13); IVORY=(.91,.85,.68); GLASS=(.25,.73,.78); WOOD=(.39,.25,.13)
for name in ['Compass','RopeCoil','Lantern','Binoculars','Telescope','WindBell','StarPrism']:
    PARTS=[]
    if name=='Compass':
        tube('compass base',(0,0,0),(0,.05,0),.24,BRASS,32)
        tube('ivory compass dial',(0,.05,0),(0,.059,0),.205,IVORY,32)
        for j in range(12):
            a=j*math.pi/6; tube('dial tick',(math.sin(a)*.17,.068,math.cos(a)*.17),(math.sin(a)*.20,.068,math.cos(a)*.20),.006,DARK,6)
        cube('needle',(0,.075,0),(.025,.018,.29),TEAL,.004); ring('carrying loop',(0,.06,-.29),.045,.012,BRASS)
    elif name=='RopeCoil':
        for j in range(7): ring('rope spiral',(0,.025+j*.017,0),.09+j*.026,.016,(.67,.52,.32))
        tube('free rope tail',(.25,.03,0),(.42,.03,-.21),.016,(.67,.52,.32))
    elif name=='Lantern':
        tube('base',(0,0,0),(0,.055,0),.16,TEAL)
        tube('warm lantern chamber',(0,.05,0),(0,.32,0),.115,(1,.72,.25))
        tube('top',(0,.33,0),(0,.38,0),.16,TEAL)
        for x in [-.12,.12]:
            for z in [-.08,.08]: tube('brass cage',(x,.04,z),(x,.35,z),.014,BRASS,8)
        ring('handle',(0,.47,0),.09,.014,BRASS,True)
    elif name=='Binoculars':
        for x in [-.11,.11]:
            tube('optics',(x,.12,-.16),(x,.12,.16),.075,DARK)
            tube('objective glass',(x,.12,.16),(x,.12,.18),.068,GLASS)
            ring('brass focusing ring',(x,.12,.08),.076,.009,BRASS,True)
        cube('hinge',(0,.12,0),(.12,.07,.09),BRASS)
    elif name=='Telescope':
        tube('scope barrel',(0,.55,-.35),(0,.62,.4),.09,BRASS)
        tube('scope lens',(0,.62,.4),(0,.624,.43),.083,GLASS)
        tube('tripod column',(0,.08,0),(0,.59,0),.018,DARK)
        for a in range(3): tube('tripod leg',(0,.22,0),(math.cos(a*2.094)*.21,0,math.sin(a*2.094)*.21),.015,DARK)
    elif name=='WindBell':
        tube('bell frame',(0,0,0),(0,.38,0),.12,BRASS)
        tube('striker',(0,.06,0),(0,.29,0),.028,DARK)
        ring('suspension',(0,.45,0),.065,.012,BRASS,True)
    else:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.27,location=U((0,.32,0)))
        o=bpy.context.object; o.name='GEO-star-prism';o.scale=(.75,.75,1.65);paint(o,GLASS);PARTS.append(o)
        tube('prism base',(0,0,0),(0,.1,0),.25,BRASS,8)
    export(name,PARTS,'Project-authored Blender geometry')

# Real CC0 base geometry, opaque regional work clothes and relaxed arms; never primitive NPC bodies.
for id, sex, hair, shirt, hair_color in [
    (0,'Female','Long',(.74,.57,.25),(.23,.12,.055)),
    (1,'Female','Buns',(.19,.38,.24),(.08,.12,.07)),
    (2,'Male','Buzzed',(.58,.25,.15),(.15,.10,.075)),
    (3,'Male','SimpleParted',(.18,.23,.44),(.55,.58,.61)),
    (5,'Male','Beard',(.11,.22,.30),(.67,.65,.58))]:
    before=set(bpy.data.objects)
    path=SOURCE/'Characters'/('Superhero_'+sex+'_FullBody.fbx')
    bpy.ops.import_scene.fbx(filepath=str(path))
    imported=[o for o in bpy.data.objects if o not in before]
    rig=next(o for o in imported if o.type=='ARMATURE')
    body=next(o for o in imported if o.type=='MESH' and o.name.lower().startswith('superhero_'))
    source_points=[body.matrix_world@v.co for v in body.data.vertices]
    bottom=min(v.z for v in source_points); height=max(v.z for v in source_points)-bottom
    skin=(.75,.50,.32) if id==2 else (.84,.66,.49)
    colors={'Skin':skin,'Shirt':shirt,'Trousers':(.10,.15,.18),'Boots':(.055,.075,.08),'Hair':hair_color,'Eyes':(.08,.06,.035)}
    stubs={k:bpy.data.materials.new('MAT-NPC-'+str(id)+'-'+k) for k in colors}
    for k,m in stubs.items(): m.diffuse_color=(*colors[k],1)
    body.data.materials.clear()
    keys=['Skin','Shirt','Trousers','Boots']
    for k in keys: body.data.materials.append(stubs[k])
    for p in body.data.polygons:
        weights={}
        for i in p.vertices:
            for g in body.data.vertices[i].groups:
                key=body.vertex_groups[g.group].name;weights[key]=weights.get(key,0)+g.weight
        joint=max(weights,key=weights.get) if weights else 'pelvis'
        z=sum((source_points[i].z-bottom)/height for i in p.vertices)/len(p.vertices)
        hand=joint.startswith(('hand_','index_','middle_','ring_','pinky_','thumb_'))
        key='Skin' if hand or joint in {'Head','neck_01'} or z>.855 else 'Boots' if z<.065 else 'Trousers' if z<.56 else 'Shirt'
        p.material_index=keys.index(key)
    eye=next(o for o in imported if o.type=='MESH' and 'Eyes' in o.name)
    front=sum((eye.matrix_world@v.co).y for v in eye.data.vertices)/len(eye.data.vertices)
    sign=1 if front>0 else -1
    for side in ['l','r']:
        bone=rig.pose.bones['upperarm_'+side];world=rig.matrix_world@bone.matrix;pivot=world.translation.copy()
        side_sign=1 if (rig.matrix_world@bone.head).x>0 else -1
        rotation=Quaternion(Vector((0,1,0)),side_sign*math.radians(70)).to_matrix().to_4x4()
        bone.matrix=rig.matrix_world.inverted()@Matrix.Translation(pivot)@rotation@Matrix.Translation(-pivot)@world
        bpy.context.view_layer.update()
    more_before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE/'Characters'/('Hair_'+hair+'.fbx')))
    imported += [o for o in bpy.data.objects if o not in more_before]
    dg=bpy.context.evaluated_depsgraph_get(); baked=[]
    canonical=Matrix.Rotation(math.pi if sign>0 else 0,4,'Z')
    for obj in [o for o in imported if o.type=='MESH']:
        if obj!=body:
            obj.data.materials.clear();obj.data.materials.append(stubs['Eyes' if obj==eye else 'Hair'])
            for p in obj.data.polygons:p.material_index=0
        mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(dg),depsgraph=dg)
        mesh.transform(canonical@obj.matrix_world)
        o=bpy.data.objects.new('GEO-NPC-baked',mesh);bpy.context.collection.objects.link(o)
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        if len(mesh.polygons)>1500:
            mod=o.modifiers.new('Runtime mesh budget','DECIMATE');mod.ratio=.30
            bpy.ops.object.modifier_apply(modifier=mod.name)
        for previous in list(o.data.color_attributes):o.data.color_attributes.remove(previous)
        attr=o.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for face in o.data.polygons:
            m=o.data.materials[face.material_index]
            value=m.diffuse_color[:]
            for loop in face.loop_indices:attr.data[loop].color_srgb=(*value[:3],1)
        o.data.color_attributes.active_color=attr
        o.data.materials.clear();o.data.materials.append(mat)
        for p in o.data.polygons:p.material_index=0
        baked.append(o)
    scale=(1.78 if sex=='Male' else 1.70)/height
    for o in baked:
        for v in o.data.vertices:v.co=(v.co-Vector((0,0,bottom)))*scale
    # A draped cloth shell hides anatomical chest/waist contours; colour alone is not a work shirt.
    adult_height=1.78 if sex=='Male' else 1.70
    width=.29 if sex=='Male' else .265
    depth=.185 if sex=='Male' else .16
    profiles=[(.52*adult_height,width*.92,.14),(.66*adult_height,width,depth),(.77*adult_height,width,depth),(.84*adult_height,.075,.06)]
    vertices=[];faces=[];sides=24
    for z,rx,ry in profiles:
        for j in range(sides):
            angle=j*2*math.pi/sides;vertices.append((math.cos(angle)*rx,math.sin(angle)*ry,z))
    for ring_index in range(len(profiles)-1):
        for j in range(sides):
            a=ring_index*sides+j;b=ring_index*sides+(j+1)%sides;faces.append((a,b,b+sides,a+sides))
    fabric=bpy.data.meshes.new('Regional work tunic');fabric.from_pydata(vertices,[],faces);fabric.update()
    garment=bpy.data.objects.new('GEO-regional-work-tunic',fabric);bpy.context.collection.objects.link(garment)
    for face in fabric.polygons:face.use_smooth=True
    paint(garment,shirt);baked.append(garment)
    export('Resident'+str(id),baked,str(path.relative_to(ROOT)))
    for o in imported:
        if o.name in bpy.data.objects and o not in GALLERY:bpy.data.objects.remove(o,do_unlink=True)

(SOURCE/'asset-manifest.json').write_text(json.dumps(REPORT,indent=2,ensure_ascii=False),encoding='utf-8')
for unused_material in list(bpy.data.materials):
    if unused_material.users==0:bpy.data.materials.remove(unused_material)
for image in list(bpy.data.images):
    if image.users==0:bpy.data.images.remove(image)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'tidal-assets-v019.blend'))
assert len(REPORT)==len(set(r['name'] for r in REPORT))
print('V019_ART_PASS',len(REPORT),flush=True)
