"""Authored V035 levels: route geometry, focal models and exported gameplay markers."""
import math
from mathutils import Vector, Matrix

MARKERS={}
STATE=None

def mark(model,name,p):
    MARKERS.setdefault(model,{})[name]=list(p)

def transform_from(start,p=(0,0,0),rotation=None,scale=1):
    rotation=rotation or Matrix.Identity(3)
    for i in range(start,len(STATE['VERTS'])):STATE['VERTS'][i]=tuple(rotation@(Vector(STATE['VERTS'][i])*scale)+U(p))

def stair(a,b,width=2.2):
    delta=Vector(b)-Vector(a);count=max(1,math.ceil(abs(delta.y)/.16));yaw=-math.degrees(math.atan2(delta.z,delta.x))
    for i in range(count):
        p=Vector(a)+delta*((i+.5)/count);p.y=Vector(a).y+delta.y*(i+1)/count-.09
        box(tuple(p),(max(.22,math.hypot(delta.x,delta.z)/count+.025),.18,width),0,.012,yaw)

def walkway(a,b,width=2,rope=True,open_mid=False):
    a,b=Vector(a),Vector(b);delta=b-a;flat=Vector((delta.x,0,delta.z));length=flat.length
    tangent=flat.normalized();side=Vector((-tangent.z,0,tangent.x));count=max(2,math.ceil(length/.25))
    for i in range(count):
        t=(i+.5)/count;p=a+delta*t
        box(tuple(p-Vector((0,.055,0))),(width,.11,length/count+.018),3,.012,math.degrees(math.atan2(tangent.x,tangent.z)))
        if i%4==0:
            for s in [-1,1]:
                pin=p+side*s*width*.40;rock(tuple(pin+Vector((0,.014,0))),(.014,.008,.014),4,subdivisions=1)
    inset=min(.30,1.4/length)
    for s in [-1,1]:
        pts=[]
        for i in range(9):
            t=inset+(1-2*inset)*i/8;p=a+delta*t+side*s*width*.52+Vector((0,.80-.12*math.sin(t*math.pi),0));pts.append(tuple(p))
        if rope:tube(pts,.022,13,7)
        for t in [inset,inset+(1-2*inset)*.25,.5,inset+(1-2*inset)*.75,1-inset]:
            if open_mid and abs(t-.5)*length<1.2:continue
            p=a+delta*t+side*s*width*.52
            profile(tuple(p-Vector((0,.14,0))),[(0,.065),(.94,.055),(1.0,.07)],3,10)
            if not rope:box(tuple(p+Vector((0,.85,0))),(width*.03,.08,.35),3)

def cliff_islet(p,rx,rz,depth=7,grass=False,grounded=False):
    cx,top,cz=p;n=64;rows=[(-depth,.68),(-depth*.88,.79),(-depth*.70,.88),(-depth*.54,.85),(-depth*.35,.95),(-depth*.12,.96),(-.32,1),(-.05,1.01)]
    if grounded:rows=[(-depth,1.65),(-depth*.65,1.45),(-depth*.28,1.17),(-.32,1.03),(-.05,1.01)]
    vs=[];fs=[]
    for y,factor in rows:
        for i in range(n):
            a=i*math.tau/n;r=factor*(1+.045*math.sin(a*5+cx)+.025*math.cos(a*9+cz))
            vs.append((cx+math.cos(a)*rx*r,top+y,cz+math.sin(a)*rz*r))
    for j in range(len(rows)-1):
        for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    fs.append(tuple((len(rows)-1)*n+i for i in range(n)));fs.append(tuple(reversed(range(n))))
    mesh(vs,fs,0,False)
    if grass:
        topvs=[(cx,top+.004,cz)]
        for i in range(72):
            a=i*math.tau/72;f=.91+.035*math.sin(a*7+cx)
            topvs.append((cx+math.cos(a)*rx*f,top+.006,cz+math.sin(a)*rz*f))
        mesh(topvs,[(0,(i+1)%72+1,i+1) for i in range(72)],7)
    for i in range(18):
        a=i*math.tau/18
        if i%3:rock((cx+math.cos(a)*rx*.85,top-.18,cz+math.sin(a)*rz*.85),(.42,.23,.34),0)

def level_wind():
    name='SanctuaryWind'
    platforms=[((0,0,-18),5.7,6.2),((-7,1.5,-3),5,5),((6,3.2,9),5,5),((0,5,23),5.3,5)]
    for p,rx,rz in platforms:
        cliff_islet(p,rx,rz,14,grass=True)
        for k in range(5):
            a=k*2.399963;start=len(STATE['VERTS']);meadow();transform_from(start,(p[0]+math.cos(a)*rx*.58,p[1],p[2]+math.sin(a)*rz*.58),scale=1.4)
    for key,p in {'Entry':(0,0,-20),'Device2':(0,0,-16),'Device0':(-7,1.5,-3),'Device1':(6,3.2,9),'Device3':(0,5,23),'Device4':(1,0,-21)}.items():mark(name,key,p)
    for i,p in enumerate([(0,0,-20),(0,0,-16),(-2.2,0,-12),(-5.8,1.5,-7.3),(-7,1.5,-3),(-4,1.5,.4),(2.2,3.2,5.2),(6,3.2,9),(3.4,3.2,12.6),(-1,5,18.8),(0,5,23)]):mark(name,'Route'+str(i),p)
    for p,rx,rz in platforms:
        for side in [-1,1]:
            x=p[0]+side*rx*.76;z=p[2]+rz*.22
            profile((x,p[1],z),[(0,.16),(.85,.14),(.95,.18)],0)
            tube([(x,p[1]+.95,z),(x,p[1]+2.3,z)],.035,3)
            start=len(STATE['VERTS']);banner((0,0,0),.45,1.35);transform_from(start,(x,p[1]+2.3,z))
    # The final bell hangs from a supported timber belfry, not a detached ceiling ornament.
    for x in [-2.0,2.0]:
        box((x,7.3,23.5),(.27,4.6,.32),3,.025)
        for z in [22.8,24.2]:tube([(x,5,z),(x,8.3,23.5)],.09,3,8)
    for x in [-2.,2.]:
        profile((x,5,23.5),[(0,.48),(.12,.48),(.19,.39),(.56,.34),(.63,.39)],0,8)
        for h in [5.7,8.9]:
            box((x,h,23.5),(.32,.13,.38),4,.012)
            for z in [23.28,23.72]:rock((x,h,z),(.03,.03,.02),4,subdivisions=1)
        flow([(x,8.6,23.5),(x*.78,9.0,23.5),(x*.34,9.65,23.5)],.065,3,10)
    box((0,9.65,23.5),(4.7,.28,.40),3,.035)
    tube([(0,9.5,23.5),(0,8.9,23.5)],.045,4)
    for side in [-1,1]:
        for j in range(8):
            x=side*(j+.5)*.34;y=10.8-abs(x)*.40
            for k in range(6):
                z=22.1+k*.52;start=len(STATE['VERTS']);box((0,0,0),(.37,.07,.56),14,.008,segments=1)
                transform_from(start,(x,y,z),Matrix.Rotation(side*math.atan(.4),3,'Y'))
        tube([(side*2.5,9.78,22.0),(0,10.85,22.0)],.08,3,10)
        tube([(side*2.5,9.78,25.0),(0,10.85,25.0)],.08,3,10)
    for z in [22.05,25.0]:tube([(0,10.85,z-.1),(0,11.22,z)],.04,4)
    for p in [(-3.4,5,22),(3.3,5,23)]:lantern(p)
    for j,p in enumerate([(0,0,-16),(-7,1.5,-3),(6,3.2,9)]):
        for rr in [.72,.94,1.05]:ring((p[0],p[1]+.025,p[2]),rr,.018,4)
        star((p[0],p[1]+.027,p[2]),.66,4,4+j)

def wind_bridge(index):
    pairs=[((-2.2,0,-12),(-5.8,1.5,-7.3)),((-4,1.5,.4),(2.2,3.2,5.2)),((3.4,3.2,12.6),(-1,5,18.8))]
    walkway(*pairs[index],width=1.8)

def level_grove():
    name='SanctuaryGrove'
    # Winding rock banks enclose a stream rather than a rectangular architectural hall.
    for side in [-1,1]:
        for j in range(16):
            z=-19+j*3;cx=side*(17.5+1.2*math.sin(j*.8))
            rock((cx,.05,z),(2.0,1.5+j%3*.45,2.1),1)
            rock((cx-side*.5,-.32,z),(2.0,.55,2.0),7)
            if j%2==0:
                flow([(cx,-.1,z),(cx-side*.8,2.8,z-.3),(cx-side*3.6,6.2,z+.2),(cx-side*8,8,z+1)],.40,3,11,.25)
    for side in [-1,1]:
        for j in range(8):
            z=-18+j*6;cx=side*8.5
            for k in range(70):
                a=k*2.399963;r=2.5*math.sqrt((k+.5)/70);x=cx+math.cos(a)*r;zz=z+math.sin(a)*r
                leaf((x,7.8,zz),(x+math.cos(a)*.9,8.0,zz+math.sin(a)*.9),.22,8 if k%3 else 9)
    box((0,-5.5,1),(38,.20,48),2,.015)
    paths=[((-13,0,-18),(-13,0,-5)),((-13,0,-5),(-6,0,-5)),((-6,0,-5),(-6,0,8)),((-6,0,8),(6,0,8)),((6,0,8),(6,0,13.8))]
    for a,b in paths:walkway(a,b,2.1)
    cliff_islet((6,0,22),5,4.8,5,grass=True,grounded=True)
    for p in [(-6,0,-5),(-6,0,8),(6,0,8)]:
        profile((p[0],-.5,p[2]),[(0,1.65),(.50,1.65),(.54,1.55)],3,40)
    for key,p in {'Entry':(-13,0,-16),'Device4':(-13,0,-18),'Device0':(-6,0,-5),'Device1':(-6,0,8),'Device2':(6,0,8),'Device3':(6,0,21),'SunWell':(-14,1.5,-5),'Seed':(6,0,23.5),'RootBridge':(6,0,13.8)}.items():mark(name,key,p)
    for i,p in enumerate([(-13,0,-16),(-13,0,-5),(-6,0,-5),(-6,0,8),(6,0,8),(6,0,13.5),(6,0,19.3),(6,0,21)]):mark(name,'Route'+str(i),p)
    for i,p in enumerate([(-10.8,0,1),(10.5,0,-5),(-10.8,0,15),(12,0,19)]):
        mark(name,'Ancient'+str(i),p);cliff_islet(p,2.9,3,5,grass=True,grounded=True)
    for j in range(12):
        a=j*2.399963;x=math.cos(a)*(8+j%4);z=math.sin(a)*19+2
        if abs(x+6)<2 or abs(z-8)<1.8 or (x>3 and x<9 and z>10):continue
        fern((x,-.32,z),1.15)
    for p in [(-11,0,-11),(-9,0,8),(7,0,10),(8,0,20)]:lantern(p)

def root_bridge():
    for side in [-1,1]:
        flow([(side*.9,-.13,-.2),(side*.8,-.04,1.5),(side*.78,.02,3.5),(side*.95,-.10,5.35)],.15,3,10)
        flow([(side*1.1,0,0),(side*1.05,.70,1.7),(side*1.1,.82,3.5),(side*1.2,.40,5.3)],.065,3,8,.6)
    for j in range(23):
        z=j*.235
        flow([(-1,-.02,z),(0,.015,z+.02),(1,-.02,z)],.085,3,8)
    for j in range(8):
        z=.5+j*.58;leaf((-1,.05,z),(-1.3,.20,z+.2),.09,9);leaf((1,.05,z),(1.3,.20,z+.2),.09,9)

def ship_hull(length=42):
    for side in [-1,1]:
        for row in range(17 if length<40 else 27):
            y=-2.6+row*.31
            radius=math.sqrt(max(.05,1-((y-1.5)/5.2)**2))*6.4
            for j in range(24):
                z=(j-11.5)*length/24;f=math.sqrt(max(.10,1-(z/(length*.58))**2));xx=side*radius*f
                if side<0 and -13<z< -7 and row>9 and row<19:continue
                if length<40 and side>0 and abs(z-6)<1.5 and .8<y<4.5:continue
                box((xx,y,z),(.13,.292,length/24-.025),3,.009,segments=1)
                if row%4==0 and j%3==0:rock((xx-side*.08,y,z),(.011,.025,.025),4,subdivisions=1)
    for j in range(14):
        z=-length*.465+j*length*.93/13
        if length<40 and abs(z-6)<1.5:continue
        points=[(-6.2,2.45,z),(-6.2,2,z),(-5.7,-.6,z),(-3.8,-2.4,z),(0,-2.6,z),(4,-2.4,z),(6.2,2,z),(6.2,2.45,z)] if length<40 else [(-3.8,-2.4,z),(-5.7,-.6,z),(-6.2,2,z),(-5,5.0,z),(0,6.8,z),(5,5,z),(6.2,2,z),(4,-2.4,z)]
        flow(points,.16,3,10)
    for side in [-1,1]:
        for y in ([-.4,2.0] if length<40 else [-.4,2.0,4.8]):
            if length<40 and side>0 and y==2.0:
                tube([(side*5.6,y,-length*.45),(side*6.1,y,0),(side*5.9,y,4.5)],.13,3,9)
                tube([(side*5.9,y,7.5),(side*5.6,y,length*.45)],.13,3,9)
            else:tube([(side*5.6,y,-length*.45),(side*6.1,y,0),(side*5.6,y,length*.45)],.13,3,9)

def level_wreck():
    name='SanctuaryDrydock';ship_hull()
    for j in range(42):box((0,-1.87,-11+j*.46),(7.4,.14,.44),3,.01)
    for side in [-1,1]:
        if side<0:
            walkway((side*4.2,1.8,-15),(side*4.2,1.8,-9),width=2.1)
            walkway((side*4.2,1.8,-3),(side*4.2,1.8,8),width=2.1)
        else:walkway((side*4.2,1.8,-15),(side*4.2,1.8,8),width=2.1)
        tube([(side*5.3,2.5,-14),(side*5.3,2.5,7)],.065,4,10)
    for j in range(11):box((0,1.725,-19+j*.48),(9,.15,.46),3,.012)
    stair((-4.2,1.8,-9),(-2.2,-1.8,-4),2.1)
    for j in range(15):box((0,1.72,14+j*.43),(8.5,.16,.41),3,.012)
    # Cargo clusters, hanging rope and copper straps give the ship a worked interior.
    for z in [-17,-8,1,17]:
        for side in [-1,1]:
            profile((side*5.1,1.8,z),[(0,.38),(.12,.42),(.65,.46),(.94,.40),(1.0,.39)],3,20)
            for h in [.12,.82]:ring((side*5.1,1.8+h,z),.42,.025,4,n=24)
    for z in [-12,-3,6]:
        for side in [-1,1]:
            flow([(side*5.5,2,z),(side*6.1,4.5,z),(side*4.8,5.2,z)],.06,5,8)
            tube([(side*4.8,5.2,z),(side*4.6,4.0,z)],.022,13,7)
    # Raised captain cabin: a fully framed destination, not a gate across an empty room.
    for side in [-1,1]:
        for j in range(11):box((side*4.1,3.25,14+j*.52),(.18,2.9,.5),3,.014)
    for x in [-3.0,3.0]:box((x,3.25,14),(2.1,2.9,.22),3,.018)
    box((0,4.75,17),(8.6,.18,6.3),3,.02)
    for x in [-1.7,1.7]:box((x,3.1,13.9),(.20,2.6,.28),3,.02)
    box((0,4.47,13.9),(3.6,.20,.28),3,.02)
    for side in [-1,1]:
        box((side*3.6,3.1,18.5),(.22,2.0,1.5),15,.025)
        for h in [2.2,3.1,4.0]:box((side*3.43,h,18.5),(.13,.08,1.7),4,.01)
        for z in [17.7,18.5,19.3]:box((side*3.43,3.1,z),(.13,1.9,.07),4,.01)
    for z in [-16,-5,6,17]:
        for x in [-4.8,4.8]:lantern((x,2.25,z))
    profile((4.8,1.8,5),[(0,.28),(1.2,.18)],4)
    for x in [-.45,1.45]:box((x,-1.75,-3),(.065,.08,7.2),4,.009)
    box((.5,-1.72,0),(2.4,.16,2),4,.012)
    for p in [(3.0,1.8,-13),(-4.4,1.8,7),(3,1.8,17)]:
        start=len(STATE['VERTS']);crate();transform_from(start,p)
    marks={'Entry':(0,1.8,-16),'Device4':(0,1.8,-18),'Device0':(.5,-1.8,-6),'Device1':(4.2,1.8,5),'Device2':(0,0,0),'Device3':(0,1.8,18),'CargoStart':(.5,-1.8,-6),'CargoEnd':(.5,-1.64,0),'FloatGangway':(0,1.8,11),'HoldGate':(-4.2,1.8,-9),'HoldLanding':(-2.2,-1.8,-4)}
    for k,p in marks.items():mark(name,k,p)
    route=[(0,1.8,-16),(4.2,1.8,-15),(4.2,1.8,-10),(4.2,1.8,5),(4.2,1.8,-12),(-4.2,1.8,-12),(-4.2,1.8,-9),(-2.2,-1.8,-4),(-2,-1.8,-7.5)]
    # A supported aft crossing joins the two side decks for the descent route.
    walkway((-4.2,1.8,-12),(4.2,1.8,-12),2.1)
    walkway((-4.2,1.8,7),(4.2,1.8,7),2.8,open_mid=True)
    for i,p in enumerate(route):mark(name,'Route'+str(i),p)

def float_gangway():
    for j in range(23):box((0,-.02,(j-11)*.25),(2.4,.20,.24),3,.012)
    for side in [-1,1]:
        box((side*.95,-.22,0),(.15,.20,5.4),3)
        for z in [-1.8,0,1.8]:
            profile((side*.8,-1.05,z),[(0,.26),(.10,.33),(.65,.33),(.78,.26)],4,16)
        tube([(side*1.24,.72,-2.6),(side*1.24,.72,2.6)],.025,13,7)
        for z in [-2.5,0,2.5]:profile((side*1.24,-.1,z),[(0,.055),(.92,.055)],3,10)

def hold_hatch():
    for x in [-1.05,1.05]:box((x,.8,0),(.14,1.6,.20),3)
    for y in [.3,.85,1.4]:box((0,y,0),(2.2,.13,.16),3)
    for x in [-.70,.70]:tube([(x,.05,-.1),(x,1.55,-.1)],.025,4)

def level_stars():
    name='SanctuaryStars'
    cliff_islet((0,0,-2),11,12,4.0)
    profile((0,-.02,-2),[(0,6.2),(.045,6.2)],15,96)
    for rr in [1.2,3.5,6.1,8.9]:ring((0,.055,-2),rr,.022,4)
    star((0,.056,-2),5.7,4,16)
    for row in range(3):
        inner=8.8+row*.58;outer=inner+.54;h=.20+row*.18
        for j in range(48):
            a=j*math.tau/48;b=(j+1)*math.tau/48
            if 2.0<a<2.65 or 4.4<a<5.1:continue
            vs=[(math.cos(t)*r,y,-2+math.sin(t)*r) for y in [h-.2,h] for r,t in [(inner,a),(outer,a),(outer,b),(inner,b)]]
            mesh(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],1)
    profile((0,-.4,10),[(0,10.0),(.35,10.0),(.42,9.9)],1,72)
    # Viewing balcony is physically raised on a ring of carved piers.
    for j in range(8):
        a=j*math.tau/8;column((math.cos(a)*3.9,0,10+math.sin(a)*3.9),3.5,.30,1)
    for j in range(8):
        a=j*math.tau/8;b=(j+1)*math.tau/8;p=(math.cos(a)*3.9,0,10+math.sin(a)*3.9);q=(math.cos(b)*3.9,0,10+math.sin(b)*3.9)
        start=len(STATE['VERTS']);arch((0,0,0),2.38,2.1,.15,.28,1)
        transform_from(start,((p[0]+q[0])*.5,0,(p[2]+q[2])*.5),Matrix.Rotation(-math.atan2(q[2]-p[2],q[0]-p[0]),3,'Z'))
    profile((0,3.4,10),[(0,5.1),(.20,5.1),(.26,5.0)],1,64)
    for rr in [3.4,4.6]:ring((0,3.67,10),rr,.016,4)
    arch((0,3.6,14.0),8,2.1,.24,.5,1)
    for x in [-4.12,4.12]:column((x,0,14),3.6,.33,1)
    ring((0,7.2,10),4.55,.065,4)
    for j in range(4):
        a=(j+.5)*math.tau/4;x,z=math.cos(a)*4.55,10+math.sin(a)*4.55
        profile((x,3.66,z),[(0,.16),(.12,.16),(.22,.065),(3.45,.065),(3.55,.13)],4,16)
    for k in range(3):
        a=k*math.pi/3
        pts=[]
        for j in range(33):
            b=j*math.pi/32;pts.append((math.cos(a)*4.55*math.cos(b),7.2+4.55*math.sin(b),10+math.sin(a)*4.55*math.cos(b)))
        tube(pts,.052,4,9)
    rock((0,11.80,10),(.15,.20,.15),4)
    for j in range(48):
        a=j*math.tau/48
        tube([(math.cos(a)*4.45,7.2,10+math.sin(a)*4.45),(math.cos(a)*4.65,7.2,10+math.sin(a)*4.65)],.014,4,5)
    for rr,count in [(2.5,16),(4.8,24),(7.4,36)]:
        for j in range(count):
            a=j*math.tau/count;box((math.cos(a)*rr,.015,-2+math.sin(a)*rr),(.48,.04,.75),1,.01,-math.degrees(a),segments=1)
    count=24
    for j in range(count):
        a=math.pi+j*math.pi/count;b=math.pi+(j+1)*math.pi/count;h=3.6*(j+1)/count
        vs=[]
        for y in [h-.20,h]:
            for r,ang in [(5.7,a),(7.35,a),(7.35,b),(5.7,b)]:vs.append((math.cos(ang)*r,y,10+math.sin(ang)*r))
        mesh(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],1)
        if j%3==0:
            x,z=math.cos(b)*7.25,10+math.sin(b)*7.25;profile((x,h,z),[(0,.09),(.82,.07),(.9,.10)],1,14)
    walkway((6.4,3.6,10),(3.5,3.6,10),width=1.8)
    walkway((-9,0,10),(-6.5,.15,10),width=1.8)
    for j in range(17):
        a=j*2.399963;x,z=math.cos(a)*10,math.sin(a)*11-2
        if (5.0<math.hypot(x,z-10)<8.1 and 3<z<11) or (abs(x+6.5)<1.8 and 4<z<11):continue
        h=.65+(j%4)*.2;box((x,h*.5,z),(1.5,h,.55),1,.035,-math.degrees(a))
        if j%3==0:column((x,0,z),1.9+(j%3)*.45,.29,1)
    for p in [(-7,0,-7),(7,0,-3),(-5,0,8),(4,3.6,12)]:lantern(p)
    for key,p in {'Entry':(0,0,-12),'Device4':(0,0,-13.5),'Device0':(0,3.6,10),'Device1':(-8,0,-5),'Device2':(7,0,-4),'Device3':(-1,3.6,13)}.items():mark(name,key,p)
    route=[(0,0,-12),(-9,0,10),(-6.5,0,10)]
    for j in range(1,13):
        a=math.pi+j*math.pi/12;route.append((math.cos(a)*6.5,3.6*j/12,10+math.sin(a)*6.5))
    route.extend([(3.5,3.6,10),(0,3.6,8.4)])
    for i,p in enumerate(route):mark(name,'Route'+str(i),p)

def telescope_base():
    profile((0,0,0),[(0,.82),(.12,.82),(.20,.65),(.32,.60),(1.25,.40),(1.40,.53),(1.55,.53),(1.68,.32)],1,40)
    for r in [.47,.62]:ring((0,.22,0),r,.02,4)
    for j in range(12):
        a=j*math.tau/12;tube([(math.cos(a)*.5,.35,math.sin(a)*.5),(math.cos(a)*.36,1.2,math.sin(a)*.36)],.018,4)
    profile((0,1.65,0),[(0,.30),(.18,.30),(.22,.25)],4,40)
    for j in range(32):
        a=j*math.tau/32;box((math.cos(a)*.39,1.92,math.sin(a)*.39),(.055,.12,.055),4,.006)

def telescope_mount():
    for side in [-1,1]:
        flow([(side*.30,-.25,0),(side*.55,-.12,0),(side*.60,.30,0),(side*.38,.42,0)],.075,4,12)
        start=len(STATE['VERTS']);ring((0,0,0),.31,.025,4,'upright',n=48);transform_from(start,(side*.58,.20,0),Matrix.Rotation(math.pi*.5,3,'Z'))
    tube([(-.67,.18,0),(.67,.18,0)],.045,4,12)
    gear((-.67,.18,0),.30,True);gear((.67,.18,0),.30,True)
    profile((0,-.4,0),[(0,.27),(.20,.27)],4,32)

def telescope_tube():
    # Lathed barrel is rotated onto +Z; its eyepiece is an authored physical marker.
    start=len(STATE['VERTS'])
    profile((0,0,0),[(-1.12,.075),(-.91,.075),(-.86,.13),(-.73,.13),(-.70,.24),(1.55,.30),(1.60,.38),(1.80,.38),(1.85,.31)],4,48)
    transform_from(start,rotation=Matrix.Rotation(math.pi*.5,3,'X'))
    for z in [-.65,-.35,.40,1.15,1.62]:
        ring((0,0,z),.255 if z<0 else .31,.025,4,'upright',n=48)
    profile_points=[(-.18,-.18,-.65),(.18,-.18,-.65),(.18,-.18,.95),(-.18,-.18,.95)]
    for a,b in [(profile_points[0],profile_points[2]),(profile_points[1],profile_points[3])]:tube([a,b],.025,3,8)
    # Objective and eyepiece have inset dark enamel surfaces and raised optical rims.
    for z,r in [(1.83,.30),(-1.13,.062)]:
        vs=[(0,0,z)]+[(math.cos(j*math.tau/48)*r,math.sin(j*math.tau/48)*r,z) for j in range(48)]
        mesh(vs,[(0,1+j,1+(j+1)%48) for j in range(48)],15,True)
    for side in [-1,1]:tube([(side*.2,-.12,-.6),(side*.4,-.25,-.75),(side*.4,-.25,-1.05)],.025,3,8)
    gear((.39,-.05,-.78),.095,True)
    for j in range(32):
        a=j*math.tau/32;rock((math.cos(a)*.335,math.sin(a)*.335,1.65),(.014,.014,.014),4,subdivisions=1)
    mark('TelescopeTube','Eyepiece',(0,.04,-1.28))

def giant_bell():
    rows=[(0,.75),(.12,.83),(.20,.78),(.32,.61),(1.05,.48),(1.22,.35),(1.35,.20),(1.42,.12),(1.35,.065),(1.27,.14),(1.15,.29),(1.0,.42),(.3,.55),(.10,.74),(0,.69)]
    vs=[(math.cos(j*math.tau/64)*r,h,math.sin(j*math.tau/64)*r) for h,r in rows for j in range(64)]
    mesh(vs,[(i*64+j,i*64+(j+1)%64,((i+1)%len(rows))*64+(j+1)%64,((i+1)%len(rows))*64+j) for i in range(len(rows)) for j in range(64)],4,True)
    for h,r in [(.16,.79),(.36,.60),(1.02,.49)]:profile((0,h,0),[(0,r),(.025,r+.018),(.045,r)],4,64)
    tube([(0,1.4,0),(0,1.76,0)],.055,4,12)
    tube([(0,.35,0),(0,-.1,0)],.035,4,10);rock((0,-.12,0),(.13,.16,.13),4)
    tube([(0,-.1,0),(.05,-.9,-.10),(.12,-1.7,-.18)],.022,13,8)

def living_fountain():
    profile((0,0,0),[(0,1.15),(.16,1.15),(.24,1.0),(.45,.95),(.55,.80),(.42,.65)],1,48)
    for j in range(8):
        a=j*math.tau/8;flow([(math.cos(a)*.8,.1,math.sin(a)*.8),(math.cos(a)*.7,.6,math.sin(a)*.7),(math.cos(a)*.20,1.0,math.sin(a)*.20)],.065,3,8,.45)
        leaf((0,.85,0),(math.cos(a)*.42,1.22,math.sin(a)*.42),.13,9)
    rock((0,1.05,0),(.13,.19,.13),4)

def captain_chart():
    for x in [-.75,.75]:
        for z in [-.4,.4]:box((x,.45,z),(.08,.90,.08),3,.012)
    for j in range(5):box(((j-2)*.34,.96,0),(.33,.09,1.0),3,.015)
    box((0,1.012,0),(1.3,.015,.78),12,.006)
    for j in range(9):tube([(-.5+j*.12,1.025,-.28),(-.38+j*.10,1.025,.28)],.002,15,5)
    for r in [.14,.20]:ring((.35,1.03,0),r,.008,4,n=32)
    rock((-.48,1.045,.24),(.07,.025,.06),4)

def star_chart():
    profile((0,0,0),[(0,.5),(.1,.5),(.15,.35),(.85,.24),(.9,.5),(.98,.5)],1,32)
    for rr in [.20,.37,.46]:ring((0,.99,0),rr,.012,4)
    star((0,.995,0),.34,4,8)

def ancient_tree():
    for side in [-1,1]:
        flow([(side*1.1,0,0),(side*1.3,2,.10),(side*.9,4.0,.12),(side*.3,6.7,.10),(0,8.5,.05)],.70,3,14,.32)
        for j in range(5):
            a=j*1.1+side;flow([(side*1.2,1.0,0),(side*1.8+math.cos(a),.40,math.sin(a)),(side*2.2+math.cos(a)*1.7,-.12,math.sin(a)*2)],.24,3,9,.16)
    for k in range(12):
        a=k*2.399963;h=5.5+k%3*.7;dx,dz=math.cos(a),math.sin(a)
        flow([(0,h,0),(dx*1.6,h+.7,dz*1.6),(dx*3.1,h+1.0,dz*3.1),(dx*4,h+.9,dz*4)],.24,3,10,.12)
        for j in range(100):
            aa=j*2.399963;rr=1.45*math.sqrt((j+.5)/100);x=dx*3+math.cos(aa)*rr;z=dz*3+math.sin(aa)*rr;y=h+1+math.sin(j)*.45
            leaf((x,y,z),(x+math.cos(aa)*.75,y+.16*math.sin(j*1.9),z+math.sin(aa)*.75),.20,8 if j%3 else 9)
    for j in range(6):fern((math.cos(j)*2.5,0,math.sin(j)*2.3),1.0)
    mark('AncientTree','Portal',(0,0,-1.9))

def windmill():
    profile((0,-1.5,0),[(0,3.4),(1.4,3.4),(1.5,3.3)],0,64)
    profile((0,0,0),[(0,2.55),(.2,2.55),(.35,2.38),(6.4,1.9),(6.55,2.1),(6.75,2.1)],0,56)
    for row in range(16):
        h=.52+row*.36;r=2.38-.48*h/6.4
        for j in range(28):
            a=(j+(row%2)*.5)*math.tau/28;box((math.cos(a)*(r+.035),h,math.sin(a)*(r+.035)),(math.tau*r/28-.018,.34,.15),0,.012,-math.degrees(a)-90,segments=1)
    profile((0,6.75,0),[(0,2.45),(.15,2.45),(2.05,.10),(2.20,0)],14,56)
    for j in range(10):
        a=j*math.tau/10;tube([(math.cos(a)*2.44,6.85,math.sin(a)*2.44),(0,8.95,0)],.025,4)
    for side in [-1,1]:
        box((side*.62,1.1,-2.41),(.16,2.2,.18),3)
    for j in range(7):box(((j-3)*.16,1.02,-2.5),(.15,2.0,.10),3,.012)
    for h in [.3,1.6]:box((0,h,-2.58),(1.26,.10,.07),4,.008)
    box((0,4.5,-2.08),(.84,1.1,.08),15,.012)
    for x in [-.48,0,.48]:box((x,4.5,-2.16),(.055,1.25,.055),3,.008)
    for h in [3.9,4.5,5.1]:box((0,h,-2.16),(1.0,.055,.055),3,.008)
    profile((0,3.15,0),[(0,3.22),(.18,3.22),(.24,3.10)],3,64)
    for j in range(28):
        a=j*math.tau/28
        if min(a,math.tau-a)<.28:continue
        x,z=math.cos(a)*3.13,math.sin(a)*3.13;profile((x,3.39,z),[(0,.045),(.85,.04)],3,8)
    ring((0,4.25,0),3.13,.045,3,start=.28,end=math.tau-.28)
    for j in range(15):
        h=j*3.2/14;box((3.6,h,0),(.10,.05,.65),3,.008)
    for z in [-.34,.34]:
        tube([(3.6,0,z),(3.6,3.4,z)],.025,13)
        flow([(3.6,3.3,z),(3.6,4.0,z),(3.2,4.15,z),(2.9,4.0,z)],.025,4,9)
    for key,p in {'Portal':(0,0,-2.85),'Rotor':(0,5.35,-2.25),'ClimbBottom':(3.6,0,0),'ClimbTop':(2.8,3.4,0),'ClimbEnd':(3.6,3.4,0)}.items():mark('Windmill',key,p)

def windmill_rotor():
    for k in range(4):
        start=len(STATE['VERTS'])
        box((0,1.9,0),(.13,3.8,.14),3,.02)
        for x in [-.57,.57]:box((x,2.0,0),(.055,2.8,.06),3,.008)
        for j in range(9):box((0,.7+j*.30,0),(1.15,.045,.055),3,.007)
        vs=[];fs=[]
        for j in range(10):
            y=.7+j*.30
            for x in [-.53,0,.53]:vs.append((x,y,.025+.025*math.sin(j*.7+x*2)))
        for j in range(9):
            for i in range(2):a=j*3+i;fs.append((a,a+1,a+4,a+3))
        mesh(vs,fs,10,True)
        transform_from(start,rotation=Matrix.Rotation(k*math.pi*.5,3,'Y'))
    rock((0,0,0),(.32,.32,.18),4)

def height(island,x,z):
    r=math.hypot(x,z)
    base=1.05+.13*math.sin(x*.25)*math.cos(z*.23)
    if island==0:base+=4.7*math.exp(-((x-11)**2/145+(z+12)**2/190))
    if island==1:base+=.6*math.exp(-((x+10)**2+(z+9)**2)/120)-.55*math.exp(-((x+2)**2/75+(z+1)**2/160))
    if island==2:base+=.20*math.sin(x*.30+z*.15)
    if island==3:base+=2.6*math.exp(-((x-4)**2+(z+10)**2)/260)
    # Stable arrival and resident footing remain compatible with existing progress and landing.
    for cx,cz,rad,y in [(0,26.4,4,1.04),(7,17,3.5,1.22)]:
        d=math.hypot(x-cx,z-cz);w=max(0,min(1,(rad+2-d)/2));base=base*(1-w)+y*w
    edge=29.0+1.7*math.sin(math.atan2(z,x)*3+island)
    if r>edge-3:base=base*(1-max(0,min(1,(r-(edge-3))/4))) - max(0,r-edge)*.42
    return base

def terrain(island):
    n=80;step=.85;vs=[]
    for j in range(n+1):
        for i in range(n+1):
            x=(i-n/2)*step;z=(j-n/2)*step;vs.append((x,height(island,x,z),z))
    for j in range(n):
        for i in range(n):
            ids=(j*(n+1)+i,j*(n+1)+i+1,(j+1)*(n+1)+i+1,(j+1)*(n+1)+i)
            ps=[vs[k] for k in ids];mid=sum(p[1] for p in ps)/4;tile=12 if mid<1.14 or island==2 else 7
            mesh(ps,[(0,3,2,1)],tile,True)

def galleon():
    ship_hull(32)
    for j in range(55):
        z=-14+j*.52;width=12.3*math.sqrt(max(.1,1-(z/(32*.58))**2))-.2
        if abs(z-6)<1.3:
            box(((4.2-width*.5)*.5,1.70,z),(width*.5+4.2,.16,.50),3,.014)
        else:box((0,1.70,z),(width,.16,.50),3,.014)
    for side in [-1,1]:
        for j in range(8):box((side*3.4,3.3,9+j*.52),(.18,3.0,.5),3,.014)
    for x in [-2.5,2.5]:box((x,3.3,9),(1.8,3.0,.2),3,.018)
    box((0,4.9,11),(7.2,.18,4.6),3,.02)
    for x in [-1.4,1.4]:box((x,3.1,8.85),(.18,2.6,.26),3,.02)
    box((0,4.45,8.85),(3.0,.18,.26),3)
    for z,h in [(-4,10),(3,8)]:
        flow([(0,1.8,z),(.15,5,z),(.40,h,z+.2)],.18,3,12,.55)
        tube([(-3.6,h-2,z), (3.6,h-2,z)],.10,3,10)
        for side in [-1,1]:tube([(side*4.2,2.5,z-4),(.3,h-.3,z)],.020,13,7)
    tube([(0,1.9,-14.5),(0,2.7,-19)],.10,3,10)
    sail=[]
    for row in range(9):
        t=row/8
        for k in range(9):
            u=k/8;sail.append(((u-.5)*(4.8-.6*t),8.0-t*3.2+(abs(math.sin(k*2.1))*.35 if row==8 else 0),-4+math.sin(u*math.pi)*.38*math.sin(t*math.pi)))
    mesh(sail,[(r*9+k,r*9+k+1,(r+1)*9+k+1,(r+1)*9+k) for r in range(8) for k in range(8)],10,True)
    walkway((10,0,6),(4.2,1.8,6),2.2)
    for p in [(-3,1.8,9),(3,1.8,10),(-3,1.8,-9)]:lantern(p)
    mark('Galleon','Portal',(0,1.8,8.2));mark('Galleon','Approach0',(10,0,6));mark('Galleon','Approach1',(4.2,1.8,6));mark('Galleon','Approach2',(0,1.8,7.3))

def observatory_exterior():
    level_stars()
    profile((0,-2,-2),[(0,10.0),(1.8,12.0),(2.0,12.0)],0,72)
    profile((0,-4.5,10),[(0,9),(4.3,10.1),(4.5,10.0)],1,72)
    for k,v in MARKERS['SanctuaryStars'].items():MARKERS.setdefault('ObservatoryExterior',{})[k]=list(v)
    mark('ObservatoryExterior','Portal',(0,3.6,8.3))

def flower_patch():
    for k in range(80):
        a=k*2.399963;r=.76*math.sqrt((k+.5)/80);x,z=math.cos(a)*r,math.sin(a)*r;h=.16+(k%7)*.047
        tube([(x,0,z),(x+.025,h,z)],.0025,9,4)
        for j in range(3):
            aa=a+j*2.1;yy=h*(.3+j*.16);dx,dz=math.cos(aa),math.sin(aa)
            mesh([(x,yy,z),(x+dx*.08-dz*.035,yy+.03,z+dz*.08+dx*.035),(x+dx*.19,yy+.04,z+dz*.19),(x+dx*.08+dz*.035,yy+.03,z+dz*.08-dx*.035)],[(0,1,2,3)],9,True)
        for j in range(7):
            aa=j*math.tau/7+k*.15;dx,dz=math.cos(aa),math.sin(aa);vs=[]
            for t,w in [(0,.006),(.6,.019),(1,.008)]:
                px=x+.025+dx*(.009+.051*t);pz=z+dz*(.009+.051*t);yy=h+math.sin(t*math.pi)*.011-t*t*.006
                vs += [(px-dz*w,yy,pz+dx*w),(px+dz*w,yy,pz-dx*w)]
            mesh(vs,[(0,1,3,2),(2,3,5,4)],10 if k%3 else 11,True)
        rock((x+.025,h+.004,z),(.010,.006,.010),12,subdivisions=1)

def ivy_drape():
    for strand in range(5):
        phase=strand*1.7;length=1.6+strand*.23
        points=[((strand-2)*.12+math.sin(j*.6+phase)*.08,-j*length/17,math.sin(j*.4+phase)*.06) for j in range(18)]
        tube(points,.009,3,6,.45)
        for j,p in enumerate(points[1:]):
            side=-1 if j%2 else 1;leaf(p,(p[0]+side*.18,p[1]-.10,p[2]+.055),.065,8 if j%3 else 9)

def gear(p,r,turn=False):
    start=len(STATE['VERTS']);profile((0,0,0),[(0,r*.88),(.055,r*.88),(.07,r*.73)],4,40)
    for j in range(28):
        a=j*math.tau/28;box((math.cos(a)*r,.035,math.sin(a)*r),(.045,.065,.075),4,.004,-math.degrees(a),segments=1)
    for j in range(6):
        a=j*math.tau/6;tube([(0,.08,0),(math.cos(a)*r*.8,.08,math.sin(a)*r*.8)],.015,4,6)
    transform_from(start,p,Matrix.Rotation(math.pi*.5,3,'Y') if turn else None)

def mirror_head():
    ring((0,0,0),.37,.030,4,'upright',n=64);ring((0,0,-.012),.325,.012,4,'upright',n=64)
    vs=[(0,0,0)]+[(math.cos(j*math.tau/64)*.32,math.sin(j*math.tau/64)*.32,.012) for j in range(64)]
    mesh(vs,[(0,j+1,(j+1)%64+1) for j in range(64)],15,True)
    for side in [-1,1]:
        flow([(side*.26,-.3,0),(side*.34,.10,0),(side*.22,.40,0),(0,.67,0)],.020,4,10)
        rock((side*.38,0,0),(.047,.047,.047),4,subdivisions=1)
    for j in range(12):
        a=j*math.tau/12;rock((math.cos(a)*.35,math.sin(a)*.35,-.025),(.014,.014,.011),4,subdivisions=1)
    tube([(0,-.36,0),(0,-.55,0)],.025,4,10)

def valve_body():
    box((0,.06,.15),(1.4,.12,.9),3,.025)
    flow([(0,.14,.25),(0,.75,.25),(0,1.6,.25),(.35,1.9,.25),(.75,1.9,.25)],.13,5,18)
    for h in [.38,1.52]:
        profile((0,h,.25),[(0,.21),(.09,.21)],4,32)
        for j in range(8):
            a=j*math.tau/8;profile((math.cos(a)*.17,h+.09,.25+math.sin(a)*.17),[(0,.018),(.025,.018)],4,6)
    tube([(0,1.15,.25),(0,1.15,-.20)],.065,4,16)
    ring((.42,1.55,-.1),.18,.025,4,'upright',n=40)
    vs=[(.42,1.55,-.1)]+[(.42+math.cos(j*math.tau/40)*.15,1.55+math.sin(j*math.tau/40)*.15,-.1) for j in range(40)]
    mesh(vs,[(0,j+1,(j+1)%40+1) for j in range(40)],10)
    tube([(.42,1.55,-.12),(.48,1.66,-.12)],.008,4,6)
    for j in range(16):
        a=(.15+j*.7/15)*math.tau;tube([(.42+math.cos(a)*.13,1.55+math.sin(a)*.13,-.12),(.42+math.cos(a)*.105,1.55+math.sin(a)*.105,-.12)],.003,4,5)

def valve_wheel():
    ring((0,0,0),.34,.028,4,'upright',n=64);ring((0,0,0),.30,.012,4,'upright',n=48)
    rock((0,0,0),(.075,.075,.065),4)
    for j in range(6):
        a=j*math.tau/6;tube([(0,0,0),(math.cos(a)*.34,math.sin(a)*.34,0)],.018,4,8)
    tube([(.34,0,0),(.34,0,-.16)],.028,3,12)

def mooring_pulley():
    for z in [-.10,.10]:ring((0,0,z),.32,.045,4,'upright',n=48)
    tube([(-.34,.16,0),(0,.50,0),(.34,.16,0)],.045,3,9)
    tube([(0,.50,0),(0,1.5,0)],.025,13,8)
    ring((0,0,0),.32,.022,13,'upright',start=0,end=math.pi,n=40)
    for x in [-.32,.32]:tube([(x,0,0),(x,-1.5,0)],.022,13,8)
    flow([(.32,-1.45,0),(.39,-1.68,0),(.20,-1.76,0),(.11,-1.61,0)],.04,4,10)

def grove_waterfall():
    for layer in range(3):
        vs=[]
        for j in range(17):
            t=j/16
            for side in [-1,1]:vs.append((.06+math.sin(t*math.pi)*.12+layer*.008,-t*1.03,side*(.48-.10*t)+math.sin(t*12)*.018))
        mesh(vs,[(j*2,j*2+1,j*2+3,j*2+2) for j in range(16)],5,True)

def install(api):
    global STATE
    STATE=api
    for key in ['box','profile','tube','ring','leaf','rock','column','arch','star','banner','chime','fern','meadow','tree','flow','lantern','crate','mesh','U']:
        globals()[key]=api[key]
    api['EXTRA_BUILDERS']={
        'FlowerPatch':flower_patch,'IvyDrape':ivy_drape,'MooringPulley':mooring_pulley,'GroveWaterfall':grove_waterfall,'ValveWheel':valve_wheel,'MirrorHead':mirror_head,'PuzzlePlinth2':valve_body,
        'SanctuaryWind':level_wind,'SanctuaryGrove':level_grove,'SanctuaryDrydock':level_wreck,'SanctuaryStars':level_stars,
        'WindBridge0':lambda:wind_bridge(0),'WindBridge1':lambda:wind_bridge(1),'WindBridge2':lambda:wind_bridge(2),
        'RootBridge':root_bridge,'FloatGangway':float_gangway,'HoldHatch':hold_hatch,
        'TelescopeBase':telescope_base,'TelescopeMount':telescope_mount,'TelescopeTube':telescope_tube,
        'GiantBell':giant_bell,'LivingFountain':living_fountain,'CaptainChart':captain_chart,'StarChart':star_chart,
        'AncientTree':ancient_tree,'Windmill':windmill,'WindmillRotor':windmill_rotor,'Galleon':galleon,'ObservatoryExterior':observatory_exterior,
        'LevelTerrain0':lambda:terrain(0),'LevelTerrain1':lambda:terrain(1),'LevelTerrain2':lambda:terrain(2),'LevelTerrain3':lambda:terrain(3)
    }
    api['MARKERS']=MARKERS
