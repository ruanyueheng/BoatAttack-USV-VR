"""Read-only inspection of the user pickaxe source; never saves the .blend."""
import bpy
import json
from mathutils import Vector
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        points=[obj.matrix_world @ Vector(c) for c in obj.bound_box]
        lo=[min(p[i] for p in points) for i in range(3)]
        hi=[max(p[i] for p in points) for i in range(3)]
        print('PICKAXE_OBJECT',json.dumps({'name':obj.name,'vertices':len(obj.data.vertices),'polygons':len(obj.data.polygons),'min':lo,'max':hi,'materials':[m.name if m else None for m in obj.data.materials],'modifiers':[(m.name,m.type) for m in obj.modifiers]}))
for mat in bpy.data.materials:
    print('PICKAXE_MATERIAL',mat.name,tuple(mat.diffuse_color))
    if mat.use_nodes:
        if mat.name in {'Material.006','Wood.001'}:
            for node in mat.node_tree.nodes:
                print('PICKAXE_NODE',mat.name,node.name,node.type,[(s.name,tuple(s.default_value) if hasattr(s.default_value,'__len__') else s.default_value) for s in node.inputs if hasattr(s,'default_value') and s.name in {'Base Color','Color','Metallic','Roughness'}])
            for link in mat.node_tree.links:
                print('PICKAXE_LINK',mat.name,link.from_node.name,link.from_socket.name,'->',link.to_node.name,link.to_socket.name)
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:
                print('PICKAXE_IMAGE',node.image.name,node.image.filepath,list(node.image.size),bool(node.image.packed_file))
