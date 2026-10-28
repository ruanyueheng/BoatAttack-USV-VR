"""v0.44 round-4 candidates after the user's round-3 vote: PortalEnter B+ (2), UIBack C tweaks (2), new BagOpen (4),
coin-clink CoinGain (4). Output Logs/V044/audio/r4/<Cue>_<id>.ogg + sheets + r4.json (note/source).

python -I Tools/Art/candidates_v044_r4.py
"""
from pathlib import Path
import json
import sys
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/Art'))
import synth_v044_audio as S
import candidates_v044_audio as C
import candidates_v044_r3 as R

SR = S.SR
R3 = ROOT / 'Logs/V044/audio/r3'
WORLD = ROOT / 'Assets/Resources/FishingAudio/World'
R.OUT = ROOT / 'Logs/V044/audio/r4'


def star_arp(sec):
    """Rising celesta sparkle that ends exactly at `sec` (the blackout peak)."""
    buf = np.zeros(int((sec + .6) * SR)); notes = (79, 83, 86, 91, 95, 98)
    for i, m in enumerate(notes): S.place(buf, S.celesta(S.hz(m), .6) * (.12 + .03 * i), sec - .55 + i * .09)
    return buf


def inhale(sec):
    """Low air drawn in: band noise + sub glide swelling to the blackout, cut with a short fade."""
    x = S.t(sec); rise = (x / sec) ** 2
    air = S.band_noise(sec, 80, 700) * rise
    sub = np.sin(2 * np.pi * np.cumsum(70 + 50 * rise) / SR) * rise * .5
    return (air + sub) * np.clip((sec - x) / .04, 0, 1)


def build():
    c = {}
    base = C.decode(R3 / 'PortalEnter_B.ogg'); L = len(base) / SR
    c['PortalEnter'] = {
        'E1': (C.mix((base, 1, 0), (star_arp(L * .85), .9, 0)), 'B + 上行星芒细铃（钢片琴六音，结尾落在黑屏最深处），呼应光幕星点', '已选 B + 合成'),
        'E2': (C.mix((base, 1, 0), (inhale(L * .85), .7, 0), (star_arp(L * .85), .45, 0)), 'B + 低频吸气尾巴（吸入感）+ 较轻星芒', '已选 B + 合成'),
    }
    click = C.px('film-special-effects-ui-click-43196'); conf = C.decode(WORLD / 'UIConfirm.ogg')
    c['UIBack'] = {
        'T1': (R.pitch(S.lowpass(C.ev(click, .12) if hasattr(C, 'ev') else R.ev(click, .12), 6000), 1.12), 'C 微调：低通从 2.5k 放宽到 6k（提亮）、升约 2 个半音、尾音截到 0.12s', 'Pixabay'),
        'T2': (C.mix((R.pitch(S.lowpass(R.ev(click, .14), 4500), 1.06), 1, 0), (R.pitch(conf, .89)[:int(.12 * SR)], .25, 0)), 'C 微调：提亮到 4.5k、升 1 个半音，并叠极轻的确认 C 降调层，与确认音成一家', 'Pixabay + 确认 C 派生'),
    }
    close = C.decode(WORLD / 'BagClose.ogg'); zipper = C.px('film-special-effects-zipper-short-81723')
    click_k = C.kz('kenney_rpg-audio.zip', 'metalClick.ogg')
    c['BagOpen'] = {
        'A': (R.mw('BagOpen2_1'), 'Meowa：帆布拉开唰+小铜扣咔哒（0.5s）', 'Meowa'),
        'B': (R.mw('BagOpen2_3'), 'Meowa 同批另一变体', 'Meowa'),
        'C': (R.ev(C.px('film-special-effects-open-bag-96178'), .38), 'Pixabay「open bag」真实开包', 'Pixabay'),
        'D': (C.mix((R.pitch(close, 1.15), .9, 0), (C.hp(R.ev(zipper, .14), 1500), .35, 0), (C.hp(R.ev(click_k, .1), 2000), .5, .1)), '与关背包 D 成对：同素材升调更亮 + 极短拉开 + 铜扣咔哒', 'Kenney + Pixabay'),
    }
    bowl = C.px('film-special-effects-coins-drop-in-bowl-572433'); clinks = C.px('film-special-effects-coins-clinking-sound-410677')
    coin = lambda rank, src: C.hp(R.ev(src, .25, rank), 1200)
    c['CoinGain'] = {
        'A': (R.mw('CoinClink_1'), 'Meowa：三四枚硬币落在一起磕碰+滚动余音', 'Meowa'),
        'B': (R.mw('CoinClink_2'), 'Meowa 同批另一变体', 'Meowa'),
        'C': (R.ev(C.px('film-special-effects-clink-jingle-coins-399024'), .7), 'Pixabay「clink jingle coins」真实硬币叮当', 'Pixabay'),
        'D': (C.mix((coin(0, bowl), 1, 0), (coin(1, clinks), .8, .06), (coin(2, bowl), .7, .13), (coin(3, clinks), .5, .19)),
              '分层：4 枚硬币碰撞瞬态错开 60ms 落下 + 自然滚动余音（素材取自两段真实硬币录音）', 'Pixabay'),
    }
    return c


R.MAXLEN.update({'PortalEnter': 1.4, 'UIBack': .2, 'BagOpen': .4, 'CoinGain': .8})
R.LOUD.update({'CoinGain': -22})
R.build = build

if __name__ == '__main__':
    R.main()
    (R.OUT / 'r3.json').replace(R.OUT / 'r4.json')
