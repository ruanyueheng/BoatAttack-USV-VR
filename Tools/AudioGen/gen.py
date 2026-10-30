"""Local AudioGen (facebook/audiogen-medium, CC-BY-NC 4.0 weights; non-commercial project use) batch SFX generator.

usage: Tools/AudioGen/.venv/Scripts/python.exe Tools/AudioGen/gen.py <outDir> <seconds> <count> <key> "<english prompt>"
Writes <key>_<n>.wav (16 kHz mono, model native rate) plus appends prompt/seed to <outDir>/audiogen.json.
"""
import json
import os
import sys
import time
from pathlib import Path

os.environ.setdefault('HF_HUB_DISABLE_TELEMETRY', '1')
import torch
from audiocraft.models import AudioGen
import soundfile as sf

out, seconds, count, key, prompt = Path(sys.argv[1]), float(sys.argv[2]), int(sys.argv[3]), sys.argv[4], sys.argv[5]
out.mkdir(parents=True, exist_ok=True)
seed = int(time.time()) % 100000; torch.manual_seed(seed)
model = AudioGen.get_pretrained('facebook/audiogen-medium', device='cuda')
model.set_generation_params(duration=seconds, top_k=250, cfg_coef=3.5)
wav = model.generate([prompt] * count).cpu().numpy()
files = []
for i, w in enumerate(wav):
    f = f'{key}_{i + 1}.wav'; sf.write(out / f, w[0], model.sample_rate); files.append(f)
log = out / 'audiogen.json'; rec = json.loads(log.read_text(encoding='utf-8')) if log.exists() else []
rec.append(dict(key=key, prompt=prompt, seconds=seconds, seed=seed, files=files, model='facebook/audiogen-medium', license='CC-BY-NC-4.0 (weights)'))
log.write_text(json.dumps(rec, indent=1, ensure_ascii=False), encoding='utf-8'); print(files)
