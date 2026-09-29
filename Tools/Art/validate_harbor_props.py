"""Round-trip exported FBX validation and a non-Unity art preview. No source assets modified."""
import bpy, json, warnings
warnings.filterwarnings("ignore", category=DeprecationWarning)
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/FishingProps/Preview'
OUT.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('Preview vertex palette');mat.use_nodes=True
nodes=mat.node_tree.nodes;bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
attr=nodes.new('ShaderNodeVertexColor');attr.layer_name='PropColor'
mat.node_tree.links.new(attr.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.4
report={}
def load(name,offset):
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Resources/FishingProps'/f'{name}.fbx'))
    objs=list(bpy.context.selected_objects);meshes=[o for o in objs if o.type=='MESH']
    assert len(meshes)==1,(name,len(meshes))
    o=meshes[0];assert o.data.color_attributes.get('PropColor'),name
    o.data.calc_loop_triangles();tris=len(o.data.loop_triangles)
    assert 0<tris<14000,(name,tris)
    coords=[o.matrix_world@Vector(c) for c in o.bound_box]
    size=[max(c[i] for c in coords)-min(c[i] for c in coords) for i in range(3)]
    assert max(size)<5 and max(size)>.1,(name,size)
    report[name]={'triangles':tris,'blender_xyz_metres':size,'vertex_color':True}
    o.data.materials.clear();o.data.materials.append(mat)
    for ob in objs:
        if ob.parent is None:ob.location+=Vector(offset)
    return o
load('CrateBody',(-1.25,0,0));load('CargoSignService',(-1.25,0,0));load('RepairCoil',(-1.25,0,.74))
load('BeaconBody',(0.2,.35,0));load('BeaconHead',(0.2,.35,2.25));load('BeaconLens',(0.2,.35,2.25))
load('ChannelMarker',(1.9,1,0));load('MaintenanceRecord',(-2.2,.55,1.35))
load('CargoSignFish',(-1.95,-.2,.15));load('CargoSignCloth',(-2.45,-.2,.15))
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.location.z=-.02
fm=bpy.data.materials.new('Slate');fm.diffuse_color=(.028,.047,.055,1);floor.data.materials.append(fm)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.world.color=(.22,.22,.22)
for loc,power,size in [((-3,-4,7),1600,5),((4,1,6),2100,4)]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,1.5))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(7,11,7));cam=bpy.context.object
cam.rotation_euler=(Vector((-.2,.3,1.6))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=7.1
scene.camera=cam;scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.filepath=str(OUT/'harbor-props.png');bpy.ops.render.render(write_still=True)
(OUT/'roundtrip-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('HARBOR_ROUNDTRIP_PASS: '+str(len(report))+' FBX meshes; preview is Blender, not Unity.')

