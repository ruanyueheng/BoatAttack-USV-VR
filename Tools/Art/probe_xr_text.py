"""Python mirror of FishingXRText.Adapt for fast iteration; the Unity check (FishingXRTextValidation) is authoritative.
Usage: python -I Tools/Art/probe_xr_text.py   (prints CJK string literals that still contain keyboard key names after adaptation)"""
import glob, re

PHRASES = [
    ("按住左键后摆，松开甩竿", "按住扳机，向前甩手柄，松开抛竿"),
    ("按住左键收线并移动鼠标 · 松开放线", "左握把转腕收线 · 持竿手横移控鱼 · 松开放线"),
    ("按住左键，鼠标向右控竿", "持竿手向右横移控鱼"), ("按住左键，鼠标向左控竿", "持竿手向左横移控鱼"),
    ("轻点近抛 · 按住远抛", "甩得轻抛得近 · 甩得用力抛得远"),
    ("松开左键甩竿", "松开扳机抛竿"), ("按住左键收线", "左握把转腕收线"),
    ("立刻松开左键", "立刻松开握把"), ("松开左键", "松开握把"),
    ("Ctrl + W 加速", "按下左摇杆加速"),
]
def key(k): return re.compile(r"(?<![A-Za-z0-9])" + re.escape(k) + r"(?![A-Za-z0-9])")
KEYS = [(key("WASD"), "左摇杆"), (re.compile("方向键"), "左摇杆"), (key("Shift"), "左扳机"), (key("Ctrl"), "左摇杆按下"),
        (key("Esc"), "B"), (key("Tab"), "事务面板"), (key("Enter"), "A"),
        (key("E"), "A"), (key("T"), "A"), (key("G"), "X"), (key("F"), "右扳机"), (key("Q"), "事务面板"), (key("R"), "手册"),
        (key("I"), "手册"), (key("J"), "手册"), (key("M"), "手册"), (key("N"), "手册"), (key("O"), "手册"), (key("U"), "手册"), (key("P"), "右扳机"), (key("F1"), "求助石 A"),
        (re.compile("左键"), "右扳机"), (re.compile("右键"), "左握把"), (re.compile("鼠标"), "手柄"), (re.compile("数字键"), "Y")]
DESKTOP_ONLY = re.compile(r"(?<![A-Za-z0-9])(Space|Alt|C|V|H|Z|S|W)(?![A-Za-z0-9])|空格|滚轮")
FORBIDDEN = re.compile(r"(?<![A-Za-z0-9])(WASD|Shift|Ctrl|Space|Esc|Tab|Alt|Enter|F1|[EFGQRTIJMNOUPCVHZSW])(?![A-Za-z0-9])|左键|右键|鼠标|空格|方向键|数字键|滚轮")
def strip(seg): return re.sub(r"\d+(\.\d+)?\s*(m|km/h|s)\b|BGM|VR|XR|PICO|CC BY", "", seg)
def adapt(s):
    for a, b in PHRASES: s = s.replace(a, b)
    lines = s.split("\n")
    for l, line in enumerate(lines):
        parts = re.split(r"(\s*[·/／、，；]\s*)", line); kept = []
        for i in range(0, len(parts), 2):
            seg = parts[i]
            if DESKTOP_ONLY.search(strip(seg)): continue
            for k, to in KEYS: seg = k.sub(to, seg)
            if kept and i > 0: kept.append(parts[i - 1])
            kept.append(seg)
        lines[l] = "".join(kept).strip()
    return "\n".join(lines)

LIT = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
hits = 0
for path in sorted(glob.glob("Assets/Scripts/Fishing/*.cs")):
    for n, line in enumerate(open(path, encoding="utf-8"), 1):
        for m in LIT.finditer(line):
            lit = m.group(1).replace("\\n", "\n")
            if not re.search(r"[一-鿿]", lit): continue
            out = adapt(lit)
            if FORBIDDEN.search(out):
                hits += 1; print(f"{path.split('/')[-1]}:{n}: {lit!r} -> {out!r}")
print("hits", hits)
