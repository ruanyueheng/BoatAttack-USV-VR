"""Original ivory enamel, sage and brushed-gold starlight watering can, metres."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
# Export only this authored asset, even when Blender's startup cube is selected.
for obj in bpy.data.objects:obj.select_set(False)
OUT=ROOT/'Assets/Resources/FishingV036'
ART=ROOT/'ArtSource/V037'
ART.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('V036 starlight garden watering can');bpy.context.window.scene=scene
parts=[]
def U(p):return Vector((p[0],-p[2],p[1]))
def material(name,color,metal=0,rough=.3):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;p.inputs['Coat Weight'].default_value=.26 if metal==0 else .08
    m.diffuse_color=(*color,1);return m
ivory=material('V036 Ivory enamel',(.88,.84,.71),0,.25)
gold=material('V036 Brushed pale gold',(.59,.42,.16),1,.32)
sage=material('V036 Sea sage enamel',(.17,.34,.32),0,.32)
inside=material('V036 Glazed inner bowl',(.30,.40,.37),0,.28)
def mesh(name,points,faces,mat):
    data=bpy.data.meshes.new(name);data.from_pydata([U(p) for p in points],[],faces);data.update()
    obj=bpy.data.objects.new('GEO-'+name,data);scene.collection.objects.link(obj);obj.data.materials.append(mat)
    for p in data.polygons:p.use_smooth=True
    parts.append(obj);return obj
def tube(name,points,radii,mat,n=12):
    v=[];f=[]
    for i,p in enumerate(points):
        p=Vector(p);d=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        d.normalize();a=d.cross(Vector((0,1,0)))
        if a.length<.01:a=d.cross(Vector((1,0,0)))
        a.normalize();b=d.cross(a).normalized()
        for j in range(n):v.append(tuple(p+(a*math.cos(j*math.tau/n)+b*math.sin(j*math.tau/n))*radii[i]))
    for i in range(len(points)-1):
        for j in range(n):a=i*n+j;b=i*n+(j+1)%n;f.append((a,b,b+n,a+n))
    return mesh(name,v,f,mat)
def ring(name,y,r,mat,center=(0,.12),thickness=.004):
    pts=[(center[0]+math.cos(a)*r,y,center[1]+math.sin(a)*r) for a in [j*math.tau/64 for j in range(65)]]
    return tube(name,pts,[thickness]*len(pts),mat,8)
profile=[(-.218,.066),(-.211,.095),(-.186,.119),(-.14,.139),(-.09,.144),(-.047,.128),(-.013,.101),(.008,.092)]
v=[];f=[];n=64
for y,r in profile:
    for j in range(n):a=j*math.tau/n;v.append((math.cos(a)*r,y,.12+math.sin(a)*r*.88))
for i in range(len(profile)-1):
    for j in range(n):a=i*n+j;b=i*n+(j+1)%n;f.append((a,b,b+n,a+n))
body=mesh('Pear shaped hollow porcelain body',v,f,ivory)
solid=body.modifiers.new('Thin glazed shell','SOLIDIFY');solid.thickness=.005;solid.offset=-1
bevel=body.modifiers.new('Soft rolled body edges','BEVEL');bevel.width=.002;bevel.segments=2
ring('Mouth gold lip',.008,.092,gold,thickness=.003)
ring('Small sage foot',-.213,.082,sage,thickness=.008)
ring('Brass foot piping',-.219,.084,gold,thickness=.002)
inner=mesh('Visible glazed interior',[(0,-.212,.12)]+[(math.cos(j*math.tau/n)*.088,-.205,.12+math.sin(j*math.tau/n)*.078) for j in range(n)],[(0,(j+1)%n+1,j+1) for j in range(n)],inside)
# Hand mount remains exactly at the already-calibrated CanPalm point.
handle=[(-.135,-.07,.12),(-.125,.045,.12),(-.09,.120,.12),(-.025,.120,.12),(.06,.120,.12),(.12,.045,.12),(.13,-.07,.12)]
tube('Satin gold arched grip',handle,[.008]*len(handle),gold,16)
tube('Ivory handle comfort sleeve',handle[2:5],[.010]*3,ivory,16)
spout=[(.105,-.135,.16),(.15,-.12,.183),(.205,-.085,.21),(.26,-.045,.232),(.31,-.02,.245),(.33,-.015,.25)]
tube('Long curved ivory spout',spout,[.032,.028,.025,.023,.022,.026],ivory,24)
tube('Brass rose nozzle',spout[-2:],[.024,.030],gold,24)
axis=(Vector(spout[-1])-Vector(spout[-2])).normalized();a=axis.cross(Vector((0,1,0))).normalized();b=axis.cross(a)
rose=[tuple(Vector(spout[-1])+axis*.001)]+[tuple(Vector(spout[-1])+axis*.001+(a*math.cos(j*math.tau/24)+b*math.sin(j*math.tau/24))*.024) for j in range(24)]
mesh('Sage sprinkler rose',rose,[(0,j+1,(j+1)%24+1) for j in range(24)],sage)
def star(name,x,y,z,r):
    pts=[(x,y,z)]+[(x+math.sin(j*math.tau/10)*(r if j%2==0 else r*.4),y+math.cos(j*math.tau/10)*(r if j%2==0 else r*.4),z) for j in range(10)]
    return mesh(name,pts,[(0,j+1,(j+1)%10+1) for j in range(10)],gold)
# Front ornament: a crescent and sparse stars; subtle relief rather than printed text.
pts=[(-.026+math.cos(a)*.026,-.105+math.sin(a)*.031,-.006) for a in [math.radians(55+j*250/32) for j in range(33)]]
tube('Crescent inlay',pts,[.0025]*len(pts),gold,8)
star('Guiding star',.026,-.095,-.008,.017);star('Tiny star',.063,-.13,-.001,.008)
for side in [-1,1]:
    branch=[(side*(.035+j*.009),-.18+j*.009,.014-j*.001) for j in range(6)]
    tube('Laurel gold stem '+str(side),branch,[.0013]*6,gold,6)
    for j in range(1,5):
        x,y,z=branch[j];mesh('Sage laurel leaf',[(x,y,z),(x+side*.012,y+.013,z-.001),(x+side*.018,y+.009,z),(x+side*.006,y-.002,z)],[(0,1,2,3)],sage)
def front(x,y):
    radius=profile[0][1]
    for (a,ra),(b,rb) in zip(profile,profile[1:]):
        if a<=y<=b:radius=ra+(rb-ra)*(y-a)/(b-a);break
    return .12-.88*math.sqrt(max(.0001,radius*radius-x*x))-.004
for o in parts:
    if any(k in o.name for k in ['Crescent','Guiding star','Tiny star','Laurel','laurel']):
        for vertex in o.data.vertices:vertex.co.y=-front(vertex.co.x,vertex.co.z)
    o.select_set(False)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=body
bpy.ops.object.convert(target='MESH')
bpy.ops.object.join();can=bpy.context.object;can.name='GEO-StarlightWateringCan'
for p in can.data.polygons:p.use_smooth=True
bpy.ops.export_scene.fbx(filepath=str(OUT/'StarlightWateringCan.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True)
bpy.ops.export_scene.gltf(filepath=str(ART/'starlight-watering-can.glb'),export_format='GLB',use_selection=True)
can.data.calc_loop_triangles();assert 1000<len(can.data.loop_triangles)<20000
scene.world=bpy.data.worlds.new('V036 soft pastel studio');scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.32,.40,.45,1);bg.inputs['Strength'].default_value=.6
center=U((.06,-.08,.12))
for name,pos,power in [('Key',(-.35,.7,-.5),40),('Rim',(.4,.45,.55),55),('Fill',(.5,.25,-.4),20)]:
    data=bpy.data.lights.new('LGT-'+name,'AREA');data.energy=power;data.shape='DISK';data.size=.45
    light=bpy.data.objects.new('LGT-'+name,data);scene.collection.objects.link(light);light.location=U(pos);light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('CAM-Can',bpy.data.cameras.new('CAM-Can'));scene.collection.objects.link(camera);camera.location=U((.63,.31,-.60));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.62;scene.camera=camera
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.film_transparent=True
scene.render.filepath=str(ART/'starlight-watering-can-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'starlight-watering-can-v037.blend'));bpy.ops.render.render(write_still=True)
report={'original':True,'palette':'ivory enamel, pale brass, sea sage','triangles':len(can.data.loop_triangles),'grip':[-.025,.120,.12],'spout':[.33,-.015,.25],'opening':[0,.005,.12]}
(ART/'watering-can-model.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print('V036 CAN PASS',json.dumps(report))
