"""v0.44 designed signature cues: procedural modal/additive synthesis (original, no third-party samples
except the CC0 Kenney layers noted inline). Writes mono 44.1 kHz WAV to Resources and a waveform+spectrogram
contact sheet plus a numeric self-check (peak, clipping, edge clicks, DC) to Logs/V044/audio.

python -I Tools/Art/synth_v044_audio.py
"""
from pathlib import Path
import json
import subprocess
import uuid
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FishingAudio/World'
LOG = ROOT / 'Logs/V044/audio'
KENNEY = ROOT / 'ArtSource/Audio/V044/2026-10-27/Originals'
SR = 44100
rng = np.random.default_rng(44)


def t(sec): return np.arange(int(sec * SR)) / SR


def env(n, attack, decay):
    x = np.arange(n) / SR
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(x - attack, 0) / decay)


def partials(f, sec, ratios, decays, amps, attack=.002):
    x = t(sec); y = np.zeros_like(x)
    for r, d, a in zip(ratios, decays, amps):
        if f * r < SR / 2.2: y += a * np.sin(2 * np.pi * f * r * x + rng.uniform(0, 6.28)) * env(len(x), attack, d)
    tail = min(len(y), int(.06 * SR)); y[-tail:] *= np.linspace(1, 0, tail) ** 2  # no hard cut where a voice ends
    return y


def band_noise(sec, lo, hi):
    n = int(sec * SR); spec = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    spec[(fr < lo) | (fr > hi)] = 0
    y = np.fft.irfft(spec, n); return y / (np.abs(y).max() + 1e-9)


def lowpass(y, cutoff):
    spec = np.fft.rfft(y); fr = np.fft.rfftfreq(len(y), 1 / SR)
    spec *= 1 / (1 + (fr / cutoff) ** 4); return np.fft.irfft(spec, len(y))


def place(buf, y, at):
    i = int(at * SR); j = min(len(buf), i + len(y)); buf[i:j] += y[:j - i]; return buf


def hz(note): return 440 * 2 ** ((note - 69) / 12)


def kenney(name):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(KENNEY / name), '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


# ---- timbres -----------------------------------------------------------------------------------
def wood(f, sec=.35): return partials(f, sec, [1, 2.57, 4.1], [.06, .025, .012], [1, .35, .15], .0008)
def bell(f, sec=1.2): return partials(f, sec, [1, 2.0, 2.76, 5.4], [.5, .25, .18, .07], [1, .3, .35, .12])
def chime(f, sec=2.2): return partials(f, sec, [1, 2.76, 5.40, 8.93], [1.1, .6, .3, .15], [1, .45, .25, .1])
def marimba(f, sec=.9): return partials(f, sec, [1, 3.93, 9.24], [.32, .05, .012], [1, .3, .08], .001)
def celesta(f, sec=1.4): return partials(f, sec, [1, 2, 3, 4.2], [.55, .25, .1, .05], [1, .25, .08, .05], .0015)
def glass(f, sec=2.6): return partials(f, sec, [1, 2.32, 4.25, 6.63], [1.4, .8, .4, .2], [1, .35, .18, .08], .004)


def flute(f, sec, attack=.12):
    x = t(sec); vib = 1 + .004 * np.sin(2 * np.pi * 5.2 * x)
    tone = np.sin(2 * np.pi * np.cumsum(f * vib) / SR) + .15 * np.sin(4 * np.pi * np.cumsum(f * vib) / SR)
    breath = band_noise(sec, f * .8, f * 3) * .18
    shape = np.clip(x / attack, 0, 1) * np.clip((sec - x) / .25, 0, 1)
    return (tone + breath) * shape


def brass(f, sec, attack=.18):
    x = t(sec); ph = 2 * np.pi * f * x
    saw = sum(np.sin(k * ph) / k for k in range(1, 12) if f * k < 6000)
    shape = np.clip(x / attack, 0, 1) * np.clip((sec - x) / .35, 0, 1)
    return lowpass(saw * shape, 900) * .6


def bird(at_sec, sec=.16, f0=3200, f1=4600):
    x = t(sec); fr = f0 + (f1 - f0) * np.sin(np.pi * x / sec)
    return np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.sin(np.pi * x / sec) ** 2


def paper(sec=.18, density=90):
    n = int(sec * SR); crackle = np.zeros(n)
    for i in rng.integers(0, n, int(density * sec)): crackle[i:i + 60] += rng.uniform(.3, 1)
    return band_noise(sec, 1800, 7500) * lowpass(crackle, 400) * np.hanning(n)


def drone_underwater(f, sec):
    x = t(sec); y = np.sin(2 * np.pi * f * x) + .5 * np.sin(2 * np.pi * f * 1.5 * x) + .3 * np.sin(2 * np.pi * f * 2.01 * x)
    y *= 1 + .3 * np.sin(2 * np.pi * .7 * x)
    return lowpass(y, 380) * np.clip(x / .4, 0, 1) * np.clip((sec - x) / .6, 0, 1)


def bubbles(sec, count=6):
    buf = np.zeros(int(sec * SR))
    for _ in range(count):
        f = rng.uniform(500, 1100); d = .14; x = t(d)
        place(buf, np.sin(2 * np.pi * np.cumsum(f * (1 + 3 * x)) / SR) * np.exp(-x / .02) * (1 - np.exp(-x / .004)) * .5, rng.uniform(0, sec - d))
    return buf


def pad(notes, sec, bright=.3):
    x = t(sec); y = np.zeros_like(x)
    for m in notes:
        for det in (-.004, .004): y += np.sin(2 * np.pi * hz(m) * (1 + det) * x) + bright * np.sin(4 * np.pi * hz(m) * x)
    return lowpass(y, 2200) * np.clip(x / .6, 0, 1) * np.clip((sec - x) / 1.0, 0, 1)


def seq(voice, notes, gap, tail, vol=1.):
    buf = np.zeros(int((gap * len(notes) + tail) * SR))
    for i, m in enumerate(notes): place(buf, voice(hz(m)) * vol, i * gap)
    return buf


# ---- cues --------------------------------------------------------------------------------------
def cue_bite():
    buf = np.zeros(int(.55 * SR))
    place(buf, wood(2100, .25), 0)  # crisp painted-float "di"
    splash = band_noise(.35, 700, 5000) * env(int(.35 * SR), .004, .07) * np.linspace(1, 0, int(.35 * SR)) * .45
    return place(buf, splash, .015)


def cue_tension_loop():
    # Exactly 2 s with integer-cycle partials so the runtime loop seam is click-free; pitch/vol scale with tension at runtime.
    x = t(2.0); y = sum(np.sin(2 * np.pi * f * x) * a for f, a in [(140, 1), (280, .45), (421, .3), (843, .12)])
    grit = band_noise(2.0, 1200, 3600) * (.5 + .5 * np.sin(2 * np.pi * 6 * x) ** 2) * .22
    return (y * (1 + .15 * np.sin(2 * np.pi * 3 * x)) + grit) * .5


def cue_rare():
    buf = seq(celesta, [84, 88, 91, 96, 100], .07, 1.5)
    for _ in range(14): place(buf, bell(rng.uniform(5000, 8000), .25) * .15, rng.uniform(.05, 1.0))
    return buf


THEMES = {
    'Wind': (lambda f: chime(f) * .8, lambda s: band_noise(s, 600, 3000) * np.hanning(int(s * SR)) * .25, [76, 79, 81, 84], lambda: flute(hz(72), 2.8) * .5),
    'Forest': (lambda f: marimba(f), lambda s: place(np.zeros(int(s * SR)), bird(0) * .25, s * .5), [67, 71, 74, 79], lambda: place(seq(marimba, [55, 62, 67], .09, 2.3) * .6, bird(0, .2) * .3, .9)),
    'Wreck': (lambda f: bell(f / 2, 1.6) * .8, lambda s: bubbles(s), [55, 58, 62, 67], lambda: drone_underwater(55, 3.2) + place(np.zeros(int(3.2 * SR)), brass(hz(43), 2.6), .3)),
    'Stars': (lambda f: celesta(f), lambda s: glass(hz(96), s) * .25, [79, 83, 86, 91], lambda: seq(glass, [72, 79, 84, 88], .18, 2.6, .6)),
}


def realm(theme, action):
    voice, texture, scale, echo = THEMES[theme]
    if action == 'Step':
        buf = seq(voice, scale[:2], .09, 1.0)
        return buf + texture(len(buf) / SR) * .6
    if action == 'Solve':
        buf = seq(voice, scale + [scale[0] + 12], .11, 1.8)
        return buf + texture(len(buf) / SR)
    e = echo(); body = seq(voice, [scale[0] + 12, scale[2] + 12], .4, 1.6, .5)
    out = np.zeros(max(len(e), len(body))); out[:len(e)] += e; out[:len(body)] += body; return out


def cue_portal_enter():
    x = t(.8); swell = band_noise(.8, 300, 6000) * (x / .8) ** 2 * .7
    gliss = np.sin(2 * np.pi * np.cumsum(300 + 900 * (x / .8) ** 2) / SR) * (x / .8) ** 1.5 * .5
    return (swell + gliss) * np.clip((.8 - x) / .03, 0, 1)


def cue_portal_pass():
    buf = np.zeros(int(1.6 * SR))
    x = t(.5); place(buf, np.sin(2 * np.pi * np.cumsum(110 - 60 * x) / SR) * np.exp(-x / .15), 0)
    place(buf, chime(hz(88), 1.5) * .5, .02); place(buf, chime(hz(95), 1.4) * .35, .09)
    for _ in range(10): place(buf, bell(rng.uniform(4500, 7500), .2) * .12, rng.uniform(.03, .7))
    return buf


def cue_stinger(kind):
    if kind == 'Discover':
        return pad([60, 64, 67, 71], 3.4, .2) * .25 + place(np.zeros(int(3.4 * SR)), seq(celesta, [72, 76, 79, 83, 88], .16, 2), .1)
    if kind == 'Tender':
        pluck = lambda f: partials(f, 1.6, [1, 2, 3], [.7, .3, .15], [1, .4, .15], .003)
        return pad([53, 60, 64], 3.6, .1) * .25 + place(np.zeros(int(3.6 * SR)), seq(pluck, [65, 69, 72, 76, 77, 72], .22, 1.6, .8), .15)
    return pad([50, 57, 62, 64], 3.8, .15) * .25 + place(np.zeros(int(3.8 * SR)), seq(glass, [74, 76, 81, 86], .35, 2.3, .6), .2)


def cue_ui(kind):
    if kind == 'UIConfirm':  # wood tick + rising fine bell
        buf = np.zeros(int(.6 * SR)); place(buf, wood(1500, .2) * .8, 0); place(buf, bell(hz(91), .5) * .3, .03); return place(buf, bell(hz(96), .5) * .25, .09)
    if kind == 'UIBack':  # paper slide + lower soft wood
        buf = np.zeros(int(.45 * SR)); place(buf, paper(.16) * .5, 0); return place(buf, wood(900, .25) * .7, .07)
    if kind == 'UIReject':  # two dull knocks, no bell
        buf = np.zeros(int(.4 * SR)); place(buf, lowpass(wood(520, .2), 1500), 0); return place(buf, lowpass(wood(470, .2), 1500) * .8, .11)
    if kind == 'UIPage':
        buf = np.zeros(int(.4 * SR)); place(buf, paper(.22, 140) * .8, 0); return place(buf, paper(.12, 200), .17)
    if kind == 'UINotify':
        buf = np.zeros(int(.6 * SR)); place(buf, paper(.1) * .3, 0); place(buf, bell(hz(88), .45) * .5, .02); return place(buf, bell(hz(95), .45) * .4, .12)
    if kind == 'CoinGain':  # Kenney CC0 coins + bright counting bell
        coins = kenney('handleCoins.ogg'); buf = np.zeros(len(coins) + int(.6 * SR)); place(buf, coins * .7, 0)
        place(buf, bell(hz(93), .6) * .35, .06); return place(buf, bell(hz(100), .6) * .3, .16)
    raise KeyError(kind)


def cue_fog_horn():
    # Distant harbour diaphone: two detuned low reeds a fifth apart, swell/sustain/release, dull with two far echoes.
    sec = 3.2; x = t(sec); ph = lambda f: 2 * np.pi * f * x * (1 - .015 * np.exp(-x / .25))
    reed = sum(np.sin(k * ph(98)) / k for k in range(1, 9)) + .6 * sum(np.sin(k * ph(147)) / k for k in range(1, 6))
    shape = np.clip(x / .35, 0, 1) * np.clip((2.6 - x) / .7, 0, 1)
    dry = lowpass(reed * shape, 650); out = dry.copy()
    for delay, g in ((.31, .28), (.67, .14)): place(out, lowpass(dry, 420) * g, delay)
    return out[:len(x)]


def cue_whale_spout():
    buf = np.zeros(int(2.4 * SR))
    breath = band_noise(1.0, 90, 500) * np.sin(np.pi * np.clip(t(1.0) / 1.0, 0, 1)) ** 1.5  # deep exhale
    spray = band_noise(1.4, 1500, 9000) * env(int(1.4 * SR), .03, .35) * np.linspace(1, 0, int(1.4 * SR)) * .8                 # blow-hole hiss
    rain = np.zeros(int(1.6 * SR))
    for _ in range(40):  # droplets falling back
        f = rng.uniform(900, 2400); d = .05; xx = t(d)
        place(rain, np.sin(2 * np.pi * f * xx) * np.exp(-xx / .012) * (1 - np.exp(-xx / .002)) * rng.uniform(.1, .3), rng.uniform(0, 1.5))
    place(buf, breath, 0); place(buf, spray, .05); return place(buf, rain, .6)


def cue_sea_glow():
    buf = np.zeros(int(2.6 * SR))
    for _ in range(18):
        place(buf, celesta(hz(rng.choice([91, 93, 96, 98, 100, 103])), .7) * rng.uniform(.08, .25), rng.uniform(0, 1.8))
    return buf + glass(hz(84), 2.6) * .06


# ---- mastering, export, self-check -------------------------------------------------------------
def master(y, rms_db=-20.0, peak_db=-3.0, fade_out=.04):
    y = y - y.mean(); n = len(y)
    active = y[np.abs(y) > np.abs(y).max() * .05]
    y *= 10 ** (rms_db / 20) / (np.sqrt(np.mean(active ** 2)) + 1e-12)
    peak = np.abs(y).max(); cap = 10 ** (peak_db / 20)
    if peak > cap: y *= cap / peak
    fi, fo = int(.002 * SR), int(fade_out * SR)
    y[:fi] *= np.linspace(0, 1, fi); y[-fo:] *= np.linspace(1, 0, fo) ** 2
    return y


def write(key, y):
    path = LOG / 'wav' / (key + '.wav'); path.parent.mkdir(exist_ok=True)
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
    ogg = OUT / (key + '.ogg'); old = OUT / (key + '.wav')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(path), '-c:a', 'libvorbis', '-q:a', '6', str(ogg)], check=True)
    meta = Path(str(ogg) + '.meta')
    if old.exists():  # superseded recording: keep its GUID on the ogg so scene references survive
        old.unlink(); Path(str(old) + '.meta').replace(meta)
    if not meta.exists():  # new cues copy the world-audio importer settings
        template = (OUT / 'JumpPush.ogg.meta').read_text(encoding='utf-8-sig').splitlines()
        template[1] = 'guid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'boatattack-v044-synth/' + key).hex
        meta.write_text('\n'.join(template) + '\n', encoding='utf-8')


def check(key, y, loop=False):
    seg = int(.005 * SR)
    stats = dict(key=key, seconds=round(len(y) / SR, 3), peak_db=round(20 * np.log10(np.abs(y).max()), 2),
                 clipped=int(np.sum(np.abs(y) >= .999)), dc=round(float(y.mean()), 6),
                 start_abs=round(float(np.abs(y[:4]).max()), 4), end_abs=round(float(np.abs(y[-seg:]).max()), 4))
    spec = np.abs(np.fft.rfft(y)) ** 2; fr = np.fft.rfftfreq(len(y), 1 / SR)
    stats['centroid_hz'] = int((fr * spec).sum() / spec.sum())
    stats['seam_jump'] = round(float(abs(y[0] - y[-1])), 4) if loop else None
    stats['ok'] = stats['clipped'] == 0 and stats['peak_db'] <= -2.5 and abs(stats['dc']) < .002 and stats['start_abs'] < .05 \
        and (stats['seam_jump'] < .05 if loop else stats['end_abs'] < .02)
    return stats


def sheet(name, items):
    import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
    fig, axes = plt.subplots(2, len(items), figsize=(3.2 * len(items), 5), squeeze=False)
    for i, (key, y, s) in enumerate(items):
        x = np.arange(len(y)) / SR
        axes[0][i].plot(x, y, lw=.4); axes[0][i].set_ylim(-1, 1); axes[0][i].axhline(10 ** (-3 / 20), c='r', lw=.5)
        axes[0][i].set_title(f"{key}\npk {s['peak_db']} dB  {'OK' if s['ok'] else 'CHECK'}", fontsize=8)
        axes[1][i].specgram(y, NFFT=1024, Fs=SR, noverlap=768, cmap='magma'); axes[1][i].set_ylim(0, 12000)
        for a in axes[:, i]: a.tick_params(labelsize=6)
    fig.tight_layout(); fig.savefig(LOG / (name + '.png'), dpi=110); plt.close(fig)


def main():
    LOG.mkdir(parents=True, exist_ok=True)
    groups = {
        'fishing': {'BitePing': cue_bite(), 'LineTensionLoop': cue_tension_loop(), 'RareShimmer': cue_rare()},
        'realm': {f'Realm{th}{a}': realm(th, a) for th in THEMES for a in ('Step', 'Solve', 'Echo')},
        'portal_story': {'PortalEnter': cue_portal_enter(), 'PortalPass': cue_portal_pass(),
                         **{'Stinger' + k: cue_stinger(k) for k in ('Discover', 'Tender', 'Relic')}},
        'sea': {'SeaFogHorn': cue_fog_horn(), 'SeaWhaleSpout': cue_whale_spout(), 'SeaGlow': cue_sea_glow()},
        'ui': {k: cue_ui(k) for k in ('UIConfirm', 'UIBack', 'UIReject', 'UIPage', 'UINotify', 'CoinGain')},
    }
    report = []
    for g, cues in groups.items():
        items = []
        for key, raw in cues.items():
            loop = key.endswith('Loop')
            y = master(raw, -22 if key.startswith(('Stinger', 'Realm')) and key.endswith(('Echo', 'Discover', 'Tender', 'Relic')) else -20,
                       fade_out=0 if loop else .04) if not loop else master_loop(raw)
            write(key, y); s = check(key, y, loop); report.append(s); items.append((key, y, s))
        sheet(g, items)
    (LOG / 'selfcheck.json').write_text(json.dumps(report, indent=1), encoding='utf-8')
    for s in report: print(('OK   ' if s['ok'] else 'CHECK') , s)


def master_loop(y):
    y = y - y.mean(); y *= 10 ** (-22 / 20) / np.sqrt(np.mean(y ** 2))
    return y * min(1, 10 ** (-3 / 20) / np.abs(y).max())


if __name__ == '__main__':
    main()
