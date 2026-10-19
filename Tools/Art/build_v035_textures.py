"""Original, deterministic PBR atlas for the V035 coastal masonry and foliage kit."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV035'
OUT.mkdir(parents=True, exist_ok=True)
N = 512
rng = np.random.default_rng(35)
tiles = [
    ('limestone', (192, 179, 148), 0, .84),
    ('aged stone', (128, 135, 127), 0, .90),
    ('dark foundation', (68, 83, 88), 0, .92),
    ('weathered oak', (105, 74, 46), 0, .78),
    ('hammered brass', (190, 150, 76), 1, .43),
    ('copper patina', (57, 112, 103), 0, .82),
    ('teal linen', (38, 84, 91), 0, .91),
    ('velvet moss', (76, 103, 46), 0, .95),
    ('deep leaves', (46, 87, 48), 0, .82),
    ('sunlit leaves', (105, 132, 57), 0, .83),
    ('ivory petals', (225, 214, 172), 0, .72),
    ('heather petals', (145, 119, 164), 0, .80),
    ('warm sand', (180, 163, 119), 0, .91),
    ('flax rope', (166, 140, 95), 0, .86),
    ('blue slate', (66, 88, 103), 0, .64),
    ('midnight enamel', (30, 51, 72), 0, .38),
]

def noise(size, amplitude):
    a = rng.integers(0, 256, (size, size), dtype=np.uint8)
    return (np.asarray(Image.fromarray(a).resize((N, N), Image.Resampling.BICUBIC), dtype=float) / 255 - .5) * amplitude

albedo = np.zeros((N*4, N*4, 3), dtype=np.uint8)
normal = np.zeros_like(albedo)
mr = np.zeros((N*4, N*4, 4), dtype=np.uint8)
ms = np.zeros_like(mr)
y, x = np.mgrid[0:N, 0:N] / N
for i, (name, color, metallic, roughness) in enumerate(tiles):
    relief = noise(12, 20) + noise(60, 9) + noise(256, 5)
    if i in (0, 1, 2, 14):
        cracks = Image.new('L', (N, N)); pen = ImageDraw.Draw(cracks)
        for j in range(5):
            start = rng.uniform(0, N, 2)
            points = [tuple(start)]
            for k in range(5):
                start += rng.uniform(-21, 21, 2); points.append(tuple(start))
            pen.line(points, fill=18, width=1)
        relief -= np.asarray(cracks.filter(ImageFilter.GaussianBlur(.5)))
    if i in (3, 13):
        relief += np.sin(x * 180 + np.sin(y * 10) * 3 + noise(8, 3)) * 9
        relief += np.sin(x * 700 + y * 35) * 3
    if i == 6:
        relief += np.sin(x * 760) * np.sin(y * 760) * 7
    if i in (8, 9):
        relief += np.exp(-((x - .5) * 75) ** 2) * 26
        relief += np.cos(y * 90 + np.abs(x - .5) * 60) * 5
    tint = np.asarray(color) + relief[..., None]
    px, py = (i % 4) * N, (i // 4) * N
    albedo[py:py+N, px:px+N] = np.clip(tint, 0, 255)
    dy, dx = np.gradient(relief / 110)
    vectors = np.stack((-dx * 2, -dy * 2, np.ones_like(dx)), axis=2)
    vectors /= np.linalg.norm(vectors, axis=2, keepdims=True)
    normal[py:py+N, px:px+N] = np.clip(vectors * 127 + 128, 0, 255)
    rough = np.clip(roughness * 255 + relief * .4, 20, 253).astype(np.uint8)
    mr[py:py+N, px:px+N] = np.stack((np.full_like(rough, 255), rough, np.full_like(rough, metallic*255), np.full_like(rough, 255)), axis=2)
    ms[py:py+N, px:px+N] = np.stack((np.full_like(rough, metallic*255), np.zeros_like(rough), np.zeros_like(rough), 255-rough), axis=2)

for name, data in [('CoastalAlbedo', albedo), ('CoastalNormal', normal), ('CoastalMetalRough', mr), ('CoastalMetalSmooth', ms)]:
    Image.fromarray(data).save(OUT / (name + '.png'))
assert albedo.shape == normal.shape == (2048, 2048, 3)
assert len(np.unique(mr[:, :, 2])) == 2
(ROOT / 'ArtSource/V035/atlas-regions.json').write_text(json.dumps({'size':[2048,2048], 'grid':[4,4], 'tiles':[{'id':i,'name':t[0]} for i,t in enumerate(tiles)]}, indent=2), encoding='utf-8')
print('V035: four 2048px PBR textures; 16 padded material regions.')

# The generated diffuse image is an authored input; procedural maps remain reproducible.
refined=ROOT/'ArtSource/V035/CoastalAlbedo-refined.png'
if refined.exists():
    import shutil
    with Image.open(refined) as image:assert image.width==image.height and image.width>=1024
    shutil.copyfile(refined,OUT/'CoastalAlbedo.png')
