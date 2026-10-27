"""A weathered coastal shelter using the existing V035 atlas and geometry helpers."""
from pathlib import Path
import hashlib,json,math,runpy
import bpy,bmesh
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
if ROOT.name=='CoastRepair':PROJECT=ROOT.parents[2]
else:PROJECT=ROOT
STAGE=ROOT
OUT=STAGE/'Assets/Resources/FishingV040'
ART=STAGE/'ArtSource/Feedback-20261026/CoastCamp'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
# This process owns a fresh background Blender scene, never the user's open file.
bpy.ops.wm.read_factory_settings(use_empty=True)
kit=runpy.run_path(str(PROJECT/'Tools/Art/build_v035_assets.py'))
g=kit['build'].__globals__;g['setup']();g['OUT']=OUT;g['SOURCE']=ART
box,mesh,tube,ring=g['box'],g['mesh'],g['tube'],g['ring']

def shelter():
    for x in (-2.0,2.0):
        for z,top in ((-1.15,3.50),(1.15,2.98)):
            tube([(x-.025,-1.1,z),(x+.02,.6,z-.025),(x-.035,1.8,z+.025),(x,top+.09,z)],.105,3,10,.82)
            for y in (2.63,2.68,2.73):ring((x,y,z),.117,.009,13,n=12)
            direction=1 if x<0 else -1
            tube([(x,top-.64,z),(x+direction*.58,top-.12,z)],.052,3,8)
    for z,y in ((-1.15,3.51),(1.15,2.99)):
        box((0,y,z),(4.42,.14,.17),3,.018)
    for x in (-1.75,1.75):
        tube([(x,3.56,-1.43),(x,2.98,1.47)],.067,3,8)
    for i in range(10):
        x=-2.28+i*.456;w=.445
        points=[]
        for z in (-1.50,1.54):
            y=3.60-(z+1.5)*.195+.018*math.sin(i*1.7+z)
            points.extend([(x,y,z),(x+w,y+.012*math.sin(i),z)])
        points += [(x,y-.055,z) for x,y,z in list(points)]
        mesh(points,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],3)
    # A curved canvas edge and stitched seams break the rigid plank silhouette.
    points=[]
    for row in range(7):
        t=row/6;z=1.41+t*.72
        for col in range(21):
            s=col/20;x=(s-.5)*4.45
            y=3.04-t*.44-.11*math.sin(math.pi*s)*math.sin(math.pi*t)+.022*math.sin(s*math.pi*10)*t
            points.append((x,y,z))
    faces=[]
    for row in range(6):
        for col in range(20):
            a=row*21+col;faces.append((a,a+1,a+22,a+21))
    mesh(points,faces,6,True)
    for col in (0,4,8,12,16,20):
        seam=[points[row*21+col] for row in range(7)]
        tube([(x,y+.009,z) for x,y,z in seam],.006,13,5)
    for x in (-2.18,2.18):
        tube([(x,2.69,2.13),(x,2.98,1.16)],.035,3,8)
        for y in (2.67,2.70):ring((x,y,2.13),.042,.006,13,n=10)
    # A back rail and its uneven slats read as a repaired windbreak, not a wall.
    for y in (.65,1.32):box((0,y,-1.16),(3.9,.09,.10),3,.012)
    for i in range(13):
        x=-1.82+i*.30;h=.9+.16*math.sin(i*2.4)
        box((x,.88,-1.17),(.19,h,.055),3,.009,yaw=math.sin(i)*2)

g['EXTRA_BUILDERS']={'IsletCamp':shelter}
obj=g['build']('IsletCamp');obj.hide_render=False
obj.data.calc_loop_triangles()
assert len(obj.data.loop_triangles)<8000
assert all(t.area>1e-10 for t in obj.data.loop_triangles)
assert len(obj.data.materials)==1 and obj.data.uv_layers
assert all(0<=n<=1 for item in obj.data.uv_layers.active.data for n in item.uv)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'islet-camp-v001.blend'))
scene=bpy.context.scene
camera=bpy.data.objects.new('CAM-camp-review',bpy.data.cameras.new('CAM-camp-review'));scene.collection.objects.link(camera)
camera.location=(7,-10,6);target=Vector((0,0,1.5));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=48;scene.camera=camera
sun=bpy.data.objects.new('LGT-camp-sun',bpy.data.lights.new('LGT-camp-sun','SUN'));scene.collection.objects.link(sun);sun.rotation_euler=(.5,-.45,-.4);sun.data.energy=2.3;sun.data.angle=.08
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1280;scene.render.resolution_y=960;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True;scene.render.filepath=str(ART/'camp-review.png');bpy.ops.render.render(write_still=True)
report={'triangles':len(obj.data.loop_triangles),'materials':len(obj.data.materials),'uv':True,'zeroAreaFaces':0,'source':'Project original geometry; existing V035 Coastal atlas','fbxSha256':hashlib.sha256((OUT/'IsletCamp.fbx').read_bytes()).hexdigest(),'dimensionsBlender':list(obj.dimensions),'sceneScope':'Owned background Blender process'}
(ART/'camp-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8')
print('ISLET_CAMP_PASS',json.dumps(report))
