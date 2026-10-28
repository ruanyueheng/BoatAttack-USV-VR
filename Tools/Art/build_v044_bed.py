"""V044 storybook bed: cedar frame matching V042 furniture, draped quilt with folds, plump pillows.
Rebuilds only FishingV035/HomeBed with the V035 coastal atlas and exporter (same as build_v042_furniture.py).
Atlas tiles: 3 cedar, 4 ochre knit, 6 teal linen, 10 cream linen.
"""
from pathlib import Path
import json, math, runpy, sys
import bpy

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/V044/Bed"
ART.mkdir(parents=True, exist_ok=True)
kit = runpy.run_path(str(ROOT / "Tools/Art/build_v035_assets.py"))
api = kit["build"].__globals__
api["SOURCE"] = ART
box, profile, tube, rock, flow, mesh = (api[k] for k in ("box", "profile", "tube", "rock", "flow", "mesh"))


def drape(x0, x1, z0, z1, top, drop, tile, nx=18, nz=16, folds=.022, thick=.03):
    """Cloth sheet over the mattress: flat top with soft folds, edges falling over the sides."""
    edge = .93
    vs = []
    for i in range(nz + 1):
        z = z0 + (z1 - z0) * i / nz
        for j in range(nx + 1):
            x = x0 + (x1 - x0) * j / nx
            over = max(0, abs(x) - edge) / (abs(x1) - edge)  # 0 on top, 1 at hem
            foot = max(0, z - (z1 - .08)) / .08
            y = top - drop * over ** 1.6 - drop * foot ** 1.6 * .9
            y += folds * math.sin(x * 6.5 + z * 2.2) * (1 - over * .5) + folds * .6 * math.sin(z * 9 + x * 1.3) * over
            xo = x + .02 * math.sin(z * 12) * over  # hem ripples outward a little
            vs.append((xo, y, z))
    n = (nx + 1)
    fs = [(i * n + j, i * n + j + 1, (i + 1) * n + j + 1, (i + 1) * n + j) for i in range(nz) for j in range(nx)]
    count = len(vs)
    vs += [(x, y - thick, z) for x, y, z in vs]
    fs += [tuple(k + count for k in reversed(f)) for f in fs]
    ring = [j for j in range(n)] + [i * n + nx for i in range(1, nz + 1)] + [nz * n + j for j in reversed(range(nx))] + [i * n for i in reversed(range(1, nz))]
    for k, a in enumerate(ring):
        b = ring[(k + 1) % len(ring)]; fs.append((a, b, b + count, a + count))
    mesh(vs, fs, tile, True)


def home_bed():
    # Cedar frame: turned posts, panelled head and foot boards, side rails.
    for x in (-.96, .96):
        profile((x, 0, -1.14), [(0, .075), (.08, .085), (.12, .065), (1.02, .06), (1.08, .08), (1.14, .085), (1.2, .05)], 3, 14)
        profile((x, 0, 1.14), [(0, .075), (.08, .085), (.12, .065), (.66, .06), (.72, .08), (.78, .05)], 3, 14)
        box((x, .34, 0), (.07, .2, 2.2), 3, .025)
    box((0, .72, -1.15), (1.86, .62, .07), 3, .03)
    flow([(-.96, 1.02, -1.15), (-.5, 1.13, -1.15), (0, 1.17, -1.15), (.5, 1.13, -1.15), (.96, 1.02, -1.15)], .045, 3, 10)
    for x in (-.62, 0, .62): box((x, .74, -1.19), (.5, .42, .02), 3, .015)
    box((0, .42, 1.15), (1.86, .22, .07), 3, .03)
    box((0, .28, 0), (1.86, .1, 2.24), 3, .02)
    # Mattress, quilt with turned-down cream sheet, pillows and a knitted throw.
    box((0, .5, 0), (1.84, .3, 2.2), 10, .09, segments=3)
    drape(-1.0, 1.0, -.45, 1.12, .7, .2, 6)
    flow([(-.98, .69, -.46), (-.5, .72, -.47), (0, .73, -.47), (.5, .72, -.47), (.98, .69, -.46)], .05, 10, 10)
    for x in (-.46, .46): rock((x, .76, -.82), (.4, .13, .25), 10, 3)
    drape(-1.0, 1.0, .62, 1.0, .755, .26, 4, nx=20, nz=8, folds=.018, thick=.025)

api["EXTRA_BUILDERS"]["HomeBed"] = home_bed
api["setup"]()
api["build"]("HomeBed")
bpy.ops.wm.save_as_mainfile(filepath=str(ART / "storybook-bed-v044.blend"))
print("BED", json.dumps(api["REPORT"]))
