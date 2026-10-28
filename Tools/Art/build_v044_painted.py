"""V044 painterly texture pass: bake vertex colour + AO + edge wear + gouache dab strokes into a small basecolor.
Usage: blender --background --factory-startup --python-exit-code 1 --python build_v044_painted.py -- Saw HomeChest
Reads ArtSource/V044/<name>.blend (written by build_v044_props.py); writes ArtSource/V044/Painted/<name>Albedo.png
and a side-by-side preview Logs/V044/models/painted-<name>.png (left vertex colour, right painted texture).
"""
from pathlib import Path
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'ArtSource/V044'
OUT = SRC / 'Painted'; OUT.mkdir(parents=True, exist_ok=True)
SHOTS = ROOT / 'Logs/V044/models'
SMALL = {'Saw', 'Axe', 'Hammer', 'Shovel', 'books', 'apple', 'WindBell'}


def node(tree, kind, **inputs):
    n = tree.nodes.new(kind)
    for k, v in inputs.items(): n.inputs[k].default_value = v
    return n


def painter(obj, seams=False, rusty=False):
    """Emission network per material kind (face attribute from build_v044_props.paint):
    wood: plank seams, per-plank tone, streaked grain and knots; iron: worn bright edges over a dark middle;
    brass: dark recesses, bright rims; steel: brushed streaks, bright edge line, rust near the grip.
    Then strong warm AO in joints and a faint gouache paper dab as the last layer."""
    mat = bpy.data.materials.new('MAT-paint-bake'); mat.use_nodes = True; t = mat.node_tree; L = t.links
    t.nodes.clear(); out = t.nodes.new('ShaderNodeOutputMaterial')
    N = lambda kind, **kw: node(t, kind, **kw)

    def math_(op, a, b=None, c=None):
        m = t.nodes.new('ShaderNodeMath'); m.operation = op
        for k, v in enumerate((a, b, c)):
            if v is None: continue
            if isinstance(v, (int, float)): m.inputs[k].default_value = v
            else: L.new(v, m.inputs[k])
        return m.outputs[0]

    def mix(a, b, fac, mode='MIX'):
        m = t.nodes.new('ShaderNodeMix'); m.data_type = 'RGBA'; m.blend_type = mode
        for sock, v in (('A', a), ('B', b)):
            if isinstance(v, tuple): m.inputs[sock].default_value = v
            else: L.new(v, m.inputs[sock])
        if isinstance(fac, (int, float)): m.inputs['Factor'].default_value = fac
        else: L.new(fac, m.inputs['Factor'])
        return m.outputs['Result']

    def ramp(src, lo, hi, c0=(0, 0, 0, 1), c1=(1, 1, 1, 1)):
        r = t.nodes.new('ShaderNodeValToRGB'); r.color_ramp.elements[0].position = lo; r.color_ramp.elements[1].position = hi
        r.color_ramp.elements[0].color = c0; r.color_ramp.elements[1].color = c1; L.new(src, r.inputs['Fac']); return r.outputs['Color']

    def vec(src, sx, sy, sz):
        m = t.nodes.new('ShaderNodeVectorMath'); m.operation = 'MULTIPLY'; m.inputs[1].default_value = (sx, sy, sz); L.new(src, m.inputs[0]); return m.outputs[0]

    vc = t.nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Color'; base = vc.outputs['Color']
    attr = t.nodes.new('ShaderNodeAttribute'); attr.attribute_type = 'GEOMETRY'; attr.attribute_name = 'kind'
    is_ = lambda k: math_('COMPARE', attr.outputs['Fac'], float(k), .5)
    co = t.nodes.new('ShaderNodeTexCoord').outputs['Object']
    sep = t.nodes.new('ShaderNodeSeparateXYZ'); L.new(co, sep.inputs[0])
    geo = t.nodes.new('ShaderNodeNewGeometry')
    bev = t.nodes.new('ShaderNodeBevel'); bev.samples = 8; bev.inputs['Radius'].default_value = .006
    dot = t.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'; L.new(bev.outputs[0], dot.inputs[0]); L.new(geo.outputs['Normal'], dot.inputs[1])
    edge = ramp(dot.outputs['Value'], .9, .99, (1, 1, 1, 1), (0, 0, 0, 1))  # 1 on rounded rims, 0 on flat faces
    convex = math_('MULTIPLY', edge, ramp(geo.outputs['Pointiness'], .48, .56))

    # --- wood
    plank_id = math_('FLOOR', math_('MULTIPLY', math_('ADD', sep.outputs['Y'], 2.0), 6.0))
    tone = N('ShaderNodeTexWhiteNoise'); tone.noise_dimensions = '1D'; L.new(plank_id, tone.inputs['W'])
    wood = mix(base, (.3, .16, .08, 1), math_('MULTIPLY', tone.outputs['Value'], .55 if seams else .0))
    wood = mix(wood, (.98, .82, .6, 1), math_('MULTIPLY', math_('SUBTRACT', 1, tone.outputs['Value']), .12))
    streak = N('ShaderNodeTexNoise', Scale=1.0, Detail=6.0, Distortion=1.5); long = max(range(3), key=lambda a: obj.dimensions[a]); L.new(vec(co, *[3 if a == long else 60 for a in range(3)]), streak.inputs['Vector'])
    wood = mix(wood, (.25, .13, .06, 1), ramp(streak.outputs['Fac'], .42, .6, (0, 0, 0, 1), (.7, .7, .7, 1)))
    knot = N('ShaderNodeTexVoronoi', Scale=7.0); L.new(vec(co, 1, 3, 3), knot.inputs['Vector'])
    wood = mix(wood, (.26, .14, .07, 1), ramp(knot.outputs['Distance'], .02, .06, (.8, .8, .8, 1), (0, 0, 0, 1)))
    seam = math_('ABSOLUTE', math_('SUBTRACT', math_('FRACT', math_('MULTIPLY', math_('ADD', sep.outputs['Y'], 2.0), 6.0)), .5))
    if seams:  # board joints only on plank-built props (chests); handles would get stray stripes
        wood = mix(wood, (.08, .04, .02, 1), ramp(seam, .42, .49))
        wood = mix(wood, (.25, .14, .07, 1), ramp(seam, .3, .43, (0, 0, 0, 1), (.5, .5, .5, 1)))

    # --- iron: polished worn rims, dark centre
    up = t.nodes.new('ShaderNodeSeparateXYZ'); L.new(geo.outputs['Normal'], up.inputs[0])
    iron = mix(mix(base, (.1, .11, .13, 1), .45), (.62, .64, .66, 1), convex)
    iron = mix(iron, (.75, .77, .78, 1), math_('MULTIPLY', ramp(up.outputs['Z'], .3, .9), .45))  # rivet heads lit on top
    iron = mix(iron, (.05, .05, .06, 1), math_('MULTIPLY', ramp(up.outputs['Z'], -.9, -.2, (1, 1, 1, 1), (0, 0, 0, 1)), .5))
    # --- brass: dark recesses, bright rims
    verd = N('ShaderNodeTexNoise', Scale=40.0, Detail=3.0); L.new(co, verd.inputs['Vector'])
    brass = mix(mix(base, (.95, .78, .45, 1), .25), (1, .95, .75, 1), convex)
    brass = mix(brass, (.36, .22, .08, 1), math_('MULTIPLY', ramp(up.outputs['Z'], -1, 0, (1, 1, 1, 1), (0, 0, 0, 1)), .4))  # undersides dark gold-brown
    brass = mix(brass, (1, .96, .8, 1), math_('MULTIPLY', ramp(up.outputs['Z'], .5, 1), .35))
    brass = mix(brass, (.35, .58, .48, 1), math_('MULTIPLY', ramp(verd.outputs['Fac'], .6, .7), .35))  # a little verdigris
    # --- steel: brushed along the long axis, bright edge line, rust near the grip
    brush = N('ShaderNodeTexNoise', Scale=1.0, Detail=4.0); L.new(vec(co, *[4 if a == long else 400 for a in range(3)]), brush.inputs['Vector'])
    steel = mix(mix(base, (.62, .68, .72, 1), .35), (.5, .56, .6, 1), ramp(brush.outputs['Fac'], .35, .7, (0, 0, 0, 1), (.5, .5, .5, 1)))
    steel = mix(steel, (.98, 1, 1, 1), convex)
    rustn = N('ShaderNodeTexNoise', Scale=30.0, Detail=5.0); L.new(co, rustn.inputs['Vector'])
    rust = math_('MULTIPLY', ramp(sep.outputs['Z'], .2, .3, (1, 1, 1, 1), (0, 0, 0, 1)), ramp(rustn.outputs['Fac'], .5, .62))
    steel = mix(steel, (.45, .22, .1, 1), math_('MULTIPLY', rust, .8 if rusty else 0))

    col = mix(base, wood, is_(1)); col = mix(col, iron, is_(2)); col = mix(col, brass, is_(3)); col = mix(col, steel, is_(4))
    # --- strong warm AO: dark lines where bands, rivets and corners meet wood
    ao = t.nodes.new('ShaderNodeAmbientOcclusion'); ao.samples = 24; ao.inputs['Distance'].default_value = .12
    col = mix(col, ramp(ao.outputs['AO'], .45, .97, (.16, .1, .08, 1), (1, 1, 1, 1)), 1, 'MULTIPLY')
    # --- faint gouache dab / paper grain as the last layer
    dabs = N('ShaderNodeTexVoronoi', Scale=76.0, Randomness=1.0); L.new(co, dabs.inputs['Vector'])
    col = mix(col, ramp(dabs.outputs['Distance'], 0, .6, (.9, .9, .9, 1), (1, 1, 1, 1)), 1, 'MULTIPLY')
    em = t.nodes.new('ShaderNodeEmission'); L.new(col, em.inputs['Color']); L.new(em.outputs[0], out.inputs['Surface'])
    return mat


def bake(obj, size, seams=False, rusty=False):
    bpy.context.view_layer.objects.active = obj; obj.select_set(True)
    if not obj.data.uv_layers or obj.data.uv_layers[0].name != 'PaintUV':
        uv = obj.data.uv_layers.new(name='PaintUV'); obj.data.uv_layers.active = uv
        for layer in list(obj.data.uv_layers):
            if layer.name != 'PaintUV': obj.data.uv_layers.remove(layer)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.01); bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new(obj.name + ' painted', size, size)
    mat = painter(obj, seams, rusty); tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img; mat.node_tree.nodes.active = tex
    original = [s.material for s in obj.material_slots]
    obj.data.materials.clear(); obj.data.materials.append(mat)
    s = bpy.context.scene; s.render.engine = 'CYCLES'; s.cycles.samples = 32; s.render.bake.margin = 4
    bpy.ops.object.bake(type='EMIT')
    obj.data.materials.clear()
    for m in original: obj.data.materials.append(m)
    obj.select_set(False)
    return img


def textured(img):
    m = bpy.data.materials.new('MAT-painted ' + img.name); m.use_nodes = True
    t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = img
    b = m.node_tree.nodes['Principled BSDF']; b.inputs['Roughness'].default_value = .7
    m.node_tree.links.new(t.outputs['Color'], b.inputs['Base Color']); return m


def shot(path):
    s = bpy.context.scene; s.render.resolution_x = s.render.resolution_y = 512; s.view_settings.view_transform = 'Standard'; s.cycles.samples = 24
    s.render.filepath = str(path); bpy.ops.render.render(write_still=True)


ROOTR = ROOT / 'Assets/Resources'
FOLDER = {**{n: 'FishingFeedbackTools' for n in ('Axe', 'Hammer', 'Shovel', 'Saw', 'HomeChest')}, 'Crab': 'FishingFish',
          **{n: 'FishingV023/Home' for n in ('lampRoundTable', 'lampRoundFloor', 'books', 'pottedPlant', 'coatRackStanding', 'radio')},
          **{n: 'FishingV019' for n in ('WindBell', 'bag', 'tool-shovel', 'pot', 'flower_purpleA', 'Telescope', 'apple', 'chest')},
          'AquariumStand': 'FishingV044', 'AquariumScape': 'FishingV044', **{'PortalFrame%d' % i: 'FishingV044' for i in (1, 3)}}  # 0 and 2 come from Hunyuan (import_v044_hunyuan.py)
SEAMS = {'HomeChest', 'chest', 'AquariumStand'}
SMALL |= {'Hammer', 'Shovel', 'tool-shovel', 'bag', 'pot', 'flower_purpleA', 'Telescope', 'lampRoundTable', 'lampRoundFloor', 'pottedPlant', 'coatRackStanding', 'radio'}

names = sys.argv[sys.argv.index('--') + 1:] or list(FOLDER)
for name in names:
    bpy.ops.wm.open_mainfile(filepath=str(SRC / (name + '.blend')))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    size = 256 if name in SMALL else 512
    if not bpy.context.scene.camera:
        raise SystemExit('blend has no preview camera; rebuild with build_v044_props.py')
    shot(SHOTS / ('painted-' + name + '-vertex.png'))
    for o in meshes: o.select_set(False)
    folder = ROOTR / FOLDER[name]
    keep = 'MAT-V019-vertex-palette' if FOLDER[name] == 'FishingV019' else 'MAT-vertex-preview'  # V019 metas remap this material name
    for o in meshes:
        img = bake(o, size, name in SEAMS, name == 'Saw')
        # Runtime looks up <folder>/<name>Albedo (single mesh) or <name>-<object>Albedo (chest body / lid).
        img.filepath_raw = str(folder / (name + ('' if len(meshes) == 1 else '-' + o.name) + 'Albedo.png')); img.file_format = 'PNG'; img.save()
    for o in meshes:
        o.data.materials.clear(); o.data.materials.append(bpy.data.materials.get(keep) or bpy.data.materials.new(keep)); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(folder / (name + '.fbx')), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE')
    for o in meshes:
        o.data.materials.clear(); o.data.materials.append(textured(bpy.data.images[o.name + ' painted']))
    shot(SHOTS / ('painted-' + name + '-texture.png'))
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / (name + '-painted.blend')))
print('PAINTED', names)
