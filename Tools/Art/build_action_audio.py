"""Reproducible licensed Foley selection and original reel/wet-fish sound design.

Run with E:/conda/python.exe. Archives are retained outside Unity; only selected
short clips go into Resources. No gameplay recording or proprietary game audio.
"""
from pathlib import Path, PurePosixPath
import hashlib
import json
import shutil
import subprocess
import uuid
import wave
import zipfile
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, resample_poly

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Audio"
OUT = ROOT / "Assets/Resources/FishingAudio/SFX"
CACHE = ROOT / "Logs/audio-source-cache"
RATE = 44100
records = []
sources = {
    "impact": {"file": "kenney_impact-sounds.zip", "creator": "Kenney", "license": "CC0-1.0", "url": "https://kenney.nl/assets/impact-sounds"},
    "rpg": {"file": "kenney_rpg-audio.zip", "creator": "Kenney", "license": "CC0-1.0", "url": "https://kenney.nl/assets/rpg-audio"},
    "steps": {"file": "Fantozzi-footsteps.7z", "creator": "Fantozzi / selection by qubodup", "license": "CC0-1.0", "url": "https://opengameart.org/content/fantozzis-footsteps-grasssand-stone"},
    "guns": {"file": "Tabasco-gunshots.zip", "creator": "Vincent Sevedge (Tabasco)", "license": "CC-BY-3.0 (archive notice retained; web listing also offers CC0)", "url": "https://opengameart.org/content/gunshot-sounds"},
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unpack(key):
    info = sources[key]
    archive = SOURCE / "Downloads" / info["file"]
    if not archive.is_file():
        raise FileNotFoundError(archive)
    info["sha256"] = sha(archive)
    destination = CACHE / key
    destination.mkdir(parents=True, exist_ok=True)
    if archive.suffix == ".zip":
        with zipfile.ZipFile(archive) as bundle:
            names = bundle.namelist()
    else:
        names = subprocess.check_output(["tar.exe", "-tf", str(archive)], text=True).splitlines()
    for name in names:
        part = PurePosixPath(name.replace("\\", "/"))
        if part.is_absolute() or ".." in part.parts or ":" in name:
            raise ValueError("Unsafe archive member: " + name)
    if archive.suffix == ".zip":
        with zipfile.ZipFile(archive) as bundle:
            bundle.extractall(destination)
    else:
        subprocess.run(["tar.exe", "-xf", str(archive), "-C", str(destination)], check=True)
    return destination


def meta(path):
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "boatattack-action-audio/" + path.name).hex
    text = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 6
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 1
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    target = path.with_name(path.name + ".meta")
    if target.exists() and target.read_text().split("guid: ")[1].splitlines()[0] != guid:
        raise ValueError("Refusing to change existing audio GUID: " + str(target))
    target.write_text(text, encoding="utf-8")


def copied(name, source, key):
    target = OUT / (name + source.suffix.lower())
    shutil.copyfile(source, target)
    meta(target)
    records.append({"name": name, "file": target.relative_to(ROOT).as_posix(), "source": key,
                    "member": source.relative_to(CACHE / key).as_posix(), "sha256": sha(target)})


def write(name, audio, description, source="original"):
    x = np.asarray(audio, dtype=np.float64)
    if x.ndim != 1 or not len(x) or not np.isfinite(x).all():
        raise ValueError("Invalid audio: " + name)
    x -= np.mean(x)
    peak = np.max(np.abs(x))
    if peak < 1e-5:
        raise ValueError("Silent audio: " + name)
    x *= .82 / peak
    target = OUT / (name + ".wav")
    pcm = np.rint(np.clip(x, -.95, .95) * 32767).astype("<i2")
    with wave.open(str(target), "wb") as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm.tobytes())
    meta(target)
    records.append({"name": name, "file": target.relative_to(ROOT).as_posix(), "source": source,
                    "design": description, "seconds": len(x) / RATE, "peak": float(np.max(np.abs(x))),
                    "rms": float(np.sqrt(np.mean(x*x))), "sha256": sha(target)})


def noise(length, seed, low, high):
    x = np.random.default_rng(seed).normal(0, 1, length)
    return sosfilt(butter(2, [low, high], btype="bandpass", fs=RATE, output="sos"), x)


def reel(name, clicks, seed):
    # One full second, integral number of ratchet teeth and continuous periodic friction.
    n = RATE
    x = np.zeros(n)
    for i in range(clicks):
        length = int(RATE * .026)
        t = np.arange(length) / RATE
        tap = noise(length, seed + i, 950 if clicks > 20 else 450, 9500) * np.exp(-t * 220)
        tap += .13 * np.sin(2*np.pi*2300*t) * np.exp(-t*300)
        at = int(i * n / clicks)
        np.add.at(x, (at + np.arange(length)) % n, tap)
    rng = np.random.default_rng(seed)
    bed = rng.normal(0, .025 if clicks > 20 else .012, n)
    # Circular highpass: no zero-state filter boundary and therefore no loop seam.
    bed -= np.roll(bed, 1) * .8
    x += bed
    x -= np.linspace(0, x[-1] - x[0], n)
    write(name, x, "Original periodic ratchet teeth, fine line friction; no game audio extraction")


def fish(name, seed):
    n = int(.42 * RATE); x = np.zeros(n)
    for offset, gain in [(0.025, 1), (.115, .58), (.228, .30)]:
        m = int(.16 * RATE); t = np.arange(m) / RATE
        thump = np.sin(2*np.pi*(105*t - 95*t*t)) * np.exp(-t*45)
        wet = noise(m, seed + int(offset*1000), 320, 6900)
        envelope = (1 - np.exp(-t*1400)) * np.exp(-t*31)
        hit = (thump*.30 + wet*.65) * envelope * gain
        at = int(offset * RATE); end = min(at+m, n); x[at:end] += hit[:end-at]
    x *= np.minimum(1, np.arange(n)/88) * np.minimum(1, np.arange(n)[::-1]/441)
    write(name, x, "Original soft wet tail/body pats and fin rustle, designed for a held live fish")


def rifle(path):
    rate, data = wavfile.read(path)
    if np.issubdtype(data.dtype, np.integer):
        data = data.astype(np.float64) / max(abs(np.iinfo(data.dtype).min), np.iinfo(data.dtype).max)
    else:
        data = data.astype(np.float64)
    if data.ndim > 1: data = np.mean(data, axis=1)
    if rate != RATE:
        from math import gcd
        divisor = gcd(rate, RATE); data = resample_poly(data, RATE//divisor, rate//divisor)
    data = sosfilt(butter(2, 100, btype="highpass", fs=RATE, output="sos"), data)
    width = int(.004*RATE)
    power = np.convolve(data*data, np.ones(width)/width, mode="same")
    first = np.flatnonzero(power > power.max()*.30)
    if not len(first): raise ValueError("Gun recording has no shot transient")
    start = max(0, int(first[0]) - int(.009*RATE))
    x = data[start:start + int(.80*RATE)].copy()
    # Suppress distant background without changing the dry attack.
    t = np.arange(len(x))/RATE
    x *= np.exp(-np.maximum(0, t-.17)*4)
    fade = min(882, len(x)//4)
    x[:44] *= np.linspace(0,1,44); x[-fade:] *= np.linspace(1,0,fade)
    write("RifleShot", x, "Tabasco SKS single shot, selected transient, mono, high-pass, background-tail attenuation", "guns")


def main():
    OUT.mkdir(parents=True, exist_ok=True); CACHE.mkdir(parents=True, exist_ok=True)
    paths = {key: unpack(key) for key in sources}
    sand = ["SandL1", "SandR1", "SandL2", "SandR2"]
    stone = ["StoneL1", "StoneR1", "StoneL2", "StoneR2"]
    for i in range(4):
        copied("StepSand"+str(i+1), paths["steps"] / ("Fantozzi-footsteps/ogg/Fantozzi-"+sand[i]+".ogg"), "steps")
        copied("StepStone"+str(i+1), paths["steps"] / ("Fantozzi-footsteps/ogg/Fantozzi-"+stone[i]+".ogg"), "steps")
        for surface in ["Grass", "Wood"]:
            copied("Step"+surface+str(i+1), paths["impact"] / f"Audio/footstep_{surface.lower()}_{i:03}.ogg", "impact")
    for i in range(3):
        copied("StonePickup"+str(i+1), paths["impact"] / f"Audio/impactGeneric_light_{i:03}.ogg", "impact")
        copied("StoneHit"+str(i+1), paths["impact"] / f"Audio/impactMining_{i:03}.ogg", "impact")
        copied("WoodPickup"+str(i+1), paths["impact"] / f"Audio/impactWood_light_{i:03}.ogg", "impact")
        copied("SoftPickup"+str(i+1), paths["rpg"] / f"Audio/cloth{i+1}.ogg", "rpg")
        fish("FishFlop"+str(i+1), 20261021+i*97)
    reel("ReelInLoop", 18, 901); reel("ReelOutLoop", 34, 1911)
    rifle(paths["guns"] / "sounds/sks.wav")
    manifest = {"version": 1, "sources": sources, "clips": records,
                "originalDesign": "Reel loops and held-fish pats are original generated Foley, not recordings from another game."}
    (SOURCE / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False)+"\n", encoding="utf-8")
    for folder in [OUT.parent, OUT]:
        target = folder.with_name(folder.name+".meta")
        if not target.exists():
            guid = uuid.uuid5(uuid.NAMESPACE_URL, "boatattack-action-audio/"+folder.name).hex
            target.write_text(f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
    notice = OUT.parent / "Attribution.txt"
    notice.write_text("Action audio credits\n\nKenney: Impact Sounds and RPG Audio, CC0 1.0.\nhttps://kenney.nl/assets/impact-sounds\nhttps://kenney.nl/assets/rpg-audio\n\nFantozzi (single-step selection by qubodup): Sand and Stone footsteps, CC0.\nhttps://opengameart.org/content/fantozzis-footsteps-grasssand-stone\n\nGunshot Sounds: Copyright (c) 2009 Vincent Sevedge (Tabasco).\nArchive notice: Creative Commons Attribution 3.0 Unported.\nhttps://creativecommons.org/licenses/by/3.0/\nhttps://opengameart.org/content/gunshot-sounds\nModified: one SKS transient selected, mono conversion, filtering, background attenuation and fades.\nThe listing also offers CC0; we retain the archive attribution as well.\n\nReel loops and fish pats: original sound design generated by Tools/Art/build_action_audio.py.\n", encoding="utf-8")
    print("Audio asset build PASS:", len(records), "clips; bytes:", sum((ROOT / r["file"]).stat().st_size for r in records))


if __name__ == "__main__":
    main()
