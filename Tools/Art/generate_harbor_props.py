"""Original, reproducible harbor props. Run with Blender --background --python this_file.
All positions below use Unity metres (X right, Y up, Z forward); Blender conversion is explicit.
Reference images are recorded in Docs/FISHING_PROP_ART.md, not copied into these assets.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FishingProps'
SOURCE=ROOT/'ArtSource/FishingProps'
OUT.mkdir(parents=True,exist_ok=True);SOURCE.mkdir(parents=True,exist_ok=True)
random.seed(601)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
PALETTE={
 'wood':(.34,.22,.115,0), 'wood_light':(.49,.34,.19,0), 'grain':(.20,.12,.065,0),
 'iron':(.085,.14,.15,.7), 'edge':(.22,.31,.30,.8), 'brass':(.68,.43,.16,.9),
 'copper':(.72,.32,.14,.85),'teal':(.065,.32,.31,.45), 'paint':(.76,.78,.67,.22),
 'ivory':(.9,.84,.63,.15), 'rope':(.55,.43,.27,0), 'stone':(.34,.40,.38,0),
 'glass':(.38,.74,.70,.8), 'ink':(.07,.14,.16,0)
}
PARTS=[];COL=None;STATS={};SOURCE_ASSETS=[]
def U(v): return Vector((v[0],-v[2],v[1]))
def begin(name):
    global PARTS,COL
    PARTS=[];COL=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(COL)
def keep(obj,name):
    obj.name=name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    COL.objects.link(obj);PARTS.append(obj);return obj

def finish(obj,color,smooth=False,variation=.06):
    if isinstance(color,str): color=PALETTE[color]
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    # Colour is stored in sRGB; alpha is the authored surface polish, not transparency.
    attr=obj.data.color_attributes.new(name='PropColor',type='BYTE_COLOR',domain='CORNER')
    for poly in obj.data.polygons:
        shade=1+random.uniform(-variation,variation)
        poly.use_smooth=smooth
        for i in poly.loop_indices: attr.data[i].color_srgb=(*[max(0,min(1,c*shade)) for c in color[:3]],color[3])
    obj.data.color_attributes.active_color=attr
    obj.data.materials.clear();obj.data.materials.append(VERTEX_MAT)
    return obj

def box(name,p,s,color,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=U(p));obj=keep(bpy.context.object,name)
    obj.scale=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Hand finished edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj,color)

def lathe(name,profile,color,segments=24,center=(0,0,0),smooth=True):
    vs=[];faces=[]
    for y,r in profile:
        for j in range(segments):
            a=j*2*math.pi/segments;vs.append(U((center[0]+r*math.cos(a),center[1]+y,center[2]+r*math.sin(a))))
    for i in range(len(profile)-1):
        for j in range(segments):
            a=i*segments+j;b=i*segments+(j+1)%segments;faces.append((a,b,b+segments,a+segments))
    faces.extend([tuple(reversed(range(segments))),tuple((len(profile)-1)*segments+j for j in range(segments))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);COL.objects.link(obj);PARTS.append(obj)
    return finish(obj,color,smooth, .025)

def tube(name,points,r,color,sides=8,closed=False):
    pts=[U(p) for p in points];vs=[];faces=[]
    for i,p in enumerate(pts):
        d=(pts[(i+1)%len(pts)]-pts[(i-1)%len(pts)]).normalized() if closed else (pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        ref=Vector((0,0,1)) if abs(d.z)<.9 else Vector((0,1,0))
        right=d.cross(ref).normalized();up=d.cross(right).normalized()
        for j in range(sides):
            a=j*2*math.pi/sides;vs.append(p+(right*math.cos(a)+up*math.sin(a))*r)
    for i in range(len(pts) if closed else len(pts)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;nexti=(i+1)%len(pts)
            faces.append((a,b,nexti*sides+(j+1)%sides,nexti*sides+j))
    if not closed: faces.extend([tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+j for j in range(sides))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);COL.objects.link(obj);PARTS.append(obj);return finish(obj,color,True,.015)

def ring(name,p,r,width,color,plane='xz',segments=32):
    pts=[]
    for j in range(segments):
        a=2*math.pi*j/segments
        v=(math.cos(a)*r,0,math.sin(a)*r) if plane=='xz' else ((math.cos(a)*r,math.sin(a)*r,0) if plane=='xy' else (0,math.cos(a)*r,math.sin(a)*r))
        pts.append(tuple(p[k]+v[k] for k in range(3)))
    return tube(name,pts,width,color,8,True)

def bolt(p,axis='z',radius=.013):
    delta=(0,.014,0) if axis=='y' else ((.014,0,0) if axis=='x' else (0,0,.014))
    return tube('Hex rivet',[p,tuple(p[i]+delta[i] for i in range(3))],radius,'brass',6)

VERTEX_MAT=bpy.data.materials.new('Harbor vertex paint');VERTEX_MAT.use_nodes=True
nodes=VERTEX_MAT.node_tree.nodes;links=VERTEX_MAT.node_tree.links
bsdf=nodes.get('Principled BSDF');vc=nodes.new('ShaderNodeVertexColor');vc.layer_name='PropColor'
links.new(vc.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.42

def export(name):
    bpy.ops.object.select_all(action='DESELECT');copies=[]
    for original in PARTS:
        obj=original.copy();obj.data=original.data.copy();bpy.context.scene.collection.objects.link(obj);obj.select_set(True);copies.append(obj)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();obj=bpy.context.object
    obj.name=name;bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Recalculate outward winding after explicit handedness conversion.
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    obj.data.calc_loop_triangles();triangles=len(obj.data.loop_triangles)
    assert triangles<14000,(name,triangles)
    STATS[name]={'triangles':triangles,'vertices':len(obj.data.vertices),'source_parts':len(PARTS),'renderers':1}
    bpy.ops.export_scene.fbx(
        filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,
        use_mesh_modifiers=True,mesh_smooth_type='OFF',colors_type='SRGB',prioritize_active_color=True,
        bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    bpy.data.objects.remove(obj,do_unlink=True)
    # Editable source retains separate boards, bolts, cables and optics, arranged by collection.
    offset=len(SOURCE_ASSETS)*2.6
    for original in PARTS: original.location.x+=offset
    SOURCE_ASSETS.append(name)

def crate():
    begin('CrateBody')
    # Actual wall boards rather than four solid overlapping blocks.
    for side in (-1,1):
        for row in range(4):
            y=.12+row*.155
            box('Long cedar plank',(0,y,side*.325),(.8,.145,.055),'wood_light' if row%3==0 else 'wood',.007)
            box('End cedar plank',(side*.375,y,0),(.06,.145,.60),'wood' if row%2 else 'wood_light',.006)
        for x in (-.29,.29):
            box('Iron strap',(x,.35,side*.36),(.047,.64,.025),'iron',.007)
            for y in (.12,.43,.61): bolt((x,y,side*.376))
        # U-shaped hemp carry handle, two bolted collars, and visible twisted strands.
        for z in (-.15,.15): box('Rope handle eye',(side*.413,.43,z),(.035,.075,.065),'iron',.01)
        handle=[(side*(.425+.11*math.sin(math.pi*j/16)),.43-.16*math.sin(math.pi*j/16),-.15+.3*j/16) for j in range(17)]
        tube('Hemp carry handle',handle,.026,'rope',8)
        for k in range(2):
            pts=[(p[0]+.018*math.cos(j*.95+k*math.pi),p[1]+.018*math.sin(j*.95+k*math.pi),p[2]) for j,p in enumerate(handle)]
            tube('Twisted rope strand',pts,.006,'wood_light',5)
    for x in (-.355,.355):
        for z in (-.348,.348):
            box('Folded corner vertical',(x,.35,z),(.085,.66,.035),'edge',.009)
            for y in (.065,.64): box('Corner shoe',(x,y,z),(.12,.075,.085),'iron',.01)
    for j in range(5):
        box('Lid board',(-.32+j*.16,.677,0),(.151,.07,.71),'wood_light' if j%2 else 'wood',.009)
    for x in (-.29,.29):
        box('Lid metal band',(x,.719,0),(.05,.02,.70),'iron',.006)
        box('Rear hinge plate',(x,.625,.373),(.10,.08,.025),'edge',.006)
        tube('Hinge pin',[(x-.065,.67,.375),(x+.065,.67,.375)],.02,'brass',10)
    box('Latch backing',(0,.57,-.378),(.07,.18,.021),'iron',.008)
    ring('Latch lift loop',(0,.54,-.403),.031,.009,'brass','xy',16)
    tube('Latch hook',[(0,.685,-.385),(0,.62,-.411),(0,.58,-.411)],.012,'edge',8)
    for side in (-1,1):
        for row in range(4):
            for j in range(2):
                x0=-.23+j*.22; y=.085+row*.155+j*.032
                # Sparse, hand-cut grain marks and not a repetitive noise texture.
                pts=[(x0+k*.032,y+.004*math.sin(k*.9+row),side*.354) for k in range(6)]
                tube('Cedar grain incision',pts,.0025,'grain',3)
    export('CrateBody')

def sign(kind):
    begin('CargoSign'+kind)
    box('Enamel badge',(0,.385,-.38),(.38,.24,.027),'teal' if kind=='Service' else 'ivory',.016)
    for x in (-.167,.167):
        for y in (.294,.476): bolt((x,y,-.409),radius=.009)
    if kind=='Service':
        for x in (-.086,.086): ring('Raised twin-ring service mark',(x,.388,-.407),.06,.01,'ivory','xy',20)
    elif kind=='Fish':
        body=[(-.10,.39,-.413),(-.03,.439,-.413),(.077,.426,-.413),(.127,.388,-.413),(.058,.35,-.413),(-.03,.353,-.413),(-.10,.39,-.413),(-.147,.435,-.413),(-.147,.348,-.413),(-.10,.39,-.413)]
        tube('Raised fish cargo emblem',body,.009,'teal',6)
        bolt((.077,.40,-.422),radius=.008)
    else:
        for x in (-.105,0,.105): tube('Diagonal fabric stripe',[(x-.025,.333,-.414),(x+.025,.442,-.414)],.013,'teal',6)
    export('CargoSign'+kind)

def coil():
    begin('RepairCoil')
    box('Mounting shoe',(0,.025,0),(.34,.05,.27),'iron',.012)
    for x in (-.137,.137):
        for z in (-.102,.102): bolt((x,.052,z),'y',.014)
    box('Laminated magnetic core',(0,.19,0),(.13,.27,.12),'iron',.009)
    for y in (.095,.30):
        lathe('Porcelain bobbin flange',[(y-.018,.102),(y-.012,.113),(y+.012,.113),(y+.018,.102)],'ivory',24)
    # Continuous copper winding; no texture or cylinder pretending to be a coil.
    winding=[]
    for j in range(257):
        a=j*math.pi*2*10/256; winding.append((.089*math.cos(a),.115+j*.164/256,.089*math.sin(a)))
    tube('Ten-turn copper winding',winding,.009,'copper',6)
    for x in (-.11,.11):
        lathe('Ceramic terminal',[(.065,.025),(.075,.03),(.112,.022)],'ivory',12,(x,0,-.084))
        bolt((x,.111,-.084),'y',.016)
        tube('Insulated lead',[(x,.119,-.084),(x*.9,.15,-.12),(x*.55,.2,-.09)],.008,'teal',6)
    box('Service ID plaque',(0,.035,-.145),(.10,.025,.012),'brass',.004)
    export('RepairCoil')

def beacon_body():
    begin('BeaconBody')
    lathe('Cut stone foot',[(0,.55),(.05,.60),(.17,.60),(.24,.50)],'stone',12,smooth=False)
    lathe('Cast iron foundation',[(.235,.49),(.255,.515),(.30,.515),(.32,.47)],'iron',24)
    for j in range(8):
        a=j*math.pi/4;bolt((.455*math.cos(a),.312,.455*math.sin(a)),'y',.025)
    lathe('Tapered eight panel tower',[(.32,.463),(.42,.457),(2.04,.293),(2.18,.293)],'paint',8,smooth=False)
    for y,r in ((.45,.468),(1.14,.401),(1.91,.328)):
        lathe('Rolled reinforcing band',[(y-.037,r),(y-.022,r+.009),(y+.022,r+.009),(y+.037,r)],'teal',32)
        for j in range(8):
            a=j*math.pi/4;ring('Band stud washer',(r*math.cos(a),y,r*math.sin(a)),.014,.006,'brass','xy',8)
    box('Door outer frame',(0,.93,-.429),(.365,.80,.065),'iron',.028)
    box('Inset maintenance door',(0,.93,-.467),(.295,.717,.028),'teal',.024)
    for y in (.69,1.18):
        box('Door hinge',(-.172,y,-.471),(.04,.09,.034),'brass',.009)
        tube('Hinge barrel',[(-.175,y-.053,-.487),(-.175,y+.053,-.487)],.016,'edge',10)
    ring('Door grip',(.094,.96,-.499),.039,.012,'brass','xy',16)
    for y in (1.16,1.22): box('Door louvre',(0,y,-.488),(.18,.018,.009),'iron',.002)
    box('Maintenance label',(0,.79,-.49),(.15,.065,.01),'ivory',.004)
    for x in (-.038,.038): ring('Service rings on door',(x,.79,-.502),.019,.005,'teal','xy',12)
    # Ladder stands off the tower; continuous rails, bent tops and seven rungs.
    for z in (-.14,.14): tube('Ladder rail',[(.48,.40,z),(.47,1.45,z),(.37,2.06,z),(.31,2.18,z)],.02,'iron',8)
    for i in range(7):
        y=.52+i*.225;x=.48-max(0,y-1.45)*.16
        tube('Ladder rung',[(x,y,-.14),(x,y,.14)],.016,'brass',8)
    lathe('Lantern gallery',[(2.13,.31),(2.18,.53),(2.24,.53),(2.265,.47)],'iron',32)
    for j in range(12):
        a=j*math.pi/6;bolt((.465*math.cos(a),2.264,.465*math.sin(a)),'y',.012)
    # Painted metal seams add scale without adding disconnected primitives at runtime.
    for j in range(8):
        a=j*math.pi/4
        tube('Tower panel seam',[(.45*math.cos(a),.55,.45*math.sin(a)),(.311*math.cos(a),1.88,.311*math.sin(a))],.005,'stone',4)
    export('BeaconBody')

def beacon_head():
    begin('BeaconHead')
    lathe('Rotating bearing',[(0,.25),(.026,.29),(.072,.29),(.084,.24)],'brass',32)
    lathe('Lantern sill',[(.084,.34),(.105,.405),(.142,.405),(.158,.34)],'iron',32)
    for i in range(8):
        a=i*math.pi/4; x=.343*math.cos(a);z=.343*math.sin(a)
        tube('Lantern upright',[(x,.148,z),(x,.68,z)],.018,'iron',8)
        lathe('Upright collar',[(.15,.026),(.178,.026)],'brass',8,(x,0,z))
    ring('Lantern upper rim',(0,.66,0),.347,.018,'brass')
    # A real folded/canted copper roof, replacing the squashed sphere silhouette.
    lathe('Copper hip roof',[(.669,.35),(.69,.49),(.72,.51),(.748,.48),(.925,.17),(.945,.145)],'copper',16,smooth=False)
    ring('Roof drip edge',(0,.72,0),.507,.009,'brass')
    for i in range(8):
        a=i*math.pi/4
        tube('Roof standing seam',[(.485*math.cos(a),.751,.485*math.sin(a)),(.167*math.cos(a),.93,.167*math.sin(a))],.006,'brass',5)
    lathe('Ventilator crown',[(.943,.12),(1.02,.12),(1.055,.085),(1.063,0.01)],'teal',16)
    for i in range(8):
        a=i*math.pi/4;box('Ventilator slit',(.12*math.cos(a),.984,.12*math.sin(a)),(.025,.035,.006),'iron',0)
    lathe('Finial',[(1.06,.025),(1.17,.025),(1.28,0)],'brass',12)
    # Needle makes the rotation target readable, mounted above the lamp not across its aperture.
    tube('Bearing needle shaft',[(0,.78,.21),(0,.78,.63)],.011,'ivory',6)
    tube('Bearing needle tip',[(-.07,.78,.51),(0,.78,.63),(.07,.78,.51)],.012,'ivory',6)
    # Copper retaining hoops around, not opaque rods through, the optic.
    for y,r in ((.19,.237),(.36,.267),(.58,.244)): ring('Lens retaining band',(0,y,0),r,.008,'brass')
    export('BeaconHead')

def beacon_lens():
    begin('BeaconLens')
    profile=[(.175,.14)]
    for i in range(12):
        y=.18+i*.034;r=.18+.078*math.sin((i+.5)/12*math.pi)
        profile.extend([(y,r-.015),(y+.012,r),(y+.028,r-.007)])
    profile.append((.61,.14))
    lathe('Twelve optical prism courses',profile,'glass',32,smooth=False)
    # A concentric bullseye optic faces the calibration pointer (+Z).
    for i in range(5): ring('Concentric lens optic',(0,.392,.265+i*.002),.045+i*.024,.010,'glass','xy',32)
    export('BeaconLens')

def record():
    begin('MaintenanceRecord')
    box('Oiled oak backing',(0,0,0),(.74,.65,.05),'wood',.023)
    box('Folded maintenance sheet',(0,0,-.034),(.625,.53,.015),'ivory',.005)
    for x in (-.339,.339): box('Brass document edge',(x,0,-.04),(.024,.625,.023),'brass',.006)
    for y in (-.289,.289): box('Brass document edge',(0,y,-.04),(.68,.02,.023),'brass',.006)
    box('Spring clip',(0,.264,-.069),(.14,.084,.027),'iron',.012)
    for x in (-.05,.05): bolt((x,.271,-.09),radius=.009)
    for i in range(5):
        box('Engraved record lines',(-.035,.112-i*.057,-.045),(.42-(i%2)*.1,.009,.004),'ink',.001)
    for x in (-.077,.077): ring('Maintenance stamp',(x,-.185,-.05),.045,.008,'teal','xy',24)
    export('MaintenanceRecord')

def channel_marker():
    begin('ChannelMarker')
    lathe('Buoy cast foot',[(0,.31),(.13,.41),(.3,.41),(.43,.22)],'teal',24)
    ring('Buoy waterline band',(0,.16,0),.415,.018,'ivory')
    lathe('Buoy mast',[(.4,.09),(.48,.075),(3.45,.047),(3.58,.068)],'iron',16)
    for y in (.48,2.88,3.33): ring('Mast retaining collar',(0,y,0),.082,.015,'brass')
    tube('Crossbar',[(-.73,3.40,0),(.73,3.40,0)],.027,'brass',8)
    for x in (-.65,.65):
        ring('Double ring navigation mark',(x,3.57,0),.50,.041,'ivory','xy',40)
        tube('Marker bracket',[(0,3.15,0),(x,3.40,0)],.016,'teal',6)
    export('ChannelMarker')

crate()
for variant in ('Service','Fish','Cloth'): sign(variant)
coil();beacon_body();beacon_head();beacon_lens();record();channel_marker()
# The .blend is an editable asset library, not required to import or build the Unity project.
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.world.color=(.15,.15,.15)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'HarborProps.blend'))
(SOURCE/'mesh-budget.json').write_text(json.dumps(STATS,indent=2)+'\n',encoding='utf-8')
print('HARBOR_ART_PASS '+json.dumps(STATS))
