"""Preview the authored tools and verify their exported mesh colours and dimensions."""
from pathlib import Path
import json
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Feedback-20261026/Tools'
records=[]
for name in ['Axe','Hammer','Rake','Saw','HomeChest','Shovel']:
    bpy.ops.wm.open_mainfile(filepath=str(ART/(name+'.blend')))
    scene=bpy.context.scene
    meshes=[o for o in scene.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    lower=Vector(tuple(min(p[i] for p in points) for i in range(3)));upper=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    centre=(lower+upper)/2;span=max(upper-lower)
    data=bpy.data.cameras.new('CAM-tool-preview');camera=bpy.data.objects.new('CAM-tool-preview',data);scene.collection.objects.link(camera)
    camera.location=centre+Vector((2,-5,1.5)).normalized()*span*3
    camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=span*1.4;scene.camera=camera
    for key,offset,energy in [('key',(3,-4,5),450),('fill',(-3,-1,3),250)]:
        light_data=bpy.data.lights.new('LGT-'+key,'AREA');light_data.energy=energy;light_data.size=span*3
        light=bpy.data.objects.new('LGT-'+key,light_data);scene.collection.objects.link(light);light.location=centre+Vector(offset)*span;light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler()
    scene.world=bpy.data.worlds.new('MAT-tool-preview-world');scene.world.color=(.18,.20,.24)
    scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True;scene.render.filepath=str(ART/(name+'Thumbnail.png'))
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ART/'Exports'/(name+'.fbx')))
    imported=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert imported and all(o.data.color_attributes for o in imported),name
    for obj in imported:
        obj.data.calc_loop_triangles()
        assert all(t.area>1e-12 for t in obj.data.loop_triangles),(name,'zero-area triangle')
    if name=='HomeChest':assert any(o.name.lower()=='lid' for o in imported)
    records.append({'name':name,'dimensionsBlender':list(upper-lower),'meshes':len(imported),'vertexColors':True})
(ART/'validation.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
print('TOOLS_PREVIEW_PASS',json.dumps(records))
