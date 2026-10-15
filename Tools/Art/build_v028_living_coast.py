"""Build the V028 coastal props in a fresh background Blender process.
Uses the project's V023 metre axes, vertex palette and FBX export convention.
"""
from pathlib import Path
import bpy, math, json, hashlib, re
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV028/Coast'
ART = ROOT / 'ArtSource/V028'
for folder in (OUT, ART): folder.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'
world = bpy.data.worlds.new('V028 prop studio')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
scene.world = world
mat = bpy.data.materials.new('MAT-V028 coastal vertex palette')
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value = .58
vertex = mat.node_tree.nodes.new('ShaderNodeVertexColor')
vertex.layer_name = 'Color'
mat.node_tree.links.new(vertex.outputs['Color'], bsdf.inputs['Base Color'])
for name, pos, energy in [('key',(3,-4,6),700),('fill',(-3,-2,3),300),('rim',(1,4,4),500)]:
    light = bpy.data.lights.new('LGT-'+name, 'AREA')
    light.energy, light.size = energy, 4
    obj = bpy.data.objects.new('LGT-'+name, light)
    scene.collection.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (-obj.location).to_track_quat('-Z','Y').to_euler()
camera = bpy.data.objects.new('CAM-V028', bpy.data.cameras.new('CAM-V028'))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = 'ORTHO'
PARTS, MODELS, REPORT = [], [], []
WOOD=(.40,.25,.14); EDGE=(.64,.44,.25); TEAL=(.12,.37,.37)
CREAM=(.91,.81,.60); INK=(.08,.12,.15); IRON=(.42,.52,.55); BRASS=(.74,.53,.25)

def unity(p): return Vector((p[0],p[2],p[1]))

def paint(obj, name, color):
    obj.name = 'GEO-'+name
    attr = obj.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
    for corner in attr.data: corner.color_srgb=(*color,1)
    obj.data.color_attributes.active_color = attr
    obj.data.materials.append(mat)
    PARTS.append(obj)
    return obj

def box(name, p, size, color, bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=unity(p))
    obj=bpy.context.object
    obj.scale=(size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Soft edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return paint(obj,name,color)

def tube(name, a, b, r, color, end=None, sides=10):
    a,b=unity(a),unity(b)
    bpy.ops.mesh.primitive_cone_add(vertices=sides,radius1=r,radius2=r if end is None else end,depth=(b-a).length,location=(a+b)*.5)
    obj=bpy.context.object
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return paint(obj,name,color)

def rock(name,p,size,color,turn=0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=unity(p))
    obj=bpy.context.object;obj.scale=(size[0],size[2],size[1]);obj.rotation_euler=(.15,.2,turn)
    return paint(obj,name,color)

def leaf(a,b,width,color):
    a,b=unity(a),unity(b);middle=a.lerp(b,.48)
    side=(b-a).cross(Vector((0,1,0))).normalized()*width
    ridge=middle+Vector((0,-width*.22,0))
    mesh=bpy.data.meshes.new('Leaf blade')
    mesh.from_pydata([a,middle-side,b,middle+side,ridge],[],[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(4,1,0),(4,2,1),(4,3,2),(4,0,3)])
    mesh.update();obj=bpy.data.objects.new('GEO-leaf',mesh);scene.collection.objects.link(obj)
    return paint(obj,'leaf',color)

def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS:obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]
    bpy.ops.object.join();obj=bpy.context.object;obj.name='GEO-'+name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.clear();obj.data.materials.append(mat)
    for face in obj.data.polygons:face.material_index=0
    triangles=sum(len(face.vertices)-2 for face in obj.data.polygons)
    assert 20<triangles<=7500,(name,triangles)
    assert len(obj.data.color_attributes['Color'].data)==len(obj.data.loops),name
    low=Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)])
    high=Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
    if name not in ('Barbecue','FishCooler'):
        for vertex in obj.data.vertices:vertex.co.z-=low.z
        high.z-=low.z;low.z=0
    assert low.z>=-.035,(name,'below ground',low.z)
    if name in ('Barbecue','FishCooler'):
        assert high.x-low.x<=1.9 and high.y-low.y<=1.1 and high.z<=1.55,(name,low,high)
    path=OUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,mesh_smooth_type='FACE')
    assert path.stat().st_size>200
    for other in MODELS:other.hide_render=True
    centre=(low+high)*.5
    camera.location=centre+Vector((1.4,-2,1.35)).normalized()*4
    camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
    view=camera.rotation_euler.to_matrix().transposed()
    corners=[view@(Vector(v)-centre) for v in obj.bound_box]
    camera.data.ortho_scale=max(max(v.x for v in corners)-min(v.x for v in corners),max(v.y for v in corners)-min(v.y for v in corners))*1.18
    scene.render.filepath=str(OUT/(name+'Thumbnail.png'))
    bpy.ops.render.render(write_still=True)
    for suffix in ('.fbx','Thumbnail.png'):
        target=OUT/(name+suffix+'.meta')
        if not target.exists():
            template=(ROOT/'Assets/Resources/FishingV023/Items'/('Workbench'+suffix+'.meta')).read_text(encoding='utf-8')
            guid=hashlib.md5(target.relative_to(ROOT).as_posix().encode()).hexdigest()
            target.write_text(re.sub(r'(?m)^guid: .*$', 'guid: '+guid,template),encoding='utf-8')
    REPORT.append(dict(name=name,triangles=triangles,unity_min=[low.x,low.z,low.y],unity_max=[high.x,high.z,high.y],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    MODELS.append(obj);PARTS.clear()
    print('[V028-ART]',name,triangles,flush=True)

def ring(name,p,r,minor,color,rotation=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=24,minor_segments=6,location=unity(p),major_radius=r,minor_radius=minor,rotation=rotation)
    return paint(bpy.context.object,name,color)

def imported(source,name,length,cooked=False):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
    assert meshes,name
    coordinates=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    low=Vector([min(v[i] for v in coordinates) for i in range(3)])
    high=Vector([max(v[i] for v in coordinates) for i in range(3)])
    middle=(low+high)*.5
    axes=sorted(range(3),key=lambda i:high[i]-low[i],reverse=True)
    factor=length/(high[axes[0]]-low[axes[0]])
    for o in meshes:
        matrix=o.matrix_world.copy()
        colors=[]
        pixels={}
        for face in o.data.polygons:
            material=o.data.materials[face.material_index]
            image=next((n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None) if material.use_nodes else None
            if image and image.name not in pixels:pixels[image.name]=list(image.pixels[:])
            for corner in face.loop_indices:
                color=tuple(material.diffuse_color[:3])
                if image:
                    uv=o.data.uv_layers.active.data[corner].uv
                    ix=min(image.size[0]-1,max(0,int(uv.x*image.size[0])))
                    iy=min(image.size[1]-1,max(0,int(uv.y*image.size[1])))
                    offset=(iy*image.size[0]+ix)*4;color=tuple(pixels[image.name][offset:offset+3])
                if cooked:
                    lum=sum(color)/3
                    color=(.10,.065,.025) if lum<.18 else (.43+lum*.30,.17+lum*.23,.045+lum*.13)
                else:
                    lum=sum(color)/3
                    color=(.66,.51,.24) if lum>.64 else (.28+lum*.2,.15+lum*.12,.075+lum*.10)
                colors.append((corner,color))
        for v in o.data.vertices:
            p=matrix@v.co-middle
            # The whole food fish lies on its side; chest preserves upright source axes.
            v.co=Vector((p[axes[0]],p[axes[1]],p[axes[2]]*.42))*factor if cooked else p*factor
        o.matrix_world.identity()
        attr=o.data.color_attributes.get('Color') or o.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for corner,color in colors:
            if cooked:
                p=o.data.vertices[o.data.loops[corner].vertex_index].co
                u=(p.x+p.y*.4+.14)/.065
                if -.17<p.x<.12 and p.z>.015 and abs(u-round(u))<.10:color=(.22,.095,.025)
            attr.data[corner].color_srgb=(*color,1)
        o.data.color_attributes.active_color=attr;o.data.materials.clear();o.data.materials.append(mat)
        o.name='GEO-'+name;PARTS.append(o)
    if cooked:
        for o in meshes:
            minimum=min(v.co.z for v in o.data.vertices)
            for v in o.data.vertices:v.co.z+=.012-minimum
    return meshes

# A compact charcoal grill: enamel trough, ash tray, recessed coals, grate, wind shield and timber side board.
for x in (-.53,.53):
    for z in (-.28,.28):
        tube('splayed iron leg',(x*.93,.75,z*.85),(x*1.1,.045,z*1.24),.028,INK,sides=10)
        box('rubber foot',(x*1.1,.027,z*1.24),(.082,.05,.082),INK)
    tube('cross brace',(x,.21,-.28),(x,.21,.28),.018,IRON)
box('ash tray',(0,.64,0),(1.17,.06,.58),IRON,.012)
box('enamel bowl',(0,.75,0),(1.25,.18,.66),TEAL,.045)
box('coal bed',(0,.85,0),(1.10,.04,.51),INK,.015)
for j in range(16):
    x=(j%4-1.5)*.25;z=(j//4-1.5)*.115
    rock('coal ember',(x,.89,z),(.105,.045,.06),(.14,.12,.11),j)
    tube('ember fissure',(x-.04,.929,z),(x+.04,.936,z+.017),.008,(.94,.35,.075),sides=5)
for x in (-.59,.59):tube('grate frame',(x,.96,-.29),(x,.96,.29),.015,IRON,sides=10)
for z in (-.29,.29):tube('grate frame',(-.59,.96,z),(.59,.96,z),.015,IRON,sides=10)
for j in range(15):tube('steel grate',(-.55+j*.079,.966,-.285),(-.55+j*.079,.966,.285),.008,IRON,sides=8)
box('wind shield',(0,1.04,.35),(1.27,.30,.035),TEAL,.012)
for j in range(6):box('vent detail',(-.45+j*.18,1.05,.373),(.055,.075,.003),INK,.007)
for z in (-.24,.24):tube('side board bracket',(.62,.78,z),(.88,.91,z),.017,IRON)
for j in range(3):box('oiled side board',(.86,.94,(j-1)*.16),(.41,.065,.15),EDGE,.008)
tube('wooden grill handle',(-.56,.79,-.38),(.56,.79,-.38),.026,WOOD,sides=12)
for x in (-.5,.5):tube('handle support',(x,.79,-.33),(x,.79,-.38),.012,IRON)
export('Barbecue')

# Companion fishbox deliberately matches the home's cream and sea-glass furnishing palette.
box('insulated cooler body',(0,.29,0),(.86,.53,.56),TEAL,.065)
box('cooler lid',(0,.59,0),(.90,.12,.59),CREAM,.035)
box('lid raised lip',(0,.656,0),(.73,.035,.43),(.85,.78,.62),.045)
for x in (-.30,.30):
    box('lid latch',(x,.49,-.29),(.07,.16,.035),IRON,.008)
    tube('latch pin',(x-.04,.53,-.315),(x+.04,.53,-.315),.01,BRASS)
for x in (-.46,.46):
    tube('rope handle',(x,.48,-.13),(x,.41,0),.018,WOOD)
    tube('rope handle',(x,.41,0),(x,.48,.13),.018,WOOD)
box('fishbox badge',(0,.32,-.292),(.19,.13,.014),CREAM,.014)
export('FishCooler')

# Kenney CC0 food fish, reshaped to a warm scored roast with lemon, parsley and a dark eye.
imported(ROOT/'ArtSource/V019/Kenney/food-kit/Models/GLB format/fish.glb','roasted fish',.57,True)
for j in range(4):
    x=-.115+j*.063
    tube('charred diagonal scoring',(x-.027,.066,-.055),(x+.024,.074,.046),.005,(.23,.105,.035),sides=6)
    tube('golden scoring edge',(x-.019,.070,-.047),(x+.029,.077,.036),.002,(.92,.61,.27),sides=5)
for x,z in ((.10,-.15),(.19,-.14)):
    tube('lemon rind',(x,.008,z),(x,.025,z),.065,(.88,.67,.14),sides=16)
    tube('lemon flesh',(x,.026,z),(x,.031,z),.052,(1,.87,.39),sides=16)
    for j in range(8):
        a=j*math.pi/4;tube('lemon segment',(x,.033,z),(x+math.cos(a)*.047,.033,z+math.sin(a)*.047),.002,(1,.94,.70),sides=4)
for j in range(4):
    a=j*2.1
    leaf((-.10,.012,-.13),(-.10+math.cos(a)*.06,.026,-.13+math.sin(a)*.06),.022,(.23,.42,.15))
export('GrilledFish')

# An old sea boot: continuous bent upper, rolled cuff, layered sole, eyelets, laces and seaweed.
box('worn sole',(.045,.035,0),(.31,.055,.48),(.12,.13,.12),.04)
box('rubber toe',(.045,.105,-.125),(.29,.14,.26),(.28,.27,.19),.065)
box('boot foot',(.035,.125,.035),(.265,.18,.25),(.34,.32,.23),.05)
box('heel shaft',(.02,.285,.115),(.24,.32,.22),(.32,.30,.21),.035)
box('open cuff interior',(.02,.451,.115),(.188,.015,.172),(.075,.085,.075),.025)
for x in (-.105,.145):box('rolled cuff',(x,.45,.115),(.036,.049,.24),(.45,.42,.29),.014)
for z in (-.008,.238):box('rolled cuff',(.02,.45,z),(.24,.049,.036),(.45,.42,.29),.014)
for j in range(5):
    y=.18+j*.043;z=-.04+j*.012
    for x in (-.067,.108):rock('brass eyelet',(x,y,z),(.014,.012,.007),BRASS)
    tube('frayed lace',(-.067,y,z-.008),(.108,y+.02,z-.011),.004,CREAM,sides=5)
for j in range(6):box('sole tread',(.045,.008,-.17+j*.063),(.23,.012,.025),(.24,.22,.16),.003)
for j in range(3):leaf((-.10,.04,-.15+j*.09),(-.18,.21+j*.025,-.12+j*.1),.016,(.21,.34,.19))
export('OldBoot')

# Dented food tin, rolled steel rims, pull ring, flaking sea-green label and rust seams.
tube('dented tin body',(0,.035,0),(0,.32,0),.115,(.43,.46,.42),end=.108,sides=16)
for y in (.035,.32):ring('rolled can rim',(0,y,0),.113,.009,IRON)
tube('tin lid',(0,.319,0),(0,.327,0),.105,(.50,.53,.47),sides=16)
ring('pull ring',(0,.339,-.015),.026,.005,IRON)
for j in range(10):
    a=j*math.pi/5
    tube('corrugated seam',(math.cos(a)*.115,.10,math.sin(a)*.115),(math.cos(a)*.112,.25,math.sin(a)*.112),.004,(.31,.35,.32),sides=5)
for j in range(9):
    a=j*2.4
    rock('rust flake',(math.cos(a)*.116,.06+(j%4)*.067,math.sin(a)*.116),(.024,.025,.009),(.48,.23,.10),a)
box('torn fish label',(0,.19,-.116),(.13,.11,.006),(.50,.63,.52),.006)
export('RustyCan')

# Reuse the licensed chest silhouette, then add brass rivets, clasp, shell and encrustation.
imported(ROOT/'ArtSource/V019/Kenney/pirate-kit/Models/GLB format/chest.glb','sea treasure chest',.48)
# Imported source is centred; move its complete mesh to the surface before adding sea detail.
minimum=min(v.co.z for o in PARTS for v in o.data.vertices)
for o in PARTS:
    for v in o.data.vertices:v.co.z-=minimum
for x in (-.16,.16):
    for y in (.07,.19):rock('brass rivet',(x,y,-.155),(.009,.009,.005),BRASS)
box('brass clasp',(0,.16,-.161),(.056,.09,.018),BRASS,.008)
ring('keyhole surround',(0,.15,-.174),.012,.004,INK,(math.pi/2,0,0))
for j in range(5):
    rock('barnacle',(-.19+j*.026,.03+(j%2)*.032,-.14),(.016,.012,.009),(.75,.73,.61),j)
export('SeaTreasure')

assert len(REPORT)==6 and len({row['name'] for row in REPORT})==6
(ART/'coast-manifest.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
for i,obj in enumerate(MODELS):obj.hide_render=False;obj.location=(i%3*2.3,i//3*2.3,0)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Living-coast-v028.blend'))
print('[V028-ART] COMPLETE',len(REPORT),flush=True)
