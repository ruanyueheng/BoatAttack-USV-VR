"""Bake the existing v041 synthesized score so story playback never synthesizes PCM on the game thread."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/FishingV042/Story"
RATE, SECONDS = 22050, 6.5
COMMON = [[164.81, 207.65, 246.94], [196, 246.94, 293.66], [220, 277.18, 329.63]]
INTRO = [[146.83, 220, 293.66], [130.81, 196, 261.63], [116.54, 174.61, 233.08],
         [146.83, 220, 277.18], [164.81, 246.94, 329.63], [146.83, 220, 293.66]]
FINALE = [[196, 246.94, 293.66], [220, 261.63, 329.63], [174.61, 220, 261.63], [196, 246.94, 293.66, 392]]


def bake():
    OUT.mkdir(parents=True, exist_ok=True)
    report = {}
    for name, shots, chords in [("Prologue", 6, INTRO), ("Chart", 3, COMMON),
                                *[(n, 2, COMMON) for n in ("Wind", "Forest", "Tide", "Star")],
                                ("Finale", 4, FINALE)]:
        pcm = np.zeros(round(RATE * SECONDS * shots), dtype=np.float64)
        for shot in range(shots):
            lo, hi = round(shot * SECONDS * RATE), round((shot + 1) * SECONDS * RATE)
            t = np.arange(lo, hi, dtype=np.float64) / RATE
            local = t - shot * SECONDS
            chord = chords[shot % len(chords)]
            value = sum(np.sin(t * hz * np.pi * 2 + n) * (.09 if n == 0 else .05)
                        * (1 + .25 * np.sin(t * .7 + n)) for n, hz in enumerate(chord))
            pluck = np.asarray(chord)[(local / .75).astype(int) % len(chord)]
            value += np.sin(t * pluck * 4 * np.pi) * np.exp(-(local % .75) * 5) * .04
            pcm[lo:hi] = value * np.clip(local / 1.4, 0, 1) * np.clip((SECONDS - local) / 1.6, 0, 1)
        peak = float(np.max(np.abs(pcm)))
        assert .05 < peak < .9 and np.isfinite(pcm).all(), (name, peak)
        file = OUT / (name + ".wav")
        with wave.open(str(file), "wb") as audio:
            audio.setparams((1, 2, RATE, len(pcm), "NONE", "not compressed"))
            audio.writeframes(np.rint(pcm * 32767).astype("<i2").tobytes())
        with wave.open(str(file), "rb") as audio:
            assert audio.getnframes() == len(pcm) and audio.getframerate() == RATE
        report[name] = {"seconds": len(pcm) / RATE, "peak": peak}
    print(json.dumps(report))


if __name__ == "__main__":
    bake()
