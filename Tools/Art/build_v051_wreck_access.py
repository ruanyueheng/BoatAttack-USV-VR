from pathlib import Path
import bpy,math,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FishingV051/WreckAccess.fbx'
SOURCE=ROOT/'ArtSource/V051/WreckAccess.blend'
# Isolated --factory-startup process only; the user's live Blender is never touched.
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
parts=[]
# Unity's FBX importer mirrors X when converting the right-handed export.
def unity(p):return Vector((-p[0],-p[2],p[1]))
def box(name,center,size,color):
    bpy.ops.mesh.primitive_cube_add(size=1,location=unity(center));o=bpy.context.object;o.name=name
    o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    attr=o.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    for d in attr.data:d.color=(*color,1)
    parts.append(o);return o
wood=(.30,.18,.085);light=(.43,.29,.15);rope=(.17,.11,.055)
start=(-11.8,.02,2.5);rise=2.65;run=5.7;width=1.8;steps=19
for i in range(steps):
    x=start[0]+run*(i+.5)/steps;y=start[1]+rise*(i+1)/steps
    box('Boarding tread %02d'%i,(x,y-.065,start[2]),(run/steps+.025,.13,width),light if i%3 else wood)
    for z in (-.55,.55):box('Tread grain',(x+.025,y+.002,start[2]+z),(.035,.006,.53),wood)
    if i%4==0:
        for z in (-width/2,width/2):box('Stair upright',(x,(y+.92)*.5,start[2]+z),(.08,y+.92,.08),wood)
# Bridge over the existing gunwale. The inner steps descend to the visible 3.24 m deck.
bridge_start=start[0]+run;bridge_end=-3.75;top=start[1]+rise
for i in range(9):
    x=bridge_start+(bridge_end-bridge_start)*(i+.5)/9
    box('Gunwale bridge plank',(x,top-.065,start[2]),((bridge_end-bridge_start)/9+.015,.13,width),light if i%2 else wood)
for i in range(4):
    x=-3.75+.27*(i+.5);y=top-(i+1)*.075
    box('Inner deck descent',(x,y-.065,start[2]),(.29,.13,width),light)
# Two continuous handrails; no bars cross either walkable opening.
def beam(name,a,b,thickness,color):
    a=unity(a);b=unity(b);d=b-a;bpy.ops.mesh.primitive_cube_add(size=1,location=(a+b)*.5);o=bpy.context.object;o.name=name
    o.dimensions=(thickness,thickness,d.length);o.rotation_euler=d.to_track_quat('Z','Y').to_euler();bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    attr=o.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    for v in attr.data:v.color=(*color,1)
    parts.append(o)
for z in (-width/2,width/2):
    beam('Sloping wooden handrail',(start[0],start[1]+1,start[2]+z),(bridge_start,top+1,start[2]+z),.065,wood)
    beam('Bridge wooden handrail',(bridge_start,top+1,start[2]+z),(bridge_end,top+1,start[2]+z),.065,wood)
    beam('Stair stringer',(start[0],start[1]-.12,start[2]+z*.86),(bridge_start,top-.12,start[2]+z*.86),.12,wood)
    for x in (bridge_start,bridge_end):box('Bridge upright',(x,top+.45,start[2]+z),(.08,1.04,.08),wood)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=bpy.context.object;obj.name='WreckAccess'
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
# No modifiers or generated cameras/lights enter the runtime asset.
assert len(obj.data.polygons)>0 and all(p.area>1e-10 for p in obj.data.polygons)
SOURCE.parent.mkdir(parents=True,exist_ok=True);OUT.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
bpy.ops.export_scene.fbx(filepath=str(OUT),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False)
report=dict(steps=steps,rise=rise,run=run,width=width,riser=rise/steps,tread=run/steps,triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),vertices=len(obj.data.vertices),fbx=str(OUT),source=str(SOURCE))
(ROOT/'Logs/WreckInspection').mkdir(parents=True,exist_ok=True)
(ROOT/'Logs/WreckInspection/stair-model.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
