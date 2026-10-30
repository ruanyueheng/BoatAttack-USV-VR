"""Studio renders of the finished game assets for coursework submission.

Reads only the exported runtime assets under Assets/ (plus their Unity-bound textures)
and writes PNG previews to Docs/Images/AssetShowcase. No source asset is modified.

Usage: blender --background --factory-startup --python Tools/Art/render_asset_showcase.py -- <group>
Groups: gear | props | props-solo | characters | all
"""
import bpy, sys, json, math, warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/Images/AssetShowcase'
OUT.mkdir(parents=True, exist_ok=True)
GEAR = ROOT / 'Assets/Resources/FishingGear'
PROPS = ROOT / 'Assets/Resources/FishingProps'
CASHIER = ROOT / 'Assets/Resources/FishingCashier'
CHARACTER = ROOT / 'Assets/Resources/FishingCharacter'

SHOWCASE_SPAN = 2.0  # Every solo render is normalised to this bounding span so exposure matches.
stats = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.render.film_transparent = False
    scene.render.resolution_percentage = 100
    world = bpy.data.worlds.new('Studio')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.26, .27, .29, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0


def backdrop(span=SHOWCASE_SPAN):
    """Seamless pale sweep behind and under the subject."""
    bpy.ops.mesh.primitive_plane_add(size=span * 120)
    floor = bpy.context.object
    floor.location.z = -span * .002
    mat = bpy.data.materials.new('Sweep')
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (.78, .79, .80, 1)
    bsdf.inputs['Roughness'].default_value = .55
    floor.data.materials.append(mat)
    return floor


def lights(span=SHOWCASE_SPAN):
    """Key plus fill, scaled with the subject so brightness is identical at any asset size."""
    aim = Vector((0, 0, span * .45))
    for direction, power, size in [((-1.4, -1.9, 2.3), 2.1, 2.4), ((1.9, .6, 1.9), 2.8, 2.0)]:
        loc = aim + Vector(direction) * span
        bpy.ops.object.light_add(type='AREA', location=loc)
        light = bpy.context.object
        light.data.shape = 'DISK'
        light.data.size = size * span
        # Area lights fall off with distance; keep irradiance constant across spans.
        light.data.energy = power * 1000 * (span / SHOWCASE_SPAN) ** 2
        light.rotation_euler = (aim - loc).to_track_quat('-Z', 'Y').to_euler()


def bounds(objs):
    corners = [o.matrix_world @ Vector(c) for o in objs if o.type == 'MESH' for c in o.bound_box]
    lo = Vector((min(c[i] for c in corners) for i in range(3)))
    hi = Vector((max(c[i] for c in corners) for i in range(3)))
    return lo, hi


def place(objs, span=SHOWCASE_SPAN, normalise=True):
    """Centre the subject on the backdrop, optionally rescaling it to the showcase span."""
    roots = [o for o in objs if o.parent is None]
    lo, hi = bounds(objs)
    size = hi - lo
    scale = span / max(size) if normalise and max(size) > 0 else 1.0
    pivot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for root in roots:
        root.scale *= scale
        root.location = (root.location - pivot) * scale
    bpy.context.view_layer.update()
    return scale


def fit_camera(objs, res, azimuth=-128, elevation=23, pad=1.12):
    """Orthographic camera framed exactly on the subject's silhouette from this angle."""
    lo, hi = bounds(objs)
    centre = (lo + hi) / 2
    a, e = math.radians(azimuth), math.radians(elevation)
    forward = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    span = max(hi - lo)
    bpy.ops.object.camera_add(location=centre + forward * span * 6)
    cam = bpy.context.object
    cam.rotation_euler = (-forward).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'
    bpy.context.view_layer.update()
    right = cam.matrix_world.to_quaternion() @ Vector((1, 0, 0))
    up = cam.matrix_world.to_quaternion() @ Vector((0, 1, 0))
    corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    us = [c.dot(right) for c in corners]
    vs = [c.dot(up) for c in corners]
    width, height = max(us) - min(us), max(vs) - min(vs)
    aspect = res[0] / res[1]
    # ortho_scale covers the longer sensor edge, so convert the tighter axis across.
    needed = max(width, height * aspect) if aspect >= 1 else max(width / aspect, height)
    cam.data.ortho_scale = needed * pad
    # Slide the camera sideways so the silhouette sits centred rather than around the pivot.
    cam.location += right * ((max(us) + min(us)) / 2 - centre.dot(right))
    cam.location += up * ((max(vs) + min(vs)) / 2 - centre.dot(up))
    bpy.context.scene.camera = cam
    return cam


def vertex_palette():
    """Mirrors Resources/FishingProps/HarborPalette.mat: vertex-colour base, no maps."""
    mat = bpy.data.materials.new('HarborPalette preview')
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    attr = mat.node_tree.nodes.new('ShaderNodeVertexColor')
    attr.layer_name = 'PropColor'
    mat.node_tree.links.new(attr.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = .4
    bsdf.inputs['Metallic'].default_value = 0
    return mat


def textured(name, base, normal=None, metal_smooth=None, smoothness=.28, metallic=0.0, uv=None):
    """Approximates a URP Lit material from the same textures the Unity material binds."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    coord = None
    if uv:
        coord = tree.nodes.new('ShaderNodeUVMap')
        coord.uv_map = uv

    def image(path, non_color):
        tex = tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(path))
        if non_color:
            tex.image.colorspace_settings.name = 'Non-Color'
        if coord:
            tree.links.new(coord.outputs['UV'], tex.inputs['Vector'])
        return tex

    tree.links.new(image(base, False).outputs['Color'], bsdf.inputs['Base Color'])
    if normal:
        nmap = tree.nodes.new('ShaderNodeNormalMap')
        tree.links.new(image(normal, True).outputs['Color'], nmap.inputs['Color'])
        tree.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    if metal_smooth:
        # Unity packs metalness in R and smoothness in A; roughness is 1 - smoothness.
        tex = image(metal_smooth, True)
        sep = tree.nodes.new('ShaderNodeSeparateColor')
        tree.links.new(tex.outputs['Color'], sep.inputs['Color'])
        tree.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
        invert = tree.nodes.new('ShaderNodeMath')
        invert.operation = 'SUBTRACT'
        invert.inputs[0].default_value = 1.0
        tree.links.new(tex.outputs['Alpha'], invert.inputs[1])
        tree.links.new(invert.outputs['Value'], bsdf.inputs['Roughness'])
    else:
        bsdf.inputs['Roughness'].default_value = 1 - smoothness
        bsdf.inputs['Metallic'].default_value = metallic
    return mat


def load(path, material=None, record=None):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objs = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in objs if o.type == 'MESH']
    tris = 0
    for mesh in meshes:
        mesh.data.calc_loop_triangles()
        tris += len(mesh.data.loop_triangles)
        if material:
            mesh.data.materials.clear()
            mesh.data.materials.append(material)
    if record:
        lo, hi = bounds(meshes)
        stats[record] = {'triangles': tris,
                         'metres_xyz': [round(v, 3) for v in (hi - lo)],
                         'meshes': len(meshes)}
    return objs


def shoot(name, res=(1400, 1400)):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = str(OUT / f'{name}.png')
    bpy.ops.render.render(write_still=True)
    print('SHOWCASE_RENDER ' + name)


def solo(path, material_factory, name, record, azimuth=-128, elevation=23, pad=1.16, res=(1400, 1400)):
    reset()
    backdrop()
    lights()
    objs = load(path, material_factory() if material_factory else None, record)
    place(objs)
    fit_camera(objs, res, azimuth, elevation, pad)
    shoot(name, res)


# ---------------------------------------------------------------- groups
def gear_group():
    """The rod as the game assembles it: reel crank seated at its authored mounting point.

    A true-scale group shot of the whole set does not read - the rod is 2.3 m and the glove
    phalanx 3 cm - so the four pieces are delivered as individual plates instead.
    """
    reset()
    span = 2.6
    backdrop(span)
    lights(span)
    mat = vertex_palette()
    objs = load(GEAR / 'RodBody.fbx', mat, 'RodBody')
    # FishingDetailArt.Rod seats the crank here, in rod-local metres.
    crank = load(GEAR / 'ReelCrank.fbx', mat, 'ReelCrank')
    for ob in crank:
        if ob.parent is None:
            ob.location += Vector((-.053, -.015, -.14))
    objs += crank
    for ob in objs:
        if ob.parent is None:
            ob.rotation_euler.y = math.radians(90)
    res = (1800, 760)
    fit_camera(objs, res, azimuth=-150, elevation=24, pad=1.07)
    shoot('fishing-gear-rod-assembled', res)


def gear_solos():
    mat = vertex_palette
    solo(GEAR / 'RodBody.fbx', mat, 'gear-01-rod-body', 'RodBody', azimuth=-140, elevation=18)
    solo(GEAR / 'ReelCrank.fbx', mat, 'gear-02-reel-crank', 'ReelCrank')
    solo(GEAR / 'GlovePalm.fbx', mat, 'gear-03-glove-palm', 'GlovePalm', azimuth=-118)
    solo(GEAR / 'GloveFinger.fbx', mat, 'gear-04-glove-finger', 'GloveFinger')


PROP_SOLOS = [
    ('BeaconBody', 'props-01-beacon-body', -135, 20),
    ('BeaconHead', 'props-02-beacon-head', -135, 25),
    ('BeaconLens', 'props-03-beacon-lens', -135, 18),
    ('CrateBody', 'props-04-crate-body', -128, 26),
    ('RepairCoil', 'props-05-repair-coil', -128, 28),
    ('ChannelMarker', 'props-06-channel-marker', -150, 16),
    ('MaintenanceRecord', 'props-07-maintenance-record', -118, 22),
    ('CargoSignFish', 'props-08-cargo-sign-fish', -115, 24),
    ('CargoSignService', 'props-09-cargo-sign-service', -115, 24),
    ('CargoSignCloth', 'props-10-cargo-sign-cloth', -115, 24),
]


def prop_solos():
    for asset, name, azimuth, elevation in PROP_SOLOS:
        solo(PROPS / f'{asset}.fbx', vertex_palette, name, asset, azimuth=azimuth, elevation=elevation)


def props_group():
    """Harbour quest props assembled the way they appear in the bay, at true scale."""
    reset()
    span = 7.0
    backdrop(span)
    lights(span)
    mat = vertex_palette()
    layout = [('CrateBody', (-1.25, 0, 0)), ('CargoSignService', (-1.25, 0, 0)), ('RepairCoil', (-1.25, 0, .74)),
              ('BeaconBody', (.2, .35, 0)), ('BeaconHead', (.2, .35, 2.25)), ('BeaconLens', (.2, .35, 2.25)),
              ('ChannelMarker', (1.9, 1, 0)), ('MaintenanceRecord', (-2.2, .55, 1.35)),
              ('CargoSignFish', (-1.95, -.2, .15)), ('CargoSignCloth', (-2.45, -.2, .15))]
    objs = []
    for asset, offset in layout:
        loaded = load(PROPS / f'{asset}.fbx', mat, asset)
        for ob in loaded:
            if ob.parent is None:
                ob.location += Vector(offset)
        objs += loaded
    res = (1800, 1250)
    fit_camera(objs, res, azimuth=-145, elevation=24, pad=1.10)
    shoot('harbor-props-set', res)


def characters():
    res = (1080, 1500)
    # FBX import puts Unity-forward +Z along Blender -Y, so the face is towards +20 degrees.
    front = 20
    clerk = CASHIER / 'CoastalClerk'
    solo(clerk / 'CoastalClerk_LOD0.fbx',
         lambda: textured('CoastalClerk', clerk / 'ClerkBaseColor.png', clerk / 'ClerkNormal.png',
                          smoothness=.28, uv='UVMap'),
         'character-01-coastal-clerk', 'CoastalClerk_LOD0', azimuth=front, elevation=8, pad=1.12, res=res)
    girl = CASHIER / 'FantasyGirl'
    solo(girl / 'FantasyGirl_LOD0.fbx',
         lambda: textured('FantasyGirl', girl / 'FantasyGirl_basecolor.jpg', girl / 'FantasyGirl_normal.jpg',
                          girl / 'FantasyGirl_metallic_smoothness.png', uv='UVMap'),
         'character-02-fantasy-girl', 'FantasyGirl_LOD0', azimuth=front, elevation=8, pad=1.12, res=res)
    # Beach character keeps the flat-colour materials the FBX itself carries.
    solo(CHARACTER / 'BeachCharacter.fbx', None,
         'character-03-beach-character', 'BeachCharacter', azimuth=front, elevation=8, pad=1.12, res=res)


GROUPS = {'gear': lambda: (gear_solos(), gear_group()),
          'props': props_group,
          'props-solo': prop_solos,
          'characters': characters}

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['all']
selected = list(GROUPS) if argv[0] == 'all' else argv
for key in selected:
    GROUPS[key]()
report = OUT / f'asset-stats-{"-".join(selected)}.json'
report.write_text(json.dumps(stats, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print('SHOWCASE_DONE ' + json.dumps(sorted(stats)))
