"""v0.44 redo candidates for jump / bag open-close / journal open-close (user disliked round 1).
Sources: Pixabay (Pixabay Content License) in ArtSource/Audio/V044/2026-10-28-pixabay and Kenney CC0 zips.
Writes Logs/V044/audio/candidates/<Cue>_<A-D>.ogg plus a waveform/spectrogram sheet per cue.

python -I Tools/Art/candidates_v044_audio.py
"""
from pathlib import Path
import subprocess
import zipfile
import io
import numpy as np
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/Art'))
import synth_v044_audio as S  # reuse mastering, self-check, sheet and synthesis helpers

PX = ROOT / 'ArtSource/Audio/V044/2026-10-28-pixabay'
DL = ROOT / 'ArtSource/Audio/Downloads'
OUT = ROOT / 'Logs/V044/audio/candidates'
SR = S.SR


def decode(data_or_path):
    args = ['ffmpeg', '-v', 'error', '-i', '-' if isinstance(data_or_path, bytes) else str(data_or_path), '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-']
    raw = subprocess.run(args, input=data_or_path if isinstance(data_or_path, bytes) else None, capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def px(slug): return decode(PX / (slug + '.mp3'))
def kz(pack, name):
    with zipfile.ZipFile(DL / pack) as z:
        member = next(m for m in z.namelist() if m.endswith('/' + name) or m == name)
        return decode(z.read(member))


def event(y, length, rank=0, pre=.012):
    """rank-th loudest transient (peaks >= .4 s apart), cut from just before its onset."""
    hop = int(.01 * SR); env = np.sqrt(np.convolve(y ** 2, np.ones(hop) / hop, 'same'))[::hop]
    order = np.argsort(env)[::-1]; peaks = []
    for i in order:
        if all(abs(i - p) > 40 for p in peaks): peaks.append(i)
        if len(peaks) > rank: break
    p = peaks[min(rank, len(peaks) - 1)]; start = p
    while start > 0 and env[start - 1] > env[p] * .08 and p - start < 12: start -= 1
    a = max(0, start * hop - int(pre * SR)); return y[a:a + int(length * SR)].copy()


def mix(*layers):
    n = max(len(l) for l, _, _ in layers); out = np.zeros(n)
    for l, gain, at in layers: S.place(out, l * gain, at)
    return out


def hp(y, cut):
    spec = np.fft.rfft(y); fr = np.fft.rfftfreq(len(y), 1 / SR); spec *= 1 / (1 + (cut / np.maximum(fr, 1)) ** 4); return np.fft.irfft(spec, len(y))


def norm(y): return y / (np.abs(y).max() + 1e-12)


def candidates():
    j, bo, bc, bk, bx = {}, {}, {}, {}, {}
    # Jump: light cloth flick + sole brush, no low "puff"
    j['A'] = mix((norm(event(px('film-special-effects-clothes-rustle-6817'), .3)), 1, 0), (norm(event(px('film-special-effects-leather-shoe-104816'), .2)), .45, .02))
    j['B'] = mix((norm(event(px('household-shirt-clothing-movement-6869'), .3, 1)), 1, 0), (norm(event(px('film-special-effects-grass-slidegrass-scuff-22708'), .2, 2)), .4, .015))
    j['C'] = mix((norm(event(kz('kenney_rpg-audio.zip', 'cloth2.ogg'), .28)), 1, 0), (norm(event(kz('kenney_impact-sounds.zip', 'footstep_wood_001.ogg'), .2)), .35, .01))
    x = S.t(.26); swish = S.band_noise(.26, 1800, 7000) * np.sin(np.pi * np.clip(x / .2, 0, 1)) ** 2
    j['D'] = mix((swish, 1, 0), (S.band_noise(.08, 2500, 9000) * S.env(int(.08 * SR), .002, .015), .5, .005))
    for k in j: j[k] = hp(j[k], 220)[:int(.38 * SR)]
    # Bag open: canvas/leather + small brass buckle, brighter
    leather = px('film-special-effects-bag-leather-67945'); buckle = px('film-special-effects-belt-buckle-103410'); inv = px('film-special-effects-inventory-open-close-3-540173')
    bo['A'] = hp(mix((norm(event(leather, .32, 0)), 1, 0), (norm(event(buckle, .25)), .55, .05)), 300)
    bo['B'] = hp(norm(inv[:int(len(inv) * .5)]), 200)[:int(.38 * SR)]
    bo['C'] = hp(mix((norm(event(kz('kenney_rpg-audio.zip', 'beltHandle1.ogg'), .3)), 1, 0), (norm(event(kz('kenney_rpg-audio.zip', 'clothBelt.ogg'), .3)), .5, .03)), 250)
    bo['D'] = hp(mix((norm(event(kz('kenney_rpg-audio.zip', 'handleSmallLeather.ogg'), .3)), 1, 0), (norm(event(kz('kenney_rpg-audio.zip', 'metalClick.ogg'), .15)), .4, .06), (S.bell(S.hz(98), .25), .12, .07)), 250)
    # Bag close: same family, darker and settling
    bc['A'] = S.lowpass(mix((norm(event(leather, .34, 2)), 1, 0), (norm(event(buckle, .2)), .35, .07)), 3500)
    bc['B'] = S.lowpass(norm(event(inv[int(len(inv) * .45):], .38)), 4500)
    bc['C'] = S.lowpass(norm(event(kz('kenney_rpg-audio.zip', 'dropLeather.ogg'), .34)), 4000)
    bc['D'] = S.lowpass(mix((norm(event(kz('kenney_rpg-audio.zip', 'cloth4.ogg'), .3)), .9, 0), (norm(event(kz('kenney_rpg-audio.zip', 'metalLatch.ogg'), .2)), .35, .1)), 3000)
    # Journal open: thick paper page turn (+ soft cover)
    bk['A'] = norm(event(px('film-special-effects-page-flip-1-178322'), .7))
    bk['B'] = norm(event(px('film-special-effects-one-page-book-flip-101928'), .7))
    bk['C'] = norm(event(px('film-special-effects-book-page-turn-104314'), .7, 1))
    bk['D'] = mix((norm(event(kz('kenney_rpg-audio.zip', 'bookOpen.ogg'), .3)), .8, 0), (norm(event(kz('kenney_rpg-audio.zip', 'bookFlip2.ogg'), .45)), 1, .12))
    # Journal close: leather cover meeting pages
    bx['A'] = norm(event(px('household-closing-book-88041'), .6))
    bx['B'] = norm(event(px('film-special-effects-book-closing-48311'), .6))
    bx['C'] = norm(event(px('household-closing-a-book-14771'), .6))
    bx['D'] = mix((norm(event(kz('kenney_rpg-audio.zip', 'bookFlip3.ogg'), .3)), .6, 0), (norm(event(kz('kenney_rpg-audio.zip', 'bookPlace1.ogg'), .35)), 1, .1))
    return {'JumpPush': j, 'BagOpen': bo, 'BagClose': bc, 'BookOpen': bk, 'BookClose': bx}


def main():
    OUT.mkdir(parents=True, exist_ok=True); report = []
    for cue, opts in candidates().items():
        items = []
        for letter, raw in sorted(opts.items()):
            raw = raw[:int((.8 if cue.startswith('Book') else .4) * SR)]
            loud = np.nonzero(np.abs(raw) > np.abs(raw).max() * .01)[0]; raw = raw[:min(len(raw), loud[-1] + int(.04 * SR))]  # drop dead tail
            y = S.master(raw, -24, -4, .03)
            key = f'{cue}_{letter}'; wav = OUT / (key + '.wav')
            import wave
            with wave.open(str(wav), 'wb') as w:
                w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
            subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(wav), '-c:a', 'libvorbis', '-q:a', '6', str(OUT / (key + '.ogg'))], check=True); wav.unlink()
            s = S.check(key, y); report.append(s); items.append((key, y, s))
        S.LOG = OUT; S.sheet(cue, items)
    for s in report: print('OK   ' if s['ok'] else 'CHECK', s['key'], s['seconds'], s['peak_db'], s['centroid_hz'])


if __name__ == '__main__':
    main()
