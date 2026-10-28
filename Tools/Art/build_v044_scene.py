"""V044 scene environment: replace island scatter models with cleaned AI-generated (Hunyuan/Tripo) meshes.
Keeps each Resources FBX path, object/mesh name (so scene fileIDs survive), metre scale, ground pivot and footprint;
decimates to the PICO budget, bakes the AI albedo down to a small texture and renders a before/after check.
Usage: blender --background --factory-startup --python-exit-code 1 --python build_v044_scene.py -- <Asset> [<Asset>...]
"""
from pathlib import Path
import bpy, math, shutil, sys, json
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
V035 = ROOT / 'Assets/Resources/FishingV035'
OUT_TEX = ROOT / 'Assets/Resources/FishingV044/Scene'
AI = ROOT / 'ArtSource/V044/AI'
BACKUP = ROOT / 'ArtSource/V044/Backup/Scene'
SHOTS = ROOT / 'Logs/V044/scene/models'
for d in (OUT_TEX, BACKUP, SHOTS): d.mkdir(parents=True, exist_ok=True)

# name: source GLB, width along X or height in metres, depth sunk below the pivot, triangle budget, texture size,
# sway = height fraction where FishingPalette wind (vertex alpha) starts; None keeps a rigid URP Lit material.
HY = AI / 'hunyuan/2026-10-28'
ASSETS = {
    'ShoreRock': dict(src=HY / 'ShoreRock.glb', width=2.2, sink=.18, tris=2900, tex=1024, sway=None),
    'CoastalPine': dict(src=HY / 'pine.glb', height=4.7, sink=.05, tris=4600, tex=1024, sway=.45),
    'DunePalm': dict(src=HY / 'palm.glb', height=4.7, sink=.05, tris=4600, tex=1024, sway=.6),
    'MangroveTree': dict(src=HY / 'mangrove.glb', height=5.6, sink=.15, tris=6000, tex=1024, sway=.5),
    'MeadowPatch': dict(src=HY / 'grass.glb', width=1.4, sink=.02, tris=2600, tex=512, sway=0, tint=(.6, .68, .46)),
    'FlowerPatch': dict(src=HY / 'flowerpatch.glb', width=1.8, sink=.02, tris=2400, tex=512, sway=0, tint=(.85, .88, .8)),
    'FernClump': dict(src=HY / 'fern.glb', height=.85, sink=.15, tris=2000, tex=512, sway=.1, tint=(.85, .9, .8)),
    # lighthouse-tide: half-buried skiff, 4.5 m long, origin at hull bottom centre (0.4 m sits under the sand), bow +Z
    'TideWreck': dict(src=HY / 'tidewreck.glb', out=ROOT / 'Assets/Resources/FishingV044/TideWreck.fbx', length=4.5, sink=0, tris=5800, tex=1024, sway=None),
    # sea-events: pivot at waterline centre, bow +Z, ~17 x 6 m, anchor empties added by market_anchors()
    'MarketShip': dict(src=HY / 'marketship.glb', out=ROOT / 'Assets/Resources/FishingV044/Sea/MarketShip.fbx', length=17, sink=1.3, tris=9500, tex=1024, sway=None, anchors=True, weld=True, clean=.002, carve=[((-3.4, -4.5, 3.2), (-1.4, 4.5, 4.4)), ((1.4, -4.5, 3.2), (3.4, 4.5, 4.4))]),
    # V020 landing jetty: visual only (walking uses the hidden 'Landing jetty' box), so fit the exact old bounds
    'LandingJetty': dict(src=HY / 'jetty.glb', dir=ROOT / 'Assets/Resources/FishingV020', box=((-1.24, -7.5, -2.8), (1.24, 7.5, 1.05)), deck=.08, legacy_axes=True, ropes=((-1.13, 1.13), .82, -7.3, 7.3, 8), sink=0, tris=4600, tex=1024, sway=None),
    # realm-redesign: name unchanged, trunk base radius <= 2.2 m, keep V35_Portal (front of trunk)
    'AncientTree': dict(src=HY / 'ancienttree.glb', height=8.5, sink=.15, tris=11500, tex=1024, sway=.55, weld=True, squeeze=1.75, trunk=2.2),
    'Windmill': dict(src=HY / 'windmill.glb', height=9, sink=.15, tris=9500, tex=1024, sway=None, weld=True),
    'Galleon': dict(src=HY / 'galleon.glb', box=((-6.46,-15.99,-2.94),(10.01,19.02,10.01)), deck=1.8, sink=0, tris=14000, tex=1024, sway=None, weld=True),
    'Lighthouse': dict(src=HY / 'lighthouse.glb', out=V035 / 'Lighthouse.fbx', height=11.9, sink=0, tris=6500, tex=1024, sway=None, weld=True),
    'DuneGrass': dict(src=HY / 'grass.glb', width=1.4, sink=.02, tris=1100, tex=512, sway=0, tint=(.6, .68, .46)),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def bounds(objs):
    # vertices, not bound_box: bound_box is stale right after transform_apply in background mode
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def render(path, size=512):
    scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 24; scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = size; scene.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('studio'); world.use_nodes = True; scene.world = world
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.80, .86, .90, 1)
    meshes = [o for o in scene.objects if o.type == 'MESH']; lo, hi = bounds(meshes); centre = (lo + hi) / 2; span = max(hi - lo)
    for name, pos, power in [('key', (3, -4, 6), 900), ('fill', (-4, -2, 2), 300)]:
        d = bpy.data.lights.new(name, 'AREA'); d.energy = power; d.size = 5; L = bpy.data.objects.new(name, d)
        scene.collection.objects.link(L); L.location = centre + Vector(pos); L.rotation_euler = (centre - L.location).to_track_quat('-Z', 'Y').to_euler()
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; c = bpy.data.objects.new('cam', cam); scene.collection.objects.link(c); scene.camera = c
    c.location = centre + Vector((1.3, -1.9, 1.1)).normalized() * span * 4; c.rotation_euler = (centre - c.location).to_track_quat('-Z', 'Y').to_euler()
    cam.ortho_scale = span * 1.25; cam.clip_end = span * 20
    scene.render.filepath = str(path); bpy.ops.render.render(write_still=True)


def clean_mesh(obj, name, cfg):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=.01)
    seen, islands = set(), []
    for f in bm.faces:
        if f in seen: continue
        stack, part = [f], []; seen.add(f)
        while stack:
            g = stack.pop(); part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen: seen.add(h); stack.append(h)
        islands.append(part)
    big = max(sum(f.calc_area() for f in p) for p in islands)
    doomed = [f for p in islands if sum(f.calc_area() for f in p) < big * cfg['clean'] for f in p]
    for box in cfg.get('carve', []):  # clear sight-lines: delete faces whose centre falls in an axis box
        a, b = Vector(box[0]), Vector(box[1])
        doomed += [f for f in bm.faces if all(a[i] <= f.calc_center_median()[i] <= b[i] for i in range(3))]
    bmesh.ops.delete(bm, geom=list(set(doomed)), context='FACES')
    print('V044-SCENE-CLEAN', name, 'islands', len(islands), 'removed faces', len(set(doomed)))
    bm.to_mesh(obj.data); bm.free(); obj.data.update()


def build(name, cfg):
    target = cfg.get('out', cfg.get('dir', V035) / (name + '.fbx')); backup = BACKUP / (name + '.fbx')
    markers = []
    if 'out' in cfg:  # brand-new asset: nothing to preserve
        obj_name = mesh_name = 'GEO-' + name
    else:
        if not backup.exists(): shutil.copy2(target, backup)
        # Original names and footprint come from the untouched backup, never from a previous run's output.
        reset(); bpy.ops.import_scene.fbx(filepath=str(backup))
        old = [o for o in bpy.context.scene.objects if o.type == 'MESH']; assert len(old) == 1, name
        obj_name, mesh_name = old[0].name, old[0].data.name
        # V35_* marker empties (portal, approach, rotor...) are looked up by name at runtime; carry them over unchanged
        markers = [(e.name, e.matrix_world.translation.copy()) for e in bpy.context.scene.objects if e.type == 'EMPTY' and e.parent == old[0]]
        render(SHOTS / (name + '-before.png'))

    reset(); bpy.ops.import_scene.gltf(filepath=str(cfg['src']))
    parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1: bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM'); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in [o for o in bpy.context.scene.objects if o != obj]: bpy.data.objects.remove(o)
    lo, hi = bounds([obj])
    if ('length' in cfg or 'box' in cfg) and hi.x - lo.x > hi.y - lo.y:  # hull length along +Y (Unity +Z); bow_flip turns it end for end
        for v in obj.data.vertices: v.co = Vector((-v.co.y, v.co.x, v.co.z))
        lo, hi = bounds([obj])
    if cfg.get('bow_flip'):
        for v in obj.data.vertices: v.co = Vector((-v.co.x, -v.co.y, v.co.z))
        lo, hi = bounds([obj])
    if 'length' in cfg:
        s = cfg['length'] / (hi.y - lo.y)
    elif 'width' in cfg and hi.y - lo.y > hi.x - lo.x:  # long side along X like the original footprint
        for v in obj.data.vertices: v.co = Vector((-v.co.y, v.co.x, v.co.z))
        obj.data.update(); lo, hi = bounds([obj])
    if 'length' not in cfg and 'box' not in cfg: s = cfg['width'] / (hi.x - lo.x) if 'width' in cfg else cfg['height'] / (hi.z - lo.z)
    if 'box' in cfg:  # non-uniform fit onto an exact footprint
        a, b = Vector(cfg['box'][0]), Vector(cfg['box'][1])
        for v in obj.data.vertices: v.co = Vector([a[i] + (v.co[i] - lo[i]) / (hi[i] - lo[i]) * (b[i] - a[i]) for i in range(3)])
        if 'deck' in cfg:  # piecewise height so the walk surface lands exactly on the old deck (the hidden collider)
            from collections import Counter
            area = Counter()
            for p in obj.data.polygons:
                if p.normal.z > .9: area[round(p.center.z, 2)] += p.area
            d0 = area.most_common(1)[0][0]; d1 = cfg['deck']
            for v in obj.data.vertices:
                z = v.co.z; v.co.z = d1 + (z - d0) * ((b.z - d1) / (b.z - d0) if z > d0 else (d1 - a.z) / (d0 - a.z))
    else:
        centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
        for v in obj.data.vertices: v.co = (v.co - centre) * s - Vector((0, 0, cfg['sink']))

    if cfg.get('weld'):  # AI meshes are split at every UV seam; weld first so collapse keeps thin parts connected
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=.002); bpy.ops.object.mode_set(mode='OBJECT')
    for _ in range(4):  # welded meshes resist one-shot collapse; repeat until inside the budget
        if len(obj.data.polygons) <= cfg['tris']: break
        mod = obj.modifiers.new('decimate', 'DECIMATE'); mod.ratio = cfg['tris'] / len(obj.data.polygons); mod.use_collapse_triangulate = True; mod.delimit = set()
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if cfg.get('clean'): clean_mesh(obj, name, cfg)  # decimation is what tears slivers off thin rails and lanterns
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons); assert 0 < tris <= cfg['tris'] * 1.05, (name, tris)
    bpy.ops.object.shade_smooth()
    if cfg['sway'] is not None:
        for old_attr in list(obj.data.color_attributes): obj.data.color_attributes.remove(old_attr)
        attr = obj.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER'); top = max(v.co.z for v in obj.data.vertices)
        for loop in obj.data.loops:
            t = (obj.data.vertices[loop.vertex_index].co.z / top - cfg['sway']) / (1 - cfg['sway'])
            attr.data[loop.index].color_srgb = (*cfg.get('tint', (1, 1, 1)), max(0., min(1., t)))  # rgb tints toward the LP meadow greens

    # Albedo only: the storybook look is in the painted colour; resized for mobile, Unity compresses to ASTC.
    mat = obj.data.materials[0]; tex = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and 'normal' not in n.image.name and 'metallic' not in n.image.name)
    tex.scale(cfg['tex'], cfg['tex']); png = OUT_TEX / (name + 'Albedo.png'); tex.filepath_raw = str(png); tex.file_format = 'PNG'; tex.save()
    for n in [n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image != tex]: mat.node_tree.nodes.remove(n)
    if cfg.get('ropes'): add_ropes(obj, tex, cfg['ropes'])
    obj.name = obj_name; obj.data.name = mesh_name; mat.name = 'MAT-V044 ' + name
    if name == 'Windmill':
        split_windmill_rotor(obj)
        markers=[(n,Vector((0,3,5.35)) if n.startswith('V35_Rotor') else at) for n,at in markers]
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    if cfg.get('anchors'): market_anchors(obj)
    for mname, at in markers:
        e = bpy.data.objects.new(mname, None); bpy.context.scene.collection.objects.link(e); e.location = at; e.parent = obj; e.select_set(True)
    if cfg.get('squeeze'):  # pull spreading roots in radially below 2.5 m so the base fits the walk/portal clearance
        limit = cfg['squeeze']; low = [v for v in obj.data.vertices if v.co.z < 2.5]
        r0 = max(Vector((v.co.x, v.co.y)).length for v in low if v.co.z < .6)
        for v in low:
            k = (limit / r0) + (1 - limit / r0) * max(0, v.co.z) / 2.5  # full squeeze at ground, none at 2.5 m
            v.co.x *= k; v.co.y *= k
        obj.data.update()
    if cfg.get('trunk'):  # report the ground-level footprint so constraints like 'trunk radius <= 2.2 m' are checked
        low = [v.co for v in obj.data.vertices if v.co.z < .6]; r = max((Vector((v.x, v.y)).length for v in low), default=0)
        print('V044-SCENE-TRUNK', name, 'ground radius', round(r, 2)); assert r <= cfg['trunk'], (name, r)
    render(SHOTS / (name + '-after.png'))
    if name in {'AncientTree','Windmill','Galleon','Lighthouse'}:
        source_dir=ROOT / 'ArtSource/V044/Scene';source_dir.mkdir(parents=True,exist_ok=True)
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=str(source_dir / (name+'.blend')))
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                             # V020 exports were unbaked; placed copies store that -90° root rotation in the scene, so match it
                             bake_space_transform=not cfg.get('legacy_axes'), add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    lo, hi = bounds([obj])
    return dict(name=name, object=obj_name, triangles=tris, min=[round(x, 3) for x in lo], max=[round(x, 3) for x in hi], texture=str(png.relative_to(ROOT)))


def split_windmill_rotor(obj):
    """The AI sails share one mesh with the tower; cut their front plane so the existing rotor animation can keep working."""
    import bmesh
    rotor=obj.copy();rotor.data=obj.data.copy();bpy.context.scene.collection.objects.link(rotor)
    pivot=Vector((0,3,5.35))
    for part,rotating in [(obj,False),(rotor,True)]:
        bm=bmesh.new();bm.from_mesh(part.data)
        remove=[f for f in bm.faces if (f.calc_center_median().y < -2.65) != rotating]
        bmesh.ops.delete(bm,geom=remove,context='FACES')
        for v in bm.verts:v.co.y=-v.co.y
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(part.data);bm.free()
    assert len(rotor.data.polygons)>100 and len(obj.data.polygons)>1000
    rotor.name=rotor.data.name='GEO-WindmillRotor'
    for v in rotor.data.vertices:v.co-=pivot
    bpy.ops.object.select_all(action='DESELECT');rotor.select_set(True)
    target=V035/'WindmillRotor.fbx';backup=BACKUP/'WindmillRotor.fbx'
    if not backup.exists():shutil.copy2(target,backup)
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
    rotor.location=pivot;rotor.select_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj


def remove_old_lighthouse():
    """Remove only the old tower from the combined garden mesh, keeping its palette, fileID and lens marker."""
    import bmesh
    target=V035/'HomeLandscaping.fbx';backup=BACKUP/'HomeLandscaping.fbx'
    if not backup.exists():shutil.copy2(target,backup)
    reset();bpy.ops.import_scene.fbx(filepath=str(backup));obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bm=bmesh.new();bm.from_mesh(obj.data)
    cut=[f for f in bm.faces if (Vector((obj.matrix_world@f.calc_center_median()).xy)-Vector((19,-12))).length<3.1]
    assert len(cut)>100
    bmesh.ops.delete(bm,geom=cut,context='FACES');bm.to_mesh(obj.data);bm.free()
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH','EMPTY'},apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
    print('V044-LIGHTHOUSE replaced',len(cut),'old tower faces; garden and lens marker retained')



def add_ropes(obj, tex, spec):
    """Thin sagging rope rails (decimation erases the AI ones). spec = (x offsets, height, y from, y to, posts).
    UVs all point at the most rope-coloured texel of the baked albedo so the rope reuses the same material."""
    import bmesh
    w, h = tex.size; px = list(tex.pixels); best, bi = 9, 0
    for i in range(0, w * h, 97):
        r, g, b = px[i * 4:i * 4 + 3]; d = (r - .78) ** 2 + (g - .62) ** 2 + (b - .38) ** 2
        if d < best: best, bi = d, i
    uv = ((bi % w + .5) / w, (bi // w + .5) / h)
    xs, z, y0, y1, posts = spec; bm = bmesh.new(); seg = 6
    for x in xs:
        for k in range(posts - 1):
            a, b = y0 + (y1 - y0) * k / (posts - 1), y0 + (y1 - y0) * (k + 1) / (posts - 1)
            ring_prev = None
            for t in range(9):
                f = t / 8; c = Vector((x, a + (b - a) * f, z - .12 * 4 * f * (1 - f)))  # parabolic sag
                ring = [bm.verts.new(c + Vector((0, 0, 0)) + .022 * Vector((math.cos(q), 0, math.sin(q))) * 1) for q in [j * 2 * math.pi / seg for j in range(seg)]]
                if ring_prev:
                    for j in range(seg): bm.faces.new((ring_prev[j], ring_prev[(j + 1) % seg], ring[(j + 1) % seg], ring[j]))
                ring_prev = ring
    rope = bpy.data.meshes.new('rope'); bm.to_mesh(rope); bm.free()
    layer = rope.uv_layers.new(); [setattr(l, 'uv', uv) for l in layer.data]
    ro = bpy.data.objects.new('rope', rope); bpy.context.scene.collection.objects.link(ro); ro.data.materials.append(obj.data.materials[0])
    bpy.ops.object.select_all(action='DESELECT'); ro.select_set(True); obj.select_set(True); bpy.context.view_layer.objects.active = obj; bpy.ops.object.join()


def market_anchors(obj):
    """Named empties sea-events hangs stalls, letters, lanterns and the owner on (Blender +Y = Unity +Z bow, -X = port)."""
    vs = [v.co for v in obj.data.vertices]; top = max(v.z for v in vs)
    # deck: the most common height among upward-facing faces in the hull's middle third
    ups = sorted(round(p.center.z, 1) for p in obj.data.polygons if p.normal.z > .9 and abs(p.center.y) < 3 and 1 < p.center.z < 4)
    deck = max(set(ups), key=ups.count) if ups else 2.0
    fore = max((v for v in vs if v.y > 1), key=lambda v: v.z); aft = max((v for v in vs if v.y < -1), key=lambda v: v.z)  # mast tips
    points = {**{'Stall%d' % i: (-1.6, -3.5 + i * 1.8, deck + .9) for i in range(5)},
              **{'Letter%d' % i: (-.6 + (i % 2) * 1.2, -6.2 - (i // 2) * .6, deck + .8) for i in range(4)},
              'LanternA': tuple(fore), 'LanternB': tuple(aft),
              'Owner': (-1.0, .1, deck)}  # behind the carved port-side window, facing the player's boat
    for name, at in points.items():
        e = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(e); e.location = at; e.parent = obj; e.select_set(True)
    print('V044-SCENE-ANCHORS deck=%.2f top=%.2f' % (deck, top), {k: tuple(round(x, 2) for x in v) for k, v in points.items()})


# LOD1 meshes for the PICO budget (perf: vegetation and scatter dominate visible triangles).
# Written beside the override materials as <Name>_LOD1.fbx; FishingV035Materials.AddLod wires them into a LODGroup.
LOD = {'ShoreRock': .25, 'CoastalPine': .2, 'DunePalm': .2, 'MangroveTree': .2, 'MeadowPatch': .2, 'DuneGrass': .25,
       'FlowerPatch': .2, 'FernClump': .2, 'AncientTree': .12, 'OrchardTree': .15, 'Windmill': .25, 'Galleon': .25, 'Lighthouse': .25}


def build_lod(name, ratio):
    reset(); bpy.ops.import_scene.fbx(filepath=str(V035 / (name + '.fbx')))
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']; assert len(objs) == 1, name
    obj = objs[0]; before = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new('lod', 'DECIMATE'); mod.ratio = ratio; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    after = sum(len(p.vertices) - 2 for p in obj.data.polygons); assert 0 < after <= before * ratio * 1.1, (name, before, after)
    obj.name = obj.data.name = 'GEO-' + name + '_LOD1'
    bpy.ops.export_scene.fbx(filepath=str(OUT_TEX / (name + '_LOD1.fbx')), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    return dict(name=name, lod0=before, lod1=after)


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if args[:1] == ['--lod']:
        report = [build_lod(n, LOD[n]) for n in (args[1:] or list(LOD))]
        (SHOTS / 'lod.json').write_text(json.dumps(report, indent=1), encoding='utf-8'); print('V044-SCENE-LOD', json.dumps(report))
    else:
        report = [build(n, ASSETS[n]) for n in (args or list(ASSETS))]
        if 'Lighthouse' in (args or ASSETS):remove_old_lighthouse()
        (SHOTS / 'export.json').write_text(json.dumps(report, indent=1), encoding='utf-8'); print('V044-SCENE-EXPORT', json.dumps(report))
