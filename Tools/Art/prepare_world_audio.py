"""Blender/aud: prepare only the Meowa outputs recorded from visible completed cards."""
from pathlib import Path
import hashlib
import json
import uuid
import wave
import aud
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Audio/MeowaWorld'
OUT = ROOT / 'Assets/Resources/FishingAudio/World'


def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def asset_meta(path, kind='audio'):
    target = Path(str(path) + '.meta')
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'boatattack-meowa-world/' + path.name).hex
    if target.exists() and ('guid: ' + guid) not in target.read_text(encoding='utf-8-sig'):
        raise ValueError('Refusing to replace a different GUID: ' + str(path))
    if kind == 'audio':
        body = '''AudioImporter:
  externalObjects: {}
  serializedVersion: 6
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    else:
        body = ('folderAsset: yes\n' if kind == 'folder' else '') + ('DefaultImporter' if kind == 'folder' else 'TextScriptImporter') + ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + body, encoding='utf-8')


def write_audio(key, data, rate):
    path = OUT / (key + '.wav')
    pcm = np.rint(np.clip(data, -.90, .90) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(rate); stream.writeframes(pcm.tobytes())
    asset_meta(path)
    return digest(path)


def main():
    source = json.loads((SOURCE / 'sources.json').read_text(encoding='utf-8-sig'))
    rows = source['records']
    if source.get('remaining') or len(rows) != 44 or len({r['key'] for r in rows}) != len(rows):
        raise ValueError('All 44 completed real source results are required; no placeholders')
    if any(not (SOURCE / 'Originals' / (r['key'] + '.mp3')).is_file() for r in rows):
        raise ValueError('Finish downloading originals before preparing runtime assets')
    OUT.mkdir(parents=True, exist_ok=True); asset_meta(OUT, 'folder')
    results = []; demo = []; water_tail = None
    short_ui = {'UIConfirm': .22, 'UIBack': .28, 'UIReject': .25, 'LanternSwitch': .35, 'TVSwitch': .40}
    for row in rows:
        key = row['key']; original = SOURCE / 'Originals' / (key + '.mp3')
        original_hash = digest(original); sound = aud.Sound.file(str(original)); rate = int(sound.specs[0])
        x = sound.data().astype(np.float64)
        if x.ndim != 2 or not np.isfinite(x).all() or len(x) < rate*.15:
            raise ValueError('Invalid source audio: ' + key)
        y = x.mean(axis=1); y -= y.mean(); original_seconds = len(y) / rate
        peak = float(np.max(np.abs(y)))
        if peak < .001: raise ValueError('Silent source: ' + key)
        block = max(1, rate // 1000); blocks = len(y) // block
        energy = np.mean(y[:blocks*block].reshape(blocks, block)**2, axis=1)
        start = 0; end = len(y)
        if not row['loop']:
            active = np.flatnonzero(energy > max(1e-10, float(energy.max()) * .0004))
            start = max(0, (int(active[0])-5)*block); end = min(len(y), (int(active[-1])+65)*block)
        y = y[start:end].copy()
        if key in short_ui:
            width = max(1, rate//100); strongest = int(np.argmax(np.mean(y[:len(y)//width*width].reshape(-1,width)**2,axis=1))) * width
            offset = max(0, strongest-int(rate*.012)); start += offset
            y = y[offset:offset+int(rate*short_ui[key])].copy()
        gated = energy[energy > max(1e-10, float(energy.max()) * .005)]
        target = -26 if row['loop'] else -21
        gain = min(10**(target/20) / max(float(np.sqrt(gated.mean())), 1e-9), (.50 if row['loop'] else .72) / max(float(np.max(np.abs(y))), 1e-9))
        y *= gain
        if row['loop']:
            n = min(int(rate*.12), len(y)//8); phase = np.linspace(0,np.pi/2,n)
            seam = y[-n:] * np.cos(phase) + y[:n] * np.sin(phase)
            y = np.concatenate((y[n:-n],seam))
        else:
            n = min(int(rate*.004),len(y)//8); tail = min(int(rate*.035),len(y)//8)
            y[:n] *= np.linspace(0,1,n); y[-tail:] *= np.linspace(1,0,tail)
        output_hash = write_audio(key,y,rate)
        results.append({**row,'sourceSha256':original_hash,'masterSha256':output_hash,'sourceSeconds':original_seconds,'seconds':len(y)/rate,'sampleRate':rate,'channels':1,'trimStartSeconds':start/rate,'normalizationGain':gain,'peak':float(np.max(np.abs(y)))})
        if key == 'WaterPourLoop': water_tail = (y[-int(rate*.18):].copy(),rate)
        if rate == 44100: demo.extend((y[:min(len(y),int(rate*1.5))],np.zeros(int(rate*.25))))
        if digest(original) != original_hash: raise ValueError('Original changed: ' + key)
        print('[WORLD AUDIO]',key,round(len(y)/rate,3),'seconds',flush=True)
    if water_tail is None: raise ValueError('Water source is required for the short finish cue')
    tail, rate = water_tail; tail *= np.sin(np.linspace(0,np.pi,len(tail)))**2
    results.append({'key':'WaterFinish','loop':False,'derivedFrom':'WaterPourLoop','seconds':len(tail)/rate,'sampleRate':rate,'channels':1,'masterSha256':write_audio('WaterFinish',tail,rate)})
    (SOURCE / 'manifest.json').write_text(json.dumps({**source,'preparation':'Real Meowa files only; mono PCM, level-matched with headroom, click-safe ends and circular loop seams; originals unchanged.','assets':results},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    notice = OUT / 'Attribution.txt'
    notice.write_text('World audio generated with the project owner\'s Meowa account.\nProject: '+source['projectUrl']+'\nOriginal files, prompts, observed URLs, costs and hashes are archived in ArtSource/Audio/MeowaWorld.\nUser music and local project files were not uploaded. Generated outputs are not declared CC0; retain provider/account licensing records.\nWaterFinish is edited from the WaterPourLoop source.\n',encoding='utf-8');asset_meta(notice,'text')
    if demo:
        preview = ROOT / 'Logs/Meowa/world-audio-preview.wav'
        preview.parent.mkdir(parents=True, exist_ok=True)
        with wave.open(str(preview),'wb') as stream:
            stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(44100);stream.writeframes(np.rint(np.clip(np.concatenate(demo),-.9,.9)*32767).astype('<i2').tobytes())
    print('WORLD AUDIO preparation PASS: 44 original downloads, 45 runtime clips',flush=True)


if __name__ == '__main__': main()
