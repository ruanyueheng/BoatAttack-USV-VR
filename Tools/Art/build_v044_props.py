"""V044 storybook remodel: rounded, bevelled, vertex-painted hand tools, home chest and crab.
Keeps every original Resources name, metre scale, pivot, grip ([0,.1,0]) and the chest's 'lid' hinge node.
Usage: blender --background --factory-startup --python-exit-code 1 --python build_v044_props.py -- [--before]
"""
from pathlib import Path
import bpy, bmesh, math, json, random, sys
from mathutils import Vector, Matrix, noise

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / 'Assets/Resources/FishingFeedbackTools'
FISH = ROOT / 'Assets/Resources/FishingFish'
HOME = ROOT / 'Assets/Resources/FishingV023/Home'
V019 = ROOT / 'Assets/Resources/FishingV019'
V044 = ROOT / 'Assets/Resources/FishingV044'
BACKUP = ROOT / 'ArtSource/V044/Backup'
SHOTS = ROOT / 'Logs/V044/models'
ART = ROOT / 'ArtSource/V044'
SHOTS.mkdir(parents=True, exist_ok=True)
BEFORE = '--before' in sys.argv

# Warm storybook palette, close to V042 cedar furniture and the coastal atlas.
WOOD = (.50, .32, .19); WOOD_LIGHT = (.68, .48, .30); WOOD_DARK = (.30, .18, .10)
STEEL = (.44, .51, .57); STEEL_EDGE = (.80, .85, .86); STEEL_DARK = (.24, .29, .34)
IRON = (.17, .19, .22); IRON_LIGHT = (.32, .35, .38)
BRASS = (.66, .50, .30); LEATHER = (.42, .20, .11); TEAL = (.16, .42, .44); CREAM = (.95, .88, .70)
INK = (.06, .08, .11); CRAB = (.78, .32, .18); CRAB_LIGHT = (.92, .62, .42); CRAB_BELLY = (.99, .86, .66)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def vertex_material():
    mat = bpy.data.materials.get('MAT-vertex-preview')
    if mat: return mat
    mat = bpy.data.materials.new('MAT-vertex-preview'); mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .55
    vc = mat.node_tree.nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Color'
    mat.node_tree.links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
    return mat


def paint(obj, base, top=None, edge=None, speck=.05, axis=2, grain=0.):
    """Hand-painted look in vertex colour: vertical gradient, soft noise, lighter worn edges facing up."""
    mesh = obj.data
    for old in list(mesh.color_attributes): mesh.color_attributes.remove(old)
    attr = mesh.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
    world = obj.matrix_world
    zs = [(world @ v.co)[axis] for v in mesh.vertices]; lo, hi = min(zs), max(zs); span = max(hi - lo, 1e-5)
    top = top or tuple(min(1, c * 1.22 + .04) for c in base)
    for poly in mesh.polygons:
        n = (world.to_3x3() @ poly.normal).normalized()
        for li in poly.loop_indices:
            p = world @ mesh.vertices[mesh.loops[li].vertex_index].co
            t = ((p[axis] - lo) / span) ** .8
            c = [base[i] * (1 - t) + top[i] * t for i in range(3)]
            if edge is not None and n.z > .55:
                k = (n.z - .55) * .9; c = [c[i] * (1 - k) + edge[i] * k for i in range(3)]
            j = 1 + speck * noise.noise(p * 38) + grain * noise.noise(Vector((p.x * 140, p.y * 140, p.z * 5)))
            attr.data[li].color_srgb = (*[max(0, min(1, x * j)) for x in c], 0)  # alpha 0: FishingPalette treats textured alpha as wind sway
    mesh.color_attributes.active_color = attr
    # Material kind per face for the painted-texture bake (build_v044_painted.py): 1 wood, 2 iron, 3 brass, 4 steel.
    kind = 1 if base in (WOOD, WOOD_DARK, WOOD_LIGHT, (.3, .18, .1)) else 2 if base == IRON else 3 if base == BRASS else 4 if base in (STEEL, STEEL_DARK) else 0
    layer = mesh.attributes.get('kind') or mesh.attributes.new('kind', 'FLOAT', 'FACE')
    for i in range(len(mesh.polygons)): layer.data[i].value = kind
    mesh.materials.clear(); mesh.materials.append(vertex_material())
    for poly in mesh.polygons: poly.use_smooth = True
    return obj


def smooth(obj, bevel=0, segments=2, angle=40):
    if bevel:
        m = obj.modifiers.new('bevel', 'BEVEL'); m.width = bevel; m.segments = segments; m.limit_method = 'ANGLE'
        bpy.context.view_layer.objects.active = obj; bpy.ops.object.modifier_apply(modifier=m.name)
    obj.data.set_sharp_from_angle(angle=math.radians(angle)) if hasattr(obj.data, 'set_sharp_from_angle') else None
    return obj


def box(name, p, s, bevel=.006, seg=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=p); o = bpy.context.object; o.name = 'GEO-' + name
    o.scale = s; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return smooth(o, bevel, seg)


def ell(name, p, s, seg=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=p)
    o = bpy.context.object; o.name = 'GEO-' + name; o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return o


def tube(name, pts, radii, sides=10, cap=True):
    """Swept ring surface along a polyline with per-point radius (handles, legs, rims)."""
    pts = [Vector(p) for p in pts]; verts = []; faces = []
    for i, p in enumerate(pts):
        tan = (pts[min(i + 1, len(pts) - 1)] - pts[max(0, i - 1)]).normalized()
        side = tan.cross(Vector((0, 0, 1)))
        if side.length < .1: side = tan.cross(Vector((0, 1, 0)))
        side.normalize(); up = tan.cross(side).normalized()
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        for j in range(sides):
            a = j * math.tau / sides; verts.append(p + r * (side * math.cos(a) + up * math.sin(a)))
    for i in range(len(pts) - 1):
        for j in range(sides): faces.append((i * sides + j, i * sides + (j + 1) % sides, (i + 1) * sides + (j + 1) % sides, (i + 1) * sides + j))
    if cap: faces += [tuple(reversed(range(sides))), tuple((len(pts) - 1) * sides + j for j in range(sides))]
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    o = bpy.data.objects.new('GEO-' + name, mesh); bpy.context.scene.collection.objects.link(o)
    return o


def slab(name, outline, thick, bevel=.004):
    """Extrude an XZ outline along Y. thick(x,z) gives the half thickness, so blades can taper to an edge."""
    n = len(outline); verts = []
    for sgn in (-1, 1): verts += [(x, sgn * thick(x, z), z) for x, z in outline]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    o = bpy.data.objects.new('GEO-' + name, mesh); bpy.context.scene.collection.objects.link(o)
    bm = bmesh.new(); bm.from_mesh(mesh); bmesh.ops.triangulate(bm, faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    return smooth(o, bevel, 1)


def handle(name, z0, z1, r0, r1, bend=0., sides=12, steps=10):
    pts = [(bend * math.sin(math.pi * i / steps), 0, z0 + (z1 - z0) * i / steps) for i in range(steps + 1)]
    radii = [r0 + (r1 - r0) * i / steps + .003 * math.sin(math.pi * i / steps) for i in range(steps + 1)]
    return tube(name, pts, radii, sides)


def bands(name, z, r, count=3, gap=.014, sides=12):
    return [tube(name, [(0, 0, z + i * gap - .004), (0, 0, z + i * gap + .004)], r, sides) for i in range(count)]


def tris(objs): return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.join(); o = bpy.context.object; o.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)  # slabs/lathes were authored inward; AO bakes black otherwise
    bm.to_mesh(o.data); bm.free()
    return o


def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE')


def render(path, size=512, view=(1.3, -1.9, 1.1)):
    scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 24; scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = size; scene.render.film_transparent = False
    scene.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('studio'); world.use_nodes = True; scene.world = world
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.80, .86, .90, 1); world.node_tree.nodes['Background'].inputs['Strength'].default_value = .7
    meshes = [o for o in scene.objects if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    centre = (lo + hi) / 2; span = max(hi - lo)
    for name, pos, power in [('key', (3, -4, 6), 900), ('fill', (-4, -2, 2), 300), ('rim', (1, 5, 4), 500)]:
        d = bpy.data.lights.new(name, 'AREA'); d.energy = power; d.size = 5; L = bpy.data.objects.new(name, d)
        scene.collection.objects.link(L); L.location = centre + Vector(pos); L.rotation_euler = (centre - L.location).to_track_quat('-Z', 'Y').to_euler()
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; c = bpy.data.objects.new('cam', cam); scene.collection.objects.link(c); scene.camera = c
    c.location = centre + Vector(view).normalized() * span * 4; c.rotation_euler = (centre - c.location).to_track_quat('-Z', 'Y').to_euler()
    cam.ortho_scale = span * 1.25; cam.clip_end = span * 20
    for o in meshes:
        for slot in o.material_slots: slot.material = vertex_material()
    scene.render.filepath = str(path); bpy.ops.render.render(write_still=True)


def before(name, src, view=(1.3, -1.9, 1.1)):
    reset(); bpy.ops.import_scene.fbx(filepath=str(src)); render(SHOTS / (name + '-before.png'), view=view)


# ---------------------------------------------------------------- models
def axe():
    parts = [paint(handle('handle', 0, .80, .029, .022, bend=.012), WOOD_DARK, WOOD_LIGHT, grain=.22)]
    parts.append(paint(ell('butt knob', (0, 0, .02), (.029, .029, .02), 12, 6), WOOD_DARK))
    parts += [paint(o, LEATHER, (.6, .32, .18)) for o in bands('grip wrap', .07, .031, 5, .016)]
    # Bearded blade towards -X; thick poll at +X, thin bright cutting edge.
    outline = [(.05, .70), (.05, .82), (-.02, .83), (-.10, .86), (-.17, .885), (-.215, .87), (-.243, .80), (-.235, .70), (-.20, .64),
               (-.15, .655), (-.09, .70), (-.02, .695)]
    blade = slab('axe head', outline, lambda x, z: .024 if x > -.03 else max(.004, .024 * (1 - (-.03 - x) / .23) ** 1.3), .007)
    for poly in blade.data.polygons: poly.use_smooth = True
    parts.append(paint(blade, STEEL_DARK, STEEL, STEEL_EDGE, axis=0))
    # Brighten the honed edge band by x.
    attr = blade.data.color_attributes['Color']
    for poly in blade.data.polygons:
        for li in poly.loop_indices:
            x = blade.data.vertices[blade.data.loops[li].vertex_index].co.x
            if x < -.19: k = min(1, (-.19 - x) / .04); c = attr.data[li].color_srgb; attr.data[li].color_srgb = (*[c[i] * (1 - k) + STEEL_EDGE[i] * k for i in range(3)], 0)
    parts.append(paint(box('poll cap', (.085, 0, .76), (.07, .056, .1), .02, 3), STEEL_DARK, STEEL))
    parts.append(paint(box('wedge', (.02, 0, .835), (.024, .02, .02), .005), WOOD_LIGHT))
    return parts


def hammer():
    parts = [paint(handle('handle', 0, .40, .025, .018, bend=.006), WOOD_DARK, WOOD_LIGHT, grain=.22),
             paint(ell('butt knob', (0, 0, .017), (.028, .028, .017), 12, 6), WOOD_DARK)]
    parts += [paint(o, LEATHER, (.6, .32, .18)) for o in bands('grip wrap', .05, .027, 4, .015)]
    # Rounded barrel head with flared striking faces and brass collar.
    head = tube('head', [(-.132, 0, .40), (-.125, 0, .40), (-.105, 0, .40), (-.03, 0, .40), (.03, 0, .40), (.105, 0, .40), (.125, 0, .40), (.132, 0, .40)],
                [.034, .040, .036, .031, .031, .036, .040, .034], 16)
    parts.append(paint(smooth(head, 0), STEEL_DARK, STEEL, STEEL_EDGE))
    parts.append(paint(tube('collar', [(0, 0, .355), (0, 0, .375)], .025, 14), BRASS, (.88, .72, .45)))
    return parts


def shovel():
    parts = [paint(handle('shaft', -.2, .62, .023, .026, bend=0), WOOD_DARK, WOOD_LIGHT, grain=.22)]
    # D-grip at the low end, rounded spade blade at the top (head +Y in Unity like the original).
    d = [(-.06, 0, -.2), (-.06, 0, -.17), (-.045, 0, -.145), (0, 0, -.135), (.045, 0, -.145), (.06, 0, -.17), (.06, 0, -.2)]
    parts.append(paint(tube('d grip', d, .013, 10), WOOD_DARK, WOOD_LIGHT))
    parts.append(paint(tube('d grip bar', [(-.068, 0, -.2), (.068, 0, -.2)], .016, 10), WOOD_LIGHT, CREAM))
    parts.append(paint(tube('d yoke', [(0, 0, -.135), (0, 0, -.11)], [.016, .019], 10), WOOD_DARK))
    parts.append(paint(tube('socket', [(0, 0, .55), (0, 0, .61), (0, 0, .645)], [.028, .031, .04], 14), STEEL_DARK, STEEL))
    outline = [(.15, .64), (.155, .70)] + [(.155 * math.cos(math.pi * i / 16), .70 + .22 * math.sin(math.pi * i / 16) ** .8) for i in range(17)] + [(-.155, .70), (-.15, .64), (-.06, .625), (.06, .625)]
    blade = slab('spade', outline, lambda x, z: .007 + .007 * (1 - abs(x) / .155) * max(0, 1 - (z - .63) / .27), .005)
    # dish the blade slightly so it reads as curved metal
    for v in blade.data.vertices: v.co.y += .03 * (1 - (v.co.x / .155) ** 2)
    parts.append(paint(blade, STEEL_DARK, STEEL_EDGE, STEEL_EDGE))
    parts.append(paint(tube('blade rib', [(0, .02, .63), (0, .028, .78)], [.012, .004], 8), STEEL_DARK))
    return parts


def saw():
    parts = []
    # Closed pistol handle: rounded loop of wood with a palm swell, rivets in brass.
    # Traditional closed hand-saw grip: thick cheek against the blade heel, D-shaped hand hole behind it.
    cheek = [(-.01, .02), (.06, .0), (.13, .02), (.15, .07), (.14, .13), (.12, .23), (.06, .25), (-.01, .22)]
    grip = slab('grip cheek', cheek, lambda x, z: .016, 0)
    hole = slab('hand hole', [(.03, .06), (.075, .06), (.1, .08), (.106, .115), (.1, .15), (.075, .175), (.03, .175)][::-1], lambda x, z: .03, 0)
    cut = grip.modifiers.new('hand hole', 'BOOLEAN'); cut.operation = 'DIFFERENCE'; cut.object = hole; cut.solver = 'EXACT'
    bpy.context.view_layer.objects.active = grip; bpy.ops.object.modifier_apply(modifier=cut.name); bpy.data.objects.remove(hole)
    parts.append(paint(smooth(grip, .006, 2), WOOD_DARK, WOOD_LIGHT, grain=.18))
    profile = [(-.01, .18), (-.005, .64), (.055, .64)]
    for i in range(15):
        z = .64 - i * .03; profile += [(.105 - i * .0012, z), (.085 - i * .0012, z - .015)]
    profile.append((.12, .18))
    blade = slab('blade', profile, lambda x, z: .0035, .0)
    parts.append(paint(blade, STEEL, STEEL_EDGE, axis=0))
    for x, z in ((.125, .2), (.125, .045)):
        parts.append(paint(tube('brass bolt', [(x, -.021, z), (x, .021, z)], .011, 10), BRASS, (.88, .72, .45)))
    attr = blade.data.color_attributes['Color']  # worn rub marks along the blade
    for poly in blade.data.polygons:
        for li in poly.loop_indices:
            v = blade.data.vertices[blade.data.loops[li].vertex_index].co; k = max(0, noise.noise(Vector((v.x * 12, 0, v.z * 30))) * 1.6)
            c = attr.data[li].color_srgb; attr.data[li].color_srgb = (*[min(1, c[i] + k * .18) for i in range(3)], 0)
    for o in parts:
        for v in o.data.vertices: v.co.x -= .02
    return parts


def chest():
    """Rounded sea chest: plank body with brass corners; lid is a separate child named 'lid' hinged at its back edge."""
    W, D, H, LH = 1.15, 1.15, .51, .64  # handles, bands and hasp must stay inside the original 1.274 x 1.275 footprint
    body = []
    for i in range(4):
        z = .065 + i * .118
        tone = [WOOD, WOOD_LIGHT, WOOD, (.6, .4, .24)][i]
        T = .07  # hollow body: four plank walls per course around an open interior
        for sy in (-1, 1): body.append(paint(box('plank', (0, sy * (D / 2 - .01 - T / 2), z), (W - .02, T, .114), .028, 2), WOOD_DARK, tone, speck=.06, grain=.15))
        for sx in (-1, 1): body.append(paint(box('plank', (sx * (W / 2 - .01 - T / 2), 0, z), (T, D - .02 - 2 * T, .114), .028, 2), WOOD_DARK, tone, speck=.06, grain=.15))
    INNER = (.3, .18, .1)
    body.append(paint(box('inner floor', (0, 0, .03), (W - .16, D - .16, .06), .01, 1), INNER, (.4, .25, .14), speck=.06, grain=.15))
    for sy in (-1, 1): body.append(paint(box('inner lining', (0, sy * (D / 2 - .085), H / 2), (W - .17, .01, H - .06), 0, 1), INNER, (.38, .23, .13), grain=.15))
    for sx in (-1, 1): body.append(paint(box('inner lining', (sx * (W / 2 - .085), 0, H / 2), (.01, D - .17, H - .06), 0, 1), INNER, (.38, .23, .13), grain=.15))
    for sx in (-1, 1):
        for sy in (-1, 1):
            body.append(paint(box('corner', (sx * (W / 2 - .03), sy * (D / 2 - .03), H / 2), (.07, .07, H), .02, 2), BRASS, (.88, .72, .45)))
    for sx in (-1, 1):
        body.append(paint(box('band', (sx * .34, 0, H / 2), (.1, D + .03, H - .01), .012, 2), IRON, IRON_LIGHT, speck=.1))
        for sy in (-1, 1):
            for z in (.1, .25, .4):
                body.append(paint(ell('rivet', (sx * .34 + .03, sy * (D / 2 + .016), z), (.014, .014, .014), 6, 3), IRON_LIGHT, (.5, .52, .54)))
    body.append(paint(box('hasp plate', (0, -D / 2 - .004, H - .07), (.16, .03, .14), .015, 2), BRASS, (.88, .72, .45)))
    body.append(paint(box('keyhole', (0, -D / 2 - .021, H - .075), (.018, .006, .04), .004, 1), (.08, .06, .04), (.12, .09, .06)))
    body.append(paint(tube('hasp ring', [(0, -D / 2 - .03, H - .13), (0, -D / 2 - .045, H - .16), (0, -D / 2 - .03, H - .19)], .012, 8), BRASS))
    for sx in (-1, 1):
        body.append(paint(tube('side handle', [(sx * W / 2, -.12, .34), (sx * (W / 2 + .045), -.08, .31), (sx * (W / 2 + .045), .08, .31), (sx * W / 2, .12, .34)], .016, 8), STEEL_DARK, STEEL))
    # Treasure filling the hollow: a gold mound rising just above the rim, coins, shells and a ribboned scroll on top.
    rnd = random.Random(7); GOLD = (.85, .64, .25)
    mound = ell('gold mound', (0, 0, 0), ((W - .17) / 2, (D - .17) / 2, H * .55), 14, 7)
    for v in mound.data.vertices: v.co.z = min(v.co.z, H * .55) + .012 * noise.noise(v.co * 30)
    mound.data.transform(Matrix.Translation((0, 0, H - .02 - H * .55 + .05)))
    for v in mound.data.vertices: v.co.z = max(v.co.z, .06)  # keep the base on the inner floor so the pivot stays at y=0
    body.append(paint(mound, (.7, .5, .18), GOLD, (1, .9, .55), .12))
    top = lambda x, y: H + .03 * (1 - min(1, (x / (W / 2 - .1)) ** 2 + (y / (D / 2 - .1)) ** 2))
    for i in range(12):
        x, y = rnd.uniform(-.45, .45), rnd.uniform(-.4, .4)
        coin = lathe('coin', [(.0, -.005), (.035, -.005), (.035, .005), (.0, .005)], 8)
        coin.data.transform(Matrix.Translation((x, y, top(x, y) + .006)) @ Matrix.Rotation(rnd.uniform(-.5, .5), 4, 'X') @ Matrix.Rotation(rnd.uniform(-.5, .5), 4, 'Y'))
        body.append(paint(coin, GOLD, (1, .86, .45), (1, .95, .7)))
    for x, y, col in ((.32, .15, (.95, .78, .7)), (-.36, -.22, (.98, .9, .8))):
        shell = ell('shell', (0, 0, 0), (.06, .05, .025), 10, 5)
        for v in shell.data.vertices: v.co.z += .006 * math.cos(math.atan2(v.co.y, v.co.x) * 9) * (v.co.z > 0)
        shell.data.transform(Matrix.Translation((x, y, top(x, y) + .015)))
        body.append(paint(shell, col, (1, .96, .9), speck=.1))
    scroll = tube('scroll', [(-.1, .22, H + .04), (.25, .12, H + .045)], .028, 12); body.append(paint(scroll, CREAM, (1, .96, .84)))
    body.append(paint(tube('scroll ribbon', [(.07, .175, H + .043), (.072, .174, H + .044)], .031, 12), (.7, .2, .18)))
    b = join(body, 'chest')
    # Lid: one closed half-barrel solid (no see-through slat gaps), planks painted as stripes. Origin at hinge.
    RZ = .30; steps = 18; arc = [(-math.cos(math.pi * i / steps) * D / 2, H + RZ * math.sin(math.pi * i / steps)) for i in range(steps + 1)]
    verts = [(x, y, z) for x in (-W / 2, W / 2) for y, z in arc]; n = len(arc)
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))] + [(k, k + 1, k + 1 + n, k + n) for k in range(n - 1)] + [(n - 1, 0, n, 2 * n - 1)]
    mesh = bpy.data.meshes.new('lid shell'); mesh.from_pydata(verts, [], faces); mesh.update()
    shell = bpy.data.objects.new('GEO-lid shell', mesh); bpy.context.scene.collection.objects.link(shell)
    smooth(shell, .025, 2); paint(shell, WOOD_DARK, WOOD_LIGHT, speck=.06, grain=.15)
    attr = mesh.color_attributes['Color']
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            v = mesh.vertices[mesh.loops[li].vertex_index].co; a = math.atan2(v.z - H, -v.y / (D / 2) * RZ)
            plank = int(a / math.pi * 7); groove = abs(a / math.pi * 7 - round(a / math.pi * 7)) < .06
            k = .62 if groove else (.92 + .1 * (plank % 2)); c = attr.data[li].color_srgb
            attr.data[li].color_srgb = (*[min(1, c[q] * k) for q in range(3)], 0)
    lid_parts = [shell]
    for sx in (-1, 1):
        pts = [(sx * .34, -math.cos(math.pi * i / 16) * (D / 2 + .012), H + (RZ + .012) * math.sin(math.pi * i / 16)) for i in range(17)]
        band = tube('lid band', pts, .03, 6);         lid_parts.append(paint(band, IRON, IRON_LIGHT, speck=.1))
        for i in range(1, 16, 2):
            y, z = pts[i][1] * 1.03, H + (pts[i][2] - H) * 1.1 + .004
            lid_parts.append(paint(ell('rivet', (sx * .34, y, z), (.014, .014, .014), 6, 3), IRON_LIGHT, (.5, .52, .54)))
    lid_parts.append(paint(box('lid hasp', (0, -D / 2 - .012, H + .05), (.12, .03, .1), .015, 2), BRASS, (.88, .72, .45)))
    lid = join(lid_parts, 'lid')
    hinge = Vector((0, D / 2 - .01, H))
    lid.data.transform(Matrix.Translation(-hinge)); lid.location = hinge
    lid.parent = b
    return [b, lid]


def crab():
    """Chunky storybook shore crab: domed lobed carapace, banded jointed legs, real two-part pincers. +Y head, +Z up."""
    parts = []
    shell = ell('carapace', (0, 0, .02), (.14, .105, .055), 24, 12)
    for v in shell.data.vertices:  # lobed front rim and a soft ridge
        p = v.co; ang = math.atan2(p.x, p.y)
        p.y += .008 * math.cos(ang * 5) * max(0, p.y / .105)
        p.z += .006 * math.cos(p.x * 40) * max(0, (p.z - .02) / .055)
    parts.append(paint(shell, (.55, .13, .06), CRAB, (.95, .45, .22), .1))
    parts.append(paint(ell('belly', (0, 0, -.006), (.128, .095, .03), 18, 8), CRAB_BELLY, CRAB_BELLY))
    for sx in (-1, 1):
        for i, sp in enumerate((-.04, -.0, .04)):
            parts.append(paint(ell('shell spot', (sx * (.045 + .02 * i), sp, .066 - abs(sp) * .25), (.016, .014, .006), 8, 4), CRAB_LIGHT, CREAM))
        for j in range(4):  # walking legs: three jointed segments, tip toward ground
            y = .035 - j * .038; spread = .04 * (j - 1.5)
            pts = [(sx * .12, y, .0), (sx * .19, y + spread * .3, .05), (sx * .245, y + spread, -.02), (sx * .245, y + spread * 1.2, -.066)]
            parts.append(paint(tube('leg', pts, [.016, .013, .010, .003], 8), (.62, .18, .08), CRAB))
            parts.append(paint(ell('knee', (sx * .19, y + spread * .3, .05), (.014, .014, .014), 6, 3), CRAB_LIGHT))
        # claw: arm to wrist, swollen palm, fixed finger and moveable dactyl
        arm = [(sx * .10, .07, .01), (sx * .15, .12, .03), (sx * .17, .16, .04)]
        parts.append(paint(tube('claw arm', arm, [.022, .019, .02], 10), (.66, .18, .08), CRAB))
        palm = ell('claw palm', (sx * .175, .195, .045), (.042, .05, .034), 16, 8)
        parts.append(paint(palm, (.7, .2, .08), CRAB_LIGHT, CRAB_LIGHT))
        parts.append(paint(tube('fixed finger', [(sx * .19, .235, .035), (sx * .185, .26, .03), (sx * .17, .252, .028)], [.016, .011, .003], 8), (.42, .1, .06), (.22, .08, .06)))
        parts.append(paint(tube('dactyl', [(sx * .16, .235, .06), (sx * .162, .262, .056), (sx * .175, .252, .046)], [.014, .01, .003], 8), (.42, .1, .06), (.22, .08, .06)))
        # eye stalks with big friendly eyes
        parts.append(paint(tube('eye stalk', [(sx * .03, .085, .05), (sx * .036, .095, .085)], [.009, .008], 8), CRAB))
        parts.append(paint(ell('eye white', (sx * .036, .095, .095), (.017, .017, .017), 12, 6), CREAM, (1, 1, 1)))
        parts.append(paint(ell('pupil', (sx * .037, .108, .099), (.009, .006, .010), 10, 5), INK, INK))
        parts.append(paint(ell('sparkle', (sx * .033, .113, .104), (.003, .002, .003), 6, 3), (1, 1, 1), (1, 1, 1)))
    parts.append(paint(tube('mouth', [(-.018, .101, .012), (0, .106, .008), (.018, .101, .012)], .003, 6), (.3, .06, .05)))
    return parts


def lathe(name, profile, sides=20):
    """Turned solid from (radius, z) pairs, bottom to top; used for lamps, pots and posts."""
    verts = [(r * math.cos(math.tau * j / sides), r * math.sin(math.tau * j / sides), z) for r, z in profile for j in range(sides)]
    n = len(profile); faces = [(i * sides + j, i * sides + (j + 1) % sides, (i + 1) * sides + (j + 1) % sides, (i + 1) * sides + j) for i in range(n - 1) for j in range(sides)]
    faces += [tuple(reversed(range(sides))), tuple((n - 1) * sides + j for j in range(sides))]
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    o = bpy.data.objects.new('GEO-' + name, mesh); bpy.context.scene.collection.objects.link(o); return o


SHADE_TOP = (1, .95, .82); TERRACOTTA = (.70, .38, .24); LEAF = (.24, .44, .25); LEAF_LIGHT = (.52, .68, .36)


def lamp(height, k):
    """Turned bronze foot, wooden stem, scalloped linen shade with a teal trim. k scales the shade."""
    parts = [paint(lathe('lamp foot', [(.0, 0), (.075 * k, 0), (.08 * k, .012), (.065 * k, .03), (.03 * k, .045)]), BRASS, (.86, .7, .45), speck=.08)]
    top = height - .26 * k
    parts.append(paint(lathe('lamp stem', [(.012, .04), (.014, top * .5), (.02, top * .52), (.013, top * .55), (.012, top)], 12), WOOD_DARK, WOOD_LIGHT, grain=.15))
    parts.append(paint(ell('bulb', (0, 0, top + .03), (.03 * k, .03 * k, .035 * k), 12, 6), (1, .93, .7), (1, .98, .88)))
    shade = lathe('fabric shade', [(.11 * k, top - .015), (.17 * k, top - .01), (.172 * k, top), (.13 * k, top + .2 * k), (.075 * k, top + .26 * k - .004), (.0, top + .26 * k)], 28)
    for v in shade.data.vertices:  # scalloped hem
        if v.co.z < top + .005: v.co.z -= .012 * k * (1 + math.cos(math.atan2(v.co.y, v.co.x) * 10)) / 2
    parts.append(paint(shade, (.74, .58, .40), (.92, .82, .62), speck=.05))
    ring = [(math.cos(a) * .172 * k, math.sin(a) * .172 * k, top - .004) for a in (math.tau * i / 28 for i in range(29))]
    parts.append(paint(tube('shade trim', ring, .006, 6, cap=False), TEAL))
    return parts


def lamp_table(): return lamp(.314, .6)
def lamp_floor(): return lamp(.86, 1.0)


def books():
    parts = []
    for i, (c, z, yaw) in enumerate([(TEAL, .0, 0.), ((.62, .26, .2), .032, .18), ((.44, .40, .22), .062, -.12)]):
        w, d, h = .15 - i * .012, .104 - i * .008, .03
        pieces = [paint(box('cover', (0, 0, z + h / 2), (w, d, h), .006, 2), tuple(x * .8 for x in c), c, speck=.06),
                  paint(box('pages', (.004, 0, z + h / 2), (w - .006, d - .01, h - .01), .003, 1), CREAM, (1, .97, .88)),
                  paint(box('spine band', (-w / 2 + .001, 0, z + h / 2), (.006, d * .9, h * .4), .002, 1), BRASS)]
        for o in pieces: o.data.transform(Matrix.Rotation(yaw, 4, 'Z')); parts.append(o)
    return parts


def potted_plant():
    pot = [(.0, 0), (.07, 0), (.08, .01), (.095, .16), (.11, .17), (.112, .2), (.1, .205), (.098, .19)]
    parts = [paint(lathe('terracotta pot', pot, 20), (.55, .27, .16), TERRACOTTA, speck=.1),
             paint(lathe('soil', [(.0, .175), (.096, .175), (.096, .185), (.0, .19)], 16), (.22, .14, .09))]
    rnd = random.Random(44)
    for i in range(11):  # arching leaves: tapered tubes flattened across the blade
        a = i * 2.4; tilt = .35 + rnd.random() * .55; length = .32 + rnd.random() * .16
        pts = [(math.cos(a) * math.sin(tilt) * length * t, math.sin(a) * math.sin(tilt) * length * t, .19 + math.cos(tilt) * length * t * 1.25 - .14 * t * t * tilt) for t in (0, .25, .5, .75, 1)]
        leaf = tube('leaf', pts, [.006, .026, .032, .022, .002], 6)
        radial = Vector((math.cos(a), math.sin(a), 0)); axis = Vector((0, 0, 1)).cross(radial)  # leaf is wide along axis
        for v in leaf.data.vertices:
            along = v.co.dot(axis)
            # keep width along `axis`, squash thickness in the radial/vertical plane around the spine
            spine = min(pts, key=lambda q: (Vector(q) - v.co).length); off = v.co - Vector(spine) - along * axis
            v.co = Vector(spine) + along * axis + off * .3
        parts.append(paint(leaf, LEAF, LEAF_LIGHT, speck=.1))
    return parts


def coat_rack():
    parts = [paint(lathe('rack post', [(.022, .0), (.02, .5), (.026, .52), (.018, .54), (.016, .72), (.028, .74), (.03, .76), (.0, .77)], 14), WOOD_DARK, WOOD_LIGHT, grain=.2)]
    for i in range(3):
        a = i * math.tau / 3
        parts.append(paint(tube('foot', [(0, 0, .12), (math.cos(a) * .08, math.sin(a) * .08, .04), (math.cos(a) * .135, math.sin(a) * .135, .008)], [.016, .014, .012], 8), WOOD_DARK, WOOD))
    for i in range(4):
        a = i * math.tau / 4 + .4
        parts.append(paint(tube('hook', [(math.cos(a) * .015, math.sin(a) * .015, .66), (math.cos(a) * .07, math.sin(a) * .07, .7), (math.cos(a) * .09, math.sin(a) * .09, .73)], [.007, .006, .006], 6), BRASS))
        parts.append(paint(ell('hook tip', (math.cos(a) * .09, math.sin(a) * .09, .733), (.009, .009, .009), 6, 3), BRASS))
    a = .4  # knitted scarf hanging from one hook
    parts.append(paint(tube('scarf', [(math.cos(a) * .08, math.sin(a) * .08, .71), (math.cos(a) * .1, math.sin(a) * .1, .6), (math.cos(a) * .095, math.sin(a) * .095, .46)], [.022, .02, .018], 8), (.55, .2, .16), (.78, .36, .26), speck=.15))
    return parts


def radio():
    W, D, H = .315, .098, .228
    parts = [paint(box('cabinet', (0, 0, H * .4), (W, D, H * .8), .02, 3), WOOD_DARK, WOOD_LIGHT, grain=.18)]
    dome = ell('dome', (0, 0, H * .78), (W / 2 - .004, D / 2 - .002, H * .22), 24, 10)
    for v in dome.data.vertices: v.co.z = max(v.co.z, -H * .02)
    parts.append(paint(dome, WOOD_DARK, WOOD_LIGHT, grain=.18))
    grille = paint(box('speaker cloth', (-.045, -D / 2 - .001, H * .5), (.17, .006, .14), .01, 2), (.72, .6, .42), (.85, .74, .55), speck=.18)
    attr = grille.data.color_attributes['Color']
    for poly in grille.data.polygons:
        for li in poly.loop_indices:
            v = grille.data.vertices[grille.data.loops[li].vertex_index].co; k = .82 if int((v.x + 1) * 160) % 2 else 1
            c = attr.data[li].color_srgb; attr.data[li].color_srgb = (c[0] * k, c[1] * k, c[2] * k, 0)
    parts.append(grille)
    for x in (-.085, -.005):
        parts.append(paint(tube('fret', [(x, -D / 2 - .005, H * .5 - .06), (x, -D / 2 - .005, H * .5 + .06)], .004, 6), BRASS))
    parts.append(paint(box('dial', (.105, -D / 2 - .002, H * .62), (.07, .006, .05), .008, 2), CREAM, (1, .95, .8)))
    parts.append(paint(tube('dial needle', [(.1, -D / 2 - .006, H * .62 - .018), (.11, -D / 2 - .006, H * .62 + .018)], .002, 4), (.6, .15, .1)))
    for x in (.085, .125):
        parts.append(paint(tube('knob', [(x, -D / 2, H * .34), (x, -D / 2 - .016, H * .34)], [.016, .014], 14), BRASS, (.86, .7, .45)))
    return parts


def wind_bell():
    """Brass harbour bell under a little cedar yoke, with clapper and a paper wish strip."""
    parts = [paint(lathe('bell', [(.0, .2), (.03, .2), (.045, .185), (.055, .14), (.07, .075), (.09, .03), (.1, .012), (.095, .0), (.085, .004), (.075, .03), (.06, .08), (.045, .14), (.0, .16)], 24), BRASS, (.9, .76, .48), STEEL_EDGE, .06)]
    parts.append(paint(box('yoke', (0, 0, .225), (.13, .04, .035), .012, 2), WOOD_DARK, WOOD_LIGHT, grain=.2))
    parts.append(paint(tube('hanging ring', [(math.cos(a) * .03, 0, .27 + math.sin(a) * .03) for a in (math.tau * i / 14 for i in range(15))], .007, 6, cap=False), IRON, IRON_LIGHT))
    parts.append(paint(tube('clapper cord', [(0, 0, .16), (0, 0, .02)], .004, 5), CREAM))
    parts.append(paint(ell('clapper', (0, 0, .018), (.018, .018, .02), 10, 5), IRON, IRON_LIGHT))
    parts.append(paint(box('wish strip', (0, .0, -.06), (.035, .004, .1), .002, 1), (.82, .3, .22), (.95, .55, .4)))
    return parts


def sack():
    """Burlap sack with a soft lumpy body, gathered neck and rope tie."""
    body = ell('sack body', (0, 0, .15), (.13, .11, .16), 20, 12)
    for v in body.data.vertices:
        v.co.z = max(v.co.z, -.135); v.co *= 1 + .05 * noise.noise(v.co * 14)
        if v.co.z > .06: v.co.x *= 1 - (v.co.z - .06) * 3.5; v.co.y *= 1 - (v.co.z - .06) * 3.5
    parts = [paint(body, (.55, .42, .26), (.75, .62, .42), speck=.15, grain=.12)]
    parts.append(paint(lathe('gathered neck', [(.03, .24), (.045, .27), (.06, .31), (.03, .33), (.0, .325)], 14), (.6, .47, .3), (.78, .66, .46), speck=.2))
    parts.append(paint(tube('rope tie', [(math.cos(a) * .036, math.sin(a) * .036, .255) for a in (math.tau * i / 16 for i in range(17))], .009, 6, cap=False), (.7, .55, .32)))
    parts.append(paint(tube('rope tail', [(.036, 0, .255), (.06, -.01, .22), (.07, -.02, .17)], .007, 6), (.7, .55, .32)))
    return parts


def standing_shovel():
    parts = shovel()
    for o in parts: o.data.transform(Matrix.Rotation(math.pi, 4, 'Y'))  # residents lean it blade-down on the ground
    return parts


def planter():
    """Terracotta porch planter: rolled rim, painted teal band and a soil top for the violet."""
    parts = [paint(lathe('planter', [(.0, 0), (.13, 0), (.14, .015), (.17, .2), (.2, .215), (.205, .25), (.19, .26), (.18, .24)], 24), (.55, .27, .16), TERRACOTTA, speck=.1)]
    parts.append(paint(lathe('teal band', [(.158, .1), (.164, .1), (.168, .14), (.162, .14)], 24), TEAL, (.3, .55, .55)))
    parts.append(paint(lathe('soil', [(.0, .22), (.18, .22), (.18, .235), (.0, .24)], 18), (.22, .14, .09)))
    return parts


def violet():
    """Clump of purple violets: rosette of rounded leaves, slim stems, five-petal flowers with gold eyes."""
    parts = []; rnd = random.Random(19)
    for i in range(9):
        a = i * 2.4; r = .06 + rnd.random() * .05
        leaf = ell('leaf', (0, 0, 0), (.055, .036, .008), 10, 5)  # ell keeps location on the object; bake rotation then position
        leaf.data.transform(Matrix.Translation((math.cos(a) * r, math.sin(a) * r, .035)) @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Rotation(-.35, 4, 'Y'))
        parts.append(paint(leaf, LEAF, LEAF_LIGHT, speck=.1))
    for i in range(6):
        a = i * 1.05 + .3; r = .03 + (i % 3) * .03; h = .2 + (i % 2) * .08 + rnd.random() * .05
        tip = (math.cos(a) * (r + .03), math.sin(a) * (r + .03), h)
        parts.append(paint(tube('stem', [(math.cos(a) * r * .5, math.sin(a) * r * .5, .02), (math.cos(a) * r, math.sin(a) * r, h * .6), tip], [.005, .004, .004], 5), LEAF))
        for k in range(5):
            b = k * math.tau / 5
            parts.append(paint(ell('petal', (tip[0] + math.cos(b) * .028, tip[1] + math.sin(b) * .028, tip[2] + .004), (.03, .022, .007), 8, 4), (.42, .26, .62), (.66, .5, .86), speck=.08))
        parts.append(paint(ell('eye', (tip[0], tip[1], tip[2] + .01), (.012, .012, .008), 6, 3), (.95, .78, .3)))
    return parts


def telescope():
    """Brass spyglass on a cedar tripod, pointing to the harbour."""
    parts = []; tilt = Matrix.Rotation(-.45, 4, 'Y'); pivot = Vector((0, 0, .5))
    for r, z0, z1 in [(.045, -.05, .2), (.038, .2, .36), (.032, .36, .48)]:
        o = lathe('tube section', [(r, z0), (r + .004, z0 + .01), (r, z0 + .02), (r, z1)], 18)
        o.data.transform(Matrix.Translation(pivot) @ tilt @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Translation((0, 0, -.2)))
        parts.append(paint(o, BRASS, (.9, .76, .48), STEEL_EDGE, .06, axis=1))
    lens = lathe('lens', [(.0, -.055), (.042, -.055), (.042, -.05), (.0, -.05)], 18)
    lens.data.transform(Matrix.Translation(pivot) @ tilt @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Translation((0, 0, -.2)))
    parts.append(paint(lens, (.35, .6, .7), (.7, .88, .95)))
    parts.append(paint(ell('pivot', (0, 0, .5), (.03, .03, .03), 10, 5), IRON, IRON_LIGHT))
    for i in range(3):
        a = i * math.tau / 3 + .5
        parts.append(paint(tube('tripod leg', [(0, 0, .49), (math.cos(a) * .22, math.sin(a) * .22, 0)], [.014, .011], 8), WOOD_DARK, WOOD_LIGHT, grain=.2))
        parts.append(paint(ell('foot', (math.cos(a) * .22, math.sin(a) * .22, .006), (.016, .016, .008), 8, 4), IRON))
    return parts


def apple():
    body = ell('apple', (0, 0, .045), (.05, .05, .045), 20, 12)
    for v in body.data.vertices:
        rr = math.hypot(v.co.x, v.co.y) / .05
        v.co.z -= .012 * max(0, 1 - rr * 2.2) * (1 if v.co.z > 0 else -1)  # dimples at stem and base
        v.co.x *= 1 + .06 * v.co.z / .045; v.co.y *= 1 + .06 * v.co.z / .045
    parts = [paint(body, (.6, .14, .1), (.9, .4, .22), speck=.12)]
    attr = body.data.color_attributes['Color']  # warm blush streaks
    for poly in body.data.polygons:
        for li in poly.loop_indices:
            v = body.data.vertices[body.data.loops[li].vertex_index].co; k = .5 + .5 * noise.noise(Vector((math.atan2(v.y, v.x) * 3, 0, v.z * 30)))
            c = attr.data[li].color_srgb; attr.data[li].color_srgb = (min(1, c[0] + k * .08), min(1, c[1] + k * .12), c[2], 0)
    parts.append(paint(tube('stem', [(0, 0, .085), (.004, 0, .1), (.01, 0, .108)], [.004, .0035, .003], 6), WOOD_DARK))
    leaf = ell('leaf', (0, 0, 0), (.024, .012, .003), 10, 4); leaf.data.transform(Matrix.Translation((.026, 0, .104)) @ Matrix.Rotation(-.4, 4, 'Y'))
    parts.append(paint(leaf, LEAF, LEAF_LIGHT))
    return parts


SAND = (.86, .76, .55); ROCK = (.46, .5, .52); ROCK_TOP = (.62, .66, .6); WEED = (.2, .5, .32); WEED_TIP = (.5, .78, .45); CORAL = (.95, .5, .45)


def aquarium_stand():
    """Low cedar cabinet under the 2 x 0.9 m tank: panelled doors, brass knobs, plinth. 2.1 x 0.5 x 1.0 m."""
    parts = [paint(box('top', (0, 0, .475), (2.1, 1.0, .05), .015, 2), WOOD_DARK, WOOD_LIGHT, grain=.2),
             paint(box('carcass', (0, 0, .25), (2.0, .92, .41), .02, 2), WOOD_DARK, WOOD, grain=.2),
             paint(box('plinth', (0, 0, .025), (2.06, .96, .05), .012, 2), WOOD_DARK, WOOD_DARK)]
    for x in (-.75, -.25, .25, .75):
        parts.append(paint(box('door panel', (x, -.465, .26), (.44, .02, .33), .012, 2), WOOD, WOOD_LIGHT, grain=.2))
        parts.append(paint(ell('knob', (x + (.17 if x < 0 else -.17), -.482, .27), (.015, .012, .015), 8, 4), BRASS, (.88, .72, .45)))
    return parts


def aquarium_scape():
    """Tank interior on a 2.0 x 0.9 footprint: rippled sand bed, chubby moss-capped rocks, swaying weeds, corals."""
    rnd = random.Random(44); parts = []
    sand = bpy.data.meshes.new('sand'); nx, ny = 16, 8; vs = []
    for j in range(ny + 1):
        for i in range(nx + 1):
            x, y = -.98 + 1.96 * i / nx, -.43 + .86 * j / ny
            vs.append((x, y, .03 + .02 * math.sin(x * 4 + y * 2) + .03 * max(0, y)))
    fs = [(j * (nx + 1) + i, j * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i) for j in range(ny) for i in range(nx)]
    sand.from_pydata(vs, [], fs); sand.update(); o = bpy.data.objects.new('GEO-sand', sand); bpy.context.scene.collection.objects.link(o)
    parts.append(paint(o, (.74, .64, .45), SAND, speck=.18))
    parts.append(paint(box('sand base', (0, 0, .015), (1.96, .86, .03), 0, 1), (.7, .6, .42)))
    for x, y, s in ((-.62, .18, .17), (-.42, .25, .1), (.55, -.05, .14), (.72, .2, .11), (.05, .28, .09)):
        rock = ell('rock', (0, 0, 0), (s, s * .8, s * .7), 14, 7)
        for v in rock.data.vertices: v.co *= 1 + .12 * noise.noise(v.co * 9 + Vector((x, y, 0)) * 3)
        rock.data.transform(Matrix.Translation((x, y, .04 + s * .35)))
        parts.append(paint(rock, (.36, .4, .43), ROCK_TOP, (.44, .6, .34), .12))  # mossy upward faces
    for k in range(9):
        x, y = rnd.uniform(-.9, .9), rnd.uniform(-.05, .38); h = rnd.uniform(.25, .5); bend = rnd.uniform(-.06, .06)
        for b in range(3):
            a = b * 2.1; pts = [(x + math.cos(a) * .02, y + math.sin(a) * .02, .04), (x + bend * .5 + math.cos(a) * .03, y + math.sin(a) * .02, .04 + h * .5), (x + bend, y, .04 + h)]
            parts.append(paint(tube('weed blade', pts, [.012, .01, .002], 5), WEED, WEED_TIP, speck=.1))
    for x, y, c in ((-.15, .1, CORAL), (.32, .3, (.98, .7, .4))):
        for b in range(5):
            a = b * 1.25; pts = [(x, y, .04), (x + math.cos(a) * .04, y + math.sin(a) * .04, .1), (x + math.cos(a) * .07, y + math.sin(a) * .07, .17)]
            parts.append(paint(tube('coral branch', pts, [.016, .012, .008], 6), tuple(v * .8 for v in c), c, speck=.1))
    return parts


# ---- V044 themed portal frames. Opening stays clear: |x| < .995 below the 2.12 m shoulder, r < .975 above it.
R_IN, SHOULDER = .975, 2.12
STONE_W = (.88, .86, .8); STONE_B = (.16, .22, .38); PLANK = (.42, .3, .2); COPPER = (.72, .42, .22); VERDI = (.36, .62, .52)


def arch_point(a, r, y=0.):
    return (math.cos(a) * r, y, SHOULDER + math.sin(a) * r)


def arc_blocks(r0, r1, depth, n, colors, top=None, bevel=.03):
    out = []
    for j in range(n):
        a = math.pi * (j + .5) / n; mid = (r0 + r1) / 2
        o = box('arch block', (0, 0, 0), (r1 - r0, depth, math.pi * mid / n * .96), bevel, 2)
        o.data.transform(Matrix.Translation(arch_point(a, mid)) @ Matrix.Rotation(-a, 4, 'Y'))
        out.append(paint(o, colors[j % len(colors)], top, speck=.08))
    return out


def pillars(colors, w=.25, depth=.34, courses=5, top=None):
    out = []
    for sx in (-1, 1):
        x = sx * (R_IN + .02 + w / 2)
        for k in range(courses):
            z0, z1 = k * SHOULDER / courses + .01, (k + 1) * SHOULDER / courses - .01
            out.append(paint(box('pillar course', (x, 0, (z0 + z1) / 2), (w, depth, z1 - z0), .03, 2), colors[k % len(colors)], top, speck=.08))
        out.append(paint(box('plinth', (x, 0, .09), (w + .14, depth + .12, .18), .03, 2), colors[0], top))
    return out


def spiral(x, r, z0, z1, turns, radius, color, top=None, phase=0., sides=6):
    pts = [(x + math.cos(phase + turns * math.tau * t) * r, math.sin(phase + turns * math.tau * t) * r, z0 + (z1 - z0) * t) for t in [i / 20 for i in range(21)]]
    return paint(tube('spiral', pts, radius, sides), color, top)


def portal_wind():
    """White stone arch: shell and feather reliefs, rope wound round the pillars, a little wind bell on top."""
    parts = pillars([STONE_W, (.94, .92, .86), (.82, .8, .74)]) + arc_blocks(R_IN + .02, R_IN + .27, .3, 11, [STONE_W, (.93, .91, .85), (.8, .78, .72)])
    for sx in (-1, 1):
        x = sx * (R_IN + .145)
        parts.append(spiral(x, .15, .25, SHOULDER - .1, 3.5, .014, (.78, .66, .45), (.9, .8, .6)))
        for face in (-1, 1):
            for k, z in enumerate((.65, 1.35)):
                shell = ell('shell relief', (0, 0, 0), (.075, .02, .065), 12, 6)
                for v in shell.data.vertices: v.co.y *= 1 + .5 * abs(math.cos(math.atan2(v.co.z, v.co.x) * 6))
                shell.data.transform(Matrix.Translation((x, face * .175, z)))
                parts.append(paint(shell, (.95, .82, .74), (1, .94, .88), speck=.08))
            feather = tube('feather relief', [(x - .05, face * .172, 1.75), (x, face * .175, 1.9), (x + .04, face * .172, 2.0)], [.004, .028, .003], 6)
            feather.scale.y = .3; parts.append(paint(feather, (.86, .9, .94), (1, 1, 1)))
    crown = arch_point(math.pi / 2, R_IN + .27)
    parts.append(paint(box('keystone', crown, (.22, .34, .2), .04, 2), (.95, .93, .88)))
    parts.append(paint(tube('bell hook', [(0, 0, crown[2] + .1), (0, 0, crown[2] + .28), (.12, 0, crown[2] + .34)], .012, 6), IRON, IRON_LIGHT))
    bell = lathe('bell', [(.0, .14), (.03, .14), (.05, .1), (.065, .03), (.075, .0), (.06, .005), (.0, .05)], 16)
    bell.data.transform(Matrix.Translation((.14, 0, crown[2] + .14))); parts.append(paint(bell, BRASS, (.92, .78, .5), STEEL_EDGE))
    parts.append(paint(box('wish strip', (.14, 0, crown[2] + .06), (.03, .004, .12), .002, 1), (.6, .78, .9), (.85, .95, 1)))
    return parts


def portal_grove():
    """Living wooden arch: twisted roots form pillars and arc, vines with leaves and flowers, moss, a sprout on top."""
    parts = []; ROOT_D, ROOT_L = (.32, .2, .11), (.55, .38, .22)
    for sx in (-1, 1):
        x = sx * (R_IN + .14)
        for k in range(3):
            parts.append(spiral(x, .07, -.02, SHOULDER + .05, 1.2, .065, ROOT_D, ROOT_L, phase=k * math.tau / 3, sides=7))
        for k in range(3):  # buttress roots
            a = k * math.tau / 3 + sx
            parts.append(paint(tube('root foot', [(x, 0, .5), (x + math.cos(a) * .2, math.sin(a) * .2, .12), (x + math.cos(a) * .32, math.sin(a) * .32, .0)], [.06, .05, .02], 8), ROOT_D, ROOT_L))
    for k in range(3):
        pts = [arch_point(math.pi * t / 16, R_IN + .14 + .06 * math.cos(k * 2.1 + t * 1.3), .06 * math.sin(k * 2.1 + t * 1.3)) for t in range(17)]
        parts.append(paint(tube('arch root', pts, .06, 7), ROOT_D, ROOT_L))
    rnd = random.Random(31)
    for sx in (-1, 1):
        x = sx * (R_IN + .14)
        pts = [(x + math.cos(t * 9) * .15, math.sin(t * 9) * .15, .3 + t * (SHOULDER - .3)) for t in [i / 24 for i in range(25)]]
        parts.append(paint(tube('vine', pts, .012, 5), LEAF, LEAF_LIGHT))
        for p in pts[::3]:
            leaf = ell('leaf', (0, 0, 0), (.05, .012, .03), 8, 4); leaf.data.transform(Matrix.Translation((p[0] + .03 * sx, p[1], p[2])) @ Matrix.Rotation(rnd.uniform(-.6, .6), 4, 'Y'))
            parts.append(paint(leaf, LEAF, LEAF_LIGHT, speck=.1))
        for z in (.4, 1.1, 1.8):
            moss = ell('moss', (0, 0, 0), (.13, .2, .06), 10, 5); moss.data.transform(Matrix.Translation((x, 0, z)))
            parts.append(paint(moss, (.3, .45, .22), (.5, .65, .3), speck=.2))
    for k in range(7):
        p = arch_point(math.pi * (k + .5) / 7, R_IN + .2, rnd.uniform(-.1, .1))
        col = [(.95, .85, .35), (.9, .55, .7), (.98, .98, .92)][k % 3]
        for b in range(5):
            a = b * math.tau / 5; parts.append(paint(ell('petal', (p[0] + math.cos(a) * .025, p[1] - .09, p[2] + math.sin(a) * .025), (.022, .01, .016), 6, 3), col, (1, 1, 1)))
    crown = arch_point(math.pi / 2, R_IN + .2)
    parts.append(paint(tube('sprout stem', [crown, (0, 0, crown[2] + .2), (.04, 0, crown[2] + .32)], [.02, .015, .01], 6), LEAF, LEAF_LIGHT))
    for s in (-1, 1):
        leaf = ell('sprout leaf', (0, 0, 0), (.09, .02, .045), 10, 5); leaf.data.transform(Matrix.Translation((.04 + s * .08, 0, crown[2] + .34)) @ Matrix.Rotation(s * .4, 4, 'Y'))
        parts.append(paint(leaf, LEAF, LEAF_LIGHT))
    return parts


def portal_tide():
    """Old ship timber arch crusted with coral and barnacles, a copper porthole keystone and hanging kelp."""
    parts = pillars([PLANK, (.5, .36, .24), (.36, .25, .17)], w=.27, top=(.62, .5, .38)) + arc_blocks(R_IN + .02, R_IN + .29, .3, 9, [PLANK, (.5, .36, .24), (.36, .25, .17)], (.62, .5, .38), .02)
    for sx in (-1, 1):
        x = sx * (R_IN + .155)
        for z in (.3, 1.0, 1.7):
            parts.append(paint(box('iron strap', (x, 0, z), (.29, .36, .05), .01, 1), IRON, IRON_LIGHT))
    rnd = random.Random(12)
    for k in range(26):  # barnacles on the outer faces
        if k < 14: sx = rnd.choice((-1, 1)); p = (sx * (R_IN + .02 + rnd.uniform(.03, .24)), rnd.choice((-1, 1)) * .17, rnd.uniform(.2, 1.4))
        else: a = rnd.uniform(.1, math.pi - .1); p = arch_point(a, R_IN + rnd.uniform(.08, .26), rnd.choice((-1, 1)) * .155)
        cone = lathe('barnacle', [(.0, 0), (.03, 0), (.018, .025), (.008, .028), (.0, .02)], 8)
        cone.data.transform(Matrix.Translation(p) @ Matrix.Rotation(math.pi / 2 * (1 if p[1] > 0 else -1), 4, 'X'))
        parts.append(paint(cone, (.75, .72, .66), (.95, .93, .88), speck=.12))
    for sx, z, c in ((-1, .35, CORAL), (1, .5, (.98, .7, .4)), (-1, 1.5, (.85, .45, .6))):
        base = (sx * (R_IN + .3), 0, z)
        for b in range(5):
            a = b * 1.25; parts.append(paint(tube('coral', [base, (base[0] + sx * .06, math.cos(a) * .06, z + .06 + math.sin(a) * .04), (base[0] + sx * .1, math.cos(a) * .1, z + .14 + math.sin(a) * .06)], [.022, .016, .008], 6), tuple(v * .8 for v in c), c))
    crown = arch_point(math.pi / 2, R_IN + .155)
    ring = [(math.cos(a) * .17, -.17, crown[2] + .2 + math.sin(a) * .17) for a in (math.tau * i / 24 for i in range(25))]
    parts.append(paint(tube('porthole ring', ring, .035, 8, cap=False), COPPER, (.95, .66, .4), VERDI))
    parts.append(paint(ell('porthole glass', (0, -.17, crown[2] + .2), (.14, .01, .14), 12, 4), (.25, .5, .55), (.5, .8, .8)))
    for k in range(6):  # kelp hanging from the arch
        a = math.pi * (k + 1) / 7; top = arch_point(a, R_IN + .03); length = .35 + .2 * (k % 3) / 2
        pts = [(top[0] + .03 * math.sin(t * 9), .0, top[2] - length * t) for t in (0, .25, .5, .75, 1)]
        kelp = tube('kelp', pts, [.025, .03, .028, .02, .005], 5); kelp.scale.y = .3
        parts.append(paint(kelp, (.2, .38, .2), (.42, .55, .28), speck=.12))
    return parts


def portal_star():
    """Deep blue stone with an engraved brass astrolabe ring, constellation studs and a four-point star on top."""
    parts = pillars([STONE_B, (.2, .27, .44), (.13, .18, .32)], top=(.3, .38, .58)) + arc_blocks(R_IN + .02, R_IN + .27, .3, 13, [STONE_B, (.2, .27, .44), (.13, .18, .32)], (.3, .38, .58))
    for face in (-1, 1):
        ring = [arch_point(math.pi * t / 32, R_IN + .145, face * .165) for t in range(33)]
        parts.append(paint(tube('astrolabe ring', ring, .022, 8, cap=False), BRASS, (.95, .82, .5), STEEL_EDGE))
        for t in range(1, 16):
            a = math.pi * t / 16; parts.append(paint(tube('tick', [arch_point(a, R_IN + .1, face * .18), arch_point(a, R_IN + .2 if t % 4 == 0 else R_IN + .16, face * .18)], .006, 4), BRASS))
        for sx in (-1, 1):  # constellations studded on the pillars
            x = sx * (R_IN + .145); stars = [(x - .05, .4), (x + .06, .7), (x - .02, 1.0), (x + .05, 1.35), (x - .04, 1.7)]
            for (px, pz) in stars: parts.append(paint(ell('star stud', (px, face * .175, pz), (.022, .012, .022), 8, 4), (.95, .85, .5), (1, .97, .8)))
            parts.append(paint(tube('constellation line', [(px, face * .172, pz) for px, pz in stars], .004, 4), (.75, .65, .4)))
    crown = arch_point(math.pi / 2, R_IN + .27)
    star = [(0, 0, crown[2] + .22)]
    for k in range(8):
        a = math.pi / 2 + k * math.tau / 8; r = .2 if k % 2 == 0 else .05
        star.append((math.cos(a) * r, 0, crown[2] + .22 + math.sin(a) * r))
    verts = [(x, y + d, z) for d in (-.03, .03) for x, y, z in star]
    faces = [(0, 1 + k, 1 + (k + 1) % 8) for k in range(8)] + [(9, 10 + (k + 1) % 8, 10 + k) for k in range(8)] + [(1 + k, 1 + (k + 1) % 8, 10 + (k + 1) % 8, 10 + k) for k in range(8)]
    mesh = bpy.data.meshes.new('four point star'); mesh.from_pydata(verts, [], faces); mesh.update()
    o = bpy.data.objects.new('GEO-four point star', mesh); bpy.context.scene.collection.objects.link(o)
    parts.append(paint(o, (.95, .8, .4), (1, .97, .8), STEEL_EDGE))
    parts.append(paint(tube('star post', [(0, 0, crown[2] + .1), (0, 0, crown[2] + .02)], .02, 6), BRASS))
    return parts


def drift_bottle():
    """Old green glass bottle, cork and a rolled letter inside; 0.4 m long, +Y forward (Unity +Z), lying on its side."""
    glass = lathe('Glass', [(.0, 0), (.055, 0), (.062, .01), (.064, .2), (.06, .25), (.035, .3), (.018, .33), (.018, .38), (.022, .385), (.02, .39), (.0, .39)], 18)
    glass.data.transform(Matrix.Translation((0, -.2, 0)) @ Matrix.Rotation(-math.pi / 2, 4, 'X'))
    paint(glass, (.25, .5, .4), (.5, .78, .62), speck=.04)
    cork = lathe('cork', [(.0, .38), (.017, .38), (.019, .42), (.0, .425)], 10)
    cork.data.transform(Matrix.Translation((0, -.2, 0)) @ Matrix.Rotation(-math.pi / 2, 4, 'X'))
    letter = tube('letter', [(0, -.16, 0), (0, .06, 0)], .03, 12)
    ribbon = tube('ribbon', [(0, -.05, 0), (0, -.04, 0)], .033, 12)
    return [glass, paint(cork, (.6, .42, .25), (.75, .58, .38), speck=.2), paint(letter, CREAM, (1, .96, .84)), paint(ribbon, (.7, .2, .18))]


JOBS = {
    'Axe': (TOOLS, axe, (1.3, -1.9, .9)), 'Hammer': (TOOLS, hammer, (1.3, -1.9, .9)), 'Shovel': (TOOLS, shovel, (1.3, -1.9, .9)),
    'Saw': (TOOLS, saw, (.9, -2.2, .9)), 'HomeChest': (TOOLS, chest, (1.3, -1.9, 1.1)), 'Crab': (FISH, crab, (1.0, 1.6, 1.4)),
    'lampRoundTable': (HOME, lamp_table, (1.3, -1.9, .9)), 'lampRoundFloor': (HOME, lamp_floor, (1.3, -1.9, .9)), 'books': (HOME, books, (1.3, -1.9, 1.4)),
    'pottedPlant': (HOME, potted_plant, (1.3, -1.9, .9)), 'coatRackStanding': (HOME, coat_rack, (1.3, -1.9, .9)), 'radio': (HOME, radio, (1.0, -2.2, .9)),
    'WindBell': (V019, wind_bell, (1.3, -1.9, .9)), 'bag': (V019, sack, (1.3, -1.9, .9)), 'tool-shovel': (V019, standing_shovel, (1.3, -1.9, .9)),
    'pot': (V019, planter, (1.3, -1.9, 1.2)), 'flower_purpleA': (V019, violet, (1.3, -1.9, 1.2)), 'Telescope': (V019, telescope, (1.3, -1.9, .9)),
    'apple': (V019, apple, (1.3, -1.9, .9)), 'chest': (V019, chest, (1.3, -1.9, 1.1)),
    'AquariumStand': (V044, aquarium_stand, (1.3, -1.9, 1.1)), 'AquariumScape': (V044, aquarium_scape, (1.3, -1.9, 1.6)),
    'PortalFrame0': (V044, portal_wind, (.6, -2.2, .5)), 'PortalFrame1': (V044, portal_grove, (.6, -2.2, .5)),
    'PortalFrame2': (V044, portal_tide, (.6, -2.2, .5)), 'PortalFrame3': (V044, portal_star, (.6, -2.2, .5)),
    'DriftBottle': (V044 / 'Sea', drift_bottle, (1.3, -1.9, 1.0)),
}
only = [a for a in sys.argv[sys.argv.index('--') + 1:] if not a.startswith('--')] if '--' in sys.argv else []
report = []
for name, (folder, build, view) in JOBS.items():
    if only and name not in only: continue
    if BEFORE: before(name, (BACKUP / 'Home' if folder == HOME else BACKUP / 'V019' if folder == V019 else BACKUP) / (name + '.fbx'), view); continue
    reset(); parts = build()
    if name == 'DriftBottle':  # sea-events gives the "Glass" child its own refraction shader
        glass = parts[0]; body = join(parts[1:], 'GEO-DriftBottle'); glass.name = 'Glass'; glass.parent = body; objs = [body, glass]
    else: objs = parts if name in ('HomeChest', 'chest') else [join(parts, 'GEO-' + name)]
    for o in objs:
        for poly in o.data.polygons: poly.material_index = 0
    if name == 'Crab':  # FishingV023Validation pins marine fauna to its catalogue length
        f = .49 / max(objs[0].dimensions); objs[0].data.transform(Matrix.Scale(f, 4))
    count = tris(objs)
    limit = 6000 if name.startswith('PortalFrame') else 5000 if name in ('HomeChest', 'chest', 'Crab') else 3000
    assert count <= limit, (name, count)
    if folder == V019: vertex_material().name = 'MAT-V019-vertex-palette'  # matches the importer remap in each .fbx.meta
    export(objs, folder / (name + '.fbx'))
    render(SHOTS / (name + '-after.png'), view=view)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART / (name + '.blend')))  # after render so the preview camera and lights are kept
    if name in ('Axe', 'Shovel', 'Crab') or folder == HOME:  # runtime UI thumbnails come from Resources too
        bpy.context.scene.render.film_transparent = True; bpy.context.scene.render.resolution_x = 384 if name == 'Crab' else 256
        bpy.context.scene.render.resolution_y = 216 if name == 'Crab' else 256
        bpy.context.scene.render.filepath = str(folder / (name + 'Thumbnail.png')); bpy.ops.render.render(write_still=True)
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    report.append(dict(name=name, triangles=count, low=[round(min(p[i] for p in pts), 3) for i in range(3)], high=[round(max(p[i] for p in pts), 3) for i in range(3)]))
print('V044_PROPS', json.dumps(report))
