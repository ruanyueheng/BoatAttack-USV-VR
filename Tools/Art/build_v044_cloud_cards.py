"""Distant cumulus card atlas: 8 cells (2 cols x 4 rows, 512x256), linear density.
Single-cloud images (card-*.png) are used whole; tiled density maps contribute whole clouds not touching the edge.
Missing cells repeat earlier shapes mirrored. Usage: python -I build_v044_cloud_cards.py <out.png> <image>..."""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

CW, CH = 512, 256
clouds = []
for path in sys.argv[2:]:
    src = np.clip((np.asarray(Image.open(path).convert('L'), np.float32) / 255 - .38) / .62, 0, 1)
    labels, n = ndimage.label(ndimage.gaussian_filter(src, 3) > .22)
    found = []
    for i, sl in enumerate(ndimage.find_objects(labels)):
        h, w = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
        # AI tile seams are not truly seamless: only whole clouds inside the image, with a dense core.
        if 0 in (sl[0].start, sl[1].start) or src.shape[0] in (sl[0].stop,) or src.shape[1] in (sl[1].stop,) or src[sl].max() < .6 or min(h, w) < 70:
            continue
        mask = ndimage.gaussian_filter((labels[sl] == i + 1).astype(np.float32), 4)
        found.append((src[sl].sum(), src[sl] * np.clip(mask * 2, 0, 1)))
    found.sort(key=lambda f: -f[0])
    clouds += [f[1] for f in found[:1 if 'card' in path else 2]]
atlas = np.zeros((1024, 1024), np.float32)
for k in range(8):
    cloud = clouds[k % len(clouds)]
    if k >= len(clouds):
        cloud = cloud[:, ::-1]
    h, w = cloud.shape
    s = min((CW - 24) / w, (CH - 12) / h)
    img = Image.fromarray((cloud * 255).astype(np.uint8)).resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)
    cw, ch = img.size
    cx, cy = k % 2 * CW, k // 2 * CH
    y0 = cy + CH - 6 - ch  # flat cloud bases sit on the cell floor
    atlas[y0:y0 + ch, cx + (CW - cw) // 2:cx + (CW - cw) // 2 + cw] = np.asarray(img, np.float32) / 255
Image.fromarray((atlas * 255).astype(np.uint8), 'L').save(sys.argv[1])
print('unique shapes', len(clouds))
