"""V035 original Blender kit. Load in an owned scene, build one named asset per call."""
from pathlib import Path
import bpy
import bmesh
import math
import random
import json
import hashlib
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV035'
SOURCE = ROOT / 'ArtSource/V035'
SCENE = 'V035 coastal craftsmanship'
PARTS = []
MODELS = {}
REPORT = []
RNG = random.Random(35)

def U(p):
    return Vector((p[0], -p[2], p[1]))

def setup():
    assert SCENE not in bpy.data.scenes, 'Use the existing V035 session instead of recreating it.'
    scene = bpy.data.scenes.new(SCENE)
    bpy.context.window.scene = scene
    scene.unit_settings.system = 'METRIC'
    scene.world = bpy.data.worlds.new('WGT-V035 soft coastal sky')
    scene.world.use_nodes = True
    bg = next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (.38, .49, .64, 1)
    bg.inputs['Strength'].default_value = .6
    scene.view_settings.view_transform = 'AgX'
    mat = bpy.data.materials.new('MAT-V035 coastal craftsmanship')
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    for filename, socket in [('CoastalAlbedo','Base Color'), ('CoastalMetalRough',None), ('CoastalNormal',None)]:
        image = bpy.data.images.load(str(OUT / (filename + '.png')))
        if filename != 'CoastalAlbedo': image.colorspace_settings.name = 'Non-Color'
        image.pack()
        tex = nodes.new('ShaderNodeTexImage'); tex.image = image
        if socket: links.new(tex.outputs['Color'], bsdf.inputs[socket])
        elif filename == 'CoastalMetalRough':
            sep = nodes.new('ShaderNodeSeparateColor'); links.new(tex.outputs['Color'], sep.inputs['Color'])
            links.new(sep.outputs['Blue'], bsdf.inputs['Metallic']); links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
        else:
            n = nodes.new('ShaderNodeNormalMap'); n.inputs['Strength'].default_value = .5
            links.new(tex.outputs['Color'], n.inputs['Color']); links.new(n.outputs['Normal'], bsdf.inputs['Normal'])
    mat.use_backface_culling = False
    return scene

def add_native(coords, faces, tile=0, smooth=False):
    offset = len(VERTS)
    VERTS.extend(tuple(v) for v in coords)
    lo = [min(v[a] for v in coords) for a in range(3)]
    hi = [max(v[a] for v in coords) for a in range(3)]
    for face in faces:
        FACES.append(tuple(offset+i for i in face)); TILES.append(tile); SMOOTH.append(smooth)
        v0,v1,v2 = (Vector(coords[j]) for j in face[:3])
        normal = (v1-v0).cross(v2-v0)
        axis = max(range(3), key=lambda a:abs(normal[a])); axes = [a for a in range(3) if a != axis]
        UVS.append([((tile%4+.035+(coords[j][axes[0]]-lo[axes[0]])/max(.0001,hi[axes[0]]-lo[axes[0]])*.93)/4,
                     1-(tile//4+.035+(coords[j][axes[1]]-lo[axes[1]])/max(.0001,hi[axes[1]]-lo[axes[1]])*.93)/4) for j in face])

def mesh(coords, faces, tile=0, smooth=False):
    add_native([U(p) for p in coords], faces, tile, smooth)

def box(p, size, tile=0, bevel=.025, yaw=0, segments=2):
    bm = bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co *= Vector((size[0],size[2],size[1]))
    if bevel:
        bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(size)*.18),segments=segments,affect='EDGES')
    bm.verts.ensure_lookup_table(); rot=Matrix.Rotation(math.radians(yaw),3,'Z')
    add_native([rot@v.co+U(p) for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces],tile)
    bm.free()

def profile(p, rows, tile=0, n=24):
    vs=[]; fs=[]
    for height,radius in rows:
        vs += [(p[0]+radius*math.cos(k*math.tau/n),p[1]+height,p[2]+radius*math.sin(k*math.tau/n)) for k in range(n)]
    for j in range(len(rows)-1):
        for k in range(n): fs.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
    fs += [tuple(reversed(range(n))),tuple((len(rows)-1)*n+k for k in range(n))]
    mesh(vs,fs,tile,True)

def tube(points, radius=.03, tile=4, n=7, taper=1):
    pts=[U(p) for p in points];vs=[];fs=[]
    for j,p in enumerate(pts):
        tangent=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized()
        side=tangent.cross(Vector((0,0,1)))
        if side.length<.05:side=tangent.cross(Vector((0,1,0)))
        side.normalize();up=tangent.cross(side).normalized();r=radius*(1+(taper-1)*j/(len(pts)-1))
        vs += [p+(side*math.cos(k*math.tau/n)+up*math.sin(k*math.tau/n))*r for k in range(n)]
    for j in range(len(pts)-1):
        for k in range(n):fs.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
    fs += [tuple(reversed(range(n))),tuple((len(pts)-1)*n+k for k in range(n))]
    add_native(vs,fs,tile,True)

def ring(p, r, thickness=.025, tile=4, plane='flat', start=0, end=math.tau, n=64):
    points=[]
    for k in range(n+1):
        a=start+(end-start)*k/n
        delta=(math.cos(a)*r,math.sin(a)*r,0) if plane=='upright' else (math.cos(a)*r,0,math.sin(a)*r)
        points.append(tuple(p[i]+delta[i] for i in range(3)))
    tube(points,thickness,tile,6)

def leaf(a,b,width,tile=8):
    a,b=U(a),U(b);axis=b-a;side=axis.cross(Vector((0,0,1))).normalized()
    if side.length<.1:side=Vector((1,0,0))
    vs=[]
    for i in range(5):
        t=i/4;center=a+axis*t+Vector((0,0,math.sin(t*math.pi)*width*.35));w=math.sin(t*math.pi)*width
        vs.extend([center-side*w,center+Vector((0,0,width*.12)),center+side*w])
    fs=[]
    for i in range(4):
        for j in range(2):fs.append((i*3+j,i*3+j+1,(i+1)*3+j+1,(i+1)*3+j))
    add_native(vs,fs,tile,True)

def rock(p, size, tile=1, subdivisions=2):
    bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=subdivisions,radius=1)
    for v in bm.verts:
        factor=1+.10*math.sin(v.co.x*9+v.co.y*4)+.07*math.cos(v.co.z*14)
        v.co *= Vector((size[0],size[2],size[1]))*factor
    bm.verts.ensure_lookup_table();add_native([v.co+U(p) for v in bm.verts],[tuple(v.index for v in f.verts) for f in bm.faces],tile,True);bm.free()

def column(p, h=3.3, r=.30, tile=0):
    profile(p,[(0,r*1.6),(.10,r*1.6),(.16,r*1.35),(.23,r*1.25),(.29,r),(.42,r*.94),(h-.45,r*.83),(h-.35,r),(h-.28,r*1.2),(h-.19,r*1.5),(h-.05,r*1.5),(h,r*1.65)],tile,28)
    for k in range(12):
        a=k*math.tau/12
        tube([(p[0]+math.cos(a)*r*.97,p[1]+.43,p[2]+math.sin(a)*r*.97),(p[0]+math.cos(a)*r*.86,p[1]+h-.45,p[2]+math.sin(a)*r*.86)],.011,1,5)
    profile((p[0],p[1]+h-.30,p[2]),[(0,r*1.24),(.045,r*1.24)],4)

def arch(p, width=3.0, spring=2.3, thick=.35, depth=.65, tile=0):
    r=width*.5
    for k in range(17):
        a=k*math.pi/17+.005;b=(k+1)*math.pi/17-.005;vs=[]
        for z in [-depth*.5,depth*.5]:
            for rad,ang in [(r,a),(r,b),(r+thick,b),(r+thick,a)]:vs.append((p[0]+math.cos(ang)*rad,p[1]+spring+math.sin(ang)*rad,p[2]+z))
        mesh(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],tile)
    for side in [-1,1]:
        x=p[0]+side*(r+thick*.5)
        for row in range(5):box((x,p[1]+spring*(row+.5)/5,p[2]),(thick,spring/5-.018,depth),tile,.018)
    for rr,dd in [(r-.035,-depth*.56),(r+thick+.06,-depth*.54)]:
        ring((p[0],p[1]+spring,p[2]+dd),rr,.037,tile,'upright',0,math.pi,48)
        for side in [-1,1]:tube([(p[0]+side*rr,p[1]+.08,p[2]+dd),(p[0]+side*rr,p[1]+spring,p[2]+dd)],.037,tile)

def star(p,r=.3,tile=4,points=8):
    vs=[p]
    for i in range(points*2):
        a=i*math.pi/points;rr=r if i%2==0 else r*.28
        vs.append((p[0]+math.sin(a)*rr,p[1],p[2]+math.cos(a)*rr))
    mesh(vs,[(0,1+i,1+(i+1)%(points*2)) for i in range(points*2)],tile)

def banner(p,width=.7,height=1.8):
    vs=[];fs=[]
    for j in range(9):
        t=j/8
        for k in range(7):
            s=k/6;vs.append((p[0]+(s-.5)*width,p[1]-t*height+(.17*abs(s-.5) if j==8 else 0),p[2]+math.sin(s*math.tau*2+t*2)*.04))
    for j in range(8):
        for k in range(6):a=j*7+k;fs.append((a,a+1,a+8,a+7))
    mesh(vs,fs,6,True)
    tube([(p[0]-width*.6,p[1]+.02,p[2]),(p[0]+width*.6,p[1]+.02,p[2])],.027,4)
    ring((p[0],p[1]-height*.36,p[2]-.047),width*.22,.012,4,'upright',n=32)
    for side in [-1,1]:tube([(p[0]+side*width*.47,p[1]-.04,p[2]-.04),(p[0]+side*width*.47,p[1]-height+.08,p[2]-.04)],.008,4)

def chime(p,scale=1):
    profile((p[0],p[1],p[2]),[(0,.24*scale),(.035*scale,.25*scale),(.08*scale,.19*scale)],3)
    for k in range(5):
        a=k*math.tau/5;xx=p[0]+math.cos(a)*.19*scale;zz=p[2]+math.sin(a)*.19*scale;length=(.42+.055*k)*scale
        tube([(xx,p[1]-.04,zz),(xx,p[1]-.14*scale,zz)],.005*scale,13)
        profile((xx,p[1]-.14*scale-length,zz),[(0,.026*scale),(length,.026*scale)],4,14)
    tube([(p[0],p[1],p[2]),(p[0],p[1]-.7*scale,p[2])],.007*scale,13)
    rock((p[0],p[1]-.72*scale,p[2]),(.07*scale,.12*scale,.035*scale),4)

def fern(p=(0,0,0),s=1):
    for k in range(9):
        a=k*2.399963;length=s*(.65+(k%3)*.12);dx,dz=math.cos(a),math.sin(a)
        pts=[]
        for j in range(10):
            t=j/9;pts.append((p[0]+dx*t*length,p[1]+math.sin(t*math.pi*.8)*length*.55,p[2]+dz*t*length))
        tube(pts,.008*s,8,5,.25)
        for j in range(1,9):
            t=j/9;point=pts[j];w=math.sin(t*math.pi)*.22*s
            for side in [-1,1]:leaf(point,(point[0]-dz*w*side+dx*.10*s,point[1]+.04*s,point[2]+dx*w*side+dz*.10*s),w*.19,9 if j%3==0 else 8)

def meadow(dune=False):
    for k in range(32):
        a=k*2.399963;r=math.sqrt((k+.5)/32)*.67;px,pz=math.cos(a)*r,math.sin(a)*r
        for j in range(8):
            aa=a+j*1.9;h=.18+RNG.random()*.30;lean=.04+RNG.random()*.07;vs=[]
            for row in range(5):
                t=row/4;xx=px+math.cos(aa)*lean*t*t;zz=pz+math.sin(aa)*lean*t*t;yy=h*math.sin(t*math.pi*.5);w=math.sin(t*math.pi)*.0045
                vs.extend([(xx-math.sin(aa)*w,yy,zz+math.cos(aa)*w),(xx+math.sin(aa)*w,yy,zz-math.cos(aa)*w)])
            mesh(vs,[(row*2,row*2+1,row*2+3,row*2+2) for row in range(4)],9 if j%3==0 else 8,True)
        if k%4==0:
            h=.32+RNG.random()*.18;tube([(px,0,pz),(px+.025,h,pz)],.0015,9,5)
            if dune:
                profile((px+.025,h-.08,pz),[(0,.004),(.02,.011),(.04,.013),(.06,.012),(.08,.009),(.11,.003)],13,8)
            else:
                rock((px+.025,h,pz),(.009,.007,.009),12)
                for j in range(6):
                    aa=j*math.tau/6;leaf((px+.025,h,pz),(px+.025+math.cos(aa)*.016,h-.006,pz+math.sin(aa)*.016),.006,11 if k%3 else 10)

def tree():
    flow([(0,0,0),(.15,1,0),(-.08,2,.12),(.25,3.2,.05),(.05,4.3,0)],.28,3,12,.27)
    for k in range(8):
        a=k*math.tau/8;dx,dz=math.cos(a),math.sin(a)
        flow([(.07,.7,0),(dx*.55,.35,dz*.55),(dx*1.5,.10,dz*1.5),(dx*2.1,-.12,dz*2.1)],.15,3,8,.18)
        h=2.4+k%3*.4;bx,bz=dx*1.6,dz*1.6
        flow([(.10,h,0),(bx*.45,h+.6,bz*.45),(bx,h+.9,bz),(bx*1.35,h+1.1,bz*1.35)],.12,3,8,.12)
        for j in range(48):
            aa=j*2.399963+k;r=.18+math.sqrt((j+.5)/48)*1.18
            xx=bx+math.cos(aa)*r;zz=bz+math.sin(aa)*r;yy=h+1.0+.36*math.cos(r)+math.sin(j*1.3)*.45+RNG.random()*.45
            leaf((xx,yy,zz),(xx+math.cos(aa)*.34,yy+.18*math.sin(j*2.4),zz+math.sin(aa)*.34),.075,8 if j%3 else 9)

def portal():
    arch((0,0,0),2.5,2.35,.43,.85)
    for x in [-1.72,1.72]:
        column((x,0,-.15),2.5,.24)
        box((x,2.64,0),(.77,.18,1.08),0)
        banner((x,2.42,-.61),.42,1.7)
    for j in range(3):box((0,.09*j-.21,.28-j*.14),(4.25-.17*j,.16,1.8-.30*j),0)
    for j in range(6):
        a=(j+.5)*math.pi/6;rock((math.cos(a)*1.83,2.35+math.sin(a)*1.83,-.55),(.045,.05,.025),4)
    ring((0,3.35,-.55),.30,.03,4,'upright',n=40)
    tube([(-.24,3.35,-.57),(0,3.12,-.57),(.24,3.35,-.57)],.02,4)
    fern((-1.85,.05,-.65),.65);fern((1.95,.05,.35),.8)

def pavilion():
    profile((0,-.25,0),[(0,4.1),(.20,4.1),(.25,3.9),(.40,3.9),(.45,3.65)],0,64)
    for rr in [1.05,2.4,3.4]:ring((0,.208,0),rr,.016,4)
    star((0,.208,0),1.0)
    for k in range(8):
        a=k*math.tau/8;x,z=math.cos(a)*3,math.sin(a)*3
        column((x,.2,z),3.8,.30)
        if k%2==0:banner((x,3.6,z-.38),.72,2.0)
        b=(k+1)*math.tau/8;xx,zz=math.cos(b)*3,math.sin(b)*3;start=len(VERTS)
        arch((0,.2,0),2.08,2.7,.16,.27)
        rot=Matrix.Rotation(math.atan2(-(zz-z),xx-x),3,'Z');offset=U(((x+xx)*.5,0,(z+zz)*.5))
        for j in range(start,len(VERTS)):VERTS[j]=tuple(rot@Vector(VERTS[j])+offset)
    profile((0,3.98,0),[(0,3.56),(.10,3.7),(.24,3.7),(.36,3.53),(.48,3.45)],0,64)
    for k in range(16):
        a=k*math.tau/16
        tube([(math.cos(a)*3.45*math.cos(t*math.pi*.49),4.49+2.08*math.sin(t*math.pi*.49),math.sin(a)*3.45*math.cos(t*math.pi*.49)) for t in [j/12 for j in range(13)]],.035,4)
    vs=[];fs=[]
    for j in range(9):
        t=j/8;rad=3.4*math.cos(t*math.pi*.49);h=4.48+2.05*math.sin(t*math.pi*.49)
        for k in range(64):vs.append((math.cos(k*math.tau/64)*rad,h,math.sin(k*math.tau/64)*rad))
    for j in range(8):
        for k in range(64):fs.append((j*64+k,j*64+(k+1)%64,(j+1)*64+(k+1)%64,(j+1)*64+k))
    mesh(vs,fs,5,True)
    profile((0,6.52,0),[(0,.18),(.15,.24),(.24,.12),(.65,.025)],4)
    for k in range(5):chime(((k-2)*.38,3.74,0),1.2)

def observatory():
    profile((0,-.3,0),[(0,4.4),(.22,4.4),(.3,4.18),(.4,4.18)],1,64)
    for rr in [1,2,3.3,4]:ring((0,.11,0),rr,.019,4)
    star((0,.12,0),2.5,4,16)
    arch((0,.1,2),4.5,2.6,.58,1.10,1)
    for x in [-3.3,3.3]:column((x,.1,1.8),4.1 if x<0 else 2.7,.39,1)
    ring((0,3.1,1.9),1.3,.04,4,'upright')
    ring((0,3.1,1.9),1.05,.036,4,'flat')
    rock((0,3.1,1.9),(.37,.37,.37),4)
    for k in range(14):
        a=k*2.399963;r=3+(k%3)*.38;rock((math.cos(a)*r,.20,math.sin(a)*r),(.35,.25,.27),1)
    fern((-2.9,.1,1.1),1.1)

def wreck():
    for side in [-1,1]:
        for row in range(13):
            for j in range(12):
                if (j+row*3)%17==0 or (row>6 and j%5==0):continue
                vs=[]
                for depth in [0,.11]:
                    for zz,rr in [(j+.025,row+.03),(j+.975,row+.03),(j+.975,row+.97),(j+.025,row+.97)]:
                        z=(zz-6)*.82;f=math.sqrt(max(.03,1-(z/5.6)**2));h=rr*.23
                        xx=side*(.25+math.sin((rr+.4)/14*math.pi*.5)*2.15)*f+side*depth
                        vs.append((xx,h+abs(z)*.13,z))
                mesh(vs,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],3)
    for j in range(11):
        z=(j-5)*.86;f=math.sqrt(max(.1,1-(z/5.6)**2))
        tube([(-2.25*f,3+abs(z)*.13,z),(-1.78*f,1.4+abs(z)*.13,z),(0,.10+abs(z)*.13,z),(1.78*f,1.4+abs(z)*.13,z),(2.25*f,3+abs(z)*.13,z)],.10,3,8)
    for j in range(15):
        z=(j-7)*.57;w=math.sqrt(max(.03,1-(z/5.6)**2))*3.6
        if j%6:box((0,1.27+abs(z)*.13,z),(w,.12,.54),3,.015)
    tube([(0,.3,0),(.20,3.5,-.2),(.72,7.8,-.6)],.18,3,12,.55)
    tube([(-2.8,5.5,-.4),(2.8,5.7,-.4)],.11,3,10)
    for x in [-2,2]:
        for z in [-3,3]:tube([(x,2.6,z),(.55,6.8,-.5)],.018,13,6)
    for j in range(15):
        z=(j-7)*.57
        for side in [-1,1]:rock((side*(1.0+.5*math.cos(z)),1.7,z),(.025,.025,.018),4)

def plinth(kind):
    profile((0,0,0),[(0,.70),(.10,.70),(.16,.58),(.22,.53),(.70,.43),(.77,.54),(.84,.56)],[0,3,4,14][kind],32)
    for rr in [.45,.57]:ring((0,.855,0),rr,.014,7 if kind==1 else 4)
    for k in range(8):
        a=k*math.tau/8;tube([(math.cos(a)*.48,.26,math.sin(a)*.48),(math.cos(a)*.43,.65,math.sin(a)*.43)],.017,3 if kind==1 else 4)
    if kind==0:
        arch((0,.84,0),.68,.55,.10,.18,4);chime((0,1.66,0),.65)
    elif kind==1:
        profile((0,.84,0),[(0,.10),(.35,.08)],4)
    elif kind==2:
        ring((0,1.2,0),.33,.045,4,'upright',n=32)
        for k in range(6):
            a=k*math.tau/6;tube([(0,1.2,0),(math.cos(a)*.33,1.2+math.sin(a)*.33,0)],.018,4)
    else:
        profile((0,.84,0),[(0,.44),(.07,.44)],15,32)
        for rr in [.19,.35]:ring((0,.92,0),rr,.014,4)
        for tilt in [0,55,110]:
            start=len(VERTS);ring((0,0,0),.25,.015,4,'upright',n=40)
            rot=Matrix.Rotation(math.radians(tilt),3,'Y');offset=U((0,1.23,0))
            for j in range(start,len(VERTS)):VERTS[j]=tuple(rot@Vector(VERTS[j])+offset)
        rock((0,1.23,0),(.085,.085,.085),4)

def move_parts(start, p, yaw=0):
    rot=Matrix.Rotation(math.radians(yaw),3,'Z');offset=U(p)
    for i in range(start,len(VERTS)):VERTS[i]=tuple(rot@Vector(VERTS[i])+offset)

def flow(points,radius=.15,tile=3,n=8,taper=1):
    pts=[Vector(p) for p in points];result=[]
    for i in range(len(pts)-1):
        a,b,c,d=pts[max(0,i-1)],pts[i],pts[i+1],pts[min(len(pts)-1,i+2)]
        for j in range(3):
            t=j/3;result.append(tuple(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)))
    result.append(tuple(pts[-1]));tube(result,radius,tile,n,taper)




def lantern(p):
    x,y,z=p
    profile((x+.30,0,z),[(0,.14),(.10,.14),(.18,.065),(y+.88,.045)],4,16)
    flow([(x+.30,y+.86,z),(x+.30,y+1.03,z),(x,y+1.03,z),(x,y+.86,z)],.020,4,7)
    tube([(x,y+.9,z),(x,y+.55,z)],.015,4)
    for h in [0,.53]:profile((x,y+h,z),[(0,.18),(.045,.18)],4,16)
    for k in range(6):
        a=k*math.tau/6;tube([(x+math.cos(a)*.15,y+.04,z+math.sin(a)*.15),(x+math.cos(a)*.15,y+.54,z+math.sin(a)*.15)],.012,4,5)
    rock((x,y+.26,z),(.09,.16,.09),10)






def grove_portal():
    arch((0,0,0),2.5,2.3,.34,.72,1)
    for side in [-1,1]:
        flow([(side*1.85,-.08,.1),(side*1.65,1.3,.2),(side*1.8,2.9,.15),(side*1.2,3.9,.1),(0,4.2,.06)],.23,3,9,.35)
        fern((side*1.9,0,-.5),1.0)
        for j in range(8):
            a=j*2.399963;leaf((side*1.8,1+j*.35,.12),(side*1.8+math.cos(a)*.35,1.15+j*.35,.12+math.sin(a)*.35),.10,9)
    leaf((-.3,3.55,-.5),(.3,4.15,-.5),.25,4)
    for j in range(3):box((0,-.1+j*.06,-j*.14),(4.0,.12,1.4),1)

def wreck_portal():
    arch((0,0,0),2.5,2.3,.36,.65,3)
    for side in [-1,1]:
        box((side*1.65,1.4,0),(.30,2.8,.90),3)
        for h in [.35,1.4,2.55]:box((side*1.65,h,-.47),(.38,.12,.045),4,.008)
        tube([(side*2,0,0),(side*2,3.2,0),(side*1,4.0,0)],.065,4,9)
    ring((0,3.45,-.48),.31,.04,4,'upright',n=40)
    for j in range(6):
        a=j*math.tau/6;tube([(0,3.45,-.48),(math.cos(a)*.31,3.45+math.sin(a)*.31,-.48)],.018,4)
    for j in range(7):box(((j-3)*.46,.035,0),(.44,.07,1.25),3,.012)

def star_portal():
    arch((0,0,0),2.5,2.3,.38,.75,1)
    for side in [-1,1]:
        column((side*1.83,0,0),2.85 if side<0 else 3.15,.25,1)
        profile((side*1.83,2.9 if side<0 else 3.2,0),[(0,.13),(.15,.19),(.30,.06)],4)
        rock((side*1.9,.15,-.5),(.42,.30,.40),1)
    for tilt in [0,65]:
        start=len(VERTS);ring((0,0,0),.34,.022,4,'upright',n=40)
        rot=Matrix.Rotation(math.radians(tilt),3,'Y');offset=U((0,3.65,-.5))
        for j in range(start,len(VERTS)):VERTS[j]=tuple(rot@Vector(VERTS[j])+offset)
    rock((0,3.65,-.5),(.08,.08,.08),4)
    box((0,.02,0),(4.0,.10,1.4),14)

def tray():
    # Uses its own original atlas; the runtime keeps the V034 tool material.
    profile((0,0,0),[(0,.204),(.006,.208),(.018,.208),(.028,.198),(.032,.192),(.008,.185)],4,64)
    # Scale the circular tray into an oval without changing its level upper lip.
    for i in range(len(VERTS)):VERTS[i]=(VERTS[i][0],VERTS[i][1]*.72,VERTS[i][2])

def crate():
    for side in [-1,1]:
        for j in range(6):box(((j-2.5)*.213,.43,side*.38),(.205,.82,.06),3,.012)
        for j in range(4):box((side*.62,.43,(j-1.5)*.19),(.06,.82,.18),3,.012)
        box((0,.82,side*.30),(1.32,.07,.06),4,.008)
        box((0,.08,side*.30),(1.32,.07,.06),4,.008)
        for x in [-.45,.45]:
            box((x,.43,side*.42),(.07,.82,.035),4,.008)
            for y in [.1,.76]:rock((x,y,side*.447),(.018,.018,.009),4)
    for j in range(7):box(((j-3)*.18,.86,0),(.175,.07,.80),3,.012)

def bridge():
    for j in range(15):box((0,0,(j-7)*.235),(3,.20,.228),3,.012)
    for x in [-1.32,1.32]:
        box((x,-.14,0),(.14,.18,3.58),3)
        for j in range(15):rock((x,.11,(j-7)*.235),(.022,.012,.022),4)

def coastal_pine():
    flow([(0,0,0),(.05,1,0),(.35,2.2,.08),(.72,3.3,.12),(1.0,4.5,.2)],.17,3,10,.22)
    for k in range(14):
        a=k*2.399963;h=1.6+k*.19;length=1.3-k*.048;dx,dz=math.cos(a),math.sin(a)
        end=(.2+dx*length,h+.30,dz*length)
        flow([(.2,h,0),(.2+dx*length*.6,h-.05,dz*length*.6),end],.055,3,7,.17)
        for j in range(28):
            aa=j*2.399963;rr=.65*math.sqrt((j+.5)/28)
            p=(end[0]+math.cos(aa)*rr,end[1]+RNG.uniform(-.18,.25),end[2]+math.sin(aa)*rr)
            for n in range(4):
                angle=aa+n*1.7;tip=(p[0]+math.cos(angle)*.34,p[1]+.10,p[2]+math.sin(angle)*.34)
                mesh([(p[0]-.06,p[1],p[2]),(p[0]+.06,p[1],p[2]),tip],[(0,1,2)],8 if j%3 else 9)

def dune_palm():
    flow([(0,0,0),(.12,1.2,0),(.4,2.6,.15),(.72,4.25,.25)],.16,3,11,.6)
    for k in range(11):
        a=k*2.399963;dx,dz=math.cos(a),math.sin(a);length=1.9+k%3*.15
        points=[]
        for j in range(15):
            t=j/14;points.append((.72+dx*length*t,4.25+math.sin(t*math.pi)*.55-.65*t*t,.25+dz*length*t))
        tube(points,.025,9,6,.18)
        for j in range(1,14):
            t=j/14;p=points[j];w=math.sin(t*math.pi)*.48
            for side in [-1,1]:leaf(p,(p[0]-dz*w*side+dx*.13,p[1]-.18,p[2]+dx*w*side+dz*.13),.022,8 if k%2 else 9)
    for k in range(3):rock((.72+math.cos(k*2.1)*.14,4.14,.25+math.sin(k*2.1)*.14),(.10,.13,.10),3)

def sieve_shell():
    n=28;rings=8;vs=[(0,.003,-.009)];fs=[]
    for row in range(1,rings+1):
        t=row/rings
        for j in range(n+1):
            a=math.pi*(.10+.80*j/n);r=t*.025*(1+.035*math.cos(a*18))
            vs.append((math.cos(a)*r,.003+math.sin(t*math.pi*.9)*.009+math.cos(a*18)*.0008*math.sin(t*math.pi),math.sin(a)*r-.009))
    for j in range(n):fs.append((0,1+j,2+j))
    for row in range(rings-1):
        for j in range(n):a=1+row*(n+1)+j;fs.append((a,a+1,a+n+2,a+n+1))
    count=len(vs);vs += [(x,y-.003,z) for x,y,z in vs];fs += [tuple(i+count for i in reversed(f)) for f in list(fs)]
    boundary=[0]+[1+row*(n+1) for row in range(rings)]+[1+(rings-1)*(n+1)+j for j in range(1,n+1)]+[1+row*(n+1)+n for row in reversed(range(rings-1))]
    for j,a in enumerate(boundary):b=boundary[(j+1)%len(boundary)];fs.append((a,b,b+count,a+count))
    mesh(vs,fs,10,True)
    for j in range(1,n,3):
        a=math.pi*(.10+.80*j/n);tube([(math.cos(a)*t*.025,.004+math.sin(t*math.pi*.9)*.009,math.sin(a)*t*.025-.009) for t in [.15,.3,.5,.7,.9,1]],.00032,10,5)

def build(name):
    global VERTS,FACES,UVS,TILES,SMOOTH
    assert name not in MODELS, 'Asset already built; inspect before replacing.'
    VERTS=[];FACES=[];UVS=[];TILES=[];SMOOTH=[]
    RNG.seed(35)
    if name in globals().get('EXTRA_BUILDERS',{}):EXTRA_BUILDERS[name]()
    elif name=='TidePortal':portal()
    elif name=='GrovePortal':grove_portal()
    elif name=='WreckPortal':wreck_portal()
    elif name=='StarPortal':star_portal()
    elif name=='WindPavilion':pavilion()
    elif name=='MangroveTree':tree()
    elif name=='CoastalPine':coastal_pine()
    elif name=='DunePalm':dune_palm()
    elif name=='FernClump':fern()
    elif name=='MeadowPatch':meadow()
    elif name=='DuneGrass':meadow(True)
    elif name=='ShoreRock':
        for p,s in [((0,.35,0),(1,.6,.7)),((.8,.15,.3),(.6,.38,.50)),((-.7,.1,-.35),(.50,.30,.4))]:rock(p,s)
    elif name=='StarObservatory':observatory()
    elif name=='WreckHull':wreck()
    elif name.startswith('PuzzlePlinth'):plinth(int(name[-1]))
    elif name=='SieveCatchTray':tray()
    elif name=='SieveShell':sieve_shell()
    elif name=='CargoCrate':crate()
    elif name=='HydraulicBridge':bridge()
    elif name=='MirrorHead':
        ring((0,0,0),.37,.035,4,'upright',n=48);box((0,0,.015),(.52,.52,.035),15,.02)
    else:raise ValueError(name)
    data=bpy.data.meshes.new('GEO-'+name);data.from_pydata(VERTS,[],FACES);data.update()
    layer=data.uv_layers.new(name='UVMap')
    for face,uvs,smooth in zip(data.polygons,UVS,SMOOTH):
        face.use_smooth=smooth
        for li,uv in zip(face.loop_indices,uvs):layer.data[li].uv=uv
    bm=bmesh.new();bm.from_mesh(data)
    if name.startswith('LevelTerrain') or name=='HomeLand':bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    if name=='HomeLand':
        bm.normal_update()
        for face in bm.faces:
            if face.normal.z<0:face.normal_flip()
    bm.to_mesh(data);bm.free();data.update()
    obj=bpy.data.objects.new('GEO-'+name,data);bpy.context.scene.collection.objects.link(obj)
    data.materials.append(bpy.data.materials['MAT-V035 coastal craftsmanship'])
    leaf_tiles=(8,9,10,11,12) if name in ('SanctuaryWind','MeadowPatch','FlowerPatch') else (8,9,11)
    if name.startswith(('Sanctuary','WindBridge')) or name in ('RootBridge','Galleon','Windmill','AncientTree','ObservatoryExterior'):leaf_tiles=tuple(set(leaf_tiles+(13,)))
    if name=='Galleon':leaf_tiles=tuple(set(leaf_tiles+(10,)))
    mixed=any(tile in leaf_tiles for tile in TILES)
    if mixed:
        foliage=bpy.data.materials.get('MAT-V035 living foliage')
        if foliage is None:
            foliage=bpy.data.materials['MAT-V035 coastal craftsmanship'].copy();foliage.name='MAT-V035 living foliage';foliage.use_backface_culling=False
        data.materials.append(foliage)
        for face,tile in zip(data.polygons,TILES):face.material_index=1 if tile in leaf_tiles else 0
    colors=data.color_attributes.new(name='V035 wind weights',type='BYTE_COLOR',domain='CORNER')
    for face,tile in zip(data.polygons,TILES):
        color=(1,1,1,1 if tile in (8,9,10,11) else 0)
        for li in face.loop_indices:colors.data[li].color=color
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    for key,point in globals().get('MARKERS',{}).get(name,{}).items():
        marker=bpy.data.objects.new('V35_'+key,None);bpy.context.scene.collection.objects.link(marker);marker.parent=obj;marker.location=U(point);marker.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,path_mode='STRIP')
    triangles=sum(len(f.vertices)-2 for f in data.polygons)
    assert 0<triangles<(140000 if name=='SanctuaryDrydock' else 110000),(name,triangles)
    assert all(math.isfinite(v) for vertex in data.vertices for v in vertex.co)
    assert all(0<=v<=1 for loop in data.uv_layers.active.data for v in loop.uv)
    MODELS[name]=obj;obj.hide_render=True
    REPORT.append({'name':name,'triangles':triangles,'vertices':len(data.vertices),'fbxSha256':hashlib.sha256((OUT/(name+'.fbx')).read_bytes()).hexdigest()})
    (SOURCE/'export-validation.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
    (SOURCE/'level-marker-manifest.json').write_text(json.dumps(globals().get('MARKERS',{}),indent=2),encoding='utf-8')
    print(json.dumps(REPORT[-1]))
    return obj

def preview(name,filename,inside=False):
    scene=bpy.context.scene
    for obj in MODELS.values():obj.hide_render=True
    model=MODELS[name];model.hide_render=False;model.location=(0,0,0)
    bpy.context.view_layer.update()
    for obj in list(scene.objects):
        if obj.name.startswith(('LGT-V035','CAM-V035')):bpy.data.objects.remove(obj,do_unlink=True)
    camera=bpy.data.objects.new('CAM-V035 review',bpy.data.cameras.new('CAM-V035 review'));scene.collection.objects.link(camera);scene.camera=camera
    camera.data.clip_start=.001
    if inside:
        camera.location=U((0,1.7,-11.8));target=U((0,2.0,6));camera.data.lens=21
    else:
        bounds=[model.matrix_world@Vector(p) for p in model.bound_box];height=max(v.z for v in bounds)-min(v.z for v in bounds);width=max(v.x for v in bounds)-min(v.x for v in bounds)
        span=max(height,width);camera.location=U((span*1.35,span*.85,-span*1.65));target=U((0,height*.46,0));camera.data.lens=46
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    for label,kind,point,energy,size,color in [('key','SUN',(5,9,-8),2.0,0,(1,.91,.76)),('fill','AREA',(-6,8,-5),1600,8,(.72,.84,1)),('rim','AREA',(3,8,8),2200,6,(1,.87,.67))]:
        data=bpy.data.lights.new('LGT-V035 '+label,kind);light=bpy.data.objects.new(data.name,data);scene.collection.objects.link(light);light.location=U(point);data.energy=energy;data.color=color
        if kind=='AREA':data.shape='DISK';data.size=size
        if kind=='SUN':data.angle=.08
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=1280;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(SOURCE/filename)
    assert scene.camera is not None
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'coastal-craftsmanship-v035.blend'))
    bpy.ops.render.render(write_still=True)
    print('Preview saved: '+scene.render.filepath)

def export_kit():
    bpy.ops.object.select_all(action='DESELECT')
    x=y=row_depth=0;rectangles=[]
    for name,obj in MODELS.items():
        points=[Vector(p) for p in obj.bound_box];low=Vector(tuple(min(p[a] for p in points) for a in range(3)));high=Vector(tuple(max(p[a] for p in points) for a in range(3)))
        width,depth=high.x-low.x,high.y-low.y
        if x>0 and x+width>128:x=0;y+=row_depth+5;row_depth=0
        obj.hide_render=False;obj.location=(x-low.x,y-low.y,0);obj.select_set(True)
        rectangles.append((name,x,y,x+width,y+depth));x+=width+5;row_depth=max(row_depth,depth)
        for child in obj.children:child.select_set(True)
    for i,a in enumerate(rectangles):
        for b in rectangles[i+1:]:assert a[3]<=b[1] or b[3]<=a[1] or a[4]<=b[2] or b[4]<=a[2],(a[0],b[0])
    (SOURCE/'gallery-layout.json').write_text(json.dumps(rectangles,indent=2),encoding='utf-8')
    bpy.ops.export_scene.gltf(filepath=str(SOURCE/'coastal-craftsmanship-kit.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=False,export_cameras=False,export_lights=False)
    markers={name:{child.name.split('.')[0][4:]:[child.location.x,child.location.z,-child.location.y] for child in obj.children if child.name.startswith('V35_')} for name,obj in MODELS.items()}
    (SOURCE/'level-marker-manifest.json').write_text(json.dumps(markers,indent=2),encoding='utf-8')
    bpy.context.view_layer.update()
    bounds=[o.matrix_world@Vector(p) for o in MODELS.values() for p in o.bound_box]
    low=Vector(tuple(min(v[a] for v in bounds) for a in range(3)));high=Vector(tuple(max(v[a] for v in bounds) for a in range(3)));center=(low+high)*.5;span=max(high-low)
    camera=bpy.context.scene.camera
    if camera:
        camera.location=center+Vector((span*.8,-span*.85,span*.85));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=42;camera.data.clip_end=2000
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'coastal-craftsmanship-v035.blend'))
    assert (SOURCE/'coastal-craftsmanship-kit.glb').stat().st_size>10000
    print('Exported kit: '+str(len(MODELS))+' original models; '+str(sum(x['triangles'] for x in REPORT))+' triangles.')

import importlib.util
_level_path=Path(__file__).with_name('v035_level_geometry.py')
_level_spec=importlib.util.spec_from_file_location('v035_level_geometry',_level_path)
_level_module=importlib.util.module_from_spec(_level_spec);_level_spec.loader.exec_module(_level_module);_level_module.install(globals())
_home_path=Path(__file__).with_name('v035_home_geometry.py')
_home_spec=importlib.util.spec_from_file_location('v035_home_geometry',_home_path)
_home_module=importlib.util.module_from_spec(_home_spec);_home_spec.loader.exec_module(_home_module);_home_module.install(globals())
