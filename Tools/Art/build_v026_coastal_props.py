"""Build the V026 coastal props in a fresh background Blender process.
Uses the project's V023 metre axes, vertex palette and FBX export convention.
"""
from pathlib import Path
import bpy, math, json, hashlib, re
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV026/Items'
ART = ROOT / 'ArtSource/V026'
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
world = bpy.data.worlds.new('V026 prop studio')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
scene.world = world
mat = bpy.data.materials.new('MAT-V026 coastal vertex palette')
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
camera = bpy.data.objects.new('CAM-V026', bpy.data.cameras.new('CAM-V026'))
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
    if name not in ('Workbench','Furnace','Forge'):
        for vertex in obj.data.vertices:vertex.co.z-=low.z
        high.z-=low.z;low.z=0
    assert low.z>=-.035,(name,'below ground',low.z)
    if name in ('Workbench','Furnace','Forge'):
        assert high.x-low.x<=1.4 and high.y-low.y<=1.1 and high.z<=1.55,(name,low,high)
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
    print('[V026-ART]',name,triangles,flush=True)

# Joinery, drawer, vise, peg rail and hand tools distinguish the workbench.
for x in (-.53,.53):
    for z in (-.31,.31):
        box('bench leg',(x,.425,z),(.105,.85,.105),TEAL)
        box('iron foot',(x,.06,z),(.118,.12,.118),INK)
    box('side stretcher',(x,.27,0),(.085,.09,.66),WOOD)
for j in range(5):box('oiled plank',(0,.915,(j-2)*.167),(1.30,.105,.158),tuple(c+(j%2)*.025 for c in EDGE))
box('drawer apron',(0,.66,-.30),(1.05,.25,.10),TEAL)
for x in (-.27,.27):
    box('drawer front',(x,.67,-.37),(.49,.19,.06),WOOD)
    tube('brass drawer pull',(x-.05,.68,-.411),(x+.05,.68,-.411),.01,BRASS)
for j in range(4):box('shelf plank',(0,.25,(j-1.5)*.155),(1.09,.035,.145),WOOD)
for j in range(3):box('spare timber',(-.19+j*.10,.295,-.02),(.078,.05,.48),EDGE)
for x in (-.54,.54):box('peg rail post',(x,1.06,.36),(.05,.30,.055),WOOD)
box('tool rail',(0,1.18,.36),(1.15,.11,.055),TEAL)
for x in (-.38,-.18,.02):tube('rail peg',(x,1.18,.35),(x,1.18,.30),.012,BRASS)
box('vise base',(.4,1,.18),(.21,.08,.23),INK)
for z in (.09,.28):box('vise jaw',(.4,1.07,z),(.20,.09,.044),IRON)
tube('vise screw',(.4,1.025,.08),(.4,1.025,.40),.014,IRON)
tube('vise handle',(.32,1.025,.41),(.48,1.025,.41),.01,BRASS)
tube('hammer haft',(-.36,.985,-.18),(-.12,.985,-.03),.018,WOOD)
box('hammer face',(-.12,1.005,-.03),(.07,.07,.15),IRON)
box('folded plan',(-.15,.977,.19),(.35,.006,.19),CREAM,.002)
for j in range(3):box('plan marks',(-.15,.982,.14+j*.04),(.22,.002,.004),TEAL,0)
for x in (-.56,.56):
    for z in (-.30,.30):tube('top bolt',(x,.966,z),(x,.971,z),.012,BRASS,sides=6)
export('Workbench')

# The front arch and recessed black firebox remain visibly open while idle.
box('hearth',(0,.075,0),(1.08,.15,1.02),(.31,.34,.35),.025)
for row in range(5):
    color=(.48+row*.018,.31+row*.01,.23+row*.009)
    for s in (-1,1):
        for j in range(3):box('side kiln brick',(s*.43,.22+row*.16,-.32+j*.30),(.20,.145,.285),color,.012)
    for j in range(3):box('back kiln brick',((j-1)*.29,.22+row*.16,.37),(.28,.145,.17),color,.012)
for j in range(9):
    a=j*math.pi/8
    obj=box('arch stone',(.34*math.cos(a),.54+.34*math.sin(a),-.37),(.13,.16,.22),(.70,.48,.31),.008)
    obj.rotation_euler.y=math.pi/2-a
box('mantel',(0,1.0,0),(1.05,.14,1.0),(.44,.45,.41),.018)
box('flue',(0,1.27,.18),(.34,.40,.32),INK,.018)
box('flue crown',(0,1.46,.18),(.42,.065,.40),IRON,.012)
box('flue darkness',(0,1.495,.18),(.27,.008,.25),(.02,.025,.03),.001)
for j in range(5):tube('fire grate',(-.28+j*.14,.205,-.43),(-.28+j*.14,.205,.25),.014,IRON)
box('ash lip',(0,.175,-.44),(.70,.04,.06),IRON)
for x in (-.48,.48):
    box('metal band',(x,.68,0),(.03,.045,.93),INK,.002)
    tube('band rivet',(x,.68,-.473),(x,.68,-.485),.013,BRASS,sides=6)
export('Furnace')

# A real anvil, hearth, tongs and stock replace the previous box-built forge.
box('forge footing',(0,.065,0),(1.26,.13,.90),(.32,.35,.36),.018)
for x in (-.47,.47):
    for z in (-.28,.28):box('forge leg',(x,.43,z),(.13,.69,.13),WOOD)
box('forge slab',(0,.76,0),(1.28,.14,.91),(.39,.41,.41),.02)
box('anvil plinth',(-.20,.875,-.02),(.53,.09,.35),INK,.014)
box('anvil waist',(-.20,1.0,-.02),(.23,.21,.24),INK,.028)
box('anvil face',(-.20,1.115,-.02),(.58,.095,.30),IRON,.014)
tube('anvil horn',(-.47,1.09,-.02),(-.65,1.10,-.02),.10,IRON,.006,10)
box('hardy hole',(-.035,1.164,-.02),(.034,.002,.034),INK,.001)
box('coal tray',(.39,.86,.10),(.38,.09,.45),INK,.014)
for j in range(8):rock('embers',(.27+j%3*.105,.935,-.04+j//3*.105),(.053,.044,.053),(.72,.28,.08) if j%3 else (.94,.53,.15),j)
box('forge chimney',(.46,1.13,.34),(.19,.55,.17),(.30,.32,.33),.01)
box('chimney cap',(.46,1.415,.34),(.24,.04,.22),IRON)
box('chimney mouth',(.46,1.438,.34),(.13,.004,.12),INK,0)
tube('hammer handle',(-.30,.852,-.32),(.03,.852,-.28),.018,WOOD)
box('steel hammer head',(.04,.865,-.28),(.11,.085,.18),IRON)
for s in (-1,1):tube('tongs',(.24+s*.025,.843,-.30),(.53-s*.022,.843,-.20),.009,IRON,sides=8)
for j in range(2):box('steel stock',(-.20+j*.17,.21,0),(.13,.08,.39),IRON)
export('Forge')

# Larger readable silhouettes, exposed end grain, leaves and differentiated mineral seams.
for j in range(3):
    y=.09+j*.052;z=(j-1)*.14
    tube('weathered bark',(-.31,y,z),(.30-j*.05,y+.01,z+.04),.07,WOOD,.052,10)
    tube('cut end',(-.316,y,z),(-.323,y,z),.055,EDGE,sides=10)
    tube('heartwood',(-.324,y,z),(-.327,y,z),.022,CREAM,sides=8)
    for k in (-1,1):tube('bark ridge',(-.25,y+.057,z+k*.025),(.24-j*.05,y+.058,z+.035+k*.02),.004,INK,sides=5)
tube('broken branch',(.06,.15,-.04),(.20,.23,-.14),.025,WOOD,.012,8)
export('Driftwood')

for j in range(11):
    angle=j*2.4;x=math.cos(angle);z=math.sin(angle);height=.34+(j%4)*.055
    tube('grass stem',(x*.035,0,z*.035),(x*.10,height*.75,z*.10),.006,(.22,.38,.17),.003,5)
    leaf((x*.035,.02,z*.035),(x*.24,height,z*.24),.026,(.26+(j%3)*.035,.46+(j%3)*.035,.22))
    if j%3==0:leaf((x*.05,.14,z*.05),(x*.31,.27,z*.31),.038,(.43,.56,.26))
export('Fiber')

for name,color in [('Stone',(.43,.48,.48)),('IronOre',(.32,.37,.39)),('Coal',(.13,.17,.20)),('Clay',(.60,.35,.24)),('Sand',(.76,.64,.40))]:
    for j in range(1 if name=='Sand' else 5):
        size=.13+(j%3)*.025
        p=(0,.055,0) if name=='Sand' else ((j%3-1)*.15,.12+(j//3)*.065,(j//3-.5)*.20)
        shape=(.34,.09,.26) if name=='Sand' else (size,.12,size*.8)
        rock('faceted deposit',p,shape,tuple(c+(j%2)*.035 for c in color),j*.67)
    if name in ('IronOre','Coal'):
        for j in range(7):
            rock('copper seam' if name=='IronOre' else 'coal glint',((j%3-1)*.11,.22+(j//3)*.012,(j//3-1)*.08),(.053,.027,.027),(.72,.43,.23) if name=='IronOre' else (.28,.36,.38),j)
    if name=='Clay':
        for j in range(3):tube('clay strata',(-.17,.19+j*.014,.10),(.14,.19+j*.014,.10),.004,(.77,.54,.37),sides=5)
    if name=='Sand':
        for j in range(6):rock('sand pebble',((j%3-1)*.10,.106,(j//3-.5)*.13),(.014,.007,.013),CREAM,j)
    export(name)

assert len(REPORT)==10 and len({row['name'] for row in REPORT})==10
(ART/'manifest.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
for i,obj in enumerate(MODELS):
    obj.hide_render=False;obj.location=(i%5*2.1,i//5*2.2,0)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Coastal-props-v026.blend'))
print('[V026-ART] COMPLETE',len(REPORT),flush=True)
