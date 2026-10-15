"""Bake the notebook's original line icons/paper and its pinned handwritten font."""
from pathlib import Path
from hashlib import sha256
import json
import math
import random
from PIL import Image, ImageDraw
from fontTools.ttLib import TTFont
from fontTools import subset

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/FishingUIPrototype"
OUT.mkdir(parents=True, exist_ok=True)
INK, GOLD, SAGE, CORAL, CREAM = "#314F48", "#D2A76E", "#6D9F89", "#EF916B", "#F4E4C3"


def icon(index):
    im = Image.new("RGBA", (512, 512))
    d = ImageDraw.Draw(im)

    def line(points, color=INK, width=3):
        p = [(round(x * 4), round(y * 4)) for x, y in points]
        d.line(p, fill=color, width=round(width * 4), joint="curve")
        for x, y in (p[0], p[-1]):
            r = width * 2
            d.ellipse((x-r, y-r, x+r, y+r), fill=color)

    def shape(box, fill, radius=0, outline=INK, width=3, ellipse=False):
        box = tuple(round(v * 4) for v in box)
        if ellipse:
            d.ellipse(box, fill=fill, outline=outline, width=width*4)
        else:
            d.rounded_rectangle(box, radius=radius*4, fill=fill, outline=outline, width=width*4)

    def polygon(points, fill, outline=INK):
        d.polygon([(x*4, y*4) for x, y in points], fill=fill)
        line(points + [points[0]], outline)

    if index != 8:
        shape((9, 9, 119, 119), ["#EEE0B6", "#E3E8CF", "#DFE8D2", "#E7E8CD", "#F0DDC1", "#F0DED0", "#E1E6CF", "#EBE0BE"][index], outline=None, ellipse=True)
    if index == 0:
        d.arc((46*4, 23*4, 83*4, 62*4), 180, 360, fill=INK, width=12)
        shape((33, 40, 96, 103), GOLD, 13)
        shape((41, 63, 87, 89), CREAM, 7)
        line([(41, 55), (88, 55)], INK, 2)
        shape((59, 73, 69, 82), SAGE, 2, width=2)
    elif index == 1:
        polygon([(35, 90), (45, 98), (90, 46), (79, 36)], GOLD)
        polygon([(24, 43), (49, 22), (72, 48), (47, 70)], SAGE)
        line([(33, 43), (48, 31)], CREAM, 3)
        line([(70, 86), (98, 86)], INK, 3)
        shape((74, 90, 101, 100), GOLD, 2, width=2)
    elif index == 2:
        polygon([(26, 39), (49, 30), (76, 40), (102, 30), (102, 91), (76, 103), (49, 92), (26, 100)], CREAM)
        line([(49, 32), (49, 90)], GOLD, 2)
        line([(76, 43), (76, 100)], GOLD, 2)
        shape((52, 48, 94, 90), "#E0E8CE", ellipse=True, width=2)
        polygon([(73, 50), (82, 70), (73, 84), (63, 68)], CORAL)
        line([(59, 81), (43, 72), (37, 79)], SAGE, 2)
    elif index == 3:
        polygon([(25, 39), (44, 32), (63, 39), (84, 32), (103, 39), (103, 98), (83, 90), (63, 98), (44, 90), (25, 98)], CREAM)
        line([(63, 39), (63, 96)], GOLD, 2)
        line([(81, 77), (87, 54), (99, 45)], INK, 2)
        polygon([(81, 70), (75, 52), (86, 48), (87, 62)], SAGE)
        line([(35, 54), (52, 54)], GOLD, 2)
        line([(35, 67), (49, 67)], GOLD, 2)
        line([(35, 80), (52, 80)], GOLD, 2)
    elif index == 4:
        line([(66, 26), (66, 76)], INK, 3)
        polygon([(69, 32), (95, 62), (69, 62)], CORAL)
        polygon([(62, 39), (42, 64), (62, 64)], CREAM)
        polygon([(29, 72), (99, 72), (86, 90), (43, 90)], SAGE)
        line([(28, 100), (40, 104), (53, 100), (65, 104), (78, 100), (93, 103)], INK, 2)
    elif index == 5:
        line([(39, 96), (79, 30), (96, 42)], GOLD, 5)
        line([(79, 31), (96, 42), (96, 83)], INK, 2)
        d.arc((80*4, 69*4, 98*4, 88*4), 0, 200, fill=INK, width=12)
        shape((31, 73, 54, 96), CORAL, ellipse=True, width=2)
        shape((38, 80, 47, 89), CREAM, ellipse=True, width=2)
        line([(53, 86), (62, 83)], INK, 2)
    elif index == 6:
        polygon([(28, 89), (43, 74), (85, 74), (103, 90)], GOLD)
        line([(64, 80), (72, 45)], INK, 3)
        for end in [(38, 46), (48, 30), (71, 27), (92, 38), (102, 57)]:
            line([(72, 45), end], SAGE, 5)
        line([(29, 100), (45, 104), (65, 100), (84, 104), (104, 100)], INK, 2)
        line([(26, 30), (34, 26), (41, 30)], INK, 2)
    elif index == 7:
        for n in range(8):
            a = n * math.pi / 4
            line([(64+18*math.cos(a), 64+18*math.sin(a)), (64+42*math.cos(a), 64+42*math.sin(a))], INK, 4)
        shape((36, 36, 92, 92), GOLD, ellipse=True)
        shape((47, 47, 81, 81), CREAM, ellipse=True, width=2)
        shape((59, 59, 69, 69), SAGE, ellipse=True, width=2)
    else:
        line([(30, 65), (97, 65)], "white", 6)
        line([(77, 42), (99, 65), (77, 88)], "white", 6)
    return im.resize((256, 256), Image.Resampling.LANCZOS)


atlas = Image.new("RGBA", (768, 768))
for i in range(9):
    atlas.alpha_composite(icon(i), (i % 3 * 256, i // 3 * 256))
atlas.save(OUT / "NavigationAtlas.png")

paper = Image.new("RGBA", (304, 304))
d = ImageDraw.Draw(paper)
d.rounded_rectangle((1, 1, 302, 302), radius=21, fill=(255, 255, 255, 255))
rng = random.Random(20261014)
for _ in range(1400):
    x, y = rng.randint(3, 300), rng.randint(3, 300)
    if 25 < x < 278 and 25 < y < 278:
        continue
    if paper.getpixel((x, y))[3]:
        value = rng.randint(236, 255)
        d.point((x, y), fill=(value, value, value, 255))
paper.save(OUT / "Paper.png")
edge = Image.new("RGBA", (304, 304))
d = ImageDraw.Draw(edge)
d.rounded_rectangle((2, 2, 301, 301), radius=20, outline=(255, 255, 255, 235), width=2)
d.rounded_rectangle((6, 6, 297, 297), radius=17, outline=(255, 255, 255, 54), width=1)
edge.save(OUT / "InkEdge.png")

info = json.loads((ROOT / "ArtSource/Fonts/VRUI/source.json").read_text(encoding="utf-8-sig"))
source = ROOT / "Logs/font-cache/LXGWWenKai-Medium-v1.522.ttf"
assert sha256(source.read_bytes()).hexdigest() == info["sha256"], "Pinned title font digest changed"
texts = "".join(p.read_text(encoding="utf-8-sig") for p in (ROOT / "Assets/Scripts/Fishing").glob("*.cs"))
# shortcut: font glyphs cover current source text; rebuild when UI copy or localization changes.
characters = {ord(c) for c in texts if ord(c) >= 32} | set(range(32, 127))
font = TTFont(source)
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=characters)
subsetter.subset(font)
for key, value in {1: "Tide Hand SC", 2: "Medium", 3: "TideHandSC-Medium-v1.522", 4: "Tide Hand SC Medium", 6: "TideHandSC-Medium"}.items():
    font["name"].setName(value, key, 3, 1, 0x409)
    font["name"].setName(value, key, 1, 0, 0)
font.save(OUT / "TideHandSC.ttf")
needed = {ord(c) for c in "潮汐手册去海边，慢一点。"}
assert needed <= set(TTFont(OUT / "TideHandSC.ttf").getBestCmap()), "Missing handwritten title glyph"
assert (OUT / "TideHandSC.ttf").stat().st_size < 2_000_000, "Title font exceeded prototype budget"
assert atlas.size == (768, 768) and atlas.getextrema()[3] == (0, 255)
for i in range(9):
    assert atlas.crop((i%3*256, i//3*256, i%3*256+256, i//3*256+256)).getbbox(), "Empty navigation glyph"
assert paper.getpixel((150, 150)) == (255, 255, 255, 255) and edge.getpixel((150, 150))[3] == 0
print("Notebook assets PASS: 9 atlas glyphs, 2 sliced surfaces, pinned Chinese title font.")
