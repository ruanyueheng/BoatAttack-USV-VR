"""Original, deterministic coastal vellum and botanical ornaments; no borrowed game artwork."""
from pathlib import Path
import math
import random
import re
import uuid
from PIL import Image, ImageDraw
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingV036/Stationery'
OUT.mkdir(parents=True, exist_ok=True)
S = 3
GOLD = (170, 139, 72, 238)
SAGE = (112, 127, 76, 242)


def surface(w, h):
    image = Image.new('RGBA', (w*S, h*S))
    return image, ImageDraw.Draw(image)


def line(draw, points, color=GOLD, width=1):
    draw.line([(round(x*S), round(y*S)) for x, y in points], fill=color, width=max(1, round(width*S)), joint='curve')


def polygon(draw, points, color):
    draw.polygon([(round(x*S), round(y*S)) for x, y in points], fill=color)


def leaf(draw, base, tip, width, color=SAGE):
    bx, by = base
    dx, dy = tip[0]-bx, tip[1]-by
    length = math.hypot(dx, dy)
    nx, ny = -dy/length*width, dx/length*width
    polygon(draw, [base, (bx+dx*.35+nx, by+dy*.35+ny),
                   (bx+dx*.70+nx*.65, by+dy*.70+ny*.65), tip,
                   (bx+dx*.67-nx*.5, by+dy*.67-ny*.5),
                   (bx+dx*.3-nx*.7, by+dy*.3-ny*.7)], color)
    line(draw, [base, tip], (232, 220, 168, 160), .45)


def star(draw, x, y, radius, color=GOLD):
    polygon(draw, [(x,y-radius), (x+radius*.22,y-radius*.22), (x+radius*.63,y),
                   (x+radius*.22,y+radius*.22), (x,y+radius),
                   (x-radius*.22,y+radius*.22), (x-radius*.63,y),
                   (x-radius*.22,y-radius*.22)], color)


def save(image, name, size):
    image = image.resize(size, Image.Resampling.LANCZOS)
    image.save(OUT / (name+'.png'))
    # Import at native size: thin filigree needs alpha and a clamped, uncompressed UI surface.
    template = (ROOT/'Assets/Resources/FishingStationery/CoastalPostmark.png.meta').read_text()
    template = re.sub(r'guid: [0-9a-f]{32}', 'guid: '+uuid.uuid5(uuid.NAMESPACE_URL, 'tidebound/v036/'+name).hex, template, count=1)
    template = template.replace('enableMipMap: 1', 'enableMipMap: 0').replace('nPOTScale: 1', 'nPOTScale: 0')
    template = template.replace('wrapU: 0', 'wrapU: 1').replace('wrapV: 0', 'wrapV: 1').replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1')
    template = template.replace('textureCompression: 1', 'textureCompression: 0')
    (OUT/(name+'.png.meta')).write_text(template)
    return image


rng = np.random.default_rng(36)
y, x = np.mgrid[:768, :1024]
edge = np.minimum.reduce([x/36, (1023-x)/36, y/36, (767-y)/36, np.ones_like(x)])
grain = rng.normal(0, .85, x.shape) + .75*np.sin(x*.025+y*.017) + .55*np.sin(y*.11)
vellum = np.clip(np.array([250, 241, 222])+grain[:, :, None]-(1-edge[:, :, None])*np.array([14, 17, 19]), 0, 255).astype('uint8')
paper = Image.fromarray(vellum)
d = ImageDraw.Draw(paper, 'RGBA')
random.seed(36)
for _ in range(1250):
    px, py = random.randrange(1024), random.randrange(768)
    d.line((px, py, px+random.randrange(2, 8), py+random.randrange(-1, 2)), fill=(160, 142, 106, 15))
save(paper, 'Vellum', (1024, 768))

image, d = surface(192, 192)
line(d, [(14,176),(18,143),(22,111),(24,75),(25,26),(70,25),(108,22),(145,18),(176,14)], GOLD, 1.5)
line(d, [(34,132),(33,100),(30,71),(25,26),(71,30),(100,33),(132,34)], SAGE, 1.25)
for base, tip, width in [((25,36),(12,11),5),((29,58),(10,40),6),((31,77),(47,53),6),
                         ((32,97),(13,73),7),((33,116),(49,91),6),((21,138),(7,118),5),
                         ((36,25),(11,12),5),((58,29),(40,10),6),((77,31),(53,47),6),
                         ((97,32),(73,13),7),((116,33),(91,49),6),((138,21),(118,7),5)]:
    leaf(d, base, tip, width)
line(d, [(26,26),(40,40),(55,44),(66,40),(72,32)], GOLD, 1)
line(d, [(26,26),(40,40),(44,55),(40,66),(32,72)], GOLD, 1)
star(d, 53, 53, 5)
star(d, 158, 14, 3.4)
star(d, 14, 158, 3.4)
corner = save(image, 'SeaLaurelCorner', (192, 192))

image, d = surface(160, 128)
for direction in [-1, 1]:
    line(d, [(80,108),(80+direction*5,82),(80+direction*14,57),(80+direction*26,39)], SAGE, 1.6)
    for bx, by, tx, ty in [(83,92,104,79),(87,78,78,55),(93,65,114,50)]:
        leaf(d, (80+(bx-80)*direction,by), (80+(tx-80)*direction,ty), 5)
star(d, 80, 23, 12)
line(d, [(32,106),(44,101),(57,101),(69,106),(81,110),(94,110),(108,105),(128,103)], GOLD, 1.2)
save(image, 'TidalCrest', (160, 128))

image, d = surface(128, 256)
polygon(d, [(13,0),(115,0),(115,242),(64,216),(13,242)], (48, 42, 25, 35))
polygon(d, [(9,0),(111,0),(111,234),(60,209),(9,234)], (126, 140, 91, 255))
line(d, [(16,0),(16,222),(60,200),(104,222),(104,0)], (231,217,166,255), 1.5)
line(d, [(21,0),(21,35)], (229,222,179,120), 1)
leaf(d, (60,163), (61,115), 8, (232,225,185,255))
leaf(d, (60,153), (42,131), 5, (232,225,185,255))
leaf(d, (60,145), (79,124), 5, (232,225,185,255))
star(d, 60, 81, 12, (244,228,177,255))
save(image, 'SageBookmark', (128, 256))

image, d = surface(256, 256)
for inset, color in [(0,(37,42,17,55)),(5,(93,111,53,255)),(15,(118,132,66,255))]:
    points=[]
    for i in range(160):
        a=2*math.pi*i/160
        radius=112-inset+3*math.sin(a*9)+2*math.cos(a*13)
        points.append((128+math.cos(a)*radius,128+math.sin(a)*radius+3))
    polygon(d, points, color)
d.ellipse((39*S,39*S,217*S,217*S), outline=(202,194,125,255), width=3*S)
d.ellipse((45*S,45*S,211*S,211*S), outline=(68,86,40,255), width=2*S)
star(d, 128, 92, 22, (221,211,148,255))
for yy in [139,155,171]:
    line(d, [(px, yy+6*math.sin((px-69)/17)) for px in range(69,188)], (217,207,143,255), 3)
save(image, 'TidalWaxSeal', (256, 256))

assert Image.open(OUT/'Vellum.png').size == (1024, 768)
assert np.asarray(paper)[80:-80,80:-80,:3].std() < 15, 'Reading area must remain quiet'
assert corner.getpixel((150,150))[3] == 0, 'Ornaments must leave the reading area transparent'
assert all(Image.open(path).getbbox() for path in OUT.glob('*.png'))
print('V036 STATIONERY PASS: five original surfaces; quiet text area and transparent ornaments verified')
