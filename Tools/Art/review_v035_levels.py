"""Assemble the authored parts for full level review, without changing the export gallery."""
import bpy,math
from mathutils import Vector
ns=bpy.app.driver_namespace['v035'];U=ns['U'];models=ns['MODELS']

def review(level):
    scene=bpy.context.scene
    old=bpy.data.collections.get('V035 level review')
    if old:
        for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
        bpy.data.collections.remove(old)
    collection=bpy.data.collections.new('V035 level review');scene.collection.children.link(collection)
    for o in models.values():o.hide_render=True
    for o in list(scene.objects):
        if o.name.startswith(('LGT-V035','CAM-V035')):bpy.data.objects.remove(o,do_unlink=True)
    def part(name,p=(0,0,0),scale=1,yaw=0):
        o=bpy.data.objects.new('REVIEW-'+name,models[name].data);collection.objects.link(o);o.location=U(p);o.rotation_euler.z=-math.radians(yaw);o.scale=Vector((scale,)*3);return o
    part(['SanctuaryWind','SanctuaryGrove','SanctuaryDrydock','SanctuaryStars'][level])
    if level==0:
        for j in range(3):part('WindBridge'+str(j))
        part('GiantBell',(0,7.8,23))
    if level==1:
        for j,p in enumerate([(-10,-.1,1),(9,-.1,-5),(-9,-.1,15),(9,-.1,17)]):part('AncientTree',p,1.2,j*67)
        for p in [(-6,0,-5),(-6,0,8),(6,0,8)]:part('PuzzlePlinth1',p);part('MirrorHead',(p[0],1.5,p[2]))
        part('RootBridge',(6,0,13.8));part('LivingFountain',(6,0,21));part('MangroveTree',(6,0,23.5),.55)
    if level==2:
        part('CargoCrate',(-2,-1.8,-6));part('PuzzlePlinth2',(4.2,1.8,5));part('FloatGangway',(0,1.8,11));part('CaptainChart',(0,1.8,18))
    if level==3:
        part('TelescopeBase',(0,3.6,10));part('TelescopeMount',(0,5.7,10));o=part('TelescopeTube',(0,5.7,10));o.rotation_euler.x=math.radians(-35)
        for p in [(-8,0,-5),(7,0,-4),(-1,3.6,13)]:part('StarChart',p)
    camera=bpy.data.objects.new('CAM-V035 full level review',bpy.data.cameras.new('CAM-V035 full level review'));collection.objects.link(camera);scene.camera=camera
    eye,target=[((0,1.65,-20),(-2,3,6)),((-13,1.65,-16),(-5,3,3)),((0,3.45,-16),(0,.5,5)),((0,1.65,-12),(0,4.1,10))][level]
    camera.location=U(eye);camera.rotation_euler=(U(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=22;camera.data.clip_start=.01;camera.data.clip_end=300
    for label,kind,p,energy,size,color in [('key','SUN',(15,20,-10),2 if level!=3 else .6,0,(1,.91,.76)),('fill','AREA',(-4,9,-5),1800 if level!=3 else 600,10,(.72,.84,1)),('rim','AREA',(5,11,12),2400 if level!=3 else 1500,8,(1,.87,.67))]:
        data=bpy.data.lights.new('LGT-V035 '+label,kind);o=bpy.data.objects.new(data.name,data);collection.objects.link(o);o.location=U(p);data.energy=energy;data.color=color
        if kind=='AREA':data.shape='DISK';data.size=size
        if kind=='SUN':data.angle=.12
        o.rotation_euler=(U((0,0,3))-o.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=1280;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ns['SOURCE']/('redesign-room-'+str(level)+'.png'))
    bpy.ops.render.render(write_still=True);print(scene.render.filepath)
bpy.app.driver_namespace['v035_review']=review
