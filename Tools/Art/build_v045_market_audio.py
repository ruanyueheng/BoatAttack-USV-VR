"""Original market foley: periodic rigging/water bed and a short brass trade chime.

No downloaded samples or paid services. Run with Python + numpy from any directory.
"""
from pathlib import Path
import json
import uuid
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/FishingAudio/World"
SR = 44100


def save(name, samples, loop=False):
    samples -= samples.mean()
    samples *= .65 / max(np.abs(samples).max(), 1e-9)
    edge = int((.003 if loop else .006) * SR)
    samples[:edge] *= np.linspace(0, 1, edge)
    samples[-edge:] *= np.linspace(1, 0, edge)
    assert np.isfinite(samples).all() and np.abs(samples).max() < .71
    assert abs(samples[0] - samples[-1]) < .003
    assert np.sqrt(np.mean(samples ** 2)) > .02
    pcm = np.round(samples * 32767).astype("<i2")
    path = OUT / (name + ".wav")
    with wave.open(str(path), "wb") as f:
        f.setparams((1, 2, SR, 0, "NONE", "not compressed"))
        f.writeframes(pcm.tobytes())
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        template = (OUT / "SeaGlow.ogg.meta").read_text(encoding="utf-8")
        import re
        template = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, template, count=1)
        meta.write_text(template, encoding="utf-8")
    return {"cue": name, "seconds": len(samples) / SR,
            "rms_dbfs": float(20 * np.log10(np.sqrt(np.mean(samples ** 2)))),
            "peak_dbfs": float(20 * np.log10(np.abs(samples).max())),
            "seam_delta": float(abs(samples[0] - samples[-1]))}


def main():
    # Integer cycles over 16 seconds keep both waveform and slow envelopes periodic.
    t = np.arange(16 * SR) / SR
    rng = np.random.default_rng(45)
    water = np.zeros_like(t)
    for cycles in rng.integers(1800, 48000, 96):
        water += np.sin(2 * np.pi * cycles * t / 16 + rng.uniform(0, 2 * np.pi)) / 96
    water *= .6 + .4 * np.cos(2 * np.pi * t / 8)
    creak = .04 * np.sin(2 * np.pi * 73 * t + .8 * np.sin(2 * np.pi * t / 4))
    creak *= (.5 + .5 * np.cos(2 * np.pi * t / 8)) ** 8
    bed = water + creak
    # A close wooden catch and three short, inharmonic brass resonances.
    t = np.arange(int(.8 * SR)) / SR
    trade = .15 * rng.standard_normal(len(t)) * np.exp(-t / .008)
    for start, hz in ((.04, 880), (.13, 1108.73), (.22, 1318.51)):
        age = np.maximum(0, t - start)
        env = (t >= start) * np.minimum(1, age / .003) * np.exp(-age / .10)
        trade += env * (np.sin(2 * np.pi * hz * age) + .25 * np.sin(2 * np.pi * hz * 2.71 * age))
    report = [save("SeaMarketLoop", bed, True), save("SeaMarketTrade", trade)]
    log = ROOT / "Logs/V045"
    log.mkdir(parents=True, exist_ok=True)
    (log / "market-audio.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
