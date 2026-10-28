"""V044 storybook UI surfaces: watercolour paper / night veil 9-slice panels, gilt star, rule, slot, title glow."""
import sys, math, random
import numpy as np
from PIL import Image, ImageFilter
out = sys.argv[1]
rng = np.random.default_rng(44)

def noise(n, blur):
    img = Image.fromarray((rng.random((n, n)) * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(blur))
    a = np.asarray(img, dtype=np.float32) / 255.0
    return (a - a.min()) / max(1e-6, a.max() - a.min())

def feather_alpha(n, margin, soft):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    d = np.minimum(np.minimum(x, n - 1 - x), np.minimum(y, n - 1 - y))
    wobble = (noise(n, 10) - .5) * 6  # deckled watercolour edge
    return np.clip((d - margin + wobble) / soft, 0, 1)

def star(canvas, cx, cy, r, color, alpha):
    """Four-point star drawn as concave diamond with soft glow, written into RGBA float canvas."""
    n = canvas.shape[0]
    y, x = np.mgrid[0:n, 0:canvas.shape[1]].astype(np.float32)
    dx, dy = np.abs(x - cx) / r, np.abs(y - cy) / r
    shape = np.clip(1 - (np.sqrt(dx) + np.sqrt(dy)), 0, 1) ** .6
    glow = np.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (2 * (r * .45) ** 2)) * .35
    a = np.clip(shape * 1.6 + glow, 0, 1) * alpha
    for i in range(3): canvas[..., i] = canvas[..., i] * (1 - a) + color[i] * a
    canvas[..., 3] = np.maximum(canvas[..., 3], a)

def hairline(canvas, inset, color, alpha, gap):
    n = canvas.shape[0]
    a = np.zeros(canvas.shape[:2], np.float32)
    lo, hi = inset, n - 1 - inset
    a[lo, lo + gap:hi - gap + 1] = 1; a[hi, lo + gap:hi - gap + 1] = 1
    a[lo + gap:hi - gap + 1, lo] = 1; a[lo + gap:hi - gap + 1, hi] = 1
    a = np.asarray(Image.fromarray((a * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(.45)), np.float32) / 255 * 1.6
    a = np.clip(a, 0, 1) * alpha
    for i in range(3): canvas[..., i] = canvas[..., i] * (1 - a) + color[i] * a

def save(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype("uint8"), "RGBA").save(f"{out}/{name}.png", optimize=True)

GOLD = (0.89, 0.74, 0.45)
N = 256
for name, base, bloom, alpha, gold_a in [
    ("Paper", (0.965, 0.925, 0.835), (0.90, 0.83, 0.70), 1.0, .70),
    ("Veil", (0.075, 0.125, 0.205), (0.16, 0.25, 0.36), .86, .62)]:
    c = np.zeros((N, N, 4), np.float32)
    mottling = noise(N, 18) * .65 + noise(N, 5) * .25 + noise(N, 1.2) * .10
    for i in range(3): c[..., i] = base[i] + (bloom[i] - base[i]) * mottling * .55
    edge = feather_alpha(N, 5, 9)
    # pigment pools darker toward the deckled edge, as in real wet-on-wet washes
    rim = np.clip(1 - edge, 0, 1) * .5 + (1 - feather_alpha(N, 12, 22)) * .25
    for i in range(3): c[..., i] = c[..., i] * (1 - rim * (.22 if name == "Paper" else -.35))
    c[..., 3] = edge * alpha
    hairline(c, 16, GOLD if name == "Veil" else (0.62, 0.47, 0.22), gold_a, 26)
    for (sx, sy) in [(16, 16), (N - 17, 16), (16, N - 17), (N - 17, N - 17)]:
        star(c, sx, sy, 9, (0.98, 0.86, 0.56) if name == "Veil" else (0.70, 0.52, 0.22), .95)
    save(c, "Panel" + name)

c = np.zeros((64, 64, 4), np.float32); star(c, 31.5, 31.5, 30, (1.0, 1.0, 1.0), 1.0); c[..., :3] = 1.0; save(c, "Star")  # white: IMGUI tint supplies the gold

w, h = 512, 16
c = np.zeros((h, w, 4), np.float32); c[..., :3] = 1.0  # tinted at draw time
xs = np.linspace(-1, 1, w); fade = np.clip(1 - np.abs(xs) ** 2.2, 0, 1)
ys = np.exp(-((np.arange(h) - 7.5) ** 2) / 1.2)
c[..., 3] = ys[:, None] * fade[None, :]
save(c, "GiltRule")

S = 128
c = np.zeros((S, S, 4), np.float32); c[..., :3] = (0.07, 0.12, 0.19)
y, x = np.mgrid[0:S, 0:S].astype(np.float32)
d = np.maximum(np.abs(x - 63.5), np.abs(y - 63.5))
c[..., 3] = np.clip((60 - d) / 6, 0, 1) * .48
ring = np.clip(1 - np.abs(d - 54) / 1.1, 0, 1) * .55
for i, g in enumerate(GOLD): c[..., i] = c[..., i] * (1 - ring) + g * ring
c[..., 3] = np.maximum(c[..., 3], ring)
save(c, "Slot")

W, H = 256, 128
y, x = np.mgrid[0:H, 0:W].astype(np.float32)
g = np.exp(-(((x - W / 2) / (W * .36)) ** 2 + ((y - H / 2) / (H * .34)) ** 2))
c = np.zeros((H, W, 4), np.float32); c[..., :3] = (0.03, 0.06, 0.11); c[..., 3] = np.clip((g - .06) / .94, 0, 1) * .72  # exact zero well inside the rect, so no box edge survives
save(c, "TitleGlow")
print("ok")

# --- V044 map/journal spread (learned from the Little Prince journey map, made coastal) ---
W, H = 1024, 512
c = np.zeros((H, W, 4), np.float32)
wash = np.asarray(Image.fromarray((rng.random((H, W)) * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(22)), np.float32) / 255
wash = (wash - wash.min()) / (wash.max() - wash.min())
for i, (lo, hi) in enumerate([(0.04, 0.13), (0.07, 0.20), (0.17, 0.38)]): c[..., i] = lo + (hi - lo) * wash
c[..., 3] = 1
for _ in range(260):  # pinprick stars
    x, y = rng.integers(0, W), rng.integers(0, H); b = rng.random() ** 3
    c[y, x, :3] = np.clip(c[y, x, :3] + .55 + .45 * b, 0, 1)
for _ in range(14):
    sx, sy = rng.integers(20, W - 20), rng.integers(20, H - 20); sub = c[sy - 16:sy + 16, sx - 16:sx + 16].copy()
    star(sub, 15.5, 15.5, 6 + rng.random() * 8, (1.0, 0.95, 0.85), .9); c[sy - 16:sy + 16, sx - 16:sx + 16] = sub
c[..., 3] = 1; save(c, "StarrySea")

N = 512
c = np.zeros((N, N, 4), np.float32)
stain = noise(N, 30) * .6 + noise(N, 6) * .3 + noise(N, 1.5) * .1
for i, (lo, hi) in enumerate([(0.98, 0.90), (0.95, 0.85), (0.86, 0.72)]): c[..., i] = lo + (hi - lo) * stain * .7
y, x = np.mgrid[0:N, 0:N].astype(np.float32)
d = np.minimum(np.minimum(x, N - 1 - x), np.minimum(y, N - 1 - y))
tear = (noise(N, 3) - .5) * 22 + (noise(N, 12) - .5) * 26  # torn deckle
edge = np.clip((d - 26 + tear) / 2.5, 0, 1)
burn = np.clip(1 - (d - 26 + tear) / 26, 0, 1)  # browned torn rim
for i, k in enumerate((.25, .34, .48)): c[..., i] *= 1 - burn * k
c[..., 3] = edge
save(c, "TornParchment")

S = 256
y, x = np.mgrid[0:S, 0:S].astype(np.float32)
r = np.hypot(x - 127.5, y - 127.5) / 128
puff = noise(S, 9) * .6 + noise(S, 3) * .4
a = np.clip((1 - r * (1.35 + (puff - .5) * .7)) * 2.2, 0, 1)
c = np.zeros((S, S, 4), np.float32)
for i, (lo, hi) in enumerate([(0.55, 0.86), (0.62, 0.90), (0.78, 0.97)]): c[..., i] = lo + (hi - lo) * puff
c[..., 3] = a * .92
save(c, "SeaFog")

c = np.zeros((S, S, 4), np.float32); c[..., :3] = 1
ring = np.clip(1 - np.abs(r - .86) * 128 / 1.6, 0, 1) + np.clip(1 - np.abs(r - .80) * 128 / .9, 0, 1) * .55
ang = np.arctan2(y - 127.5, x - 127.5)
ticks = (np.abs(((ang / (2 * np.pi) * 32) % 1) - .5) > .44) * ((r > .80) & (r < .86))
c[..., 3] = np.clip(ring + ticks * .8, 0, 1)
for k in range(4):
    t = k * np.pi / 2; star(c, 127.5 + np.cos(t) * 110, 127.5 + np.sin(t) * 110, 10 if k == 3 else 6, (1, 1, 1), 1)
c[..., :3] = 1; save(c, "CompassRing")
print("spread ok")

# --- world label plate: small cream card with gilt hairline (sliced sprite, 24px border) ---
W, H = 192, 96
y, x = np.mgrid[0:H, 0:W].astype(np.float32)
qx, qy = np.maximum(np.abs(x - (W - 1) / 2) - (W / 2 - 22), 0), np.maximum(np.abs(y - (H - 1) / 2) - (H / 2 - 22), 0)
dist = np.hypot(qx, qy)
c = np.zeros((H, W, 4), np.float32)
grain = np.asarray(Image.fromarray((rng.random((H, W)) * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(3)), np.float32) / 255
for i, v in enumerate((0.97, 0.93, 0.84)): c[..., i] = v - (grain - .5) * .05
c[..., 3] = np.clip((18 - dist) / 1.5, 0, 1) * .94
gild = np.clip(1 - np.abs(dist - 12) / .9, 0, 1) * (dist > 0)
for i, g in enumerate((0.72, 0.55, 0.26)): c[..., i] = c[..., i] * (1 - gild) + g * gild
save(c, "SignPlate")
print("sign ok")

# --- PICO controller line glyphs (A B X Y Trigger Grip), white on transparent, 128px cells, tinted at use ---
from PIL import ImageDraw, ImageFont
S = 4 * 128  # supersample
atlas = Image.new("RGBA", (6 * S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(atlas)
font = ImageFont.truetype(sys.argv[2], 220) if len(sys.argv) > 2 else None
lw = 26
for i, key in enumerate("ABXY"):
    cx, cy = i * S + S // 2, S // 2
    d.ellipse((cx - 200, cy - 200, cx + 200, cy + 200), outline="white", width=lw)
    if font: d.text((cx, cy + 6), key, font=font, fill="white", anchor="mm")
cx = 4 * S + S // 2  # trigger: hooked blade outline (quadratic Bezier edges) under the body line
def bez(p0, p1, p2, n=24): return [((1-t)**2*p0[0]+2*(1-t)*t*p1[0]+t*t*p2[0], (1-t)**2*p0[1]+2*(1-t)*t*p1[1]+t*t*p2[1]) for t in [k/n for k in range(n+1)]]
outer = bez((cx + 110, 110), (cx + 150, 380), (cx - 60, 450)); inner = bez((cx - 60, 450), (cx + 20, 300), (cx - 110, 110))
d.line(outer + inner + [outer[0]], fill="white", width=lw, joint="curve")
d.line((cx - 210, 100, cx + 210, 100), fill="white", width=lw)
cx = 5 * S + S // 2  # grip: side paddle with grip ridges
d.rounded_rectangle((cx - 110, 50, cx + 110, 462), radius=100, outline="white", width=lw)
for k in range(3): d.line((cx - 50, 190 + k * 60, cx + 50, 190 + k * 60), fill="white", width=lw - 6)
atlas.resize((6 * 128, 128), Image.LANCZOS).save(f"{out}/ControllerGlyphs.png", optimize=True)
print("glyphs ok")

# --- ink-spread transition mask: 0 at centre -> 1 at corners, with watercolour bleed noise (thresholded by shader) ---
N = 512
y, x = np.mgrid[0:N, 0:N].astype(np.float32)
r = np.hypot(x - N / 2, y - N / 2) / (N * .7071)
bleed = noise(N, 14) * .55 + noise(N, 4) * .3 + noise(N, 1.2) * .15
m = np.clip(r * .78 + (bleed - .5) * .32 + .06, 0, 1)
Image.fromarray((m * 255).astype("uint8"), "L").save(f"{out}/InkMask.png", optimize=True)
print("ink ok")

# --- XR notebook surfaces: plain watercolour paper (tint at use) + white gilt frame (hairline + corner stars), 9-slice 40px ---
N = 256
c = np.zeros((N, N, 4), np.float32)
mott = noise(N, 18) * .65 + noise(N, 5) * .25 + noise(N, 1.2) * .10
for i in range(3): c[..., i] = 1 - mott * .10
c[..., 3] = feather_alpha(N, 4, 7)
save(c, "PanelPlain")
c = np.zeros((N, N, 4), np.float32); c[..., :3] = 1
hairline(c, 14, (1, 1, 1), 1, 24); c[..., 3] = 0
a = np.zeros((N, N), np.float32); lo, hi = 14, N - 15
a[lo, lo + 24:hi - 23] = a[hi, lo + 24:hi - 23] = 1; a[lo + 24:hi - 23, lo] = a[lo + 24:hi - 23, hi] = 1
c[..., 3] = np.clip(np.asarray(Image.fromarray((a * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(.5)), np.float32) / 255 * 1.8, 0, 1)
for (sx, sy) in [(14, 14), (N - 15, 14), (14, N - 15), (N - 15, N - 15)]: star(c, sx, sy, 11, (1, 1, 1), 1)
c[..., :3] = 1; save(c, "GiltFrame")
print("xr ok")
