"""Stable Audio Open 1.0 (stabilityai, Stability AI Community License; non-commercial project use) batch SFX generator.
44.1 kHz stereo -> saved mono WAV.

usage: Tools/AudioGen/.venv/Scripts/python.exe Tools/AudioGen/sao.py <outDir> <seconds> <count> <key> "<prompt>" ["<negative>"]
Appends prompt/seed/model to <outDir>/sao.json.
"""
import json
import sys
import time
from pathlib import Path

import soundfile as sf
import torch
from diffusers import StableAudioPipeline

out, seconds, count, key, prompt = Path(sys.argv[1]), float(sys.argv[2]), int(sys.argv[3]), sys.argv[4], sys.argv[5]
negative = sys.argv[6] if len(sys.argv) > 6 else 'low quality, distorted, music, voice, speech, noise floor, hum'
out.mkdir(parents=True, exist_ok=True)
pipe = StableAudioPipeline.from_pretrained('stabilityai/stable-audio-open-1.0', torch_dtype=torch.float16).to('cuda')
seed = int(time.time()) % 100000
gen = torch.Generator('cuda').manual_seed(seed)
audio = pipe(prompt, negative_prompt=negative, num_inference_steps=100, audio_end_in_s=seconds,
             num_waveforms_per_prompt=count, generator=gen).audios
rate = pipe.vae.sampling_rate; files = []
for i, a in enumerate(audio):
    f = f'{key}_{i + 1}.wav'; sf.write(out / f, a.float().cpu().numpy().mean(0), rate); files.append(f)
log = out / 'sao.json'; rec = json.loads(log.read_text(encoding='utf-8')) if log.exists() else []
rec.append(dict(key=key, prompt=prompt, negative=negative, seconds=seconds, seed=seed, files=files,
                model='stabilityai/stable-audio-open-1.0', license='Stability AI Community License'))
log.write_text(json.dumps(rec, indent=1, ensure_ascii=False), encoding='utf-8'); print(files)
