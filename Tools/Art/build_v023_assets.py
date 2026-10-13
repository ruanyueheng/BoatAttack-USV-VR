"""Fresh background Blender: CC0 furniture, original marine fauna and calibrated craft kit.
Run with --factory-startup --python-exit-code 1. Runtime meshes keep Unity metre/grip coordinates.
"""
from pathlib import Path
import bpy, bmesh, math, json, hashlib, shutil, sys
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/V023'
OUT = ROOT / 'Assets/Resources/FishingV023'
FISH = ROOT / 'Assets/Resources/FishingFish'
PREVIEW = ROOT / 'Docs/Images/V023'
for folder in (ART, OUT/'Home', OUT/'Items', FISH, PREVIEW, ART/'Originals'):
    folder.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 16; scene.cycles.use_denoising = True
scene.render.film_transparent = True; scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'
world = bpy.data.worlds.new('V023 studio'); world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45; scene.world = world
mat = bpy.data.materials.new('MAT-V023 vertex palette'); mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .52
vertex = mat.node_tree.nodes.new('ShaderNodeVertexColor'); vertex.layer_name = 'Color'
mat.node_tree.links.new(vertex.outputs['Color'], bsdf.inputs['Base Color'])
for name, pos, power in [('key',(3,-4,6),800),('fill',(-3,-2,3),350),('rim',(1,4,4),600)]:
    data=bpy.data.lights.new('LGT-'+name,'AREA'); data.energy=power; data.size=4
    obj=bpy.data.objects.new('LGT-'+name,data); scene.collection.objects.link(obj); obj.location=pos
    obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('CAM-V023'); camera=bpy.data.objects.new('CAM-V023',data)
scene.collection.objects.link(camera); scene.camera=camera; data.type='ORTHO'
PARTS=[]; GALLERY=[]; REPORT=[]
WOOD=(.40,.24,.12); EDGE=(.69,.46,.23); TEAL=(.10,.34,.36); CREAM=(.91,.85,.68)
INK=(.065,.11,.15); BRASS=(.79,.57,.26); IRON=(.43,.55,.60); GREEN=(.28,.48,.29)

def U(p): return Vector((p[0],p[2],p[1]))
def paint(obj,color):
    for old in list(obj.data.color_attributes): obj.data.color_attributes.remove(old)
    attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    for v in attr.data: v.color_srgb=(*color[:3],1)
    obj.data.color_attributes.active_color=attr; obj.data.materials.clear(); obj.data.materials.append(mat)
    for face in obj.data.polygons: face.material_index=0
    PARTS.append(obj); return obj
def box(name,p,size,color,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=U(p)); obj=bpy.context.object; obj.name='GEO-'+name
    obj.scale=(size[0],size[2],size[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Soft working edges','BEVEL'); mod.width=bevel; mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return paint(obj,color)
def ell(name,p,size,color,segments=20,rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=U(p))
    obj=bpy.context.object; obj.name='GEO-'+name; obj.scale=(size[0],size[2],size[1])
    for face in obj.data.polygons: face.use_smooth=True
    return paint(obj,color)
def tube(name,a,b,r,color,end=None,sides=12):
    a,b=U(a),U(b)
    bpy.ops.mesh.primitive_cone_add(vertices=sides,radius1=r,radius2=r if end is None else end,depth=(b-a).length,location=(a+b)*.5)
    obj=bpy.context.object; obj.name='GEO-'+name; obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    for face in obj.data.polygons: face.use_smooth=True
    return paint(obj,color)
def rope(name,points,r,color):
    # One continuous ring surface, with variable radius and closed end caps.
    pts=[U(p) for p in points]; verts=[]; faces=[]; n=10
    for i,p in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        side=tangent.cross(Vector((0,0,1)))
        if side.length<.01: side=tangent.cross(Vector((0,1,0)))
        side.normalize(); up=tangent.cross(side).normalized(); radius=r[i] if isinstance(r,list) else r
        for j in range(n):
            a=j*math.tau/n; verts.append(p+radius*(side*math.cos(a)+up*math.sin(a)))
    for i in range(len(pts)-1):
        for j in range(n): faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
    faces.extend([tuple(reversed(range(n))),tuple((len(pts)-1)*n+j for j in range(n))])
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new('GEO-'+name,mesh); scene.collection.objects.link(obj)
    for face in mesh.polygons: face.use_smooth=True
    return paint(obj,color)
def eyes(x,y,z,r=.018):
    for s in (-1,1):
        ell('eye surround',(s*x,y,z),(r*1.5,r*1.5,r),CREAM,12,6)
        ell('eye',(s*x,y,z+r*.65),(r,r,r*.65),INK,12,6)
        ell('eye sparkle',(s*x-r*.2,y+r*.3,z+r),(r*.24,)*3,(1,1,1),8,4)
def photo(obj,path,fish=False):
    for model in GALLERY: model.hide_render=True
    obj.hide_render=False; obj.location=(0,0,0); scene.render.resolution_x=384 if fish else 256
    scene.render.resolution_y=216 if fish else 256
    pts=[obj.matrix_world@Vector(c) for c in obj.bound_box]; low=Vector([min(p[i] for p in pts) for i in range(3)]); high=Vector([max(p[i] for p in pts) for i in range(3)])
    centre=(low+high)*.5; span=max(high-low)
    camera.location=centre+Vector((1.2,1.8,1.3)).normalized()*max(2,span*3)
    camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
    inv=camera.rotation_euler.to_matrix().transposed(); projected=[inv@(p-centre) for p in pts]
    w=max(p.x for p in projected)-min(p.x for p in projected); h=max(p.y for p in projected)-min(p.y for p in projected)
    data.ortho_scale=max(w,h*scene.render.resolution_x/scene.render.resolution_y)*1.25
    scene.render.filepath=str(path); bpy.ops.render.render(write_still=True)
def export(name,folder,source,fish=False,photo_enabled=True):
    bpy.ops.object.select_all(action='DESELECT')
    for o in PARTS: o.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]; bpy.ops.object.join(); obj=bpy.context.object; obj.name='GEO-'+name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.clear(); obj.data.materials.append(mat)
    for face in obj.data.polygons: face.material_index=0
    tris=sum(len(p.vertices)-2 for p in obj.data.polygons)
    if fish:
        lengths={'SeaTurtle':.62,'Octopus':.54,'Crab':.49,'Lobster':.65,'Seahorse':.46,'Stingray':.78,'Jellyfish':.41,'Cuttlefish':.50}
        factor=lengths[name]/max(obj.dimensions)
        for vertex in obj.data.vertices: vertex.co*=factor
        if tris>4900:
            modifier=obj.modifiers.new('Marine triangle budget','DECIMATE');modifier.ratio=4900/tris
            bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=modifier.name)
        tris=sum(len(p.vertices)-2 for p in obj.data.polygons);assert tris<=5000,(name,tris)
    assert tris<=90000 if name=='HouseShell' else tris<=24000,(name,tris)
    path=folder/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},bake_anim=False,
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,mesh_smooth_type='FACE')
    assert path.stat().st_size>200
    GALLERY.append(obj)
    if photo_enabled: photo(obj,folder/(name+'Thumbnail.png'),fish)
    REPORT.append(dict(name=name,source=source,triangles=tris,dimensions=list(obj.dimensions),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    obj.location=(len(GALLERY)%8*16,len(GALLERY)//8*16,0); obj.hide_render=True
    print('[V023-ART]',name,tris,flush=True); PARTS.clear(); return obj
def cc0(name,pack,alias,folder,span=None):
    original=ART/'Originals'/(pack+'-'+name+'.glb')
    if not original.exists():
        candidates=list((ART/'Kenney'/pack).glob('**/'+name+'.glb')); assert candidates,(pack,name)
        shutil.copyfile(candidates[0],original)
    # Kenney's survival GLBs reference a neighbouring palette PNG.
    if pack=='survival-kit' and not (ART/'Originals/Textures/colormap.png').exists():
        textures=list((ART/'Kenney'/pack).glob('**/Textures/colormap.png')); assert textures
        (ART/'Originals/Textures').mkdir(exist_ok=True); shutil.copyfile(textures[0],ART/'Originals/Textures/colormap.png')
    cached=folder/(alias+'.fbx')
    # shortcut: --resume trusts existing exports; run without it after source or coordinate changes.
    if '--resume' in sys.argv and cached.exists() and (folder/(alias+'Thumbnail.png')).exists():
        before=set(scene.objects); bpy.ops.import_scene.fbx(filepath=str(cached))
        objs=[o for o in scene.objects if o not in before and o.type=='MESH']; assert len(objs)==1
        obj=objs[0]; obj.data.materials.clear(); obj.data.materials.append(mat)
        for face in obj.data.polygons: face.material_index=0
        GALLERY.append(obj); obj.hide_render=True; obj.location=(len(GALLERY)%8*16,len(GALLERY)//8*16,0)
        REPORT.append(dict(name=alias,source=original.relative_to(ROOT).as_posix(),triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),dimensions=list(obj.dimensions),sha256=hashlib.sha256(cached.read_bytes()).hexdigest()))
        return
    before=set(scene.objects); bpy.ops.import_scene.gltf(filepath=str(original))
    imported=[o for o in scene.objects if o not in before]; meshes=[o for o in imported if o.type=='MESH']
    for obj in meshes:
        # GLTF instances (such as both pillows) must not transform the same shared mesh twice.
        obj.data=obj.data.copy()
        colors=[]
        for material in obj.data.materials:
            node=material.node_tree.nodes.get('Principled BSDF') if material and material.use_nodes else None
            socket=node.inputs['Base Color'] if node else None
            tex=socket.links[0].from_node.image if socket and socket.is_linked and socket.links[0].from_node.type=='TEX_IMAGE' else None
            colors.append((socket.default_value[:] if socket else (.6,.6,.6,1),tex))
        uv=obj.data.uv_layers.active; pixels={t.name:list(t.pixels) for _,t in colors if t}
        old=obj.data.color_attributes.active_color
        old_colors=[tuple(c.color_srgb) for c in old.data] if old else None;old_domain=old.domain if old else None
        for old in list(obj.data.color_attributes):obj.data.color_attributes.remove(old)
        attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for face in obj.data.polygons:
            color,tex=colors[face.material_index]
            for i in face.loop_indices:
                value=color
                if tex and uv:
                    u,v=uv.data[i].uv; w,h=tex.size[:]; x=max(0,min(w-1,int(u*w))); y=max(0,min(h-1,int(v*h)))
                    value=pixels[tex.name][(y*w+x)*4:(y*w+x)*4+4]
                elif old_colors:
                    value=old_colors[obj.data.loops[i].vertex_index if old_domain=='POINT' else i]
                if alias=='bedDouble':
                    part=obj.name.lower()
                    if 'cover' in part:value=(.20,.44,.49,1)
                    elif 'pillow' in part:value=(.95,.86,.68,1)
                    elif 'wood' in obj.data.materials[face.material_index].name.lower():value=(.60,.44,.29,1)
                attr.data[i].color_srgb=(*value[:3],1)
        obj.data.color_attributes.active_color=attr; PARTS.append(obj)
    # Freeze transforms before sharing MeshFilter.sharedMesh in Unity.
    matrices={obj.name:obj.matrix_world.copy() for obj in meshes}
    for obj in meshes:
        obj.data.transform(matrices[obj.name]); obj.parent=None; obj.matrix_world.identity()
    pts=[v.co for obj in meshes for v in obj.data.vertices]; low=Vector([min(p[i] for p in pts) for i in range(3)]); high=Vector([max(p[i] for p in pts) for i in range(3)])
    factor=span/max(high-low) if span else 1
    for obj in meshes:
        for v in obj.data.vertices:
            v.co=(v.co-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))*factor;v.co.y=-v.co.y
        # FBX space baking needs the GLTF forward reflection; preserve outward face winding too.
        mesh=bmesh.new();mesh.from_mesh(obj.data);bmesh.ops.reverse_faces(mesh,faces=list(mesh.faces));mesh.to_mesh(obj.data);mesh.free()
        obj.data.color_attributes.active_color=obj.data.color_attributes['Color']
    imported_names=[obj.name for obj in imported]
    export(alias,folder,original.relative_to(ROOT).as_posix())
    for name in imported_names:
        obj=bpy.data.objects.get(name)
        if obj is not None and obj not in GALLERY: bpy.data.objects.remove(obj,do_unlink=True)

for pack in ('furniture-kit','survival-kit'):
    license=ART/'Originals'/(pack+'-License.txt')
    if not license.exists(): shutil.copyfile(ART/'Kenney'/pack/'License.txt',license)
for name in ['bedDouble','cabinetBedDrawer','loungeSofa','loungeChair','tableCoffee','rugRectangle','bookcaseOpen',
    'books','desk','chairDesk','kitchenCabinetDrawer','kitchenSink','kitchenStove','kitchenFridgeSmall',
    'tableRound','chair','pottedPlant','lampRoundFloor','lampRoundTable','coatRackStanding','radio','bathroomSink','toilet']:
    cc0(name,'furniture-kit',name,OUT/'Home')
cc0('resource-planks','survival-kit','Plank',OUT/'Items',.58)
cc0('resource-wood','survival-kit','Driftwood',OUT/'Items',.6)
cc0('resource-stone','survival-kit','Stone',OUT/'Items',.5)

# Distinct original species. +Z is the head, +Y is up; no borrowed rig or texture license.
for name in ['SeaTurtle','Octopus','Crab','Lobster','Seahorse','Stingray','Jellyfish','Cuttlefish']:
    if name=='SeaTurtle':
        ell('shell',(0,0,0),(.18,.095,.22),(.26,.36,.16)); ell('plastron',(0,-.042,0),(.174,.04,.214),CREAM)
        for z in range(-1,2):
            for x in range(-1,2): ell('shell scute',(x*.092,.083-abs(x)*.017,z*.102),(.047,.016,.058),(.47,.54,.24),12,6)
        for s in (-1,1):
            for z in (-1,1):
                flipper=ell('flipper',(s*.19,-.024,z*.126),(.105,.026,.05),GREEN)
                flipper.rotation_euler.z=s*z*.38
        ell('neck',(0,-.012,.218),(.055,.044,.065),GREEN); ell('head',(0,.0,.275),(.061,.048,.061),GREEN)
        eyes(.043,.016,.30,.012); tube('tail',(0,-.015,-.19),(0,-.02,-.285),.026,GREEN,.003)
    elif name=='Octopus':
        ell('mantle',(0,.10,0),(.11,.14,.105),(.57,.25,.34)); ell('face',(0,.013,.022),(.12,.075,.11),(.68,.34,.39)); eyes(.07,.047,.108,.025)
        for arm in range(8):
            angle=arm*math.tau/8; points=[]
            for j in range(17):
                t=j/16; a=angle+.7*t*t; r=.07+.19*t
                points.append((math.sin(a)*r,-.04+math.sin(t*math.pi*1.6)*.038,math.cos(a)*r))
            rope('arm '+str(arm),points,[.035*(1-j/18) for j in range(17)],(.66,.30,.36))
            for j in range(3,14,2):
                p=points[j]; ell('sucker',(p[0],p[1]-.017,p[2]),(.011,.007,.011),(.93,.69,.64),8,4)
    elif name=='Crab' or name=='Lobster':
        lobster=name=='Lobster'; red=(.72,.23,.12); lighter=(.93,.46,.23)
        ell('carapace',(0,0,0),(.13 if not lobster else .085,.055,.10 if not lobster else .16),red)
        if lobster:
            for j in range(5): ell('tail segment',(0,-.009,-.14-j*.025),(.075-j*.007,.042,.025),lighter)
            for s in (-1,0,1): ell('tail fan',(s*.042,-.015,-.278),(.035,.018,.044),red)
            for s in (-1,1): rope('antenna',[(s*.025,.05,.13),(s*.06,.07,.22),(s*.11,.11,.36)],.006,lighter)
        for s in (-1,1):
            for j in range(4):
                z=.075-j*.043; rope('walking leg',[(s*.09,-.012,z),(s*.19,-.025,z-.035),(s*.24,-.065,z-.015)], [.014,.011,.004],lighter)
            rope('claw arm',[(s*.10,.01,.08),(s*.18,.022,.13),(s*.20,.03,.18)],.022,red)
            ell('claw',(s*.205,.038,.19),(.046,.027,.05),lighter)
            tube('claw thumb',(s*.178,.039,.19),(s*.17,.039,.257),.013,red,.003)
            tube('claw finger',(s*.231,.039,.19),(s*.194,.039,.257),.015,red,.003)
            tube('eye stalk',(s*.041,.029,.079),(s*.052,.075,.10),.009,red)
        eyes(.052,.075,.10,.013)
    elif name=='Seahorse':
        orange=(.90,.51,.15)
        pts=[(0,-.20,-.015),(0,-.19,-.075),(0,-.23,-.11),(0,-.27,-.065),(0,-.245,.01),(0,-.15,.025),(0,-.055,.025),(0,.04,.035),(0,.13,.018),(0,.18,.045)]
        rope('curved trunk',pts,[.008,.009,.010,.011,.014,.019,.038,.039,.024,.023],orange)
        ell('head',(0,.185,.063),(.032,.032,.05),orange); tube('snout',(0,.171,.08),(0,.163,.151),.014,orange,.011)
        for j in range(6): tube('spine',(0,-.08+j*.04,-.008),(0,-.07+j*.04,-.048),.009,BRASS,.001)
        for j in range(6): ell('belly ridge',(0,-.08+j*.025,.042),(.034,.004,.014),CREAM,12,6)
        eyes(.028,.195,.073,.01); ell('dorsal fin',(0,-.01,-.022),(.006,.041,.04),(.97,.74,.35))
    elif name=='Stingray':
        verts=[]; faces=[]; rings=16; sides=48
        for i in range(rings+1):
            r=i/rings
            for j in range(sides):
                a=j*math.tau/sides; diamond=1/(abs(math.sin(a))+abs(math.cos(a)))
                x=math.sin(a)*.31*r*diamond; z=math.cos(a)*.24*r*diamond
                y=.03*(1-r*r)+.018*math.sin(a*3)*r*r
                verts.append(U((x,y,z)))
        for i in range(rings):
            for j in range(sides): faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
        mesh=bpy.data.meshes.new('Ray wings'); mesh.from_pydata(verts,[],faces); mesh.update()
        obj=bpy.data.objects.new('GEO-ray wings',mesh); scene.collection.objects.link(obj); paint(obj,(.23,.40,.52))
        mod=obj.modifiers.new('Closed soft wing','SOLIDIFY'); mod.thickness=.012; bpy.context.view_layer.objects.active=obj; bpy.ops.object.modifier_apply(modifier=mod.name)
        ell('ray trunk',(0,.013,0),(.09,.04,.23),(.27,.46,.55)); eyes(.046,.052,.13,.016)
        rope('ray tail',[(0,0,-.18),(.01,-.004,-.3),(.025,.012,-.43),(.03,.02,-.54)],[.025,.019,.010,.002],INK)
        for s in (-1,1):
            for j in range(4): ell('wing spot',(s*(.11+j*.035),.024,.06-j*.044),(.012,.003,.009),(.57,.73,.77),8,4)
    elif name=='Jellyfish':
        ell('bell',(0,.065,0),(.13,.085,.13),(.54,.76,.82)); ell('bell rim',(0,.011,0),(.139,.018,.139),(.72,.91,.91))
        for arm in range(12):
            a=arm*math.tau/12; pts=[]
            for j in range(11):
                t=j/10; pts.append((math.sin(a)*(.075+.018*math.sin(t*8)),.005-t*(.18+(arm%3)*.045),math.cos(a)*(.075+.012*math.sin(t*7))))
            rope('tentacle',pts,[.006*(1-j/14) for j in range(11)],(.75,.85,.95))
        for j in range(4): ell('oral arm',((j-1.5)*.024,-.1,0),(.02,.09,.025),(.81,.57,.77))
    else:
        ell('cuttle mantle',(0,.015,-.045),(.09,.049,.20),(.45,.63,.58)); ell('head',(0,.005,.135),(.083,.047,.055),(.57,.73,.62))
        for s in (-1,1):
            pts=[(s*(.08+.011*math.sin(j)),.002,-.20+j*.018) for j in range(20)]
            rope('ruffled fin',pts,.016,(.74,.84,.67))
            for j in range(7): ell('mantle stripe',(s*.04,.058,-.13+j*.028),(.038,.004,.007),(.25,.43,.37),12,6)
        for j in range(8):
            x=(j-3.5)*.016; rope('arm',[(x,0,.16),(x*1.25,-.018,.215),(x*1.5,-.009,.27)], [.017,.012,.003],(.60,.74,.62))
        eyes(.067,.027,.147,.020)
    # Budget is measured after joining; marine meshes remain centred for fish/net/hand reuse.
    export(name,FISH,'Project-authored V023 marine mesh',True)

# Crafted objects, all with real silhouettes and exactly the same coordinates used by hand sockets.
items=['Workbench','FieldGuide','Binoculars','Lantern','Shell','Fiber','Clay','IronOre','Coal','Sand','Stick','Rope','Charcoal',
       'IronIngot','Brick','Glass','Furnace','StonePickaxe','IronPickaxe','HuntingSpear','RawMeat','CookedMeat']
for name in items:
    if name=='Workbench':
        for x in (-.53,.53):
            for z in (-.31,.31): box('tapered bench leg',(x,.42,z),(.10,.84,.10),WOOD,.012)
            box('side brace',(x,.27,0),(.09,.08,.60),EDGE)
        for j in range(5): box('bench top',(0,.91,(j-2)*.167),(1.30,.105,.158),EDGE,.008)
        for j in range(4): box('lower shelf',(0,.26,(j-1.5)*.16),(1.08,.035,.145),WOOD,.005)
        box('drawer',(0,.64,-.29),(.78,.21,.15),WOOD,.01)
        tube('drawer pull',(-.09,.65,-.39),(.09,.65,-.39),.009,BRASS)
        box('vise mount',(.4,1,.20),(.18,.10,.18),IRON,.009)
        for z in (.14,.27): box('vise jaw',(.4,1.09,z),(.20,.09,.036),INK,.006)
        tube('vise screw',(.4,1.01,.12),(.4,1.01,.37),.012,IRON)
        tube('vise handle',(.33,1.01,.38),(.47,1.01,.38),.009,BRASS)
        tube('hammer haft',(-.34,.987,-.16),(-.11,.987,.015),.016,WOOD)
        box('hammer head',(-.11,1.01,.015),(.07,.08,.14),IRON,.009)
        for x in (-.57,.57):
            for z in (-.27,.27): ell('recessed fastener',(x,.966,z),(.014,.004,.014),INK,12,6)
    elif name=='FieldGuide':
        box('bound cover',(0,0,0),(.44,.058,.52),TEAL,.014); box('page block',(0,.027,0),(.405,.032,.48),CREAM,.005)
        for j in range(5): box('page edges',(0,.015+j*.005,.244),(.398,.001,.003),EDGE,0)
        box('spine',(-.20,0,0),(.035,.065,.51),WOOD); box('brass book label',(0,.047,0),(.15,.003,.19),BRASS,.006)
        box('title inset',(0,.050,0),(.13,.002,.16),TEAL,.006)
    elif name=='Binoculars':
        for s in (-1,1):
            tube('optical barrel',(s*.15,0,-.15),(s*.15,0,.21),.105,INK,.14,24)
            for z in (-.14,.13,.21): tube('brass collar',(s*.15,0,z),(s*.15,0,z+.017),.147 if z>0 else .11,BRASS,sides=24)
            ell('objective',(s*.15,0,.233),(.119,.119,.008),(.21,.66,.75))
            for j in range(12):
                a=j*math.tau/12; tube('rubber ribs',(s*.15+math.sin(a)*.115,math.cos(a)*.115,-.02),(s*.15+math.sin(a)*.124,math.cos(a)*.124,.09),.004,TEAL,sides=6)
        tube('bridge',(-.15,0,0),(.15,0,0),.043,IRON); tube('focus knob',(0,0,-.03),(0,0,.035),.04,BRASS)
    elif name=='Lantern':
        tube('base',(0,0,0),(0,.07,0),.16,TEAL); tube('chamber',(0,.07,0),(0,.42,0),.118,(.96,.72,.30))
        tube('cap',(0,.43,0),(0,.50,0),.17,TEAL,.13)
        for s in (-1,1):
            for z in (-1,1): tube('cage',(s*.105,.035,z*.08),(s*.105,.46,z*.08),.012,BRASS)
        rope('handle',[(math.cos(j*math.pi/16)*.13,.50+math.sin(j*math.pi/16)*.13,0) for j in range(17)],.012,BRASS)
    elif name in ('StonePickaxe','IronPickaxe'):
        tube('ash handle',(0,-.26,0),(0,.335,0),.024,WOOD,.035,20)
        rope('curved pick',[(-.28,.26,0),(-.21,.30,0),(-.11,.33,0),(0,.34,0),(.11,.325,0),(.22,.285,0),(.28,.26,0)], [.009,.024,.044,.050,.037,.018,.003],IRON if name=='IronPickaxe' else (.41,.44,.41))
        tube('head collar',(0,.27,0),(0,.365,0),.047,IRON if name=='IronPickaxe' else BRASS,sides=20)
        for j in range(6): tube('leather grip',(0,-.22+j*.029,0),(0,-.20+j*.029,0),.026,TEAL,sides=20)
    elif name=='HuntingSpear':
        tube('shaft',(0,0,-.38),(0,0,.75),.022,WOOD,.014,20)
        tube('spearhead',(0,0,.70),(0,0,.92),.045,IRON,.001,4)
        for j in range(7): tube('bound grip',(0,0,-.055+j*.017),(0,0,-.045+j*.017),.027,TEAL,sides=16)
    elif name=='Furnace':
        box('hearth',(0,.075,0),(1.08,.15,1.02),(.30,.32,.34),.025)
        for row in range(5):
            for side in (-1,1):
                for j in range(3): box('side brick',(side*.43,.22+row*.16,-.32+j*.30),(.20,.145,.285),(.48+row*.015,.28,.19),.012)
            for j in range(3): box('back brick',((j-1)*.29,.22+row*.16,.36),(.28,.145,.18),(.56,.34,.23),.012)
        for j in range(7):
            a=j*math.pi/6; obj=box('arched lintel',(.34*math.cos(a),.55+.34*math.sin(a),-.36),(.16,.15,.20),(.65,.39,.26),.010); obj.rotation_euler.y=math.pi/2-a
        box('mantel',(0,1.0,0),(1.05,.14,1),(.42,.41,.36),.015)
        box('chimney',(0,1.27,.18),(.34,.4,.32),INK,.02); box('chimney vent',(0,1.485,.18),(.21,.012,.20),(.02,.02,.02),.004)
        for j in range(5): tube('grate',(-.28+j*.14,.20,-.43),(-.28+j*.14,.20,.26),.013,IRON)
    elif name=='Rope':
        pts=[]
        for j in range(220):
            a=j*.24; r=.06+j*.00055; pts.append((math.cos(a)*r,.025,math.sin(a)*r))
        rope('coil',pts,.010,CREAM); rope('free rope end',[(.16,.025,0),(.25,.025,-.1),(.30,.025,-.18)],.010,CREAM)
    elif name=='Stick':
        tube('stick',(-.26,.032,0),(.26,.032,0),.029,WOOD,.023,16)
        for j in range(3): tube('grain',(-.2,.05,(j-1)*.008),(.2,.05,(j-1)*.008),.0015,EDGE,sides=5)
    elif name=='IronIngot':
        box('cast ingot',(0,.055,0),(.30,.11,.16),IRON,.025); box('stamp',(0,.112,0),(.06,.002,.05),INK,.006)
    elif name=='Brick':
        box('fired clay',(0,.05,0),(.27,.1,.13),(.65,.31,.19),.008)
        for x in (-.075,0,.075): ell('kiln hole',(x,.099,0),(.019,.004,.026),(.30,.16,.12),12,6)
    elif name=='Glass':
        for j in range(3): box('glass sheets',((j-1)*.009,.013+j*.012,0),(.24,.008,.20),(.54+j*.10,.78+j*.04,.83),.003)
        box('edge binding',(0,.03,-.1),(.245,.035,.01),CREAM,.002)
    elif name in ('RawMeat','CookedMeat'):
        ell('steak',(0,.035,0),(.16,.043,.12),(.70,.23,.19) if name=='RawMeat' else (.46,.23,.10))
        for j in range(5): tube('marbling' if name=='RawMeat' else 'grill mark',(-.10,.072,-.075+j*.035),(.09,.072,-.025+j*.03),.003,CREAM if name=='RawMeat' else INK,sides=6)
    elif name=='Shell':
        for j in range(13):
            a=(j-6)*.1; rope('shell rib',[(0,.02,-.09),(math.sin(a)*.17,.075,.06),(math.sin(a)*.22,.03,.16)], [.025,.032,.021],(.89,.70+j*.008,.53))
        ell('hinge',(0,.03,-.08),(.07,.025,.045),CREAM)
    elif name=='Fiber':
        for j in range(13): rope('fibre', [((j-6)*.013,.013,0),((j-6)*.019,.13,.012),((j-6)*.025,.30+(j%3)*.025,.04)],.006,GREEN)
        tube('binding',(-.10,.11,.01),(.10,.11,.01),.012,CREAM)
    else:
        color={'Clay':(.62,.36,.24),'IronOre':(.31,.33,.37),'Coal':(.095,.12,.14),'Charcoal':(.12,.14,.15),'Sand':(.79,.67,.43)}[name]
        for j in range(4):
            if name=='Charcoal': tube('charred log',(-.20,.02+j*.03,(j-1.5)*.06),(.20,.02+j*.03,(j-1.5)*.06),.035,color)
            else: ell('mineral',((j%2-.5)*.15,.065,j//2*.12),(.12,.075,.11),color,12,6)
        if name=='IronOre':
            for j in range(9): ell('ore fleck',((j%3-1)*.065,.13,j//3*.055),(.012,.008,.018),(.77,.46,.22),8,4)
    export(name,OUT/'Items','Project-authored V023 craft mesh')

# Sea-green clapboard cottage: 12 x 10 metres, generous front doorway and six open windows.
box('floor',(0,.08,0),(12,.16,10),WOOD,.025)
for j in range(40): box('floorboard',(0,.17,-4.87+j*.25),(11.8,.024,.237),(.52+(j%3)*.025,.35+(j%3)*.018,.20),.003)
for side in (-1,1):
    for j in range(15):
        y=.28+j*.20
        for z in (-3.6,0,3.6):
            if z==0 and .9<y<2.5: continue
            box('side clapboard',(side*6,y,z),(.16,.185,3.55),TEAL,.004)
    for z in (-1.8,1.8):
        box('window sill',(side*6,1.02,z),(.30,.12,3.35),CREAM)
        box('window header',(side*6,2.48,z),(.25,.12,3.35),CREAM)
for j in range(15):
    y=.28+j*.20
    box('rear wall',(0,y,5),(12,.185,.16),TEAL,.004)
    for s in (-1,1): box('front wall',(s*3.75,y,-5),(4.5,.185,.16),TEAL,.004)
box('door header',(0,2.96,-5),(3.15,.20,.24),CREAM)
for z in (-5,5):
    for j in range(9): box('gable clapboard',(0,3.23+j*.19,z),(12*(1-(j+.2)/9),.18,.16),TEAL,.004)
for x in (-5.96,-1.55,1.55,5.96): box('front posts',(x,1.70,-5.03),(.13,3.10,.22),CREAM)
for s in (-1,1):
    # Roof slope rises 1.7 m across each 6 m half.
    panel=box('terracotta roof',(s*3.05,4.08,0),(6.5,.18,10.8),(.47,.21,.15),.008)
    panel.rotation_euler.y=s*math.atan2(1.7,6)
    for z in range(28): tube('roof seam',(s*.04,4.96,-5.1+z*.38),(s*6.28,3.20,-5.1+z*.38),.027,(.64,.32,.23),sides=8)
    for x in (-5.6,0,5.6): tube('exposed rafter',(x,3.10,-5.1),(x,4.84,0),.075,WOOD,sides=8)
box('porch deck',(0,.14,-6.5),(13,.24,3),WOOD,.02)
for x in (-6,6):
    box('porch post',(x,1.71,-7.7),(.19,3.1,.19),CREAM)
    box('porch railing',(x,.86,-6.5),(.11,.09,2.8),CREAM)
for s in (-1,1):
    box('front rail',(s*4.0,.92,-7.7),(4.0,.10,.12),CREAM)
    for j in range(10): box('rail spindle',(s*(2.1+j*.39),.64,-7.7),(.045,.53,.045),CREAM,.003)
    for z in (-6.6,3.8): tube('foundation piling',(s*5,-2,z),(s*5,.1,z),.16,WOOD,sides=12)
export('HouseShell',OUT/'Home','Project-authored V023 coastal cottage',photo_enabled=False)

(ART/'manifest.json').write_text(json.dumps(REPORT,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Coast-v023.blend'))
assert len(REPORT)==57,len(REPORT)
print('[V023-ART] COMPLETE',len(REPORT),flush=True)
