"""Metre-scale cottage and kitchen garden; XY reflection cancels this project's FBX import reflection."""
import math
from mathutils import Vector, Matrix

API=None

def placed(fn,p=(0,0,0),yaw=0,scale=1):
    start=len(API['VERTS']);fn();rot=Matrix.Rotation(math.radians(yaw),3,'Z')
    for i in range(start,len(API['VERTS'])):API['VERTS'][i]=tuple(rot@(Vector(API['VERTS'][i])*scale)+U(p))

def reflected(fn):
    def build():
        start=len(API['VERTS']);face_start=len(API['FACES']);fn()
        for i in range(start,len(API['VERTS'])):
            p=API['VERTS'][i];API['VERTS'][i]=(-p[0],p[1],p[2])
        for i in range(face_start,len(API['FACES'])):
            API['FACES'][i]=tuple(reversed(API['FACES'][i]));API['UVS'][i]=list(reversed(API['UVS'][i]))
    return build

def land_height(x,z):
    a=math.atan2((z-3)/30,(x-5)/41);r=math.hypot((x-5)/41,(z-3)/30)/(1+.055*math.sin(3*a)+.035*math.cos(5*a))
    y=0 if r<.74 else -4.8*min(1,((r-.74)/.38)**1.18)
    if z< -11 and abs(x)<5:y=min(y,-4.8*min(1,(-z-11)/9)*max(0,1-(abs(x)/5)**4))
    return y

def home_land():
    routes=[([(0,-8.3),(7.6,-9),(10.2,-11),(29.2,-11),(30.8,12),(7.6,12),(7.6,5.8)],2.4),([(-7,-9),(-11,-7),(-18,0),(-19,8.8)],2)]
    routes += [([(12.3,z),(27,z)],1.5) for z in [-7.5,-2.5,2.5,7.5]]
    routes += [([(x,-7.5),(x,7.8)],1.25) for x in [12.2,15.8,19.4,23,26.6]]
    def near_path(x,z):
        for points,w in routes:
            for a,b in zip(points,points[1:]):
                dx,dz=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((x-a[0])*dx+(z-a[1])*dz)/(dx*dx+dz*dz)))
                if math.hypot(x-a[0]-t*dx,z-a[1]-t*dz)<w*.5:return True
        return False
    for j in range(148):
        for i in range(188):
            x,z=-42+i*.5,-34+j*.5
            # A real opening replaces these tiles; grass must never close the well below its rim.
            if 9 <= x < 11.5 and -9.5 <= z < -6.5:continue
            ps=[(xx,land_height(xx,zz),zz) for xx,zz in [(x,z),(x+.5,z),(x+.5,z+.5),(x,z+.5)]]
            y=land_height(x+.25,z+.25);tile=1 if y< -2.1 else 12 if y<-.3 or near_path(x+.25,z+.25) else 7
            mesh(ps,[(0,3,2,1)],tile,True)
    # Match the rectangular grid boundary exactly while leaving a round 1.42 m shaft mouth.
    angles=[k*math.tau/96 for k in range(96)]
    angles += [math.atan2(z+8,x-10.2)%math.tau for x,z in [(9,-9.5),(11.5,-9.5),(11.5,-6.5),(9,-6.5)]]
    angles=sorted(set(angles));inner=[];outer=[]
    for a in angles:
        dx,dz=math.cos(a),math.sin(a)
        t=min((11.5-10.2)/dx if dx>1e-8 else (9-10.2)/dx if dx< -1e-8 else 1e9,
              (-6.5+8)/dz if dz>1e-8 else (-9.5+8)/dz if dz< -1e-8 else 1e9)
        inner.append((10.2+dx*.71,0,-8+dz*.71));outer.append((10.2+dx*t,0,-8+dz*t))
    n=len(angles);mesh(inner+outer,[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)],7,True)

def roof(cx,front,back,half=6.7,eave=3.45,ridge=6.65):
    def y(t):return eave+(ridge-eave)*(1-t)**1.22+.10*math.sin(t*math.pi*2)
    for side in [-1,1]:
        rows=17;cols=max(5,math.ceil((back-front)/.44))
        for row in range(rows):
            t0=row/rows;t1=min(1,(row+1.10)/rows)
            for k in range(cols):
                z0=front+(back-front)*(k+(.5 if row%2 else 0))/cols
                z1=min(back,z0+(back-front)/cols-.018)
                if z0>=back:continue
                pts=[(cx+side*half*t0,y(t0)+.09,z0),(cx+side*half*t1,y(t1)+.02,z0),(cx+side*half*t1,y(t1)+.02,z1),(cx+side*half*t0,y(t0)+.09,z1)]
                pts+= [(p[0],p[1]-.065,p[2]) for p in pts]
                mesh(pts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],2)
        for z in [front-.025,back+.025]:tube([(cx+side*half*j/24,y(j/24),z) for j in range(25)],.095,3,10)
        tube([(cx+side*half,eave,front),(cx+side*half,eave,back)],.095,3,10)
        # Continuous timber ceiling sits under the layered shingles.
        for j in range(16):
            a=j/16;b=(j+1)/16
            mesh([(cx+side*half*a,y(a)-.1,front),(cx+side*half*b,y(b)-.1,front),(cx+side*half*b,y(b)-.1,back),(cx+side*half*a,y(a)-.1,back)],[(3,2,1,0)],3)
    tube([(cx,ridge+.10,front-.06),(cx,ridge+.10,back+.06)],.135,2,14)

def window(p,width=1.25,height=1.45):
    x,y,z=p;box(p,(width,height,.065),15,.016)
    for side in [-1,1]:
        box((x+side*width*.54,y,z-.04),(.12,height+.23,.13),3,.018)
        sx=x+side*(width*.5+.30)
        box((sx,y,z-.03),(.39,height+.08,.09),3,.022)
        for j in range(7):box((sx,y-height*.43+j*height*.145,z-.10),(.35,.055,.08),2,.008)
    for yy in [y-height*.55,y+height*.55]:box((x,yy,z-.05),(width+.28,.12,.19),3,.018)
    box((x,y,z-.06),(.055,height,.12),4,.006);box((x,y,z-.06),(width,.055,.12),4,.006)
    box((x,y-height*.65,z-.20),(width+.20,.26,.42),3,.028)
    for j in range(7):
        a=j*2.3999;px=x-width*.43+j*width*.14;py=y-height*.55+.14
        leaf((px,py,z-.25),(px+math.cos(a)*.25,py+.35,z-.25+math.sin(a)*.22),.095,8)
        flower((px,py+.28,z-.27),.11,10 if j%2 else 11)

def flower(p,r=.13,tile=11):
    x,y,z=p
    for j in range(5):
        a=j*math.tau/5;leaf(p,(x+math.cos(a)*r,y+.02,z+math.sin(a)*r),r*.42,tile)
    rock((x,y+.015,z),(.027,.018,.027),4,subdivisions=1)

def cottage_shell():
    # Existing room, bedside, crafting and door collision coordinates stay exact.
    for j in range(49):
        for k in range(5):box((-5.88+j*.245,.145,-3.98+k*1.99),(.231,.11,1.975),3,.008)
    box((0,-1.05,0),(12.35,2.10,10.25),0,.07)
    for side in [-1,1]:
        box((side*6,1.75,0),(.17,3.10,10),0,.025)
        box((side*6,3.46,0),(.19,.34,10.1),3,.02)
        box((side*3.75,1.75,-5),(4.5,3.10,.17),0,.025)
    box((0,1.75,5),(12,3.10,.17),0,.025);box((0,3.10,-5),(3.0,.28,.20),3,.025)
    for z in [-5.12,5.12]:
        mesh([(-6.4,3.29,z),(6.4,3.29,z),(0,6.56,z)],[(0,1,2)],0)
        for x in [-5.9,-3.7,0,3.7,5.9]:
            if z<0 and abs(x)<1:continue
            box((x,1.74,z),(.17,3.10,.20),3,.016)
        box((0,3.32,z),(12.9,.19,.22),3,.02)
    roof(0,-5.65,5.65)
    # Structural trusses are visible from the furnished interior.
    for z in [-4.6,-1.7,1.5,4.6]:
        box((0,3.32,z),(11.9,.22,.22),3,.025)
        for side in [-1,1]:
            tube([(side*5.9,3.35,z),(0,6.36,z)],.105,3,10)
            flow([(side*5.85,2.55,z),(side*5.55,2.91,z),(side*5.0,3.32,z)],.09,3,10)
            box((side*5.75,3.35,z),(.19,.36,.31),4,.01)
            for dz in [-.18,.18]:rock((side*5.75,3.32,z+dz),(.045,.045,.02),4,subdivisions=1)
        tube([(0,3.35,z),(0,6.35,z)],.065,3,10)
    for side in [-1,1]:
        for z in [-4.95,4.95]:box((side*5.90,1.7,z),(.18,3.0,.22),3,.02)
        for x in [side*3.75]:window((x,1.82,-5.15),1.25,1.40)
        for z in [-1.7,2.5]:placed(lambda:window((0,1.82,0),1.15,1.4),(side*6.14,0,z),90*side)
        box((side*1.56,1.57,-5.2),(.17,2.78,.22),3,.025)
        for dz in [-5.3,-7.7]:
            profile((side*6.1,.20,dz),[(0,.19),(.12,.23),(.22,.15),(2.5,.13),(2.65,.21)],3,12)
            flow([(side*6.1,2.03,dz),(side*5.8,2.49,dz),(side*5.25,2.83,dz)],.075,3,9)
    # Round attic oculus and layered brass frame.
    for r in [.44,.52,.58]:ring((0,4.83,-5.25),r,.035,3 if r==.58 else 4,'upright',n=64)
    mesh([(0,4.83,-5.20)]+[(math.cos(k*math.tau/40)*.43,4.83+math.sin(k*math.tau/40)*.43,-5.20) for k in range(40)],[(0,j+1,(j+1)%40+1) for j in range(40)],15)
    tube([(-.43,4.83,-5.28),(.43,4.83,-5.28)],.025,4);tube([(0,4.4,-5.28),(0,5.26,-5.28)],.025,4)
    for j in range(38):box((-6.33+j*.342,.14,-6.5),(.325,.12,3.15),3,.012)
    box((0,-1,-6.5),(13,2.15,3.2),0,.05)
    # Porch roof rises out of the front gable without reducing the broad doorway.
    roof(0,-7.95,-4.85,6.6,2.98,4.05)
    for x in [-2.2,2.2]:
        tube([(x,2.6,-5.3),(x,2.6,-5.7)],.025,4)
        placed(lambda:lantern((0,0,0)),(x,1.85,-5.75),scale=.60)
    for row in range(10):
        for col in range(3):box((3.8+(col-1)*.27,4.35+row*.30,2.7),(.25,.28,.9),0,.015)
    box((3.8,7.4,2.7),(1.06,.18,1.12),1,.03);box((3.8,7.51,2.7),(.72,.06,.76),15,.008)
    for side in [-1,1]:
        for row in range(6):box((side*5.86,.38+row*.125,0),(.11,.11,9.80),3,.008)
    for j in range(11):box((-5.7+j*.47,.55,4.86),(.44,.70,.10),3,.009)
    for z in [-.5,2.5]:
        tube([(0,3.32,z),(0,2.82,z)],.012,4,7)
        for h in [2.48,2.84]:profile((0,h,z),[(0,.16),(.045,.18),(.065,.12)],4,24)
        for j in range(8):
            a=j*math.tau/8;tube([(math.cos(a)*.14,2.53,z+math.sin(a)*.14),(math.cos(a)*.14,2.84,z+math.sin(a)*.14)],.009,4,7)
        rock((0,2.68,z),(.075,.13,.075),10,2)

def path(points,width=2.5):
    for a,b in zip(points,points[1:]):
        a,b=Vector(a),Vector(b);d=b-a;n=max(1,math.ceil(d.length/.45));side=Vector((-d.z,0,d.x)).normalized()
        for i in range(n):
            p=a+d*(i+.5)/n
            # Gravel is part of HomeLand; only edge stones are added here to avoid overlapping coplanar crossings.
        for i in range(max(1,int(d.length/.6))):
            p=a+d*(i+.5)/max(1,int(d.length/.6))
            for s in [-1,1]:
                q=p+side*s*(width*.5+.05);q.y=land_height(q.x,q.z)+.015
                rock(tuple(q),(.15,.065,.11),1,1)

def fence(a,b):
    a,b=Vector(a),Vector(b);delta=b-a;n=max(1,math.ceil(delta.length/1.8));side=Vector((-delta.z,0,delta.x)).normalized()
    for i in range(n+1):
        p=a+delta*i/n;p.y=land_height(p.x,p.z)-.10
        profile(tuple(p),[(0,.065),(1.15,.065),(1.22,.095),(1.31,0)],3,8)
    for h in [.38,.85]:
        points=[]
        for i in range(n*4+1):
            p=a+delta*i/(n*4);p.y=land_height(p.x,p.z)+h;points.append(tuple(p))
        tube(points,.038,3,7)
    for i in range(max(1,int(delta.length/.30))):
        p=a+delta*(i+.5)/max(1,int(delta.length/.30));p.y=land_height(p.x,p.z)
        tube([tuple(p-Vector((0,.04,0))),tuple(p+Vector((0,1.0,0)))],.027,3,6)

def home_details():
    path([(0,0,-8.3),(7.6,0,-9),(10.2,0,-11),(29.2,0,-11),(30.8,0,12),(7.6,0,12),(7.6,0,5.8)],2.4)
    for z in [-7.5,-2.5,2.5,7.5]:path([(12.3,0,z),(27,0,z)],1.5)
    for x in [12.2,15.8,19.4,23,26.6]:path([(x,0,-7.5),(x,0,7.8)],1.25)
    for a,b in [((13.8,0,-9.4),(29,0,-9.4)),((29,0,-9.4),(29,0,10)),((29,0,10),(11.2,0,10)),((11.2,0,10),(11.2,0,7.6)),((11.2,0,-1.8),(11.2,0,2))]:fence(a,b)
    for j in range(36):
        a=j*math.tau/36; x=5+math.cos(a)*37;z=3+math.sin(a)*26
        if z< -14 and abs(x)<6:continue
        y=land_height(x,z);rock((x,y-.15,z),(1.6+(j%3)*.4,1.0+(j%4)*.35,1.3),0 if j%3 else 1)
    # Fine slate-roofed potting shelter behind the vegetable garden.
    for x in [19,25]:
        for z in [14,17]:profile((x,0,z),[(0,.16),(.15,.18),(2.5,.13)],3,10)
    roof(22,13.6,17.6,3.4,2.52,3.9)
    for x in [19.15+j*.30 for j in range(20)]:box((x,1.20,17),(.28,2.4,.10),3,.015)
    for x in [19,25]:box((x,1.0,15.5),(.12,2,3),3,.025)
    box((22,.82,16.2),(5.5,.18,.90),3,.035)
    for j in range(6):placed(lambda:crate(),(19.7+j*.82,0,16),scale=.45)
    # Weathered lighthouse anchors the western headland.
    x,z=-19,12
    profile((x,0,z),[(0,2.5),(.3,2.5),(.5,2.15),(9.0,1.38),(9.3,1.7),(9.55,1.8)],0,40)
    for h in [2.3,4.7,7.1]:profile((x,h,z),[(0,1.95-h*.055),(.22,1.95-h*.055)],2,40)
    profile((x,9.55,z),[(0,1.45),(.08,1.45)],0,40)
    profile((x,11.05,z),[(0,1.8),(.10,1.8),(.85,.03)],2,40)
    for j in range(10):
        a=j*math.tau/10;tube([(x+math.cos(a)*1.42,9.55,z+math.sin(a)*1.42),(x+math.cos(a)*1.42,11.05,z+math.sin(a)*1.42)],.04,4)
    rock((x,10.2,z),(.48,.60,.48),10)
    API['MARKERS'].setdefault('HomeLandscaping',{})['LighthouseLens']=[-x,10.2,z]
    ring((x,9.35,z),2.2,.055,4,n=64)
    for j in range(18):
        a=j*math.tau/18;tube([(x+math.cos(a)*2.2,8.7,z+math.sin(a)*2.2),(x+math.cos(a)*2.2,9.35,z+math.sin(a)*2.2)],.025,4)
    path([(-7,0,-9),(-11,0,-7),(-18,0,0),(-19,0,8.8)],2)

def well():
    # Inward-facing masonry continues 5.4 metres below the ground; no cap or grass under the opening.
    for row in range(20):
        for j in range(18):
            a=(j+row*.5)*math.tau/18;b=a+math.tau/18-.012;vs=[]
            for y in [-5.4+row*.27,-5.4+(row+1)*.27-.010]:
                for r,t in [(.71,a),(.90,a),(.90,b),(.71,b)]:vs.append((math.cos(t)*r,y,math.sin(t)*r))
            mesh(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],0)
    n=72;wall=[(math.cos(k*math.tau/n)*.76,y,math.sin(k*math.tau/n)*.76) for y in [-5.42,.03] for k in range(n)]
    mesh(wall,[(k,(k+1)%n,(k+1)%n+n,k+n) for k in range(n)]+[tuple(reversed(range(n)))],0)
    for row in range(4):
        for j in range(18):
            a=(j+row*.5)*math.tau/18;b=a+math.tau/18-.016;vs=[]
            for y in [row*.22,(row+1)*.22-.012]:
                for r,t in [(.70,a),(.94,a),(.94,b),(.70,b)]:vs.append((math.cos(t)*r,y,math.sin(t)*r))
            mesh(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],0)
    for x in [-1.14,1.14]:profile((x,0,0),[(0,.13),(2.55,.11),(2.65,.14)],3,12)
    roof(0,-1.22,1.22,1.65,2.42,3.4)
    tube([(-1.3,1.65,0),(1.5,1.65,0)],.10,3,12)
    for j in range(12):ring((0,1.65,0),.16+j*.003,.011,13,'upright',n=32)
    tube([(0,1.65,0),(0,.40,0)],.018,13)
    tube([(1.5,1.65,0),(1.5,1.35,0),(1.7,1.35,0)],.04,4)
    profile((0,.29,0),[(0,.24),(.32,.31),(.37,.31)],3,18)
    ring((0,.66,0),.31,.025,4);tube([(-.30,.63,0),(0,.94,0),(.30,.63,0)],.018,4,8)

def garden_fixtures():
    placed(well,(10.2,0,-8))
    for z in [-3.8,.5]:
        if z<0:
            box((10.2,.78,z),(1.55,.15,.85),3,.035)
            for x in [9.55,10.85]:
                for dz in [-.30,.30]:box((x,.39,z+dz),(.09,.78,.09),3,.015)
            for i in range(5):box((9.70+i*.24,.91,z),(.18,.08,.25),13,.012)
            profile((10.7,.87,z+.08),[(0,.13),(.28,.18),(.32,.18)],0,16)
            for j in range(6):leaf((10.7,1.17,z+.08),(10.7+math.cos(j)*.22,1.4,z+.08+math.sin(j)*.22),.09,8)
        else:placed(crate,(10.2,0,z),scale=1.15)
    # A real wheelbarrow, not a solid cube prop.
    for x in [-.47,.47]:tube([(31+x,.30,13),(31+x,.52,14.9)],.045,3,8)
    for x in [-.40,.40]:tube([(31+x,0,14),(31+x,.50,14.1)],.035,3,8)
    for j in range(7):box((30.55+j*.15,.50,13.8),(.13,.06,1.2),3,.01)
    for x in [30.48,31.52]:box((x,.72,13.8),(.06,.45,1.25),3,.018)
    box((31,.72,13.20),(1.1,.45,.07),3,.018)
    placed(lambda:ring((0,0,0),.30,.055,2,'upright',n=40),(31,.30,12.75),90)
    tube([(30.65,.30,12.75),(31.35,.30,12.75)],.035,4)

def home_jetty():
    start=Vector((0,-1.15,-21));end=Vector((0,.20,-8));delta=end-start
    for j in range(49):
        p=start+delta*(j+.5)/49;box(tuple(p-Vector((0,.06,0))),(2.58,.12,delta.length/49-.014),3,.014)
        for x in [-.98,.98]:rock((x,p.y+.007,p.z),(.014,.007,.014),4,1)
    for side in [-1,1]:
        pts=[]
        for j in range(11):
            p=start+delta*j/10;x=side*1.32
            profile((x,p.y-2.4,p.z),[(0,.105),(3.25,.09),(3.35,.13)],3,12)
            for h in [p.y+.65,p.y+.80]:ring((x,h,p.z),.112,.014,13,n=24)
            pts.append((x,p.y+.9,p.z))
        tube(pts,.045,3,9)
        for a,b in zip(pts,pts[1:]):
            mid=((a[0]+b[0])*.5,(a[1]+b[1])*.5-.16,(a[2]+b[2])*.5)
            tube([a,mid,b],.018,13,7)
    for j in range(15):box((-1.92+j*.273,-1.23,-21),(.255,.16,3.0),3,.014)
    for x in [-1.85,1.85]:
        for z in [-22.2,-19.8]:
            profile((x,-4.4,z),[(0,.15),(3.9,.13),(4.0,.18)],3,12)
            for h in [-.72,-.65,-.58]:ring((x,h,z),.15,.018,13,n=24)

def farm_plot():
    box((0,.012,0),(2.1,.024,2.1),7,.005)
    for side in [-1,1]:
        box((side*1.06,.09,0),(.10,.18,2.20),3,.018)
        box((0,.09,side*1.06),(2.20,.18,.10),3,.018)
    for x in [-1.06,1.06]:
        for z in [-1.06,1.06]:profile((x,0,z),[(0,.085),(.23,.085),(.27,.06)],3,8)

def farm_soil():
    box((0,.045,0),(2.03,.08,2.03),12,.018)
    for row in range(6):
        z=-.84+row*.335;flow([(-.96,.105,z),(-.5,.12,z+.02),(0,.10,z),(.5,.12,z-.02),(.96,.105,z)],.045,12,7)

def turnips(stage):
    for x in [-.48,.48]:
        for z in [-.48,.48]:
            h=.10+stage*.10;s=.13+stage*.075
            if stage==2:
                profile((x,.07,z),[(0,.025),(.07,.14),(.15,.20),(.25,.21),(.30,.17)],10,20)
                profile((x,.25,z),[(0,.206),(.08,.17),(.11,.07)],11,20)
            for j in range(4+stage*2):
                a=j*2.3999;length=.17+stage*.11+(j%2)*.08
                leaf((x,.15,z),(x+math.cos(a)*length,.15+h,z+math.sin(a)*length),s,8 if j%2 else 9)
                tube([(x,.10,z),(x+math.cos(a)*length*.65,.15+h*.65,z+math.sin(a)*length*.65)],.008,9,5)

def hoe():
    tube([(0,-.22,0),(0,.38,0)],.018,3,10)
    box((0,-.24,.09),(.16,.035,.24),14,.012)
    ring((0,-.21,0),.028,.010,4,n=24)

def watering_can():
    profile((0,-.21,.12),[(0,.11),(.03,.14),(.19,.13),(.22,.09)],5,28)
    flow([(.10,-.14,.12),(.24,-.10,.18),(.32,-.02,.24)],.028,5,12)
    rock((.33,-.015,.25),(.05,.025,.05),4)
    tube([(-.10,-.14,.12),(-.20,-.05,.12),(-.13,.06,.12),(0,.06,.12)],.018,4,10)
    tube([(-.10,-.01,.12),(0,.025,.12),(.10,-.01,.12)],.014,4,10)

def orchard_tree():
    flow([(0,0,0),(.16,1,0),(-.12,2.2,0),(0,3.3,0)],.20,3,12,.45)
    for j in range(10):
        a=j*2.3999;end=(math.cos(a)*1.9,2.5+(j%3)*.45,math.sin(a)*1.9)
        flow([(0,1.4+j*.08,0),(end[0]*.5,end[1]-.2,end[2]*.5),end],.075,3,9,.32)
        for k in range(120):
            b=k*2.3999;r=math.sqrt((k%30+.5)/30)*1.15;y=end[1]+(k//30-.9)*.28+.32*math.sqrt(max(0,1-r*r/1.4))
            p=(end[0]+math.cos(b)*r,y,end[2]+math.sin(b)*r)
            leaf((p[0]-.18,p[1]-.07,p[2]),(p[0]+.26,p[1]+.08,p[2]+.10),.16,8 if k%3 else 9)
            if k%7==0:rock((p[0],p[1]-.15,p[2]),(.07,.08,.07),4,1)

def home_bed():
    for x in [-.94,.94]:
        for z in [-1.10,1.10]:profile((x,0,z),[(0,.07),(.45,.07),(.52,.10),(1.03,.07),(1.13,.11)],3,12)
    box((0,.40,0),(2.02,.20,2.4),3,.05)
    rock((0,.61,0),(1.0,.18,1.16),10,3)
    rock((0,.78,.19),(1.0,.15,.94),6,3)
    for x in [-.5,.5]:rock((x,.83,-.77),(.44,.16,.31),10,3)
    for x in [-.7,-.35,0,.35,.7]:tube([(x,.52,-1.17),(x,1.14,-1.17)],.035,3,8)
    flow([(-.96,1.04,-1.18),(-.5,1.25,-1.18),(0,1.32,-1.18),(.5,1.25,-1.18),(.96,1.04,-1.18)],.07,3,10)
    for x in [-.95,.95]:tube([(x,.55,-.3),(x,.65,1.05)],.018,4)
    # Quilting seams follow the cloth surface rather than a flat billboard.
    for z in [-.45,-.1,.25,.60,.95]:tube([(-.91,.79,z),(-.45,.89,z),(0,.92,z),(.45,.89,z),(.91,.79,z)],.007,10,5)

def home_sofa():
    for x in [-1.05,1.05]:
        for z in [-.33,.33]:profile((x,0,z),[(0,.06),(.22,.08)],3,10)
    rock((0,.35,0),(1.22,.24,.48),6,3)
    rock((0,.77,.36),(1.22,.46,.19),6,3)
    for x in [-1.19,1.19]:rock((x,.61,0),(.19,.32,.53),6,3)
    for x in [-.73,0,.73]:
        rock((x,.58,-.03),(.35,.13,.38),10,3)
        tube([(x-.31,.67,-.39),(x,.70,-.40),(x+.31,.67,-.39)],.008,4,6)
    for x in [-.85,.85]:rock((x,.86,.15),(.25,.27,.12),6,3)

def home_desk():
    box((0,.86,0),(2.15,.12,.87),3,.035)
    for x in [-.88,.88]:
        for z in [-.29,.29]:profile((x,0,z),[(0,.055),(.08,.08),(.19,.05),(.60,.055),(.78,.09)],3,12)
    for x in [-.7,0,.7]:
        box((x,.68,0),(.65,.23,.72),3,.025);ring((x,.70,-.38),.045,.010,4,'upright',n=24)
    box((0,.935,0),(1.12,.018,.60),10,.008)
    for j in range(7):tube([(-.46+j*.14,.947,-.22),(-.36+j*.14,.948,.22)],.005,4,5)
    for j in range(4):box((.76,.96+j*.035,.12),(.26,.027,.32),2 if j%2 else 13,.006)

def home_bookcase():
    for x in [-.77,.77]:box((x,1.07,0),(.10,2.14,.43),3,.02)
    box((0,1.07,.20),(1.54,2.14,.055),3,.01)
    for h in [.10,.59,1.08,1.57,2.12]:box((0,h,0),(1.7,.09,.46),3,.02)
    for row in range(4):
        for j in range(9):
            x=-.67+j*.16;h=.30+(j%3)*.035;y=.16+row*.49+h*.5
            box((x,y,-.025),(.11,h,.27),[2,6,10,13][(j+row)%4],.009)
            for dy in [-h*.35,h*.35]:box((x,y+dy,-.17),(.09,.012,.01),4,.001)
    for x in [-.79,.79]:profile((x,0,0),[(0,.07),(.15,.085)],3,10)
    box((0,2.19,0),(1.84,.12,.51),3,.025)

def cabinet(sink=False):
    box((0,.43,0),(1.42,.81,.70),3,.028)
    for x in [-.35,.35]:
        box((x,.43,-.367),(.65,.69,.035),10,.018)
        box((x,.43,-.39),(.50,.53,.025),3,.016)
        tube([(x+.20,.37,-.42),(x+.20,.51,-.42)],.014,4,8)
    for x in [-.60,.60]:
        for z in [-.26,.26]:profile((x,0,z),[(0,.055),(.13,.07)],3,10)
    if sink:
        for x in [-.59,.59]:box((x,.89,0),(.35,.10,.78),0,.025)
        for z in [-.30,.30]:box((0,.89,z),(.87,.10,.18),0,.02)
        profile((0,.72,0),[(0,.20),(.04,.27),(.13,.35),(.17,.38)],14,36)
        flow([(.35,.93,.18),(.35,1.22,.18),(.15,1.26,.18),(.12,1.12,.18)],.018,4,10)
        for x in [.24,.46]:profile((x,.94,.18),[(0,.03),(.06,.035)],4,10)
    else:box((0,.89,0),(1.52,.11,.80),0,.025)

def stove():
    box((0,.45,0),(1.42,.89,.70),0,.035);box((0,.92,0),(1.52,.10,.80),14,.025)
    box((0,.43,-.36),(1.12,.50,.05),15,.025)
    for x in [-.48,.48]:box((x,.43,-.40),(.04,.42,.025),4,.005)
    tube([(-.48,.63,-.44),(.48,.63,-.44)],.025,4,10)
    for x in [-.42,0,.42]:rock((x,.78,-.40),(.045,.045,.025),4,2)
    for x in [-.38,.38]:
        for z in [-.20,.20]:
            ring((x,.98,z),.14,.018,15,n=28);ring((x,.98,z),.075,.015,4,n=24)
    for x in [-.62,.62]:
        for z in [-.26,.26]:profile((x,0,z),[(0,.06),(.12,.06)],14,10)

def fridge():
    box((0,.75,0),(.78,1.5,.70),10,.055)
    for h,hh in [(.46,.83),(1.19,.55)]:
        box((0,h,-.37),(.72,hh,.065),0,.03)
        tube([(.24,h-.15,-.43),(.24,h+.15,-.43)],.021,4,10)
    for x in [-.30,.30]:
        for z in [-.25,.25]:profile((x,0,z),[(0,.045),(.08,.045)],14,10)

def coffee_table():
    box((0,.50,0),(1.50,.12,.85),3,.07)
    for x in [-.60,.60]:
        for z in [-.28,.28]:profile((x,0,z),[(0,.04),(.10,.07),(.20,.045),(.44,.06)],3,12)
    box((0,.23,0),(1.32,.06,.66),3,.025)
    for x in [-.30,.30]:profile((x,.56,0),[(0,.11),(.015,.11)],4,24)

def rug():
    box((0,.014,0),(3,.028,1.8),6,.012)
    for side in [-1,1]:
        box((side*1.38,.030,0),(.12,.012,1.70),10,.002)
        box((0,.030,side*.78),(2.85,.012,.12),10,.002)
    for x in [-1.2,1.2]:
        for z in [-.60,.60]:
            ring((x,.039,z),.10,.007,4,n=24)
    for side in [-1,1]:
        for j in range(30):tube([(side*1.48,.012,-.84+j*.058),(side*1.56,.01,-.84+j*.058)],.005,10,5)

def install(api):
    global API
    API=api
    for key in ['box','profile','tube','ring','leaf','rock','flow','lantern','crate','mesh','U']:globals()[key]=api[key]
    builders={'HomeLand':home_land,'CottageShell':cottage_shell,'HomeLandscaping':home_details,'GardenFixtures':garden_fixtures,'FarmPlot':farm_plot,'FarmSoil':farm_soil,
              'Turnip0':lambda:turnips(0),'Turnip1':lambda:turnips(1),'Turnip2':lambda:turnips(2),'GardenHoe':hoe,'GardenWateringCan':watering_can,'OrchardTree':orchard_tree,'HomeBed':home_bed,'HomeSofa':home_sofa,'HomeDesk':home_desk,'HomeJetty':home_jetty,
              'HomeBookcase':home_bookcase,'HomeCabinet':cabinet,'HomeSink':lambda:cabinet(True),'HomeStove':stove,'HomeFridge':fridge,'HomeCoffeeTable':coffee_table,'HomeRug':rug}
    api['EXTRA_BUILDERS'].update({name:reflected(fn) for name,fn in builders.items()})
