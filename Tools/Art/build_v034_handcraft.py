"""Original harbour craft assets. Run only in a fresh background Blender process."""
from pathlib import Path
import bpy, math, json, hashlib
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FishingV034'
ART=ROOT/'ArtSource/V034'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
assert not bpy.data.filepath,'Use --factory-startup, never an artist scene'
for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
scene.unit_settings.system='METRIC'
parts=[];models=[];manifest=[]
def U(p):return Vector((p[0],p[2],p[1]))

image=bpy.data.images.load(str(OUT/'HandcraftAlbedo.png'))
metal=bpy.data.images.load(str(OUT/'HandcraftMetalRough.png'));metal.colorspace_settings.name='Non-Color'
normal=bpy.data.images.load(str(OUT/'HandcraftNormal.png'));normal.colorspace_settings.name='Non-Color'
mat=bpy.data.materials.new('MAT-Harbour painted wood brass linen');mat.use_nodes=True
nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
tex=nodes.new('ShaderNodeTexImage');tex.image=image;links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
packed=nodes.new('ShaderNodeTexImage');packed.image=metal
separate=nodes.new('ShaderNodeSeparateColor');links.new(packed.outputs['Color'],separate.inputs['Color'])
links.new(separate.outputs['Blue'],bsdf.inputs['Metallic']);links.new(separate.outputs['Green'],bsdf.inputs['Roughness'])
ntex=nodes.new('ShaderNodeTexImage');ntex.image=normal;nmap=nodes.new('ShaderNodeNormalMap');nmap.inputs['Strength'].default_value=.3
links.new(ntex.outputs['Color'],nmap.inputs['Color']);links.new(nmap.outputs['Normal'],bsdf.inputs['Normal'])

def surface(obj,tile,smooth=True):
    if obj.type=='CURVE':
        bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj=bpy.context.object
    obj.name='GEO-'+obj.name
    obj.data.materials.clear();obj.data.materials.append(mat)
    uv=obj.data.uv_layers.new(name='UVMap') if not obj.data.uv_layers else obj.data.uv_layers.active
    bounds=[(min(v.co[a] for v in obj.data.vertices),max(v.co[a] for v in obj.data.vertices)) for a in range(3)]
    for face in obj.data.polygons:
        face.use_smooth=smooth
        axis=max(range(3),key=lambda a:abs(face.normal[a]));axes=[a for a in range(3) if a!=axis]
        for li in face.loop_indices:
            point=obj.data.vertices[obj.data.loops[li].vertex_index].co
            coords=[(point[a]-bounds[a][0])/max(.00001,bounds[a][1]-bounds[a][0]) for a in axes]
            uv.data[li].uv=((tile%4+.045+coords[0]*.91)/4,1-(tile//4+.045+coords[1]*.91)/2)
    parts.append(obj);return obj

def box(name,p,size,tile=0,bevel=.005):
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=U(p));obj=bpy.context.object;obj.name=name
    obj.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Soft hand-finished edges','BEVEL');mod.width=bevel;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
        for face in obj.data.polygons:face.use_smooth=True
        mod=obj.modifiers.new('Weighted broad plank normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    return surface(obj,tile,False)

def path(name,points,radius,tile=1,cyclic=False,sides=6):
    bpy.ops.object.select_all(action='DESELECT')
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=3;curve.bevel_depth=radius;curve.bevel_resolution=2;curve.resolution_u=3
    spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
    for target,p in zip(spline.points,points):target.co=(*U(p),1)
    spline.use_cyclic_u=cyclic;obj=bpy.data.objects.new(name,curve);scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');return surface(bpy.context.object,tile)

def tube(name,a,b,r,tile=3,vertices=20,r2=None):
    bpy.ops.object.select_all(action='DESELECT');a,b=U(a),U(b)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=r if r2 is None else r2,depth=(b-a).length,location=(a+b)*.5)
    obj=bpy.context.object;obj.name=name;obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return surface(obj,tile)

def ell(name,p,size,tile=6):
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=10,radius=1,location=U(p))
    obj=bpy.context.object;obj.name=name;obj.scale=(size[0],size[2],size[1]);return surface(obj,tile)

def oval(name,rx,rz,y,r,tile=1):
    return path(name,[(math.cos(i*math.tau/64)*rx,y,math.sin(i*math.tau/64)*rz) for i in range(64)],r,tile,True)

def finish(name,limit,markers=None):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for key,p in (markers or {}).items():
        marker=bpy.data.objects.new(key,None);scene.collection.objects.link(marker);marker.parent=obj;marker.location=U(p);marker.select_set(True)
    count=sum(len(p.vertices)-2 for p in obj.data.polygons);assert 0<count<limit,(name,count)
    assert obj.data.uv_layers and all(math.isfinite(v) for vertex in obj.data.vertices for v in vertex.co)
    path_out=OUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path_out),use_selection=True,object_types={'MESH','EMPTY'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,path_mode='STRIP')
    manifest.append({'name':name,'triangles':count,'vertices':len(obj.data.vertices),'sha256':hashlib.sha256(path_out.read_bytes()).hexdigest()})
    parts.clear();models.append(obj);obj.hide_render=True;return obj

# A finely finished waist-high cabinet, with real plank seams, brass pins and painted trim.
for i in range(5):box('cedar top plank',(-.414+i*.207,.93,0),(.204,.045,.56),1,.009)
box('rounded teal apron',(0,.855,0),(.98,.105,.50),2,.014)
box('gilt apron line',(0,.885,.255),(.87,.004,.004),3,.001)
for x in [-.435,.435]:
    for z in [-.21,.21]:
        points=[(x*.90,.035,z*.96),(x*.94,.13,z*1.04),(x*.98,.36,z),(x,.67,z),(x,.85,z)]
        path('curved painted timber leg',points,.029,2)
        ell('brass foot',(x*.90,.03,z*.96),(.031,.019,.031),3)
        ell('apron pin',(x,.861,z*1.18),(.007,.007,.004),3)
box('low timber cross brace',(0,.22,0),(.84,.035,.035),0,.005)
for side in [-1,1]:box('side brace',(side*.414,.26,0),(.032,.035,.40),0,.005)
table=finish('CraftTable',18000)

# Woven linen runner. The weave and stitched edges live in the texture, not noisy geometry.
box('linen work mat',(0,.003,0),(.66,.006,.42),5,.004)
for x in [-.305,.305]:path('subtle linen stitched edge',[(x,.007,-.18),(x,.007,.18)],.0009,4)
linen=finish('LinenMat',2500)

# Shallow oval wicker sieve. The centre stays open and the hands use its slim side grips.
oval('upper continuous willow rim',.158,.116,.019,.0085,1)
oval('lower basket rim',.143,.101,-.016,.004,0)
for i in range(32):
    a=i*math.tau/32
    path('fine sloping wicker rib',[(math.cos(a)*.143,-.015,math.sin(a)*.101),(math.cos(a)*.151,.005,math.sin(a)*.109),(math.cos(a)*.158,.020,math.sin(a)*.116)],.0022,1)
for i in range(-9,10):
    z=i*.0102;x=.141*math.sqrt(max(0,1-z*z/(.099*.099)))
    path('woven bottom warp',[(-x,-.015,z),(x,-.015,z)],.00135,4)
for i in range(-12,13):
    x=i*.0106;z=.099*math.sqrt(max(0,1-x*x/(.141*.141)))
    path('woven bottom weft',[(x,-.012,-z),(x,-.012,z)],.00135,4)
for s in [-1,1]:
    path('carved oval handle',[(s*.148,.016,-.038),(s*.187,.020,-.034),(s*.202,.022,0),(s*.187,.020,.034),(s*.148,.016,.038)],.006,1)
    tube('indigo linen grip',(s*.193,.022,-.025),(s*.193,.022,.025),.008,2)
    for j in range(6):path('linen grip stitching',[(s*.199,.025,-.019+j*.007),(s*.199,.030,-.016+j*.007)],.0007,4)
sieve=finish('SieveFrame',16000,{'SieveGripA':(.193,.022,0),'SieveGripB':(-.193,.022,0)})

# Small shell with a scalloped outline and continuous curved ribs.
verts=[];faces=[];segments=18;rings=7
for ring in range(rings+1):
    r=ring/rings
    for k in range(segments+1):
        a=math.pi*(.10+.80*k/segments);radius=r*.026*(1+.06*math.cos(k*math.pi))
        verts.append(U((math.cos(a)*radius,.003+math.sin(r*math.pi)*.008+math.cos(k*math.pi)*.0007,math.sin(a)*radius-.008)))
for r in range(rings):
    for k in range(segments):
        p=r*(segments+1)+k;faces.append((p,p+1,p+segments+2,p+segments+1))
mesh=bpy.data.meshes.new('continuous scalloped sea shell');mesh.from_pydata(verts,[],faces);mesh.update()
obj=bpy.data.objects.new('rose pearl sea shell',mesh);scene.collection.objects.link(obj);surface(obj,7)
for k in range(1,segments,2):
    path('fine shell ridge',[(math.cos(math.pi*(.10+.80*k/segments))*r*.025,.004+math.sin(r*math.pi)*.008,math.sin(math.pi*(.10+.80*k/segments))*r*.025-.008) for r in [.15,.3,.5,.7,.9,1]],.00045,6)
shell=finish('SieveShell',4000)

verts=[U((0,.008,0))];faces=[]
for ring in range(1,6):
    r=ring/5
    for k in range(40):
        a=k*math.tau/40;verts.append(U((math.cos(a)*.135*r,.002+.006*(1-r*r),math.sin(a)*.092*r)))
for k in range(40):faces.append((0,1+k,1+(k+1)%40))
for ring in range(4):
    for k in range(40):
        a=1+ring*40+k;b=1+ring*40+(k+1)%40;faces.append((a,a+40,b+40,b))
mesh=bpy.data.meshes.new('shallow fine sand bed');mesh.from_pydata(verts,[],faces);mesh.update()
obj=bpy.data.objects.new('shallow siftable sand bed',mesh);scene.collection.objects.link(obj);surface(obj,4)
for k in range(34):
    a=k*2.399963;r=.018+(k%7)*.013
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=0,radius=1,location=U((math.cos(a)*r,.008+(k%3)*.001,math.sin(a)*r*.67)))
    grain=bpy.context.object;grain.name='warm fine sand';grain.scale=(.0018+(k%3)*.0005,.0016,.0013);surface(grain,1)
sand=finish('SieveSand',15000)

# A miniature open brass reel. No solid disc: the spindle, rope and handle stay readable.
box('painted reel foot',(0,.013,0),(.225,.026,.16),2,.010)
box('brass foot inset',(0,.029,0),(.19,.005,.126),3,.003)
for x in [-.089,.089]:ell('brass mounting pin',(x,.032,.045),(.004,.003,.004),3)
tube('brass spindle',(0,.134,-.062),(0,.134,.035),.009,3)
# The visual reel's turning axis faces +Z; the support is a small rear arch.
path('rear pedestal arch',[(-.083,.031,-.047),(-.065,.10,-.047),(0,.142,-.047),(.065,.10,-.047),(.083,.031,-.047)],.008,2)
box('maker nameplate',(0,.033,.064),(.078,.001,.020),6,.002)
body=finish('WinderBody',9000,{'WinderSupportA':(.08,.14,.055),'WinderSupportB':(-.08,.14,.055)})

for z in [-.024,.024]:
    path('open reel rim',[(math.cos(k*math.tau/64)*.071,math.sin(k*math.tau/64)*.071,z) for k in range(64)],.0035,3,True)
    for a in [0,math.tau/3,math.tau*2/3]:path('slender reel spoke',[(0,0,z),(math.cos(a)*.070,math.sin(a)*.070,z)],.0034,2)
tube('rope spindle',(0,0,-.027),(0,0,.027),.024,1)
box('tapered brass crank',(.046,0,.033),(.102,.010,.007),3,.003)
tube('crank pin',(.100,0,.031),(.100,0,.063),.004,3)
ell('polished wooden knob',(.100,0,.073),(.011,.016,.019),0)
crank=finish('WinderCrank',10000,{'WinderHandle':(.100,0,.073)})

for row in range(2):
    for k in range(10):
        z=-.020+k*.0044;r=.028+row*.0044
        path('continuous wound flax',[(math.cos(a*math.tau/24)*r,math.sin(a*math.tau/24)*r,z+(a/24)*.002) for a in range(25)],.0024,4)
coil=finish('WinderCoil',16000)

# A small engraved star charm adds a maritime storybook signature, kept away from the grip.
points=[]
for i in range(10):
    a=math.pi/2+i*math.pi/5;r=.010 if i%2==0 else .0043;points.append((math.cos(a)*r,.001,math.sin(a)*r))
verts=[U(p) for p in points]+[U((0,.001,0))]
mesh=bpy.data.meshes.new('brass five point charm');mesh.from_pydata(verts,[],[(10,i,(i+1)%10) for i in range(10)]);mesh.update()
obj=bpy.data.objects.new('tiny engraved star',mesh);scene.collection.objects.link(obj);surface(obj,3,False)
charm=finish('StarCharm',1000)

ART.joinpath('handcraft-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
ART.joinpath('BUILD_NOTES.md').write_text('Original V034 harbour handcraft assets. Metres; Unity X right, Y up, +Z toward the user. Single packed PBR atlas, bevelled wood and continuous weave. No third-party game assets copied. CraftTable is 1.03m wide and 0.95m tall; SieveFrame is 0.42m wide including handles; WinderCrank is 0.14m diameter with a 0.10m crank. Rotor pivot is its local origin. Unity animates mesh transforms, not Blender-only procedural materials.\n',encoding='utf-8')

# Preserve portable models in a clean layout and an editable engineering scene.
for index,obj in enumerate(models):obj.hide_render=False;obj.location=U((index*1.3,0,0))
bpy.ops.object.select_all(action='DESELECT')
for obj in models:obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(ART/'harbour-handcraft.glb'),export_format='GLB',use_selection=True,export_apply=True,export_animations=False)

# An honest asset review, without a UI plate or foreground hands hiding the forms.
for obj in models:obj.hide_render=True
table.hide_render=False;table.location=Vector((0,0,0))
linen.hide_render=False;linen.location=U((0,.958,0))
sieve.hide_render=False;sieve.location=U((-.22,1.015,.04))
sand.hide_render=False;sand.location=sieve.location.copy()
shell.hide_render=False;shell.location=sieve.location+U((.018,-.009,.008))
body.hide_render=False;body.location=U((.245,.962,-.025))
crank.hide_render=False;crank.location=body.location+U((0,.134,0))
coil.hide_render=False;coil.location=crank.location.copy()
charm.hide_render=False;charm.location=U((-.08,.966,.13))
scene.world.color=(.15,.18,.20)
for name,pos,power,size,color in [('warm key',(-2,3,3),450,3,(1,.83,.65)),('sky fill',(2,2,0),180,2,(.65,.80,1)),('edge light',(0,3,-2),250,2,(1,.92,.78))]:
    data=bpy.data.lights.new('LGT-'+name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    light=bpy.data.objects.new('LGT-'+name,data);scene.collection.objects.link(light);light.location=U(pos);light.rotation_euler=(U((0,.65,0))-light.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('CAM-asset review',bpy.data.cameras.new('CAM-asset review'));scene.collection.objects.link(camera)
camera.location=U((1.35,1.6,1.8));camera.rotation_euler=(U((0,.50,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.8;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
scene.view_settings.view_transform='AgX'
scene.render.filepath=str(ART/'handcraft-model-review.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'harbour-handcraft-v3.blend'))
bpy.ops.render.render(write_still=True)
print('[V034-ART] PASS '+json.dumps(manifest),flush=True)
