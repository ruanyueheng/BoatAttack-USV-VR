"""Subset Noto Serif SC (OFL) to glyphs used by game scripts + GB2312 level-1 for the V044 display face."""
import glob, sys
from fontTools import subset
src, out = sys.argv[1], sys.argv[2]
chars = set(chr(c) for c in range(0x20, 0x7f)) | set("·—…“”‘’「」『』《》【】、，。：；！？（）×→←↑↓√／～ⅠⅡⅢⅣ")
for path in glob.glob("Assets/Scripts/**/*.cs", recursive=True):
    chars |= {ch for ch in open(path, encoding="utf-8", errors="ignore").read() if ord(ch) > 0x2000}
for hi in range(0xB0, 0xD8):
    for lo in range(0xA1, 0xFF):
        try: chars.add(bytes([hi, lo]).decode("gb2312"))
        except UnicodeDecodeError: pass
opts = subset.Options(); opts.layout_features = ["kern", "liga", "palt", "vert"]; opts.name_IDs = ["*"]; opts.notdef_outline = True
font = subset.load_font(src, opts); s = subset.Subsetter(opts); s.populate(text="".join(chars)); s.subset(font)
subset.save_font(font, out, opts); print(len(chars), "chars ->", out)
