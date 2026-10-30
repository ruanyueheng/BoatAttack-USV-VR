"""Read-only inspection; does not save the supplied .blend."""
import bpy
import json
from mathutils import Vector
for obj in bpy.data.objects:
    if obj.type!='MESH':continue
    points=[obj.matrix_world@Vector(p) for p in obj.bound_box]
    print('LANTERN_OBJECT',json.dumps({'name':obj.name,'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'min':[min(p[i] for p in points) for i in range(3)],'max':[max(p[i] for p in points) for i in range(3)],'materials':[m.name if m else None for m in obj.data.materials],'modifiers':[(m.name,m.type) for m in obj.modifiers]}))
for mat in bpy.data.materials:
    print('LANTERN_MATERIAL',mat.name,tuple(mat.diffuse_color))
    if not mat.use_nodes:continue
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image:print('LANTERN_IMAGE',node.image.name,node.image.filepath,list(node.image.size),bool(node.image.packed_file))
        if node.type in {'BSDF_PRINCIPLED','EMISSION','BSDF_GLASS','GROUP'}:
            print('LANTERN_NODE',mat.name,node.name,node.type,[(s.name,str(s.default_value),s.is_linked) for s in node.inputs if hasattr(s,'default_value') and s.name in {'Base Color','Color','Emission Color','Emission Strength','Metallic','Roughness','Alpha','Transmission Weight'}])
    for link in mat.node_tree.links:
        print('LANTERN_LINK',mat.name,link.from_node.name,link.from_socket.name,'->',link.to_node.name,link.to_socket.name)
