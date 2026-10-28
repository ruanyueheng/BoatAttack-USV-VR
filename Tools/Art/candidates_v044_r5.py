"""v0.44 round 5: CoinGain A + tinkle tail (3), PortalEnter new direction (4), BagOpen new direction (4).
AudioGen drafts (16 kHz, ArtSource/Audio/V044/2026-10-28-audiogen) are used only as the mid/low body; every candidate
gets a 44.1 kHz top layer (synth glass/stardust, Pixabay or Kenney) so the final asset is full-band.

python -I Tools/Art/candidates_v044_r5.py
"""
from pathlib import Path
import sys
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/Art'))
import synth_v044_audio as S
import candidates_v044_audio as C
import candidates_v044_r3 as R

SR = S.SR
AG = ROOT / 'ArtSource/Audio/V044/2026-10-28-audiogen'
R.OUT = ROOT / 'Logs/V044/audio/r5'
rng = np.random.default_rng(5)


def ag(name): return C.hp(C.decode(AG / (name + '.wav')), 150)  # drafts carry sub-bass wobble ("puff")  # ffmpeg resamples 16k -> 44.1k


def tinkle(sec=.35, count=6, lo=3800, hi=6500):
    """Settling coins: small inharmonic metal pings, spacing widening and level falling like coins coming to rest."""
    buf = np.zeros(int((sec + .15) * SR)); at = 0
    for i in range(count):
        f = rng.uniform(lo, hi); g = .5 * (1 - i / count) ** 1.5
        S.place(buf, S.partials(f, .12, [1, 1.53, 2.31], [.05, .03, .015], [1, .5, .3], .0008) * g, at)
        at += sec / count * (0.6 + i * .25)
    return buf


def stardust(sec):
    """Glassy rising shimmer for the top band: high glass partials climbing and swelling into the blackout."""
    buf = np.zeros(int((sec + .5) * SR))
    for i in range(10):
        m = 86 + i * 1.3 + rng.uniform(-.4, .4)
        S.place(buf, S.glass(S.hz(m), .5) * (.04 + .012 * i), sec * (.25 + .07 * i))
    return C.hp(buf, 2500)


def water_close(sec=.5):
    """Soft closing like slipping into calm water: low-passed splash with a bubble, no transient spike."""
    w = S.lowpass(R.ev(C.px('nature-water-splash-80537'), sec), 1800)
    return w * np.hanning(len(w)) ** .5


def air_inhale(sec):
    x = S.t(sec); rise = (x / sec) ** 2
    return S.band_noise(sec, 300, 5000) * rise * np.clip((sec - x) / .05, 0, 1)


def build():
    c = {}
    coinA = C.decode(R.OUT.parent / 'r4/CoinGain_A.ogg'); L = len(coinA) / SR
    c['CoinGain'] = {
        'T1': (C.mix((coinA, 1, 0), (tinkle(.3, 5), .5, L * .55)), 'A + 5 个细碎金属余响，间隔渐疏、0.3s 衰减（硬币落定）', 'Meowa + 合成'),
        'T2': (C.mix((coinA, 1, 0), (tinkle(.4, 7, 4500, 7500), .45, L * .5)), 'A + 7 个更亮更密的叮当，0.4s 衰减', 'Meowa + 合成'),
        'T3': (C.mix((coinA, 1, 0), (C.hp(R.pitch(R.ev(C.px('film-special-effects-coin-clink-149690'), .3), 1.25), 2500), .4, L * .55)),
               'A + 真实单枚硬币轻碰（升调高通）作尾音', 'Meowa + Pixabay'),
    }
    # Portal: soft air inhale (body) -> glassy stardust rising (top) -> water-like soft close (tail).
    c['PortalEnter'] = {
        'N1': (C.mix((ag('PortalWater_6'), 1, 0), (stardust(1.1), .6, 0), (water_close(), .35, 1.05)),
               '本地 AudioGen 草稿「上扬后像沉入平静水面」作主体 + 44.1k 玻璃星尘上扬 + 柔和水面闭合', 'AudioGen + 合成 + Pixabay'),
        'N2': (C.mix((ag('PortalWater_1'), 1, 0), (stardust(1.2), .5, 0)), '另一 AudioGen 草稿（更长的上扬）+ 较轻星尘', 'AudioGen + 合成'),
        'N3': (C.mix((ag('PortalAir_6'), 1, 0), (stardust(.9), .55, 0), (water_close(.4), .3, .85)), 'AudioGen「吸入气流+玻璃感」草稿 + 星尘 + 水面闭合', 'AudioGen + 合成 + Pixabay'),
        'N4': (C.mix((air_inhale(.9), .6, 0), (stardust(.9), 1, 0), (water_close(.5), .45, .82)), '纯 44.1k 分层：柔和气流吸入 + 玻璃星尘上扬 + 水面闭合（无 AI）', '合成 + Pixabay'),
    }
    buckle = C.hp(R.ev(C.kz('kenney_rpg-audio.zip', 'beltHandle2.ogg'), .15), 1800)
    rustle = R.ev(C.px('film-special-effects-clothes-rustle-6817'), .25, 2)
    c['BagOpen'] = {
        'N1': (C.mix((R.ev(ag('BagFlap_3'), .35), 1, 0), (buckle, .45, .08)), 'AudioGen「帆布包盖掀开」草稿 + Kenney 皮带扣轻响（44.1k 高频层）', 'AudioGen + Kenney'),
        'N2': (C.mix((R.ev(ag('BagFlap_1'), .35), 1, 0), (buckle, .35, .12)), '另一帆布掀开草稿 + 扣响稍晚', 'AudioGen + Kenney'),
        'N3': (C.mix((C.hp(R.ev(ag('BagNotebook_5'), .38), 120), 1, 0), (C.hp(S.paper(.12, 160), 3000), .3, .02)), '手账翻开式：AudioGen 布面手账轻摩擦 + 44.1k 纸面高频', 'AudioGen + 合成'),
        'N4': (C.mix((C.hp(rustle, 300), 1, 0), (buckle, .5, .1)), '真实录音：Pixabay 布料轻“嚓”+ Kenney 皮带扣（无 AI）', 'Pixabay + Kenney'),
    }
    return c


R.MAXLEN.update({'CoinGain': 1.0, 'PortalEnter': 1.6, 'BagOpen': .4})
R.LOUD.update({'CoinGain': -22, 'BagOpen': -24, 'PortalEnter': -22})
R.build = build

if __name__ == '__main__':
    R.main()
    (R.OUT / 'r3.json').replace(R.OUT / 'r5.json')
