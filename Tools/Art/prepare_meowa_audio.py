"""Blender background Python: normalize only real, downloaded Meowa output.
The input manifest is recorded from the rendered Meowa audio cards, not a guessed API.
"""
from pathlib import Path
import hashlib
import json
import uuid
import wave
import sys
import aud
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
INSPECTION = '--inspection' in sys.argv
SOURCE = ROOT / ('ArtSource/Audio/MeowaInspection' if INSPECTION else 'ArtSource/Audio/Meowa')
OUT = ROOT / 'Assets/Resources/FishingAudio/SFX'
PREVIEW = ROOT / 'Logs/Meowa'
CUES = (['PickaxeInspect', 'RifleInspect', 'FishInspect', 'RifleShot'] if INSPECTION else
        ['RodCast', 'FloatSplash', 'NetFishPlace', 'FarmTill', 'FarmPlant', 'FarmWater', 'FarmHarvest', 'ShopPurchase'])


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def write_wav(path, x, rate):
    pcm = np.rint(np.clip(x, -.98, .98) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(rate); f.writeframes(pcm.tobytes())


def audio_meta(path):
    target = Path(str(path) + '.meta')
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'boatattack-meowa-action-20261022/' + path.name).hex
    if target.exists() and ('guid: ' + guid) not in target.read_text():
        raise ValueError('Refusing to change audio GUID: ' + str(target))
    target.write_text(f'''fileFormatVersion: 2
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
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')


def main():
    manifest = json.loads((SOURCE / 'sources.json').read_text(encoding='utf-8-sig'))
    rows = manifest['variants']
    expected = {cue + str(n) for cue in CUES for n in [1, 2]}
    if len(rows) != len(expected) or {r['key'] for r in rows} != expected:
        raise ValueError('All real generated variants are required; no fallback assets are made')
    if any(not (SOURCE / 'Originals' / (key + '.mp3')).is_file() for key in expected):
        raise ValueError('Complete the original downloads before preparing any runtime files')
    OUT.mkdir(parents=True, exist_ok=True); PREVIEW.mkdir(parents=True, exist_ok=True)
    prepared = []; demo = []
    for row in rows:
        key = row['key']; original = SOURCE / 'Originals' / (key + '.mp3')
        original_sha = sha(original)
        sound = aud.Sound.file(str(original)); rate, channels = int(sound.specs[0]), int(sound.specs[1])
        x = sound.data().astype(np.float64)
        if x.ndim != 2 or not np.isfinite(x).all(): raise ValueError('Invalid generated audio: ' + key)
        x = np.mean(x, axis=1); x -= np.mean(x)
        peak = float(np.max(np.abs(x)))
        if len(x) < rate*.1 or peak < .001: raise ValueError('Silent or truncated generated audio: ' + key)
        block = max(1, rate // 1000)
        count = len(x) // block
        power = np.mean(x[:count * block].reshape(count, block)**2, axis=1)
        active = np.flatnonzero(power > max(1e-10, float(np.max(power)) * .0004))
        start = max(0, (int(active[0]) - 8) * block)
        end = min(len(x), (int(active[-1]) + 120) * block)
        y = x[start:end].copy()
        if key.startswith('FishInspect'):
            # One animation peak every ~0.785 s. Keep one real short burst, not a two-second bed
            # which would be cut off or overlap at the next peak. Preserve the downloaded original.
            event_block = max(1, rate // 100)
            event_count = len(y) // event_block
            event_power = np.mean(y[:event_count * event_block].reshape(event_count, event_block)**2, axis=1)
            event_start = max(0, int(np.argmax(event_power)) * event_block - int(rate * .035))
            y = y[event_start:min(len(y), event_start + int(rate * .65))].copy()
            start += event_start
        gated = power[power > max(1e-9, float(np.max(power))*.005)]
        rms = float(np.sqrt(np.mean(gated)))
        gain = min(10**(-16/20) / max(rms, 1e-9), .82 / max(float(np.max(np.abs(y))), 1e-9))
        y *= gain
        fade_in = min(int(rate*.003), len(y)//8)
        fade_out = min(int(rate*(.055 if key.startswith('FishInspect') else .025)), len(y)//8)
        if fade_in > 0: y[:fade_in] *= np.linspace(0, 1, fade_in)
        if fade_out > 0: y[-fade_out:] *= np.linspace(1, 0, fade_out)
        target = OUT / (key + '.wav'); write_wav(target, y, rate); audio_meta(target)
        prepared.append({**row, 'sourceSha256': original_sha, 'masterSha256': sha(target),
                         'sourceSeconds': len(x)/rate, 'seconds': len(y)/rate, 'sampleRate': rate,
                         'sourceChannels': channels, 'channels': 1, 'trimStartSeconds': start/rate,
                         'normalizationGain': gain, 'peak': float(np.max(np.abs(y)))})
        if rate == 44100: demo.extend([y, np.zeros(int(rate*.35))])
        if sha(original) != original_sha: raise ValueError('Original changed: ' + key)
        print('[MEOWA AUDIO]', key, round(len(y)/rate, 3), 's, peak', round(float(np.max(np.abs(y))), 3), flush=True)
    result = {**manifest, 'preparation': 'Decoded supplied Meowa MP3 outputs, mono, removed dead air, constant gain and short click-safe edge fades; no synthetic substitute.', 'variants': prepared}
    (SOURCE / 'manifest.json').write_text(json.dumps(result, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
    if demo: write_wav(PREVIEW / ('meowa-inspection-preview.wav' if INSPECTION else 'meowa-action-preview.wav'), np.concatenate(demo), 44100)
    notice = OUT.parent / ('MeowaInspectionAttribution.txt' if INSPECTION else 'MeowaAttribution.txt')
    notice.write_text('Additional action audio generated using the project owner\'s Meowa account.\nSource project: '+manifest['projectUrl']+'\n'+str(len(CUES))+' action categories, two generated variations each.\nPrompts, original MP3s, generation cost and hashes are retained in '+str(SOURCE.relative_to(ROOT)).replace('\\','/')+'.\nNo provided background music or local project files were uploaded to the generation service.\nThese generated outputs are not declared CC0; retain the account/provider licensing records for any distribution.\n', encoding='utf-8')
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'boatattack-meowa-action-20261022/notice'+('/inspection' if INSPECTION else '')).hex
    Path(str(notice)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
    print('Meowa audio preparation PASS:', len(prepared), 'real generated clips', flush=True)


if __name__ == '__main__': main()
