"""Technical audit of every shipped clip: peak, clipping, active RMS, edge clicks. python -I Tools/Art/audit_audio.py"""
from pathlib import Path
import json, subprocess
import numpy as np
ROOT = Path(__file__).resolve().parents[2]; SR = 44100
rows = []
for f in sorted((ROOT / 'Assets/Resources/FishingAudio').rglob('*')):
    if f.suffix.lower() not in ('.wav', '.ogg', '.mp3'): continue
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(f), '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'], capture_output=True).stdout
    y = np.frombuffer(raw, np.float32).astype(float)
    if len(y) == 0: rows.append(dict(file=str(f.relative_to(ROOT)), error='decode')); continue
    a = y[np.abs(y) > np.abs(y).max() * .05]; e = int(.003 * SR)
    rows.append(dict(file=f.relative_to(ROOT / 'Assets/Resources/FishingAudio').as_posix(), sec=round(len(y) / SR, 2),
        peak=round(20 * np.log10(np.abs(y).max() + 1e-9), 1), clip=int((np.abs(y) > .985).sum()),
        rms=round(20 * np.log10(np.sqrt((a ** 2).mean()) + 1e-9), 1), head=round(float(np.abs(y[:4]).max()), 3), tail=round(float(np.abs(y[-e:]).max()), 3)))
(ROOT / 'Logs/V044/audio/audit.json').write_text(json.dumps(rows, indent=1), encoding='utf-8')
for r in rows:
    flag = []
    if r.get('clip'): flag.append('CLIP')
    if r.get('head', 0) > .05: flag.append('HEAD-CLICK')
    if r.get('tail', 0) > .05 and 'Loop' not in r['file'] and 'Music' not in r['file']: flag.append('TAIL-CUT')
    if r.get('rms', -99) > -12: flag.append('LOUD')
    if r.get('rms', 0) < -32: flag.append('QUIET')
    if flag: print(r['file'], r.get('sec'), r.get('peak'), r.get('rms'), flag)
print(len(rows), 'clips')
