"""v044 NPC Chinese voice lines: edge-tts (Microsoft neural voices, non-commercial coursework use) -> ffmpeg loudnorm
-> mono 44.1 kHz Ogg Vorbis at -16 LUFS / -1.5 dBTP. Reads the single line table the game also reads, so subtitles
and audio stay one-to-one. Re-runs only lines whose text or voice settings changed (hash in manifest.json).
python Tools/Art/build_v044_voice.py [--force]"""
import asyncio, hashlib, json, subprocess, sys, tempfile
from pathlib import Path
import edge_tts
ROOT=Path(__file__).resolve().parents[2]
VOICE=ROOT/'Assets/Resources/FishingV044/Voice'; TABLE=VOICE/'lines.txt'
# id -> (voice, rate, pitch, who)
CAST={0:('zh-CN-XiaoyiNeural','+6%','+2Hz','铃音 · 风铃修缮师 · 活泼少女'),
      1:('zh-CN-XiaoxiaoNeural','-12%','-3Hz','青禾 · 潮林守护人 · 温和慢语'),
      2:('zh-CN-YunxiNeural','+10%','+0Hz','阿砾 · 沉船打捞员 · 爽朗青年'),
      3:('zh-CN-YunjianNeural','-8%','-4Hz','望星 · 星象记录者 · 沉稳学者'),
      4:('zh-TW-HsiaoChenNeural','+0%','+0Hz','汐禾 · 钓具店店员 · 亲切店员'),
      5:('zh-CN-YunyangNeural','-15%','-10Hz','林舟 · 老舵手 · 年长低沉'),
      6:('zh-CN-liaoning-XiaobeiNeural','+8%','-2Hz','潮商·阿汛 · 流动商人 · 爽朗（代选）')}
FFMPEG='ffmpeg'
def lines():
    for raw in TABLE.read_text(encoding='utf-8').splitlines():
        if not raw or raw.startswith('#'):continue
        id,key,text=raw.split('\t');id=int(id)
        if not key.startswith('opt_'):yield id,key,text.replace('\\n','\n')
async def one(sem,id,key,text,old,force,manifest):
    voice,rate,pitch,_=CAST[id];digest=hashlib.sha1(f'{voice}|{rate}|{pitch}|{text}'.encode()).hexdigest()[:16]
    out=VOICE/str(id)/(key+'.ogg');out.parent.mkdir(parents=True,exist_ok=True)
    manifest.append({'id':id,'key':key,'text':text,'voice':voice,'rate':rate,'pitch':pitch,'hash':digest,'file':str(out.relative_to(ROOT)).replace('\\','/')})
    if not force and out.exists() and old.get(f'{id}/{key}')==digest:return
    async with sem:
        with tempfile.TemporaryDirectory() as tmp:
            mp3=Path(tmp)/'a.mp3'
            for attempt in range(4):
                try:await edge_tts.Communicate(text,voice,rate=rate,pitch=pitch).save(str(mp3));break
                except Exception as e:
                    if attempt==3:raise
                    await asyncio.sleep(2+attempt*3)
            # 0.12 s lead-in silence keeps the first consonant from being clipped by Vorbis priming.
            # Two-pass loudnorm: single-pass undershoots on short lines by ~2-3 LU.
            probe=subprocess.run([FFMPEG,'-hide_banner','-i',str(mp3),'-af','loudnorm=I=-16:TP=-1.5:LRA=9:print_format=json','-f','null','-'],capture_output=True,text=True,check=True).stderr
            m=json.loads(probe[probe.rindex('{'):probe.rindex('}')+1])
            norm=f"loudnorm=I=-16:TP=-1.5:LRA=9:measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true"
            subprocess.run([FFMPEG,'-y','-loglevel','error','-i',str(mp3),'-af',f'adelay=120,{norm},aresample=44100',
                            '-ac','1','-c:a','libvorbis','-q:a','5',str(out)],check=True)
    print('voiced',id,key,flush=True)
async def main(force):
    path=VOICE/'manifest.json';old={}
    if path.exists():old={f"{m['id']}/{m['key']}":m['hash'] for m in json.loads(path.read_text(encoding='utf-8'))['lines']}
    sem=asyncio.Semaphore(4);manifest=[]
    await asyncio.gather(*[one(sem,id,key,text,old,force,manifest) for id,key,text in lines()])
    manifest.sort(key=lambda m:(m['id'],m['key']))
    keys={f"{m['id']}/{m['key']}" for m in manifest};assert len(keys)==len(manifest),'duplicate line key'
    for m in manifest:assert (ROOT/m['file']).stat().st_size>2000,m['file']
    path.write_text(json.dumps({'cast':{str(k):{'voice':v[0],'rate':v[1],'pitch':v[2],'who':v[3]} for k,v in CAST.items()},'lines':manifest},ensure_ascii=False,indent=1)+'\n',encoding='utf-8')
    page(manifest)
    print('V044_VOICE_DONE',len(manifest))
def page(manifest):
    # Listening page: per-character 1-5 rating saved in localStorage, exportable as JSON for feedback.
    import html
    out=ROOT/'Logs/V044/npc/voices.html';out.parent.mkdir(parents=True,exist_ok=True);rows=[]
    for id,(voice,rate,pitch,who) in CAST.items():
        rows.append(f'<section><h2>{html.escape(who)}</h2><p class=meta>{voice} · rate {rate} · pitch {pitch}</p>'
                    f'<p>评分：'+''.join(f'<label><input type=radio name=r{id} value={v}>{v}</label>' for v in range(1,6))+f' <input class=note id=n{id} placeholder="意见（可选）"></p><ol>')
        for m in [m for m in manifest if m['id']==id]:
            rows.append(f'<li><audio controls preload=none src="../../../{m["file"]}"></audio> <code>{m["key"]}</code> {html.escape(m["text"])}</li>')
        rows.append('</ol></section>')
    out.write_text('''<!doctype html><meta charset=utf-8><title>NPC 配音试听 v044</title>
<style>body{font:15px/1.6 "Noto Serif SC",serif;background:#13203a;color:#f7ebd0;max-width:1100px;margin:auto;padding:24px}
h2{color:#ecc882;border-bottom:1px solid #c49652}.meta{color:#b8c6d6}audio{height:28px;vertical-align:middle}li{margin:4px 0}code{color:#b3f0d4}
.note{width:420px}button{font-size:16px;padding:6px 16px}</style>
<h1>NPC 配音试听与打分</h1><p>音色由 npc 代理代选，请复核。每个角色打 1–5 分，可写意见；点“导出评分”复制 JSON 发给开发者。</p>'''+''.join(rows)+'''
<button onclick="let r={};document.querySelectorAll('section').forEach((s,i)=>{let id=s.querySelector('input[type=radio]').name.slice(1);let c=s.querySelector('input:checked');r[id]={score:c?+c.value:null,note:document.getElementById('n'+id).value}});localStorage.v044voices=JSON.stringify(r);navigator.clipboard.writeText(JSON.stringify(r,null,1));alert(JSON.stringify(r,null,1))">导出评分</button>
<script>try{let r=JSON.parse(localStorage.v044voices||'{}');for(let id in r){let v=r[id];if(v.score)document.querySelector(`input[name=r${id}][value="${v.score}"]`).checked=true;document.getElementById('n'+id).value=v.note||''}}catch(e){}</script>
''',encoding='utf-8')
if __name__=='__main__':asyncio.run(main('--force' in sys.argv))
