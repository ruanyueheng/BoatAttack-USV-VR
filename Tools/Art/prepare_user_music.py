"""Blender background Python: inspect user MP3s with aud, preserve originals,
write level-matched PCM masters and a streaming-music manifest. No uploads.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
import uuid
import wave
import aud
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Audio/UserMusic'
OUT = ROOT / 'Assets/Resources/FishingAudio/Music'
PREVIEW = ROOT / 'Logs/UserMusic'
FILES = [
    ('Sailing', 'Open Sea Drift & Voyage.mp3'),
    ('Fishing', 'Serene Waters Fishing Spot.mp3'),
    ('Home', "Fisherman's Hearth Island Home.mp3"),
    ('HarborShop', 'Harbor Tackle Shop BGM.mp3'),
    ('Exploration', 'Tropical Shoreline Discovery.mp3'),
    ('RealmWind', 'Breeze Corridor of Windmill Island.mp3'),
    ('RealmForest', 'Emerald Mangrove Garden.mp3'),
    ('RealmWreck', 'Echoes of the Sunken Hull.mp3'),
    ('RealmStars', 'Celestial Sanctuary of Star Trails.mp3'),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def wav(path, samples, rate):
    if not np.isfinite(samples).all():
        raise ValueError('Non-finite audio samples')
    pcm = np.rint(np.clip(samples, -.999, .999) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(samples.shape[1]); f.setsampwidth(2); f.setframerate(rate); f.writeframes(pcm.tobytes())


def meta(path, folder=False, audio=False):
    target = Path(str(path) + '.meta')
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'boatattack-user-music-20261022/' + path.name).hex
    if target.exists():
        prior = target.read_text(encoding='utf-8-sig')
        if 'guid: ' + guid not in prior:
            raise ValueError('Refusing to replace asset GUID: ' + str(target))
    if audio:
        body = '''AudioImporter:
  externalObjects: {}
  serializedVersion: 6
  defaultSettings:
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.85
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  preloadAudioData: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    else:
        importer = 'DefaultImporter' if folder else 'TextScriptImporter'
        body = ('folderAsset: yes\n' if folder else '') + importer + ''':
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + body, encoding='utf-8')


def main():
    for p in [SOURCE / 'Originals', OUT, PREVIEW]: p.mkdir(parents=True, exist_ok=True)
    tracks = []
    for zone, (key, filename) in enumerate(FILES):
        original = Path('E:/') / filename
        original_sha = sha(original)
        archived = SOURCE / 'Originals' / filename
        if archived.exists() and sha(archived) != original_sha:
            raise ValueError('An archived original already differs: ' + filename)
        shutil.copy2(original, archived)
        sound = aud.Sound.file(str(original))
        rate, channels = int(sound.specs[0]), int(sound.specs[1])
        samples = sound.data().astype(np.float64)
        if samples.ndim != 2 or samples.shape[1] != channels or len(samples) < rate * 5:
            raise ValueError('Invalid/too short BGM: ' + filename)
        if not np.isfinite(samples).all(): raise ValueError('Invalid samples: ' + filename)
        samples -= np.mean(samples, axis=0, keepdims=True)
        block = max(1, rate // 10)
        frame_count = len(samples) // block
        power = np.mean(samples[:frame_count * block].reshape(frame_count, block, channels) ** 2, axis=(1, 2))
        gate = max(1e-9, float(np.percentile(power, 90)) * .01)
        active = power[power > gate]
        if not len(active): raise ValueError('Silent BGM: ' + filename)
        rms = math.sqrt(float(np.mean(active)))
        peak = float(np.max(np.abs(samples)))
        gain = min(10 ** (-21 / 20) / rms, .86 / max(peak, 1e-9))
        samples *= gain
        master = OUT / (key + '.wav')
        wav(master, samples, rate); meta(master, audio=True)
        duration = len(samples) / rate
        nonquiet = np.flatnonzero(power > gate)
        loop_start = min(1, max(0, int(nonquiet[0]) * .1 - .05))
        tail = .65 if zone >= 5 else .18
        cross = 6.5 if zone >= 5 else 3.5
        track = {'zone': zone, 'key': key, 'title': Path(filename).stem, 'duration': duration,
                 'loopStart': loop_start, 'tailTrim': tail, 'loopCrossfade': cross,
                 'originalName': filename, 'sourceSha256': original_sha, 'masterSha256': sha(master),
                 'sampleRate': rate, 'channels': channels, 'normalizationGain': gain,
                 'sourcePeak': peak, 'activeRmsDb': 20*math.log10(rms),
                 'masterPeak': float(np.max(np.abs(samples))),
                 'masterActiveRmsDb': 20*math.log10(rms*gain)}
        tracks.append(track)
        if zone == 5:
            # Audible hand-off preview only; not an extra game track.
            end = int((duration-tail)*rate); n = int(cross*rate); intro = int(loop_start*rate)
            before = samples[max(0, end-int(12*rate)):end].copy()
            after = samples[intro:intro+int(12*rate)].copy()
            phase = np.linspace(0, math.pi*.5, n)[:, None]
            mixed = before[-n:]*np.cos(phase) + after[:n]*np.sin(phase)
            preview = np.concatenate([before[:-n], mixed, after[n:]])
            preview *= min(1, .95/max(float(np.max(np.abs(preview))), 1e-9))
            wav(PREVIEW / 'RealmWind-loop-preview.wav', preview, rate)
        if sha(original) != original_sha: raise ValueError('Original was modified: ' + filename)
        print('[USER-MUSIC]', key, round(duration, 3), 's,', channels, 'ch, gain', round(gain, 4), flush=True)
    manifest = {'version': 1, 'source': 'Nine original files provided by the user; never uploaded to an audio-generation service.',
                'normalization': 'Constant gain to gated active RMS -21 dBFS with peak headroom; not a LUFS measurement or dynamic compressor.',
                'tracks': tracks}
    text = json.dumps(manifest, indent=2, ensure_ascii=False) + '\n'
    (SOURCE / 'manifest.json').write_text(text, encoding='utf-8')
    (OUT / 'Manifest.json').write_text(text, encoding='utf-8')
    meta(OUT, folder=True); meta(OUT / 'Manifest.json')
    print('User music preparation PASS:', len(tracks), 'preserved originals, normalized streaming masters and loop metadata', flush=True)


if __name__ == '__main__': main()
