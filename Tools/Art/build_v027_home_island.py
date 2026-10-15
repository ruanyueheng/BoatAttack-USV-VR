"""Reference-inspired coastal island; fresh background Blender only, never the live scene."""
from pathlib import Path
import bpy, math, random, json, sys
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/V027'
OUT=ROOT/'Assets/Resources/FishingV027/Home'
PREVIEW=ROOT/'Docs/Images/V027'
for folder in (ART,OUT,PREVIEW): folder.mkdir(parents=True,exist_ok=True)
BLOCKOUT='--blockout' in sys.argv
rng=random.Random(27015)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=24 if BLOCKOUT else 48
scene.cycles.use_denoising=True
scene.render.resolution_x=1400
scene.render.resolution_y=1050
scene.render.resolution_percentage=70 if BLOCKOUT else 100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='Standard'
world=bpy.data.worlds.new('COL-V027 cream sky');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs['Color'].default_value=(.92,.89,.78,1);bg.inputs['Strength'].default_value=.70
mat=bpy.data.materials.new('MAT-V027 handpainted coastal palette');mat.use_nodes=True
bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bsdf.inputs['Roughness'].default_value=.86
vertex=mat.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
mat.node_tree.links.new(vertex.outputs['Color'],bsdf.inputs['Base Color'])
PARTS=[];MODELS=[];REPORT=[]
WOOD=(.66,.68,.57);WOOD_DARK=(.34,.43,.38);WOOD_LIGHT=(.74,.75,.65)
CREAM=(.95,.90,.76);SAND=(.80,.71,.53);ROOF=(.72,.32,.18);TILE=(.80,.43,.23)
GRASS=(.53,.63,.39);LEAF=(.25,.44,.32);ROCK=(.65,.64,.56);INK=(.14,.27,.25)

def unity(p): return Vector((p[0],p[2],p[1]))

def paint(obj,name,color,variation=.025):
    obj.name='GEO-'+name
    attr=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    for face in obj.data.polygons:
        factor=1+rng.uniform(-variation,variation)
        for loop in face.loop_indices: attr.data[loop].color_srgb=(*[min(1,max(0,v*factor)) for v in color],1)
    obj.data.color_attributes.active_color=attr;obj.data.materials.append(mat);PARTS.append(obj)
    return obj

def mesh(name,vertices,faces,color):
    data=bpy.data.meshes.new(name);data.from_pydata([unity(v) for v in vertices],[],faces);data.update()
    obj=bpy.data.objects.new('GEO-'+name,data);scene.collection.objects.link(obj)
    return paint(obj,name,color)

def box(name,p,size,color,yaw=0):
    x,y,z=p;sx,sy,sz=[v*.5 for v in size];a=math.radians(yaw)
    points=[]
    for dx,dy,dz in [(-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),(-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)]:
        points.append((x+dx*math.cos(a)+dz*math.sin(a),y+dy,z-dx*math.sin(a)+dz*math.cos(a)))
    # Unity-to-Blender coordinate exchange reverses handedness.
    faces=[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)]
    return mesh(name,points,faces,color)

def tube(name,a,b,r,color,end=None,sides=10):
    a,b=unity(a),unity(b)
    bpy.ops.mesh.primitive_cone_add(vertices=sides,radius1=r,radius2=r if end is None else end,depth=(b-a).length,location=(a+b)*.5)
    obj=bpy.context.object;obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return paint(obj,name,color)

def rock(name,p,size,color=ROCK,subdivision=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivision,radius=1,location=unity(p))
    obj=bpy.context.object
    for v in obj.data.vertices:
        v.co*=rng.uniform(.83,1.16);v.co.x*=size[0];v.co.y*=size[2];v.co.z*=size[1]
    obj.rotation_euler.z=rng.random()*math.tau
    return paint(obj,name,color,.10)

def height(x,z):
    radius=math.sqrt(((x+6)/29)**2+((z-4)/24)**2)
    base=-2.6+3.2*max(0,1-radius)**.52
    hill=3.7*math.exp(-((x+14)/12)**2-((z-11)/11)**2)
    h=base+hill+.10*math.sin(x*.47)*math.sin(z*.31)
    flat=max(abs(x)/7.8,abs(z+1.5)/8.3)
    blend=max(0,min(1,(flat-1)*2.4));blend=blend*blend*(3-2*blend)
    h*=blend
    if abs(x)<1.7 and -22<z<-7.8:
        walk=1.05+max(0,min(1,(z+21)/13))*1.35-2.2
        h=min(h,walk-.34)
    return h

def island():
    vertices=[];faces=[];indices={};step=.85
    for ix in range(71):
        for iz in range(60):
            x=-36+ix*step;z=-21+iz*step
            if ((x+6)/29)**2+((z-4)/24)**2>1.04: continue
            indices[ix,iz]=len(vertices);vertices.append((x,height(x,z),z))
    for ix,iz in indices:
        if all(k in indices for k in [(ix+1,iz),(ix,iz+1),(ix+1,iz+1)]):
            a,b,c,d=[indices[k] for k in [(ix,iz),(ix+1,iz),(ix+1,iz+1),(ix,iz+1)]]
            faces.extend([(a,b,c),(a,c,d)])
    obj=mesh('IslandGround',vertices,faces,GRASS)
    attr=obj.data.color_attributes['Color']
    for face in obj.data.polygons:
        point=sum((obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices),Vector())/len(face.loop_indices)
        x,z,y=point.x,point.y,point.z
        path=abs(x+max(0,z-1)*1.25)<1.1 and 0<z<12
        color=SAND if y<-.90 or path else GRASS
        if y<-.90: color=tuple(v*.95 for v in SAND)
        for i in face.loop_indices: attr.data[i].color_srgb=(*[min(1,v*rng.uniform(.94,1.06)) for v in color],1)
        face.use_smooth=True
    return finish('IslandGround')

def finish(name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS: obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]
    if len(PARTS)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name='GEO-'+name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.clear();obj.data.materials.append(mat)
    for face in obj.data.polygons: face.material_index=0
    obj.data.calc_loop_triangles();count=len(obj.data.loop_triangles)
    REPORT.append({'name':name,'vertices':len(obj.data.vertices),'triangles':count})
    MODELS.append(obj);PARTS.clear()
    if not BLOCKOUT:
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,mesh_smooth_type='FACE')
    return obj

def roof(name,cx,front,back,half,eave,ridge,color):
    for side in [-1,1]:
        a=(cx,eave+(ridge-eave),front);b=(cx+side*half,eave,front);c=(cx+side*half,eave,back);d=(cx,ridge,back)
        slope=mesh(name+' slope', [a,b,c,d],[(3,2,1,0) if side<0 else (0,1,2,3)],color)
        assert all(face.normal.z>0 for face in slope.data.polygons),'Roof must face upwards in single-sided Unity rendering'
        lower=[(p[0],p[1]-.12,p[2]) for p in [a,b,c,d]]
        face=(0,1,2,3) if side<0 else (3,2,1,0)
        mesh(name+' closed underside',lower,[face],WOOD_LIGHT)
        for i in range(4):
            j=(i+1)%4
            mesh(name+' roof edge',[[a,b,c,d][i],[a,b,c,d][j],lower[j],lower[i]],[(0,1,2,3)],color)
        if not BLOCKOUT:
            for row in range(10):
                lo=row/10;hi=(row+1)/10
                for col in range(18):
                    z0=front+(back-front)*col/18;z1=front+(back-front)*(col+1)/18-.035
                    y0=ridge-(ridge-eave)*lo+.042;y1=ridge-(ridge-eave)*hi+.042
                    pts=[(cx+side*half*lo,y0,z0),(cx+side*half*hi,y1,z0),(cx+side*half*hi,y1,z1),(cx+side*half*lo,y0,z1)]
                    tint=tuple(min(1,v*rng.uniform(.87,1.09)) for v in TILE)
                    mesh('Weathered roof tile',pts,[(3,2,1,0) if side<0 else (0,1,2,3)],tint)
    tube('Rounded roof ridge',(cx,ridge+.07,front),(cx,ridge+.07,back),.12,color,sides=8)

def window(name,x,y,z,w=1.1,h=1.3,side=False):
    if side:
        start=len(PARTS);window(name,0,0,0,w,h)
        for obj in PARTS[start:]:obj.rotation_euler.z=math.pi*.5 if x>0 else -math.pi*.5;obj.location=unity((x,y,z))
        return
    box(name+' dark glazing',(x,y,z),(w,h,.04),INK)
    for dx in [-w*.5,w*.5]: box(name+' cream jamb',(x+dx,y,z-.04),(.10,h+.18,.10),CREAM)
    for dy in [-h*.5,h*.5]: box(name+' cream sill',(x,y+dy,z-.05),(w+.20,.11,.14),CREAM)
    box(name+' mullion',(x,y,z-.065),(.055,h,.075),CREAM)
    box(name+' middle rail',(x,y,z-.07),(w,.06,.075),CREAM)
    for sign in [-1,1]:
        sx=x+sign*(w*.5+.32);box(name+' sage shutter',(sx,y,z-.06),(.40,h+.12,.08),WOOD_DARK)
        for j in range(5): box('Shutter louvre',(sx,y-h*.38+j*h*.19,z-.105),(.38,.055,.04),WOOD_LIGHT)

def house():
    # Door opening/floor/inner walls match FishingHome's existing collision contract.
    box('Cottage floor',(0,.12,0),(12,.16,10),WOOD_LIGHT)
    box('Cottage rear wall',(0,1.75,5),(12,3.1,.14),WOOD)
    for sign in [-1,1]:
        box('Cottage side wall',(sign*6,1.75,0),(.14,3.1,10),WOOD)
        box('Cottage front wing',(sign*3.75,1.75,-5),(4.5,3.1,.14),WOOD)
    box('Cottage door lintel',(0,3.06,-5),(3,.24,.16),WOOD)
    if not BLOCKOUT:
        for sign in [-1,1]:
            for col in range(21):
                shade=rng.uniform(.94,1.05);color=tuple(v*shade for v in WOOD)
                box('Vertical aged front plank',(sign*(1.64+col*.211),1.75,-5.105),(.195,2.99,.038),color)
            for col in range(48):
                shade=rng.uniform(.94,1.05);color=tuple(v*shade for v in WOOD)
                box('Vertical aged side plank',(sign*6.105,1.75,-4.88+col*.207),(.038,2.99,.19),color)
        for col in range(57):
            shade=rng.uniform(.94,1.05)
            box('Vertical aged rear plank',(-5.88+col*.21,1.75,5.105),(.19,2.99,.038),tuple(v*shade for v in WOOD))
    for z in [-5.06,5.06]:
        points=[(x,y,depth) for depth in [z-.08,z+.08] for x,y in [(-6.4,3.2),(6.4,3.2),(0,5.98)]]
        gable=mesh('Solid cream gable',points,[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],WOOD_LIGHT)
        assert gable.data.polygons[0].normal.y<-.99 and gable.data.polygons[1].normal.y>.99,'Gable must close the cottage from inside and outside'
    roof('Main terracotta roof',0,-5.5,5.5,6.4,3.32,5.98,ROOF)
    for sign in [-1,1]:
        for z in [-5.18,5.18]: box('Cream corner trim',(sign*6.04,1.78,z),(.16,3.24,.18),CREAM)
        tube('Main bargeboard',(0,6.03,-5.58),(sign*6.4,3.36,-5.58),.095,CREAM,sides=6)
        box('Entry door trim',(sign*1.53,1.52,-5.17),(.14,2.74,.18),CREAM)
        window('Front cottage window',sign*3.8,1.8,-5.16)
        for z in [-2.0,2.5]:window('Side cottage window',sign*6.16,1.8,z,1.05,1.3,True)
    box('Entry header',(0,2.87,-5.17),(3.2,.16,.18),CREAM)
    # Small front dormer follows the painting without reducing the walkable room.
    box('Dormer front',(-2.5,4.60,-5.52),(2.25,1.55,.15),WOOD_LIGHT)
    mesh('Dormer gable',[(-3.75,5.28,-5.57),(-1.25,5.28,-5.57),(-2.5,6.45,-5.57)],[(0,1,2)],CREAM)
    roof('Dormer tiled roof',-2.5,-5.8,-3.5,1.35,5.26,6.49,ROOF)
    window('Attic window',-2.5,4.63,-5.65,.83,1.1)
    box('Stone chimney',(3.65,5.48,2.7),(.75,3.0,.78),(.56,.57,.49))
    box('Chimney crown',(3.65,7.02,2.7),(.92,.18,.94),CREAM)
    box('Chimney soot',(3.65,7.13,2.7),(.64,.05,.66),INK)
    for row in range(8): box('Chimney courses',(3.65,4.25+row*.32,2.28),(.76,.038,.045),(.39,.42,.37))
    for col in range(31): box('Porch weathered plank',(-6.2+col*.412,.14,-6.5),(.389,.12,3),WOOD_LIGHT)
    for x in [-6.20,6.20]:
        for z in [-5.15,-7.8]: tube('Porch post',(x,.2,z),(x,2.65,z),.11,CREAM,sides=8)
        tube('Porch top rail',(x,2.58,-5.15),(x,2.58,-7.8),.095,WOOD_LIGHT,sides=8)
        tube('Porch low rail',(x,.96,-5.2),(x,.96,-7.7),.05,CREAM,sides=6)
    for x in [-4.3,4.4]:
        tube('Clay plant pot',(x,.2,-7.4),(x,.69,-7.4),.30,ROOF,end=.4,sides=10)
        for j in range(5): rock('Porch foliage',(x+rng.uniform(-.35,.35),.82+rng.random()*.3,-7.4+rng.uniform(-.2,.2)),(.32,.26,.32),LEAF)
    return finish('HouseShell')

def lighthouse():
    x,z=-14,10;y=height(x,z)+.10
    tube('Lighthouse stone plinth',(x,y-.2,z),(x,y+.35,z),2.5,ROCK,end=2.3,sides=24)
    tube('Cream lighthouse tower',(x,y+.35,z),(x,y+13.5,z),2.10,CREAM,end=1.50,sides=32)
    for n in range(5):
        level=y+2+n*2.30
        box('Lighthouse window slit',(x,level,z-1.96+n*.1),(.52,.86,.08),INK)
        for side in [-1,1]: box('Lighthouse window trim',(x+side*.31,level,z-2.00+n*.1),(.09,1.02,.07),SAND)
    box('Lighthouse entrance',(x,y+1.15,z-2.1),(1.1,1.65,.10),WOOD_DARK)
    tube('Lantern gallery deck',(x,y+13.44,z),(x,y+13.66,z),2.15,SAND,sides=32)
    tube('Lantern blue glazing',(x,y+13.66,z),(x,y+15.48,z),1.46,(.37,.57,.56),sides=16)
    tube('Lantern warm light',(x,y+14.0,z),(x,y+14.92,z),.23,(1,.75,.34),sides=12)
    for i in range(16):
        a=math.tau*i/16;dx,dz=1.49*math.cos(a),1.49*math.sin(a)
        tube('Lantern mullion',(x+dx,y+13.65,z+dz),(x+dx,y+15.5,z+dz),.05,INK,sides=6)
        dx,dz=2.05*math.cos(a),2.05*math.sin(a)
        tube('Gallery baluster',(x+dx,y+13.62,z+dz),(x+dx,y+14.58,z+dz),.045,INK,sides=6)
    for radius,level in [(2.07,y+14.58),(2.07,y+14.08),(1.52,y+15.49)]:
        for i in range(24):
            a,b=math.tau*i/24,math.tau*(i+1)/24
            tube('Gallery rail',(x+radius*math.cos(a),level,z+radius*math.sin(a)),(x+radius*math.cos(b),level,z+radius*math.sin(b)),.038,INK,sides=6)
    tube('Copper lantern roof',(x,y+15.53,z),(x,y+17.10,z),1.90,ROOF,end=.12,sides=24)
    tube('Roof finial',(x,y+17.1,z),(x,y+17.65,z),.10,SAND,end=.06,sides=10)
    for i in range(11):
        a=math.tau*i/11
        rock('Plinth shoreline stone',(x+2.4*math.cos(a),y-.1,z+2.4*math.sin(a)),(.6,.40,.55),ROCK)

def tree(x,z,size):
    y=height(x,z)
    tube('Coastal tree trunk',(x,y,z),(x+.15,y+size*.72,z+.1),size*.065,WOOD_DARK,end=size*.022,sides=7)
    for i in range(4 if BLOCKOUT else 7):
        angle=math.tau*i/7;px=x+math.cos(angle)*size*.23;pz=z+math.sin(angle)*size*.23
        yy=y+size*(.60+.22*rng.random())
        tube('Tree branch',(x,y+size*.35,z),(px,yy,pz),size*.035,WOOD_DARK,end=size*.01,sides=6)
        rock('Wind-shaped canopy',(px,yy,pz),(size*.35,size*.22,size*.32),LEAF,1 if BLOCKOUT else 2)

def scenery():
    lighthouse()
    for x,z,scale in [(-23,-4,(3.4,2.8,2.5)),(-18,-10,(3.0,2.5,2.5)),(-28,3,(2.6,2.7,2.8)),(-9,-15,(2.4,1.8,2.4)),(14,-8,(2.5,1.7,2.7))]:
        rock('Major painted coastal outcrop',(x,height(x,z)-.85,z),scale,ROCK,1 if BLOCKOUT else 2)
    for i in range(55 if not BLOCKOUT else 15):
        a=math.tau*i/55;r=1+rng.uniform(-.035,.04)
        x=-6+29*r*math.cos(a);z=4+24*r*math.sin(a)
        if abs(x)<3.2 and z<-12: continue
        yy=height(x,z)
        rock('Weathered sea rock',(x,yy-.25,z),(rng.uniform(1.0,2.4),rng.uniform(.9,1.9),rng.uniform(.9,2.0)),ROCK,1)
    for x,z,size in [(-22,15,6.5),(-25,9,5.7),(-19,19,7.1),(-9,22,6.2),(-2,20,5.4),(8,19,6.0),(13,10,4.8),(14,3,4.6),(-23,2,4.8)]: tree(x,z,size)
    if not BLOCKOUT:
        for i in range(28):
            x,z=rng.uniform(-25,16),rng.uniform(-8,21)
            if abs(x)<8 and -9<z<7: continue
            if abs(x+max(0,z-1)*1.25)<1.7: continue
            y=height(x,z)
            rock('Salt-tolerant shrub',(x,y+.36,z),(.65,.46,.62),GRASS)
            for j in range(3):
                tube('Wild grass',(x+rng.uniform(-.4,.4),y,z+rng.uniform(-.3,.3)),(x+rng.uniform(-.2,.2),y+.5,z+rng.uniform(-.2,.2)),.025,SAND,end=.008,sides=4)
    for i in range(12):
        if i==10:continue
        x=-20+i*1.15;z=2.6+i*.55;y=height(x,z)
        box('Weathered hillside fence post',(x,y+.53,z),(.12,1.06,.12),WOOD_LIGHT)
        if i>0 and i not in (9,10,11):
            x0=x-1.15;z0=z-.55;y0=height(x0,z0)
            for up in [.52,.91]: tube('Handmade fence rail',(x0,y0+up,z0),(x,y+up,z),.047,WOOD_LIGHT,sides=6)
    # Authored details dress the existing playable 1.3-metre-rise boardwalk.
    for i in range(33):
        z=-21+i*.39;y=1.05+(z+21)/13*1.35-2.2
        box('Pier plank',(0,y+.035,z),(2.60,.11,.35),SAND)
    for x in [-1.32,1.32]:
        for z in [-20,-16,-12,-8.7]:
            y=1.05+max(0,min(1,(z+21)/13))*1.35-2.2
            tube('Pier timber pile',(x,-2.50,z),(x,y+.95,z),.12,WOOD_DARK,sides=8)
    for x in [-1.75,1.75]:
        for z in [-22,-20]: tube('Mooring pile',(x,-2.6,z),(x,-.35,z),.15,WOOD_LIGHT,sides=8)
    for x,z in [(2.2,-19.8),(-2.2,-20.2)]:
        box('Dock fishing crate',(x,-.8,z),(.78,.60,.73),SAND)
        for j in range(4): box('Crate timber edge',(x,-1.05+j*.17,z-.38),(.78,.08,.04),WOOD_DARK)
    return finish('IslandDetails')

ground=island();shell=house();details=scenery()
total=sum(r['triangles'] for r in REPORT)
assert REPORT[0]['triangles']<14000 and total<65000,(REPORT,total)
assert len(MODELS)==3 and all(len(o.data.color_attributes['Color'].data)>0 for o in MODELS)
sun=bpy.data.lights.new('LGT-warm afternoon','SUN');sun.energy=2.0;sun.angle=.17
sun.color=(1,.90,.74);sun_obj=bpy.data.objects.new('LGT-warm afternoon',sun);scene.collection.objects.link(sun_obj)
sun_obj.rotation_euler=(math.radians(28),math.radians(-16),math.radians(-32))
for label,pos,energy,size in [('Sun',(-28,-40,55),1700,22),('Sky',(30,-15,40),900,35)]:
    light=bpy.data.lights.new('LGT-'+label,'AREA');light.energy=energy;light.shape='DISK';light.size=size
    obj=bpy.data.objects.new('LGT-'+label,light);scene.collection.objects.link(obj);obj.location=pos
    obj.rotation_euler=(Vector((-5,3,4))-obj.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('CAM-V027 front',bpy.data.cameras.new('CAM-V027 front'));scene.collection.objects.link(camera);scene.camera=camera
camera.data.type='ORTHO';camera.data.ortho_scale=70
sea=bpy.data.materials.new('MAT-preview sea');sea.use_nodes=True
s=next(n for n in sea.node_tree.nodes if n.type=='BSDF_PRINCIPLED');s.inputs['Base Color'].default_value=(.11,.49,.47,1);s.inputs['Roughness'].default_value=.40
bpy.ops.mesh.primitive_plane_add(size=450,location=(0,0,-2.20));water=bpy.context.object;water.name='GEO-preview sea only';water.data.materials.append(sea)
views=[('front',(36,-64,42),70),('side',(66,-2,37),68),('back',(-26,62,39),70),('top',(-6,4,90),68)]
for name,position,span in views[:1] if BLOCKOUT else views:
    camera.location=position;target=Vector((-6,4,5));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=span
    scene.render.filepath=str(PREVIEW/('island-blockout.png' if BLOCKOUT else 'island-'+name+'.png'));bpy.ops.render.render(write_still=True)
camera.location=views[0][1];camera.rotation_euler=(Vector((-6,4,5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=70
if not BLOCKOUT:
    bpy.ops.object.select_all(action='DESELECT')
    for obj in MODELS: obj.select_set(True)
    bpy.context.view_layer.objects.active=shell
    bpy.ops.export_scene.gltf(filepath=str(ART/'Coastal-home-v027.glb'),export_format='GLB',use_selection=True,export_animations=False,export_apply=True,export_yup=True)
    assert (ART/'Coastal-home-v027.glb').stat().st_size<15000000
    bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Coastal-home-v027.blend'))
    (ART/'model-report.json').write_text(json.dumps({'models':REPORT,'total_triangles':total,'views':[v[0] for v in views],'lighthouse_base_local_y':height(-14,10)+.10,'ground_height_house_origin':height(0,0),'single_image_assumptions':True},indent=2))
print('V027 model PASS',json.dumps(REPORT),'total',total,'blockout',BLOCKOUT)
