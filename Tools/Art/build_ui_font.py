"""Build the redistributable static UI font from pinned Noto Sans SC (SIL OFL)."""
from pathlib import Path
from hashlib import sha256
import json
import urllib.request
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

ROOT = Path(__file__).resolve().parents[2]
info = json.loads((ROOT / "ArtSource/Fonts/source.json").read_text())
source = ROOT / "Logs/font-cache/NotoSansSC-variable.ttf"
if not source.exists():
    source.parent.mkdir(parents=True, exist_ok=True)
    url = "https://raw.githubusercontent.com/google/fonts/" + info["commit"] + "/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf"
    source.write_bytes(urllib.request.urlopen(url, timeout=60).read())
assert sha256(source.read_bytes()).hexdigest() == "a3041811a78c361b1de50f953c805e0244951c21c5bd412f7232ef0d899af0da", "Unexpected font source"
text = "".join(p.read_text(encoding="utf-8-sig") for p in (ROOT / "Assets/Scripts").rglob("*.cs"))
characters = {ord(c) for c in text if ord(c) >= 32} | set(range(32, 127))
font = TTFont(source)
instantiateVariableFont(font, {"wght": 500}, inplace=True)
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=characters)
subsetter.subset(font)
for key, value in {1: "Tide Sans SC", 2: "Medium", 3: "TideSansSC-Medium-v14", 4: "Tide Sans SC Medium", 6: "TideSansSC-Medium"}.items():
    font["name"].setName(value, key, 3, 1, 0x409)
    font["name"].setName(value, key, 1, 0, 0)
output = ROOT / "Assets/Resources/Fonts/TideSansSC.ttf"
output.parent.mkdir(parents=True, exist_ok=True)
font.save(output)
check = TTFont(output)
needed = {n for n in characters if 0x4e00 <= n <= 0x9fff or 32 <= n <= 126}
assert needed <= set(check.getBestCmap()), "Missing UI glyphs"
assert output.stat().st_size < 3_000_000, "UI font exceeded mobile budget"
print(f"Font PASS: {len(check.getBestCmap())} glyphs, {output.stat().st_size:,} bytes")
