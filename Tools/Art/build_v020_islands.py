"""Original v20 coastal meshes in metres. Run stages in Blender's live MCP, or --background.
Unity coordinates: X east, Y up, Z north. FBX uses the same palette as the fishing world.
"""
from pathlib import Path
import bpy, bmesh, math, random, json, hashlib, sys
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV020'
SOURCE = ROOT / 'ArtSource/V020'
PREVIEW = ROOT / 'Docs/Images/V020'
for folder in (OUT, SOURCE, PREVIEW):
    folder.mkdir(parents=True, exist_ok=True)
STAGE = globals().get('STAGE', 'all')
SCENE_NAME = 'V020 Coastal Islands'
scene = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
mat = bpy.data.materials.get('MAT-V020-island-palette')
if mat is None:
    mat = bpy.data.materials.new('MAT-V020-island-palette')
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    vertex = mat.node_tree.nodes.new('ShaderNodeVertexColor')
    vertex.layer_name = 'Color'
    mat.node_tree.links.new(vertex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = .78

SAND, WET, GRASS = (.83,.73,.48), (.49,.43,.30), (.24,.43,.29)
STONE, LIGHT_STONE, MOSS = (.31,.38,.41), (.53,.58,.55), (.27,.40,.31)
WOOD, BARK, BRASS, GLOW = (.40,.25,.13), (.26,.20,.13), (.76,.56,.27), (.36,.83,.80)

def U(p):
    return Vector((p[0], -p[2], p[1]))

def bucket(name):
    key = 'COL-V020-' + name
    col = bpy.data.collections.get(key)
    if col is not None:
        # Only this generator's owned collection is replaced on a stage retry.
        for obj in list(col.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    else:
        col = bpy.data.collections.new(key)
        scene.collection.children.link(col)
    col['v020_asset'] = name
    col.hide_render = False
    return col

def color_mesh(obj, color):
    attr = obj.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
    for face in obj.data.polygons:
        for loop in face.loop_indices:
            c = color(obj.data.vertices[obj.data.loops[loop].vertex_index].co) if callable(color) else color
            attr.data[loop].color_srgb = (*c[:3], 1)
    obj.data.color_attributes.active_color = attr
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj

def mesh(name, vertices, faces, color, col, smooth=False):
    data = bpy.data.meshes.new('GEO-V020-' + name)
    data.from_pydata([U(v) for v in vertices], [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(data); bm.free()
    obj = bpy.data.objects.new(data.name, data)
    col.objects.link(obj)
    for face in data.polygons:
        face.use_smooth = smooth
    return color_mesh(obj, color)

def tube(name, points, radii, color, col, sides=8):
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    for i, p in enumerate(pts):
        tangent = (pts[min(i+1,len(pts)-1)] - pts[max(0,i-1)]).normalized()
        side = tangent.cross(Vector((0,1,0)))
        if side.length < .01:
            side = tangent.cross(Vector((1,0,0)))
        side.normalize(); up = tangent.cross(side).normalized()
        radius = radii[i] if isinstance(radii,(list,tuple)) else radii
        for j in range(sides):
            a = j*math.tau/sides
            verts.append(p + radius*(math.cos(a)*side + math.sin(a)*up))
        if i:
            for j in range(sides):
                k=i*sides+j; n=i*sides+(j+1)%sides
                faces.append((k,n,n-sides,k-sides))
    faces += [tuple(reversed(range(sides))), tuple(range((len(pts)-1)*sides,len(pts)*sides))]
    return mesh(name,verts,faces,color,col,True)

def plank(name, center, size, color, col, yaw=0, tilt=0, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(center))
    obj = bpy.context.object
    obj.name = 'GEO-V020-' + name
    for old in list(obj.users_collection): old.objects.unlink(obj)
    col.objects.link(obj)
    obj.scale = (size[0],size[2],size[1])
    obj.rotation_euler = (math.radians(tilt),0,-math.radians(yaw))
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Worn edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    color_mesh(obj,color)
    return obj

def rock(name, center, size, col, seed=0):
    rng=random.Random(seed); verts=[]; faces=[]; sides=18
    phases=[rng.uniform(-.14,.14) for _ in range(sides)]
    for row in range(9):
        t=row/8; bulge=max(.08,math.sin(t*math.pi)**.36)*(.85+.1*math.sin(t*13))
        for j in range(sides):
            a=j*math.tau/sides; f=bulge*(1+phases[j]+.035*math.sin(t*27+a*3))
            verts.append((center[0]+size[0]*.5*f*math.cos(a)+.1*size[0]*t,
                          center[1]+size[1]*t-.06,
                          center[2]+size[2]*.5*f*math.sin(a)))
            if row:
                k=row*sides+j; n=row*sides+(j+1)%sides
                faces.append((k,n,n-sides,k-sides))
    faces += [tuple(reversed(range(sides))),tuple(range(8*sides,9*sides))]
    def shade(p):
        h=(p.z-center[1])/size[1]
        base=MOSS if h>.85 else STONE
        f=.91+.10*math.sin(h*30)+.05*math.cos(p.x*2+p.y)
        return tuple(min(1,c*f) for c in base)
    return mesh(name,verts,faces,shade,col,True)

def height(island,x,z):
    r=math.hypot(x/(1.3 if island==2 else 1),z/(.88 if island==2 else 1))/30
    if r>.93:
        return .92-2.42*min(1,(r-.93)/.19)
    ridge=max(0,min(1,(-z-4)/20))*max(0,1-r)*3.5
    return .92+max(0,.72-r)*(3.3 if island==3 else 2.5)+ridge+.06*math.sin(x*.3)*math.sin(z*.4)*(1-r)

def terrain(island):
    col=bucket('IslandTerrain'+str(island)); verts=[(0,height(island,0,0),0)]; faces=[]
    sides, rings=128,26
    for row in range(1,rings+1):
        r=row/rings*1.12
        for j in range(sides):
            a=j*math.tau/sides
            f=1+.035*math.sin(a*3+island)+.018*math.cos(a*7-island)
            if island==0: f*=1-.23*max(0,math.cos(a))**4
            if island==1: f*=1+.10*math.sin(a*2)
            if island==3: f*=.94+.035*math.cos(a*5)
            x=math.cos(a)*r*30*f*(1.3 if island==2 else 1)
            z=math.sin(a)*r*30*f*(.88 if island==2 else 1)
            # Small coves affect only the outer shore, keeping the authored footpaths supported.
            h=height(island,x,z) if r<.9 else .92-2.42*max(0,(r-.93)/.19)
            verts.append((x,h,z))
            k=1+(row-1)*sides+j; n=1+(row-1)*sides+(j+1)%sides
            if row==1: faces.append((0,n,k))
            else: faces.extend(((k,n,n-sides),(k,n-sides,k-sides)))
    bottom=len(verts); verts.append((0,-2.5,0))
    for j in range(sides):
        k=1+(rings-1)*sides+j; n=1+(rings-1)*sides+(j+1)%sides
        faces.append((bottom,k,n))
    routes=[((0,26),(0,15)),((0,15),(-6,16)),((0,15),(7,15)),((0,15),(8,-9)),((0,15),(-6,8)),((0,15),(-13,5))]
    def shade(p):
        x,z=p.x,-p.y; r=math.hypot(x/(1.3 if island==2 else 1),z/(.88 if island==2 else 1))/30
        distance=100
        for a,b in routes:
            v=Vector((b[0]-a[0],b[1]-a[1])); q=Vector((x-a[0],z-a[1]))
            t=max(0,min(1,q.dot(v)/v.length_squared)); distance=min(distance,(q-v*t).length)
        grass=min(1,max(0,(.82-r)*10))*min(1,max(0,(distance-1.15)*.75))
        if island==2: grass*=.24
        if island==3: grass*=.62
        base=tuple(SAND[k]*(1-grass)+GRASS[k]*grass for k in range(3))
        if p.z<.2: base=WET
        grain=1+.035*math.sin(x*1.4+z*.9)+.015*math.sin(z*3.2)
        return tuple(c*grain for c in base)
    mesh('sculpted-shore-'+str(island),verts,faces,shade,col,True)

def wind():
    col=bucket('WindArch'); verts=[]; faces=[]
    # A continuous eroded arch, with an open passage and an asymmetric weathered crown.
    for i in range(49):
        a=i/48*math.pi; cx=4.7*math.cos(a); cy=1.1+6.8*math.sin(a)
        for j in range(16):
            b=j/16*math.tau; radius=1.05+.18*math.sin(a*7+b*3)+.08*math.cos(a*17)
            verts.append((cx+math.cos(a)*radius*math.cos(b),cy+math.sin(a)*radius*math.cos(b),radius*1.35*math.sin(b)))
            if i:
                k=i*16+j; n=i*16+(j+1)%16; faces.append((k,n,n-16,k-16))
    faces += [tuple(reversed(range(16))),tuple(range(48*16,49*16))]
    mesh('sea-carved-arch',verts,faces,lambda p:tuple(c*(.94+.08*math.sin(p.z*7)) for c in LIGHT_STONE),col,True)
    for j in range(5):
        rock('arch-foot-'+str(j),(4.5 if j%2 else -4.5,-.3,(j-2)*.62),(3.1,2.6,3.5),col,30+j)
    for j in range(6):
        x=(j-2.5)*.45
        tube('bell-rope',[(x,6.6,-1.32),(x,5.5-j*.11,-1.35)],.024,WOOD,col,6)
        tube('wind-chime',[(x,5.5-j*.11,-1.35),(x,4.6-j*.11,-1.35)],[.09,.10],BRASS,col,10)
    col=bucket('CoastalRock')
    rock('striated-monolith',(0,0,0),(4.6,8.4,3.8),col,51)
    rock('fallen-foot',(1.8,-.08,.6),(3.4,2.2,3.1),col,56)
    col=bucket('LookoutDeck')
    for j in range(13):
        plank('deck-board',(0,.05,-1.7+j*.28),(3.6,.15,.25),tuple(c*(.9+j%3*.07) for c in WOOD),col)
    for x in (-1.65,1.65):
        for z in (-1.65,1.65):
            tube('carved-upright',[(x,.1,z),(x,2.75,z)],[.13,.10],WOOD,col,12)
        for y in (.65,1.05): tube('rope-rail',[(x,y,-1.65),(x,y*.87,0),(x,y,1.65)],.045,SAND,col,8)
    # Open front for the existing rope ladder; pitched copper patina canopy with visible rafters.
    for side in (-1,1):
        mesh('pitched-canopy',[(0,3.3,-2.1),(0,3.3,2.1),(side*2.3,2.4,2.1),(side*2.3,2.4,-2.1)],[(0,1,2,3)],(.19,.38,.38),col)
        for j in range(7):
            z=-1.9+j*.63; tube('roof-rib',[(side*2.3,2.36,z),(0,3.26,z)],.04,BRASS,col)
    col=bucket('LandingJetty')
    for j in range(38):
        plank('jetty-board',(0,0,-7.3+j*.394),(2.4,.16,.35),tuple(c*(.87+j%5*.05) for c in WOOD),col)
    for x in (-.9,.9):
        plank('jetty-stringer',(x,-.19,0),(.19,.25,15),BARK,col)
    for x in (-1.1,1.1):
        for z in (-6.5,-2.2,2.2,6.5):
            tube('mooring-pile',[(x,-2.8,z),(x,1.05,z)],[.14,.11],WOOD,col,12)
        for y in (.55,.90):
            tube('jetty-rope-rail',[(x,y,-6.5),(x,y-.12,-4.35),(x,y,-2.2),(x,y-.12,0),(x,y,2.2),(x,y-.12,4.35),(x,y,6.5)],.025,SAND,col,6)

def leaf(name, start, end, width, color, col):
    a,b=Vector(start),Vector(end); direction=(b-a).normalized()
    side=direction.cross(Vector((0,1,0))).normalized()
    verts=[]
    for j in range(5):
        t=j/4; center=a.lerp(b,t)+Vector((0,.16*math.sin(t*math.pi),0))
        w=width*math.sin(t*math.pi)**.8
        verts.extend((center-side*w,center+Vector((0,.04,0)),center+side*w))
    faces=[]
    for j in range(4):
        k=j*3
        faces.extend(((k,k+3,k+4,k+1),(k+1,k+4,k+5,k+2)))
    obj=mesh(name,verts,faces,color,col,True)
    mod=obj.modifiers.new('Leaf underside','SOLIDIFY');mod.thickness=.012
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name);obj.select_set(False)

def forest():
    rng=random.Random(20); col=bucket('Mangrove')
    trunk=[(0,0,0),(.1,1.2,0),(-.15,2.6,.15),(.2,4.4,.25),(.3,6.5,.3)]
    tube('old-growth-trunk',trunk,[.46,.38,.31,.23,.06],BARK,col,12)
    for j in range(9):
        a=j*math.tau/9
        tube('exposed-root',[(0,1.1,0),(math.cos(a)*.8,.38,math.sin(a)*.8),(math.cos(a)*1.65,.06,math.sin(a)*1.65)],[.23,.14,.05],BARK,col,8)
    for j in range(10):
        a=j*2.399; tip=Vector((math.cos(a)*(1.8+j%3*.3),4.8+j%3*.75,math.sin(a)*(1.8+j%3*.3)))
        base=Vector((0,2.8+j%4*.45,0)); mid=base.lerp(tip,.6)+Vector((0,.5,0))
        tube('sweeping-branch',[base,mid,tip],[.16,.1,.025],BARK,col,8)
        for k in range(19):
            b=k*2.399+j; offset=Vector((math.cos(b)*(.35+k%3*.28),rng.uniform(-.15,.5),math.sin(b)*(.35+k%3*.28)))
            s=tip+offset
            leaf('broad-canopy-leaf',s,s+Vector((math.cos(b)*1.25,.13,math.sin(b)*1.25)),.32,tuple(c*(.82+rng.random()*.38) for c in GRASS),col)
    col=bucket('CoastalPine')
    tube('wind-bent-pine',[(0,0,0),(.1,2.3,0),(.4,4.5,.2),(.7,7,.3)],[.32,.24,.16,.02],BARK,col,12)
    for level in range(6):
        y=2+level*.8; radius=2.45-level*.29
        for j in range(7):
            a=j*math.tau/7+level*.7
            tip=(math.cos(a)*radius+.1,y+.3,math.sin(a)*radius)
            tube('pine-bough',[(.1,y,0),tip],[.08,.012],BARK,col,6)
            for k in range(4):
                b=a+(k-1.5)*.25; start=Vector((.1,y,0)).lerp(Vector(tip),.32+k*.15)
                leaf('pine-needles',start,start+Vector((math.cos(b)*1.05,.15,math.sin(b)*1.05)),.28,(.18,.34+level*.014,.27),col)
    col=bucket('CoastalFern')
    for j in range(9):
        a=j*math.tau/9; tip=(math.cos(a)*1.2,.6,math.sin(a)*1.2)
        tube('fern-stem',[(0,0,0),(0,.55,0),tip],[.03,.025,.01],GRASS,col,6)
        for k in range(1,7):
            t=k/7; p=Vector((0,.45,0)).lerp(Vector(tip),t)
            for side in (-1,1):
                end=p+Vector((math.cos(a+side*.65)*.48*(1-t*.7),.08,math.sin(a+side*.65)*.48*(1-t*.7)))
                leaf('fern-pinna',p,end,.085,GRASS,col)

def wreck():
    col=bucket('Shipwreck'); rng=random.Random(220)
    # Real curved ribs and staggered hull strakes, with broken edges and an open interior.
    for j in range(15):
        z=-7+j; width=2.65*max(.2,math.sin((j+.8)/16*math.pi))
        pts=[]
        for k in range(13):
            a=k/12*math.pi; pts.append((width*math.cos(a),.25+2.7*(1-math.sin(a)),z))
        tube('exposed-frame',pts,.095,WOOD,col,8)
    for side in (-1,1):
        for row in range(6):
            y=.65+row*.36
            for j in range(12):
                if (side==1 and j>7 and row>2) or (side==-1 and j in (2,3) and row>3): continue
                z=-6.3+j*1.06
                w=2.6*max(.2,math.sin((z+7.8)/16*math.pi))
                x=side*w*math.sqrt(max(.05,1-((2.95-y)/2.7)**2))
                plank('splintered-strake',(x,y,z),(.17,.32,1.12),tuple(c*(.84+rng.random()*.27) for c in WOOD),col,yaw=side*(z*2.7),tilt=side*12)
    tube('curved-keel',[(0,.18,z) for z in range(-7,9)],.18,BARK,col,10)
    for j in range(24):
        x=(j%6-2.5)*.58; z=(j//6-1.5)*1.0
        if j in (8,9,14): continue
        plank('partial-deck',(x,1.15,z),(.53,.13,.94),WOOD,col,tilt=(j%3-1)*5)
    tube('broken-mast',[(.2,1.1,-1),(.35,4.2,-1),(.95,8,-.8)],[.24,.19,.1],WOOD,col,12)
    tube('fallen-yard',[(-3.8,5.2,-1.2),(3.5,4.8,-.7)],.11,WOOD,col,10)
    for side in (-1,1):
        tube('standing-rigging',[(side*2.0,2.4,3),(.95,7.9,-.8)],.032,SAND,col,6)
        tube('slack-mooring',[(side*2.3,2.2,-4),(side*3.2,1.3,-3),(side*4.2,.05,-1)],.047,SAND,col,6)
    mesh('torn-linen-sail',[(-2.8,5.1,-1),(.3,5,-1),(.2,2.9,-1.1),(-.3,3.15,-1.08),(-.65,2.6,-1.1),(-1.1,3.3,-1.05),(-1.5,2.85,-1.1),(-2,3.7,-1.05)],[(0,1,2,3,4,5,6,7)],(.76,.70,.53),col)
    for j in range(7):
        plank('washed-up-timber',(3.5+j*.23,.07,-5+j*.8),(.23,.14,2.7),WOOD,col,yaw=j*31)

def star():
    col=bucket('Observatory')
    # Individually cut radial stones, fluted columns, broken capitals and a physical armillary.
    for tier in range(3):
        radius=5.2-tier*.5
        for j in range(24):
            # The missing northwest masonry opens both the letter approach and the beacon return route.
            if j>=20: continue
            a=j*math.tau/24; b=(j+.96)*math.tau/24; inner=radius-.95; y=tier*.22
            verts=[(r*math.cos(t),h,r*math.sin(t)) for h in (y,y+.20) for r,t in ((inner,a),(radius,a),(radius,b),(inner,b))]
            mesh('radial-foundation',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],LIGHT_STONE,col)
    for j in range(7):
        a=j*math.tau/8+.12; x,z=math.cos(a)*4.15,math.sin(a)*4.15
        h=2.0 if j==3 else 4.1+(j%3)*.55
        tube('fluted-ruin-column',[(x,.2,z),(x,.55,z),(x,h-.25,z),(x,h,z)],[.50,.38,.33,.48],LIGHT_STONE,col,16)
        for k in range(8):
            b=k*math.tau/8
            tube('column-flute',[(x+math.cos(b)*.34,.55,z+math.sin(b)*.34),(x+math.cos(b)*.29,h-.25,z+math.sin(b)*.29)],.035,STONE,col,6)
        rock('weathered-capital',(x-.1,h-.04,z),(1.1,.32,1.0),col,90+j)
    tube('armillary-stand',[(0,.1,0),(0,1.2,0),(0,2.6,0)],[.7,.38,.18],STONE,col,16)
    for j in range(3):
        rotation=Matrix.Rotation(math.radians(35+j*48),4,'X') @ Matrix.Rotation(math.radians(j*42),4,'Z')
        pts=[]
        for k in range(65):
            a=k*math.tau/64; p=rotation@Vector((math.cos(a)*1.9,math.sin(a)*1.9,0))
            pts.append((p.x,p.z+3.8,-p.y))
        tube('bronze-celestial-ring',pts,.055,BRASS,col,8)
    rock('fallen-pillar',(-3,.55,-1),(2.8,1.0,.85),col,129)
    col=bucket('TideGate'); radius=1.35
    for side in (-1,1):
        for row in range(5):
            plank('weathered-jamb',(side*1.6,.3+row*.56,0),(.56,.53,.72),LIGHT_STONE,col,bevel=.08)
            plank('jamb-inlay',(side*1.6,.3+row*.56,-.375),(.06,.39,.025),GLOW,col,bevel=.01)
    for j in range(15):
        a=j/15*math.pi; b=(j+.95)/15*math.pi
        verts=[(r*math.cos(t),2.58+r*math.sin(t),z) for z in (-.38,.38) for r,t in ((radius,a),(radius+.54,a),(radius+.54,b),(radius,b))]
        mesh('carved-arch-stone',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],LIGHT_STONE,col)
    for j in range(3):
        tube('tide-rune',[(x,3.94+j*.11+.05*math.sin(x*4),-.40) for x in (-.45,-.3,-.15,0,.15,.3,.45)],.019,GLOW,col,6)
    for side in (-1,1): rock('gate-foot',(side*1.65,-.1,0),(1.12,.45,1.05),col,190+(side+1))

def export():
    report=[]
    for col in list(scene.collection.children):
        name=col.get('v020_asset')
        if not name: continue
        objects=[o for o in col.objects if o.type=='MESH']
        assert objects, name
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.hide_set(False); obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1: bpy.ops.object.join()
        obj=bpy.context.object; obj.name='GEO-V020-'+name
        if obj.data.users>1: obj.data=obj.data.copy()
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        bm=bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.triangulate(bm,faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free()
        obj.data.materials.clear();obj.data.materials.append(mat)
        for face in obj.data.polygons: face.material_index=0
        assert len(obj.data.color_attributes['Color'].data)==len(obj.data.loops),name
        assert all(math.isfinite(v) for p in obj.data.vertices for v in p.co),name
        triangles=len(obj.data.polygons)
        assert 100<triangles<24000,(name,triangles)
        obj.data.calc_loop_triangles()
        assert all(t.area>1e-9 for t in obj.data.loop_triangles),('degenerate triangles',name)
        path=OUT/(name+'.fbx')
        # FBX -> Unity reflects X. Mirror an export copy and reverse winding; retain the editable source.
        original=obj.data;converted=original.copy();bm=bmesh.new();bm.from_mesh(converted)
        bmesh.ops.scale(bm,vec=Vector((-1,1,1)),verts=list(bm.verts))
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(converted);bm.free();obj.data=converted
        try:
            bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},bake_anim=False,
                axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',mesh_smooth_type='FACE',add_leaf_bones=False)
        finally:
            obj.data=original;bpy.data.meshes.remove(converted)
        assert path.stat().st_size>1000
        report.append(dict(name=name,triangles=triangles,dimensions=list(obj.dimensions),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        col.hide_render=True
    assert len(report)==14, len(report)
    (SOURCE/'asset-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalIslands-v020.blend'))
    print('V020_EXPORT',json.dumps(report),flush=True)

def render_asset(name):
    target=bpy.data.collections['COL-V020-'+name]
    for col in scene.collection.children:
        if col.get('v020_asset'): col.hide_render=col!=target
        if col.get('v020_review'): col.hide_render=True
    lights=bpy.data.collections.get('COL-V020-preview-lights')
    if lights is None:
        lights=bpy.data.collections.new('COL-V020-preview-lights');scene.collection.children.link(lights)
        world=bpy.data.worlds.new('MAT-V020-preview-sky');world.use_nodes=True
        bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
        bg.inputs['Color'].default_value=(.24,.34,.42,1);bg.inputs['Strength'].default_value=.65;scene.world=world
        data=bpy.data.lights.new('LGT-V020-sun','SUN');data.energy=3;data.angle=math.radians(8)
        obj=bpy.data.objects.new(data.name,data);lights.objects.link(obj);obj.rotation_euler=(.45,-.6,-.7)
        data=bpy.data.cameras.new('CAM-V020-review');data.lens=55
        cam=bpy.data.objects.new(data.name,data);lights.objects.link(cam);scene.camera=cam
        scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
        scene.render.image_settings.file_format='PNG'
    obj=next(o for o in target.objects if o.type=='MESH')
    corners=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    low=Vector([min(p[i] for p in corners) for i in range(3)]);high=Vector([max(p[i] for p in corners) for i in range(3)])
    center=(low+high)*.5;size=max(high-low)
    cam=scene.camera
    assert 'PERSP' in [v.identifier for v in cam.data.bl_rna.properties['type'].enum_items]
    cam.data.type='PERSP';cam.data.lens=55
    cam.location=center+Vector((size*1.3,-size*1.9,size*.9))
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(PREVIEW/('source-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
    assert Path(scene.render.filepath).stat().st_size>1000
    print('V020_RENDER',scene.render.filepath,flush=True)

def gallery():
    if scene.camera is None: render_asset('WindArch')
    key='COL-V020-library-review';review=bpy.data.collections.get(key)
    if review is None:
        review=bpy.data.collections.new(key);scene.collection.children.link(review)
    for old in list(review.objects): bpy.data.objects.remove(old,do_unlink=True)
    review['v020_review']=True;review.hide_render=False
    assets=[c for c in scene.collection.children if c.get('v020_asset')]
    for i,col in enumerate(assets):
        col.hide_render=True
        obj=next(o for o in col.objects if o.type=='MESH');obj.hide_set(True)
        copy=obj.copy();review.objects.link(copy);copy.name='GEO-V020-review-'+col['v020_asset'];copy.hide_set(False)
        scale=10/max(obj.dimensions);copy.scale=Vector((scale,scale,scale))
        copy.location=Vector((i%4*15,-(i//4)*15,-min(v.co.z for v in obj.data.vertices)*scale))
    cam=scene.camera;center=Vector((22.5,-22.5,3.5));cam.location=center+Vector((35,-65,57))
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    assert 'ORTHO' in [v.identifier for v in cam.data.bl_rna.properties['type'].enum_items]
    cam.data.type='ORTHO';cam.data.ortho_scale=85
    scene.render.filepath=str(PREVIEW/'blender-library.png');bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalIslands-v020.blend'))
    print('V020_LIBRARY',len(assets),flush=True)

if STAGE in ('terrain','all'):
    for island in range(4): terrain(island)
if STAGE in ('wind','all'): wind()
if STAGE in ('forest','all'): forest()
if STAGE in ('wreck','all'): wreck()
if STAGE in ('star','all'): star()
if STAGE in ('export','all'): export()
if STAGE=='render': render_asset(globals().get('ASSET','Shipwreck'))
if STAGE in ('gallery','all'): gallery()
print('V020_STAGE', STAGE, 'complete',flush=True)
