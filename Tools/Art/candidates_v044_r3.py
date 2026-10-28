"""v0.44 round-3 candidates for every sound the user did not keep (see Docs/AUDIO_V044.md).
Layers Meowa generations (project account, credit log in ArtSource/Audio/V044/2026-10-28-meowa/meowa.json),
Pixabay (Pixabay Content License), Kenney CC0 and procedural layers. Writes Logs/V044/audio/r3/<Cue>_<A-D>.ogg,
a waveform/spectrogram sheet per cue and r3.json (design note + source per candidate) for listen.html.

python -I Tools/Art/candidates_v044_r3.py
"""
from pathlib import Path
import json
import subprocess
import sys
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/Art'))
import synth_v044_audio as S
import candidates_v044_audio as C

MW = ROOT / 'ArtSource/Audio/V044/2026-10-28-meowa'
OUT = ROOT / 'Logs/V044/audio/r3'
SR = S.SR


def mw(name): return C.decode(MW / (name + '.mp3'))
def px(slug): return C.px(slug)
def n(y): return C.norm(y)
def ev(y, sec, rank=0): return n(C.event(y, sec, rank))


def pitch(y, ratio):
    """Resample-based pitch/time shift (tape style): ratio > 1 raises pitch and shortens."""
    idx = np.arange(0, len(y) - 1, ratio); return np.interp(idx, np.arange(len(y)), y)


def loopable(y, sec=2.0, xf=.25):
    """Equal-power crossfade the tail into the head so the clip wraps without a seam."""
    L = int(sec * SR); X = int(xf * SR); y = y[:L + X].copy()
    if len(y) < L + X: y = np.concatenate([y, np.zeros(L + X - len(y))])
    w = np.linspace(0, np.pi / 2, X); out = y[:L].copy()
    out[:X] = y[:X] * np.sin(w) + y[L:L + X] * np.cos(w); return out


def chime_up(f0=88, f1=95, gain=.3):
    buf = np.zeros(int(.6 * SR)); S.place(buf, S.bell(S.hz(f0), .45) * gain, 0); return S.place(buf, S.bell(S.hz(f1), .45) * gain * .8, .07)


def build():
    c = {}
    # 1 Bite: float yanked under + short tense alert (research: transient / body / tail + rising alert)
    splash = px('nature-water-splash-80537'); plop = px('film-special-effects-plopping-plop-popping-41836')
    c['BitePing'] = {
        'A': (ev(mw('BitePing_1'), .6), 'Meowa「清脆扑通」：入水咕咚+清脆尾', 'Meowa'),
        'B': (ev(mw('BitePing_3'), .6), 'Meowa「强劲水花」：拽入水更猛', 'Meowa'),
        'C': (C.mix((ev(plop, .25), 1, 0), (ev(splash, .45), .55, .02), (chime_up(84, 91, .25), 1, .08)), '分层：Pixabay 水下咕咚(瞬态)+小水花(主体)+双音上行细铃(紧张提示)', 'Pixabay + 合成'),
        'D': (C.mix((mw('BitePing_4'), 1, 0), (ev(splash, .4), .35, .0)), '分层：Meowa「干脆叮响」+Pixabay 水花垫底，提示感最强', 'Meowa + Pixabay'),
    }
    reel = px('technology-fly-fishing-reel-running-5-40946')
    c['LineTensionLoop'] = {
        'A': (loopable(mw('LineTensionLoop_1')), 'Meowa「紧绷钓线-平稳」，首尾交叉淡化成无缝 2s 循环', 'Meowa'),
        'B': (loopable(mw('LineTensionLoop_2')), 'Meowa「紧绷钓线-微风」，带风感', 'Meowa'),
        'C': (loopable(C.hp(reel[int(3 * SR):int(5.4 * SR)], 250)), 'Pixabay 真实飞蝇轮出线的棘轮声（取中段稳定部分）（研究建议：绕线轮咔嗒+线摩擦）', 'Pixabay'),
        'D': (loopable(C.mix((C.hp(reel[int(3 * SR):int(5.4 * SR)], 250), .7, 0), (mw('LineTensionLoop_1'), .6, 0))), '分层：真实棘轮(C)+Meowa 线鸣(A)', 'Pixabay + Meowa'),
    }
    warp = px('film-special-effects-warp-magic-2-382390'); whoosh = px('film-special-effects-magic-teleport-whoosh-352764')
    trans = px('film-special-effects-magical-transformation-sfx-short-449033')
    c['PortalEnter'] = {
        'A': (mw('PortalEnter_1')[:int(1.2 * SR)], 'Meowa「星光传送门-进入微光」，截 1.2s', 'Meowa'),
        'B': (mw('PortalEnter_2')[:int(1.2 * SR)], 'Meowa「海滨传送门-温暖吸入」，截 1.2s', 'Meowa'),
        'C': (C.hp(ev(warp, 1.0), 80), 'Pixabay warp magic：短促魔法吸入', 'Pixabay'),
        'D': (C.mix((ev(whoosh, 1.0), .8, 0), (S.cue_portal_enter(), .5, 0)), '分层：Pixabay 魔法 whoosh+原合成上扬滑音（保留海风感）', 'Pixabay + 合成'),
    }
    c['PortalPass'] = {
        'A': (mw('PortalPass_1')[:int(1.7 * SR)], 'Meowa「金色传送门落地鸣音」，对齐光环 1.6s', 'Meowa'),
        'B': (mw('PortalPass_2')[:int(1.7 * SR)], 'Meowa「星光传送辉光波」', 'Meowa'),
        'C': (ev(trans, 1.6), 'Pixabay magical transformation short', 'Pixabay'),
        'D': (C.mix((mw('PortalPass_2')[:int(1.7 * SR)], .8, 0), (ev(splash, .9), .25, .05)), '分层：Meowa 辉光波+轻海浪（扣海岸主题）', 'Meowa + Pixabay'),
    }
    latch = px('film-special-effects-wooden-trunk-latch-1-183944'); chest = px('household-chest-opening-87569')
    c['ChestOpen'] = {
        'A': (mw('ChestOpen_1'), 'Meowa「轻柔嘎吱」', 'Meowa'),
        'B': (mw('ChestOpen_2'), 'Meowa「轻巧搭扣」', 'Meowa'),
        'C': (C.mix((ev(latch, .3), 1, 0), (ev(chest, .8), .8, .12)), '分层：Pixabay 木箱搭扣(先)+木盖掀开(后)：铺垫→揭晓两段式', 'Pixabay'),
        'D': (C.mix((mw('ChestOpen_3'), 1, 0), (chime_up(91, 98, .18), 1, .25)), '分层：Meowa「柔和轻叩」+揭晓细铃', 'Meowa + 合成'),
    }
    jw = px('film-special-effects-whoosh-gaming-jump-243507')
    c['JumpPush'] = {
        'A': (mw('JumpPush_1'), 'Meowa「木质刮擦」：鞋底擦木板', 'Meowa'),
        'B': (mw('JumpPush_2'), 'Meowa「沙地沙沙声」', 'Meowa'),
        'C': (mw('JumpPush_4'), 'Meowa「清脆布料」', 'Meowa'),
        'D': (C.hp(ev(jw, .3), 400), 'Pixabay 轻 whoosh，高通去掉低频"噗"', 'Pixabay'),
    }
    buckle = px('film-special-effects-belt-buckle-103410')
    c['BagOpen'] = {
        'A': (mw('BagOpen_1')[:int(.45 * SR)], 'Meowa「帆布包打开铜按扣」', 'Meowa'),
        'B': (mw('BagOpen_2')[:int(.45 * SR)], 'Meowa「皮革帆布小包快速解扣」', 'Meowa'),
        'C': (C.mix((pitch(C.decode(OUT.parent / 'candidates/BagClose_D.ogg'), 1.12), .9, 0), (ev(buckle, .2), .5, .05)), '与已选"关背包 D"同一家族：同素材升调更亮+铜扣，开/关成对', 'Kenney + Pixabay'),
    }
    conf = [mw(f'UIConfirm_{i}') for i in (1, 2, 3, 4)]; click = px('film-special-effects-ui-click-43196')
    c['UIConfirm'] = {
        'A': (conf[0], 'Meowa「木铃」', 'Meowa'), 'B': (conf[2], 'Meowa「暖木微光」', 'Meowa'), 'C': (conf[3], 'Meowa「柔和铃击」', 'Meowa'),
        'D': (ev(px('film-special-effects-ui-wood-reward-398837'), .35), 'Pixabay UI wood', 'Pixabay'),
    }
    c['UIBack'] = {
        'A': (pitch(conf[0], .84), '与确认 A 同音色，降 3 个半音（返回=下行）', 'Meowa 派生'),
        'B': (pitch(conf[2], .84), '与确认 B 同音色，降 3 个半音', 'Meowa 派生'),
        'C': (S.lowpass(ev(click, .2), 2500), 'Pixabay 软点击，低通变闷', 'Pixabay'),
    }
    rej = lambda y: C.mix((S.lowpass(pitch(y, .75), 1800), 1, 0), (S.lowpass(pitch(y, .75), 1800), .8, .11))
    c['UIReject'] = {
        'A': (rej(conf[0]), '确认 A 同音色，降低+闷化，同音重复两次（拒绝=重复不上行）', 'Meowa 派生'),
        'B': (rej(conf[2]), '确认 B 同音色的拒绝版', 'Meowa 派生'),
        'C': (C.mix((S.lowpass(ev(click, .15), 1500), 1, 0), (S.lowpass(ev(click, .15), 1500), .8, .1)), 'Pixabay 点击闷化双击', 'Pixabay'),
    }
    c['UIPage'] = {
        'A': (mw('UIPage_1')[:int(.5 * SR)], 'Meowa「厚书翻页-温暖」，截 0.5s', 'Meowa'),
        'B': (mw('UIPage_2')[:int(.5 * SR)], 'Meowa「日记翻页-清脆」', 'Meowa'),
        'C': (C.decode(OUT.parent / 'candidates/BookOpen_A.ogg')[:int(.45 * SR)], '与已选"翻开日志 A"同素材的短版（翻页音色统一）', 'Pixabay'),
    }
    c['UINotify'] = {
        'A': (mw('UINotify_1')[:int(.8 * SR)], 'Meowa「故事书通知提示音」', 'Meowa'),
        'B': (mw('UINotify_2')[:int(.8 * SR)], 'Meowa「故事书通知铃声」', 'Meowa'),
        'C': (ev(px('film-special-effects-soft-notification-146623'), .8), 'Pixabay soft notification', 'Pixabay'),
        'D': (ev(px('film-special-effects-soft-notification-131438'), .8), 'Pixabay soft notification 2', 'Pixabay'),
    }
    # Upgrade candidate for a kept sound (research: coin cascade whose pitch climbs, ending on a bigger "ding").
    coins = C.decode(ROOT / 'Assets/Resources/FishingAudio/World/CoinGain.ogg'); casc = np.zeros(int(1.2 * SR))
    for i, m in enumerate((88, 91, 95, 100)): S.place(casc, S.bell(S.hz(m), .5) * (.18 + .05 * i), .05 + i * .07)
    c['CoinGain'] = {'U1': (C.mix((coins, .8, 0), (casc, 1, .02)), '升级候选：保留版硬币+四连上行细铃（金额越多越长的"计数"感）', 'Kenney + 合成')}
    return c


LOUD = {'UIConfirm': -26, 'UIBack': -27, 'UIReject': -27, 'UIPage': -26, 'UINotify': -26, 'JumpPush': -25, 'BagOpen': -24}
MAXLEN = {'JumpPush': .4, 'BagOpen': .45, 'UIConfirm': .35, 'UIBack': .35, 'UIReject': .45, 'UIPage': .5, 'UINotify': .8, 'BitePing': .8, 'ChestOpen': 1.0}


def main():
    OUT.mkdir(parents=True, exist_ok=True); meta = {}; bad = []
    for cue, opts in build().items():
        items = []
        for letter, (raw, note, src) in opts.items():
            loop = cue.endswith('Loop'); raw = raw[:int(MAXLEN.get(cue, 2.2) * SR)]
            if not loop:
                lead = np.nonzero(np.abs(raw) > np.abs(raw).max() * .02)[0]
                raw = raw[max(0, lead[0] - int(.004 * SR)):lead[-1] + int(.05 * SR)]
            y = S.master_loop(raw) if loop else S.master(raw, LOUD.get(cue, -22), -4, .03)
            key = f'{cue}_{letter}'; wav = OUT / (key + '.wav')
            with wave.open(str(wav), 'wb') as w:
                w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
            subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(wav), '-c:a', 'libvorbis', '-q:a', '6', str(OUT / (key + '.ogg'))], check=True); wav.unlink()
            s = S.check(key, y, loop); items.append((key, y, s)); meta[key] = dict(note=note, source=src, seconds=s['seconds'], ok=s['ok'])
            if not s['ok']: bad.append(s)
        S.LOG = OUT; S.sheet(cue, items)
    (OUT / 'r3.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding='utf-8')
    print(len(meta), 'candidates;', 'self-check issues:', bad)


if __name__ == '__main__':
    main()
