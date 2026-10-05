"""Read-only source/export hand audit. Run with Blender --background --python this_file.
Does not save a blend/FBX or touch the Unity project/import cache.
"""
import json
import math
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source_json():
    """Pure stdlib fallback: source GLB evidence only, NOT an FBX/Unity validation."""
    data = (ROOT / 'ArtSource/Characters/BeachCharacter.glb').read_bytes()
    document = json.loads(data[20:20 + struct.unpack_from('<I', data, 12)[0]])
    nodes = document['nodes']
    by_name = {node['name']: i for i, node in enumerate(nodes)}
    parents = {child: i for i, node in enumerate(nodes) for child in node.get('children', [])}

    def rotate(q, v):
        u = q[:3]
        uv = cross(u, v)
        uuv = cross(u, uv)
        return [v[i] + 2 * (q[3] * uv[i] + uuv[i]) for i in range(3)]

    def cross(a, b):
        return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]

    def norm(v):
        n = math.sqrt(sum(x*x for x in v))
        return [x/n for x in v]

    def point(index, value):
        node = nodes[index]
        v = rotate(node.get('rotation', [0, 0, 0, 1]), [value[i]*node.get('scale', [1, 1, 1])[i] for i in range(3)])
        v = [v[i]+node.get('translation', [0, 0, 0])[i] for i in range(3)]
        return point(parents[index], v) if index in parents else v

    def position(name):
        return point(by_name[name], [0, 0, 0])

    def sub(a, b):
        return [a[i]-b[i] for i in range(3)]

    def length(a, b):
        return math.sqrt(sum(x*x for x in sub(a, b)))

    print('SOURCE_ONLY', json.dumps({'skin_joints': [len(s['joints']) for s in document['skins']],
          'clips': [a['name'] for a in document['animations']],
          'meshes': [(m['name'], sum(document['accessors'][p['attributes']['POSITION']]['count'] for p in m['primitives'])) for m in document['meshes']]}))
    for side in ('L', 'R'):
        wrist = position('Wrist.'+side)
        knuckles = [position(digit+'2.'+side) for digit in ('Index', 'Middle', 'Ring', 'Pinky')]
        average = [sum(p[i] for p in knuckles)/4 for i in range(3)]
        forward = norm(sub(average, wrist))
        dorsal = norm(cross(forward, norm(sub(knuckles[0], knuckles[3]))))
        # glTF is right-handed. Reflection on import changes the cross-product parity;
        # Unity recomputes its frame from imported bones rather than copying these vectors.
        print('SOURCE_HAND', json.dumps({'side': side,
              'upper_m': length(position('UpperArm.'+side), position('LowerArm.'+side)),
              'lower_m': length(position('LowerArm.'+side), wrist),
              'wrist_to_knuckles_m': length(wrist, average),
              'palm_forward_gltf': forward, 'cross_normal_gltf': dorsal,
              'finger_joints': [n for n in by_name if n.endswith('.'+side) and n.startswith(('Index', 'Middle', 'Ring', 'Pinky', 'Thumb'))]}))


if '--source-json' in sys.argv:
    source_json()
    sys.exit(0)

import bpy


def vector(v):
    return [round(float(x), 6) for x in v]


def audit(path, gltf):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if gltf:
        bpy.ops.import_scene.gltf(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))
    rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    for obj in bpy.context.scene.objects:
        if obj.animation_data:
            obj.animation_data.action = None
            for track in obj.animation_data.nla_tracks:
                track.mute = True
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    bpy.context.view_layer.update()
    print('HAND_AUDIT', json.dumps({
        'file': str(path.relative_to(ROOT)),
        'bones': len(rig.data.bones),
        'clips': [(a.name, vector(a.frame_range)) for a in bpy.data.actions],
        'meshes': [(o.name, len(o.data.vertices), len(o.data.polygons)) for o in bpy.context.scene.objects
                   if o.type == 'MESH' and o.name.startswith('Beach_')],
    }))
    for side in ('L', 'R'):
        wrist = rig.pose.bones['Wrist.' + side]
        bones = []
        for bone in rig.pose.bones:
            if bone.name in ['Shoulder.' + side, 'UpperArm.' + side, 'LowerArm.' + side, wrist.name] or wrist.name in [p.name for p in bone.parent_recursive]:
                bones.append({'name': bone.name, 'parent': bone.parent.name if bone.parent else '',
                              'head': vector(rig.matrix_world @ bone.head),
                              'tail': vector(rig.matrix_world @ bone.tail)})
        print('HAND_BONES', json.dumps({'side': side, 'bones': bones}))
    # Report animation curves on object/root transforms rather than presuming in-place motion.
    for action in bpy.data.actions:
        if not action.name.endswith(('Idle', 'Walk', 'Run')):
            continue
        curves = []
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for c in bag.fcurves:
                        if not c.data_path.startswith('pose.bones[') or c.data_path.startswith('pose.bones["Root"]'):
                            values = [p.co.y for p in c.keyframe_points]
                            if values:
                                curves.append([c.data_path, c.array_index, round(min(values), 6), round(max(values), 6)])
        print('HAND_ROOT_CURVES', json.dumps({'clip': action.name, 'curves': curves}))


audit(ROOT / 'ArtSource/Characters/BeachCharacter.glb', True)
audit(ROOT / 'Assets/Resources/FishingCharacter/BeachCharacter.fbx', False)
