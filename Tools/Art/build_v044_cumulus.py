"""Bake the tileable cumulus atlas for FishingCoastalSky (1024px, linear data).

R: large cumulus clumps (domed height, 0 = clear sky)
G: smaller fair-weather puffs, different layout (second parallax deck)
B: tileable fbm used to erode cloud edges
Usage: python -I build_v044_cumulus.py <output.png> [big.png small.png]
(optional AI watercolor density maps replace R/G; scene-art Logs/V044/sky/gen)
"""
import sys
import numpy as np
from PIL import Image

N = 1024
rng = np.random.default_rng(4404)
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N


def fbm(octaves, base):
    out = np.zeros((N, N), np.float32)
    amp = .5
    for o in range(octaves):
        f = base * 2 ** o
        lat = rng.random((f, f)).astype(np.float32)
        x, y = xx * f, yy * f
        x0, y0 = x.astype(int) % f, y.astype(int) % f
        fx, fy = x - np.floor(x), y - np.floor(y)
        fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        x1, y1 = (x0 + 1) % f, (y0 + 1) % f
        a = lat[y0, x0] * (1 - fx) + lat[y0, x1] * fx
        b = lat[y1, x0] * (1 - fx) + lat[y1, x1] * fx
        out += amp * (a * (1 - fy) + b * fy)
        amp *= .5
    return out / (1 - .5 ** octaves)


def clumps(count, radius, puffs):
    """Each clump is a cluster of domed metaballs; wraps at the tile edge."""
    h = np.zeros((N, N), np.float32)
    for _ in range(count):
        cx, cy = rng.random(2)
        size = radius * (.6 + .8 * rng.random())
        for _ in range(puffs):
            # Puffs spread wide and pile up toward the clump centre: a broad base with a towering crown.
            ox, oy = rng.normal(0, size * .9), rng.normal(0, size * .45)
            r = size * (.38 + .6 * rng.random() ** 2) * (1.2 - min(1, abs(ox) / (size * 1.6)) * .5)
            dx = (xx - (cx + ox) + .5) % 1 - .5
            dy = (yy - (cy + oy) + .5) % 1 - .5
            d2 = (dx * dx + dy * dy) / (r * r)
            # Soft gaussian puffs summed (not hard domes): fluffy edges, dense cores.
            h += np.exp(-d2 * 2.2) * (.35 + .25 * rng.random())
    return 1 - np.exp(-h * 1.6)


erode = fbm(5, 8)
big = clumps(9, .13, 22)
small = clumps(12, .09, 16)
big = np.clip((big * (.55 + .9 * erode) - .30) / .70, 0, 1)
small = np.clip((small * (.55 + .9 * erode[::-1]) - .30) / .70, 0, 1)
if len(sys.argv) > 3:
    big, small = (np.asarray(Image.open(f).convert('L').resize((N, N)), np.float32) / 255 for f in sys.argv[2:4])
    # AI maps keep grey haze between clouds; cut it so only the clumps remain (~30% cover).
    big, small = np.clip((big - .38) / .62, 0, 1), np.clip((small - .25) / .75, 0, 1)
img = np.stack([big, small, erode], -1)
Image.fromarray((img * 255 + .5).astype(np.uint8), 'RGB').save(sys.argv[1])
print('cover R', float((big > .2).mean()), 'G', float((small > .2).mean()))
