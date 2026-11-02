"""Blender/aud: master downloaded V059 Meowa clips; never change archived originals.

Run with Blender --background --python Tools/Art/prepare_v059_audio.py.
The sources file records only completed, visibly verified website outputs.
"""
from pathlib import Path
import hashlib
import json
import uuid
import wave
import aud
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Audio/MeowaWorld'
OUTPUT = ROOT / 'Assets/Resources/FishingAudio/World'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources = json.loads((SOURCE / 'v059-sources.json').read_text(encoding='utf-8-sig'))
    records = sources['records']
    expected = {'SawCut', 'RabbitCall', 'FoxCall', 'TortoiseCall', 'SeaTurtleCall'}
    if {row['key'] for row in records} != expected or len(records) != len(expected):
        raise ValueError('Exactly five verified source downloads are required')
    results = []
    for row in records:
        key = row['key']
        original = SOURCE / 'Originals' / (key + '.mp3')
        before = sha(original)
        if row.get('downloadSha256') != before or row.get('downloadBytes') != original.stat().st_size:
            raise ValueError('Archived download does not match its verified provenance: ' + key)
        sound = aud.Sound.file(str(original))
        rate = int(sound.specs[0])
        decoded = sound.data().astype(np.float64)
        if decoded.ndim != 2 or not len(decoded) or not np.isfinite(decoded).all():
            raise ValueError('Invalid decoded audio: ' + key)
        samples = decoded.mean(axis=1)
        samples -= samples.mean()
        block = max(1, rate // 1000)
        energy = np.mean(samples[:len(samples)//block*block].reshape(-1, block)**2, axis=1)
        active = np.flatnonzero(energy > max(1e-10, float(energy.max()) * .002))
        if len(active) == 0:
            raise ValueError('Silent source: ' + key)
        start = max(0, (int(active[0])-5)*block)
        end = min(len(samples), (int(active[-1])+45)*block)
        if key == 'SawCut':
            # One short push/pull contact per committed stroke; avoid stacking 2-second clips.
            width = min(len(energy), 650)
            strongest = int(np.argmax(np.convolve(energy, np.ones(width), mode='valid')))
            start = strongest * block
            end = min(len(samples), start + int(rate*.65))
        data = samples[start:end].copy()
        rms = float(np.sqrt(np.mean(data**2)))
        target = .16 if key == 'SawCut' else .12
        gain = min(target/max(rms, 1e-9), .82/max(float(np.max(np.abs(data))), 1e-9))
        data *= gain
        fade = min(int(rate*.008), len(data)//8)
        data[:fade] *= np.linspace(0, 1, fade)
        tail = min(int(rate*.035), len(data)//8)
        data[-tail:] *= np.linspace(1, 0, tail)
        output = OUTPUT / (key + '.wav')
        with wave.open(str(output), 'wb') as stream:
            stream.setnchannels(1)
            stream.setsampwidth(2)
            stream.setframerate(rate)
            stream.writeframes(np.rint(np.clip(data, -.9, .9)*32767).astype('<i2').tobytes())
        # Preserve an existing import GUID on repeat mastering.
        meta = Path(str(output) + '.meta')
        if not meta.exists():
            template = (OUTPUT / 'ForgeStrike.wav.meta').read_text(encoding='utf-8-sig')
            guid = uuid.uuid5(uuid.NAMESPACE_URL, 'boatattack-meowa-v059/' + key).hex
            template = template.replace(template.split('guid: ')[1].splitlines()[0], guid)
            meta.write_text(template, encoding='utf-8')
        if sha(original) != before:
            raise ValueError('Archived source changed')
        results.append(dict(row, sourceSha256=before, masterSha256=sha(output),
                            sourceSampleRate=rate, sourceChannels=decoded.shape[1], sourceSeconds=len(decoded)/rate,
                            sampleRate=rate, channels=1, seconds=len(data)/rate,
                            trimStartSeconds=start/rate, gain=gain,
                            peak=float(np.max(np.abs(data))), rms=float(np.sqrt(np.mean(data**2)))))
        print('[V059 AUDIO]', key, results[-1]['seconds'], flush=True)
    manifest = dict(sources, creditsSpent=sum(row['cost'] for row in records),
                    preparation='Blender aud decode to mono PCM16 with contact-length saw edit, level matching and click-safe ends; original download hashes verified and originals unchanged.', assets=results)
    (SOURCE / 'v059-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


if __name__ == '__main__':
    main()
