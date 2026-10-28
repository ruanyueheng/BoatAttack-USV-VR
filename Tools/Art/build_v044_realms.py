"""V044 'four-island realm' storybook props. Headless:
blender --background --factory-startup --python-exit-code 1 --python Tools/Art/build_v044_realms.py
Built in Blender Z-up metres; exported with the V035 FBX axis settings so Blender +Z = Unity +Y, Blender -Y = Unity +Z.
"""
from pathlib import Path
import bpy, bmesh, math, json, random, uuid
import numpy as np
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV044'
SOURCE = ROOT / 'ArtSource/V044'
PREVIEW = ROOT / 'Logs/V044/realms'
for d in (OUT, SOURCE, PREVIEW): d.mkdir(parents=True, exist_ok=True)
RNG = random.Random(44)
MAT_NAME = 'RealmStorybook'
# atlas cells (4 cols x 2 rows): brass, gold, shell cream, stone, moss, blue+stars, wood, deep blue
BRASS, GOLD, CREAM, STONE, MOSS, CLOTH, WOOD, DEEP = range(8)
CELL_RGB = [(.72, .53, .24), (.96, .66, .26), (.96, .88, .72), (.62, .62, .58),
            (.40, .52, .27), (.12, .22, .44), (.52, .35, .21), (.09, .15, .32)]

# ---------------------------------------------------------------- atlas
def noise(size, cells, rng):
    g = rng.random((cells + 1, cells + 1))
    x = np.linspace(0, cells, size, endpoint=False)
    i = x.astype(int); f = x - i; f = f * f * (3 - 2 * f)
    a = g[np.ix_(i, i)] * (1 - f)[None, :] + g[np.ix_(i, i + 1)] * f[None, :]
    b = g[np.ix_(i + 1, i)] * (1 - f)[None, :] + g[np.ix_(i + 1, i + 1)] * f[None, :]
    return a * (1 - f)[:, None] + b * f[:, None]

def fbm(size, rng, base=4, octaves=5):
    return sum(noise(size, base * 2 ** o, rng) / 2 ** o for o in range(octaves)) / 1.94

def star_mask(h, w, cx, cy, r):
    yy, xx = np.mgrid[0:h, 0:w]
    a = np.arctan2(yy - cy, xx - cx); d = np.hypot(yy - cy, xx - cx)
    edge = r * (.45 + .55 * np.abs(np.cos(a * 2.5)) ** 3)
    return np.clip((edge - d) / 2.0, 0, 1)

def make_atlas():
    rng = np.random.default_rng(44)
    S = 1024; cw, ch = 256, 512
    img = np.zeros((S, S, 3))
    paper = fbm(S, rng, 32, 3)
    for c, rgb in enumerate(CELL_RGB):
        n1 = fbm(512, rng, 3); n2 = fbm(512, rng, 12, 3)
        # brush strokes: noise stretched along one axis
        strokes = noise(512, 24, rng)[:, ::4].repeat(4, axis=1)
        col = np.array(rgb)[None, None, :] * (0.82 + .30 * n1[..., None] + .12 * (strokes[..., None] - .5))
        col = col * (0.93 + .1 * n2[..., None])
        h, w = ch, cw
        n1 = n1[:h, :w]; col = col[:h, :w]
        if c == STONE:  # lichen and salt specks
            lich = fbm(512, rng, 10)[:h, :w] > .62
            col[lich] = col[lich] * .55 + np.array([.55, .62, .38]) * .45
            salt = rng.random((h, w)) > .992
            col[salt] = (.95, .94, .9)
        if c == WOOD:
            yy = np.arange(h)[:, None]; grain = np.sin(yy * .35 + fbm(512, rng, 6)[:h, :w] * 9)
            col *= (0.88 + .12 * grain[..., None])
        if c in (BRASS, GOLD):
            streak = fbm(512, rng, 6, 3)[:h, :w]
            col *= (0.9 + .3 * streak[..., None])
            patina = fbm(512, rng, 8)[:h, :w] > .8
            col[patina] = col[patina] * .6 + np.array([.36, .55, .45]) * .4
        if c == CREAM:
            yy = np.arange(h)[:, None]; band = .5 + .5 * np.sin(yy * .12)
            col *= (0.9 + .1 * band[..., None]); col[..., 0] += .04 * band
        if c == CLOTH:
            for sy in range(40, h, 85):
                for sx in range(30 + (sy // 85 % 2) * 60, w, 120):
                    m = star_mask(h, w, sx, sy, 22)[..., None]
                    col = col * (1 - m) + np.array([.98, .78, .30]) * m
        # watercolor edge darkening toward cell borders
        yy, xx = np.mgrid[0:h, 0:w]
        dist = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)).astype(float)
        edge = np.clip(1 - dist / 14, 0, 1) * (.6 + .4 * n1)
        col *= (1 - .25 * edge[..., None])
        r0, c0 = c // 4, c % 4
        img[r0 * ch:(r0 + 1) * ch, c0 * cw:(c0 + 1) * cw] = col
    img *= (0.95 + .08 * paper[..., None])
    img = np.clip(img, 0, 1)
    path = OUT / 'RealmStorybook.png'
    im = bpy.data.images.new('RealmStorybook', S, S, alpha=True)
    px = np.ones((S, S, 4)); px[..., :3] = img[::-1]  # Blender rows are bottom-up
    im.pixels.foreach_set(px.astype(np.float32).ravel())
    im.filepath_raw = str(path); im.file_format = 'PNG'; im.save()
    return im

def cell_rect(tile):
    c0, r0 = tile % 4, tile // 4
    u0 = c0 / 4; v1 = 1 - r0 / 2  # top of cell in UV space
    return u0 + .02, v1 - .5 + .02, .25 - .04, .5 - .04

# ---------------------------------------------------------------- building blocks
PARTS = []

def finish(obj, tile, smooth=True, bevel=0, segs=3, subsurf=0, jitter=0, taper=0):
    bpy.context.view_layer.objects.active = obj
    if bevel:
        m = obj.modifiers.new('bev', 'BEVEL'); m.width = bevel; m.segments = segs; m.limit_method = 'ANGLE'
    if subsurf:
        m = obj.modifiers.new('sub', 'SUBSURF'); m.levels = subsurf
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    old = obj.data; obj.modifiers.clear(); obj.data = me; bpy.data.meshes.remove(old)
    while me.uv_layers: me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name='UVMap')  # one identically named layer, else join() leaves parts unmapped
    bm = bmesh.new(); bm.from_mesh(me)
    if jitter:
        for v in bm.verts: v.co += Vector([RNG.uniform(-jitter, jitter) for _ in range(3)])
    if taper:  # narrow toward the top and add a soft lopsided bulge
        zs = [v.co.z for v in bm.verts]; z0, z1 = min(zs), max(zs)
        for v in bm.verts:
            t = (v.co.z - z0) / (z1 - z0); k = 1 - taper * t * t + .06 * math.sin(t * 3.1)
            v.co.x *= k; v.co.y *= 1 - taper * .5 * t * t; v.co.x += .05 * math.sin(t * 2.2)
    bm.normal_update()
    lo = Vector([min(v.co[a] for v in bm.verts) for a in range(3)])
    hi = Vector([max(v.co[a] for v in bm.verts) for a in range(3)])
    uv = bm.loops.layers.uv.verify(); u0, v0, du, dv = cell_rect(tile)
    for f in bm.faces:
        f.smooth = smooth
        ax = max(range(3), key=lambda a: abs(f.normal[a])); a, b = [i for i in range(3) if i != ax]
        for l in f.loops:
            s = (l.vert.co[a] - lo[a]) / max(1e-4, hi[a] - lo[a]); t = (l.vert.co[b] - lo[b]) / max(1e-4, hi[b] - lo[b])
            l[uv].uv = (u0 + s * du, v0 + t * dv)
    bm.to_mesh(me); bm.free()
    PARTS.append(obj)
    return obj

def add(kind, loc=(0, 0, 0), scale=(1, 1, 1), rot=(0, 0, 0), **kw):
    getattr(bpy.ops.mesh, 'primitive_' + kind + '_add')(location=loc, rotation=rot, **kw)
    o = bpy.context.active_object; o.scale = scale
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o

def lathe(rows, tile, n=16, loc=(0, 0, 0), smooth=True):
    """rows: (z, r) profile, revolved about Z."""
    bm = bmesh.new(); rings = []
    for z, r in rows:
        rings.append([bm.verts.new((loc[0] + r * math.cos(k * math.tau / n), loc[1] + r * math.sin(k * math.tau / n), loc[2] + z)) for k in range(n)])
    for a, b in zip(rings, rings[1:]):
        for k in range(n): bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if rows[0][1] > 1e-4: bm.faces.new(list(reversed(rings[0])))
    if rows[-1][1] > 1e-4: bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('lathe'); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('lathe', me); bpy.context.collection.objects.link(o)
    return finish(o, tile, smooth)

def tube(pts, radius, tile, n=8, caps=True):
    """radius: float or per-point list."""
    bm = bmesh.new(); pts = [Vector(p) for p in pts]; rings = []
    rr = radius if isinstance(radius, (list, tuple)) else [radius] * len(pts)
    for j, p in enumerate(pts):
        t = (pts[min(j + 1, len(pts) - 1)] - pts[max(j - 1, 0)]).normalized()
        side = t.cross(Vector((0, 0, 1)))
        if side.length < .1: side = t.cross(Vector((1, 0, 0)))
        side.normalize(); up = t.cross(side).normalized()
        rings.append([bm.verts.new(p + (side * math.cos(k * math.tau / n) + up * math.sin(k * math.tau / n)) * rr[j]) for k in range(n)])
    for a, b in zip(rings, rings[1:]):
        for k in range(n): bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if caps: bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('tube'); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('tube', me); bpy.context.collection.objects.link(o)
    return finish(o, tile)

def join(name, pivot=(0, 0, 0), ground=False):
    global PARTS
    bpy.ops.object.select_all(action='DESELECT')
    for p in PARTS: p.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    if len(PARTS) > 1: bpy.ops.object.join()
    o = bpy.context.active_object; o.name = o.data.name = name
    # move origin to pivot without moving geometry
    o.data.transform(__import__('mathutils').Matrix.Translation(-Vector(pivot))); o.location = pivot
    if ground: o.data.transform(__import__('mathutils').Matrix.Translation((0, 0, -min(v.co.z for v in o.data.vertices))))
    o.data.materials.clear(); o.data.materials.append(MAT)
    PARTS = []
    return o

# ---------------------------------------------------------------- models
def echo_shell():
    finish(add('ico_sphere', (0, 0, .07), (.21, .18, .085), subdivisions=2), STONE, jitter=.012)
    finish(add('uv_sphere', (.01, 0, .115), (.17, .14, .045), segments=12, ring_count=6), MOSS, jitter=.006)
    # scallop: fan with 9 radial ribs and a wavy rim, tilted back on the moss stone
    R, A, NR, NA = .27, math.radians(62), 6, 27
    tilt = Matrix.Rotation(math.radians(-22), 4, 'X'); base = Vector((0, .02, .15))
    def fan(r, th, lift=0):
        k = .5 + .5 * math.cos(9 * th * math.pi / A)  # 1 on a rib, 0 between
        rim = R * (1 + .035 * k) * r
        y = -.05 * (1 - (r * r)) * math.cos(th) - .014 * k * r - lift
        return tilt @ Vector((rim * math.sin(th), y, rim * math.cos(th) - .02)) + base
    bm = bmesh.new()
    g = [[bm.verts.new(fan(.08 + .92 * i / NR, -A + 2 * A * j / NA)) for j in range(NA + 1)] for i in range(NR + 1)]
    for i in range(NR):
        for j in range(NA): bm.faces.new((g[i][j], g[i][j + 1], g[i + 1][j + 1], g[i + 1][j]))
    me = bpy.data.meshes.new('fan'); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('fan', me); bpy.context.collection.objects.link(o)
    sm = o.modifiers.new('thick', 'SOLIDIFY'); sm.thickness = .022; sm.offset = 0
    finish(o, GOLD)
    for k in range(9):  # cream highlight ridges along each rib
        th = (k - 4) * A / 4.5
        tube([fan(t, th, .006) for t in (.12, .4, .7, .97)], [.006, .01, .012, .01], CREAM, n=5)
    for side in (-1, 1):  # two hinge ears
        bm = bmesh.new()
        vs = [bm.verts.new(tilt @ Vector(v) + base) for v in [(0, -.01, -.02), (side * .08, -.01, -.01), (side * .07, -.01, .04), (side * .02, -.01, .03)]]
        bm.faces.new(vs if side > 0 else vs[::-1])
        me = bpy.data.meshes.new('ear'); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new('ear', me); bpy.context.collection.objects.link(o)
        sm = o.modifiers.new('thick', 'SOLIDIFY'); sm.thickness = .016
        finish(o, BRASS, bevel=.004, segs=1)
    return join('EchoShell', ground=True)

def windbreak_stone():
    finish(add('cube', (0, 0, .65), (.45, .25, .65)), STONE, bevel=.2, segs=2, subsurf=1, jitter=.008, taper=.3)
    # wind spirals inlaid in gold on front (-Y) and back (+Y)
    for side in (-1, 1):
        pts = []
        for j in range(30):
            t = j / 29; a = t * math.tau * 1.8; r = .05 + .26 * t
            pts.append((r * math.cos(a) * side, side * .245 - side * .012 * (1 - t), .72 + r * math.sin(a)))
        tube(pts, [.022 * (1 - .4 * j / 29) for j in range(30)], GOLD, n=6)
    finish(add('uv_sphere', (-.12, 0, 1.24), (.26, .19, .07), segments=14, ring_count=7), MOSS, jitter=.01)
    # brass bracket + chime
    tube([(.12, 0, 1.18), (.15, 0, 1.3), (.25, 0, 1.36), (.34, 0, 1.34)], .016, BRASS, n=6)
    lathe([(0, 0), (.0, .02), (-.01, .045), (-.07, .055), (-.11, .07), (-.12, .06)], BRASS, n=14, loc=(.34, 0, 1.33))
    finish(add('uv_sphere', (.34, 0, 1.19), (.022, .022, .022), segments=8, ring_count=5), GOLD)
    return join('WindbreakStone', ground=True)

def gust_banner():
    lathe([(0, .09), (.06, .1), (.1, .06), (2.18, .035), (2.18, 0)], WOOD, n=10)
    lathe([(2.12, .055), (2.18, .055), (2.2, .04)], BRASS, n=12)  # collar
    lathe([(2.2, .03), (2.26, .06), (2.32, .05), (2.36, .02), (2.44, 0)], BRASS, n=12)  # finial
    lathe([(1.97, .05), (2.03, .05)], BRASS, n=12)  # flag ring
    pole = join('GustBanner')
    # Cloth: triangular pennant, pivot at flag root on pole
    root = Vector((0, 0, 2.0)); L, H = .95, .5
    bm = bmesh.new(); cols, rows = 8, 4; grid = []
    for i in range(cols + 1):
        u = i / cols; half = H * .5 * (1 - u * .96)
        grid.append([bm.verts.new((.035 + u * L, .045 * math.sin(u * 5.5) * u, -H * .5 + (j / rows - .5) * 2 * half)) for j in range(rows + 1)])
    for i in range(cols):
        for j in range(rows): bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    me = bpy.data.meshes.new('Cloth'); bm.to_mesh(me); bm.free()
    cloth = bpy.data.objects.new('Cloth', me); bpy.context.collection.objects.link(cloth)
    cloth.location = root
    s = cloth.modifiers.new('thick', 'SOLIDIFY'); s.thickness = .018; s.offset = 0
    finish(cloth, CLOTH); PARTS.clear()
    cloth.data.materials.append(MAT); cloth.parent = pole
    return pole, [cloth]

def tide_gauge():
    lathe([(0, .3), (.04, .32), (.18, .3), (.24, .24), (.26, .12)], STONE, n=14)
    finish(add('cube', (0, 0, 1.3), (.06, .035, 1.06)), BRASS, bevel=.025, segs=2, smooth=True)
    for k in range(21):  # ticks every 0.1 m from 0.2 m to 2.2 m, long every 0.5
        z = .25 + k * .1; w = .11 if k % 5 == 0 else .065
        finish(add('cube', (-.06 - w / 2 + .02, -.04, z), (w / 2, .012, .009)), GOLD if k % 5 == 0 else DEEP, smooth=False)
    lathe([(2.34, .07), (2.38, .08), (2.44, .06), (2.5, 0)], GOLD, n=12)
    gauge = join('TideGauge')
    # Float: ring buoy around post + crown bulb; origin at its centre, rests at y=0.25
    finish(add('torus', (0, 0, 0), major_radius=.15, minor_radius=.06, major_segments=20, minor_segments=8), CREAM)
    finish(add('torus', (0, 0, 0), (1.02, 1.02, .6), major_radius=.15, minor_radius=.062, major_segments=20, minor_segments=8), DEEP)
    finish(add('uv_sphere', (.2, 0, .03), (.04, .04, .04), segments=10, ring_count=6), GOLD)
    fl = join('Float'); fl.location = (0, 0, .25); fl.parent = gauge
    return gauge, [fl]

def grab_crank():
    # wheel in Blender XZ plane, axle along -Y (= Unity +Z forward); origin at hub (rotation pivot)
    R = math.radians
    finish(add('torus', (0, 0, 0), rot=(R(90), 0, 0), major_radius=.19, minor_radius=.022, major_segments=28, minor_segments=8), BRASS)
    finish(add('cylinder', (0, 0, 0), rot=(R(90), 0, 0), radius=.055, depth=.06, vertices=16), GOLD, bevel=.012, segs=2)
    for k in range(4):
        a = k * math.tau / 4 + R(45)
        tube([(.05 * math.cos(a), 0, .05 * math.sin(a)), (.18 * math.cos(a), 0, .18 * math.sin(a))], [.014, .01], BRASS, n=6)
    tube([(0, -.03, 0), (.12, -.04, .02), (.19, -.05, 0)], .02, BRASS, n=8)  # crank arm
    crank = join('GrabCrank')
    lathe([(0, .022), (.02, .03), (.07, .034), (.11, .03), (.13, .02), (.135, 0)], WOOD, n=12)
    h = join('Handle'); h.rotation_euler = (R(90), 0, 0)  # lathe +Z -> -Y
    bpy.context.view_layer.objects.active = h; h.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    h.location = (.19, -.05, 0); h.parent = crank
    return crank, [h]

BUILDERS = {'EchoShell': (echo_shell, 1500), 'WindbreakStone': (windbreak_stone, 3000),
            'GustBanner': (gust_banner, 1500), 'TideGauge': (tide_gauge, 3000), 'GrabCrank': (grab_crank, 1500)}

# ---------------------------------------------------------------- export / unity sidecars
def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)

def world_bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = [min(p[a] for p in pts) for a in range(3)]; hi = [max(p[a] for p in pts) for a in range(3)]
    # report as Unity (x, y, z) = Blender (x, z, y)
    size = [hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1]]
    return [round(s, 3) for s in size], round(lo[2], 4)

def export(name, root, kids):
    bpy.ops.object.select_all(action='DESELECT')
    for o in [root] + kids: o.select_set(True)
    path = OUT / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path.resolve()), use_selection=True, object_types={'MESH', 'EMPTY'}, bake_anim=False,
                             axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, path_mode='STRIP')
    t = sum(tris(o) for o in [root] + kids); size, minz = world_bounds([root] + kids)
    for o in [root] + kids: assert len(o.data.materials) == 1 and o.data.materials[0].name == MAT_NAME, o.name
    return {'name': name, 'triangles': t, 'sizeUnityXYZ': size, 'minY': minz,
            'children': {k.name: {'triangles': tris(k), 'localPosUnity': [round(k.location.x, 3), round(k.location.z, 3), round(-k.location.y, 3)]} for k in kids}}

def write_sidecars():
    png_meta = OUT / 'RealmStorybook.png.meta'
    guid = None
    if png_meta.exists():  # keep guid stable across reruns
        guid = next(l.split()[1] for l in png_meta.read_text().splitlines() if l.startswith('guid:'))
    guid = guid or uuid.uuid4().hex
    tmpl = (ROOT / 'Assets/Resources/FishingV035/CoastalAlbedo.png.meta').read_text()
    lines = tmpl.replace('3b381fc23cadb0942b5451b74cd7cb73', guid).replace('maxTextureSize: 2048', 'maxTextureSize: 1024').split('\n')
    i = next(k for k, l in enumerate(lines) if 'buildTarget: Android' in l)
    for k in range(i, i + 12):
        lines[k] = lines[k].replace('textureFormat: -1', 'textureFormat: 49').replace('overridden: 0', 'overridden: 1')
    png_meta.write_text('\n'.join(lines))
    mat_path = OUT / 'RealmStorybook.mat'
    mat = (ROOT / 'Assets/Materials/Blocker.mat').read_text()
    assert 'guid: 8d2bb70cbf9db8d4da26e15b26e74248' in mat  # URP Simple Lit
    mat = mat.replace('m_Name: Blocker', 'm_Name: RealmStorybook')
    for prop in ('_BaseMap', '_MainTex'):
        mat = mat.replace(f'    - {prop}:\n        m_Texture: {{fileID: 0}}', f'    - {prop}:\n        m_Texture: {{fileID: 2800000, guid: {guid}, type: 3}}')
    for c in ('_BaseColor', '_Color'):
        mat = mat.replace(f'{c}: {{r: 0, g: 0, b: 0, a: 0.5}}', f'{c}: {{r: 1, g: 1, b: 1, a: 1}}')
    mat = mat.replace('_SpecColor: {r: 0.1999999, g: 0.1999999, b: 0.1999999, a: 1}', '_SpecColor: {r: 0.25, g: 0.22, b: 0.18, a: 1}')
    mat = mat.replace('  - _SPECULAR_COLOR\n', '')
    mat = mat.replace('    - _Smoothness: 0.5', '    - _Smoothness: 0.3')
    mat_path.write_text(mat)
    if not (OUT.parent / 'FishingV044.meta').exists():
        (OUT.parent / 'FishingV044.meta').write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

# ---------------------------------------------------------------- preview
def preview(name, objs):
    scene = bpy.context.scene
    for o in scene.objects:
        if o.type == 'MESH' and o.name != 'ground': o.hide_render = o not in objs
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    hi = max(p.z for p in pts); span = max(hi, max(abs(p.x) for p in pts) * 2)
    tgt = Vector((sum(p.x for p in pts) / len(pts), 0, hi * .48))
    lift = -min(min(p.z for p in pts), 0); objs[0].location.z += lift; bpy.context.view_layer.update()
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]; tgt.z += lift
    cam = scene.camera
    cam.location = tgt + Vector((span * 1.1, -span * 1.9, span * .55)); cam.data.lens = 50
    cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str((PREVIEW / f'art_{name}.png').resolve())
    bpy.ops.render.render(write_still=True)
    objs[0].location.z -= lift

def setup_scene():
    scene = bpy.context.scene
    for o in list(scene.objects): bpy.data.objects.remove(o, do_unlink=True)
    scene.unit_settings.system = 'METRIC'
    world = bpy.data.worlds.new('sky'); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (.55, .62, .78, 1); bg.inputs['Strength'].default_value = .7
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 32; scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.view_settings.view_transform = 'AgX'
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
    for nm, kind, loc, e, colr in [('key', 'SUN', (3, -4, 6), 3.5, (1, .9, .75)), ('fill', 'AREA', (-4, -3, 3), 300, (.7, .8, 1)), ('rim', 'AREA', (2, 4, 4), 500, (1, .85, .6))]:
        d = bpy.data.lights.new(nm, kind); d.energy = e; d.color = colr
        if kind == 'AREA': d.size = 4
        l = bpy.data.objects.new(nm, d); scene.collection.objects.link(l); l.location = loc
        l.rotation_euler = (Vector((0, 0, .8)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    # ground disc for contact shadow (not exported)
    bpy.ops.mesh.primitive_circle_add(radius=6, fill_type='NGON'); g = bpy.context.active_object; g.name = 'ground'
    gm = bpy.data.materials.new('ground'); gm.use_nodes = True
    gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.78, .74, .64, 1); g.data.materials.append(gm)

def main():
    global MAT
    setup_scene()
    atlas = make_atlas()
    MAT = bpy.data.materials.new(MAT_NAME); MAT.use_nodes = True
    nt = MAT.node_tree; bsdf = nt.nodes['Principled BSDF']; tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = atlas
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value = .65
    report = []
    for name, (fn, budget) in BUILDERS.items():
        r = fn(); root, kids = (r, []) if not isinstance(r, tuple) else r
        rep = export(name, root, kids); rep['budget'] = budget
        assert rep['triangles'] <= budget, rep
        report.append(rep); print(json.dumps(rep))
        preview(name, [root] + kids)
        for o in [root] + kids: o.hide_render = True; o.location.x += 50  # park out of later shots
    write_sidecars()
    (SOURCE / 'realm-export-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')

main()
