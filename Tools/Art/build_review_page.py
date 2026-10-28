"""Build Logs/V044/audio/review.html: the round-5 full audit (pending picks first, then every shipped sound by category).
python -I Tools/Art/build_review_page.py
"""
from pathlib import Path
import html
import json

ROOT = Path(__file__).resolve().parents[2]
FA = ROOT / 'Assets/Resources/FishingAudio'
OUT = ROOT / 'Logs/V044/audio/review.html'
REL = '../../../Assets/Resources/FishingAudio/'

CATS = [
 ('钓鱼', [
  ('SFX/RodCast*', '抛竿：甩出钓竿、鱼线飞出'), ('SFX/FloatSplash*', '浮漂飞完落水，进入等鱼'),
  ('World/FishBite', '鱼试探/咬钩时浮漂下沉的水声（与 BitePing 叠加）'), ('World/BitePing', '咬钩提示：浮漂被猛拽入水，玩家要立刻提竿'),
  ('World/FishHook', '提竿成功、鱼挂上钩'), ('World/LineTensionLoop', '遛鱼时鱼线张力循环（拉力越大越响越高）'),
  ('SFX/ReelInLoop', '收线时鱼轮转动（循环）'), ('SFX/ReelOutLoop', '鱼拉走放线时鱼轮转动（循环）'),
  ('World/FishSplash', '鱼跃出水面/再入水；鱼出水落地'), ('World/LineSnap', '鱼线断了'),
  ('World/RareShimmer', '稀有/新种/破纪录鱼出水；升级成功庆祝'), ('SFX/FishFlop*', '手里活鱼扑腾'),
  ('SFX/FishInspect*', '左手检视鱼'), ('SFX/NetFishPlace*', '把鱼放进抄网'), ('World/FishStore', '鱼放入鱼箱'),
  ('SFX/GrillSizzleLoop', '烤鱼滋滋声（循环）')]),
 ('农事 / 水井 / 生活', [
  ('SFX/FarmTill*', '翻土成功'), ('SFX/FarmPlant*', '播种成功'), ('SFX/FarmWater*', '浇水成功'), ('SFX/FarmHarvest*', '收获成功'),
  ('World/CanHandle', '拿起/放下水壶'), ('World/WaterPourLoop', '水壶倒水（循环）'), ('World/WaterFinish', '倒水结束'),
  ('World/WellBucket', '水井打水桶'), ('World/WellRopeLoop', '水井绳索卷动（循环）'), ('World/FruitShake', '摇果树掉果子'),
  ('World/CookSizzleLoop', '灶台烹饪（循环）'), ('World/CushionCloth', '睡觉时被褥接触'), ('World/LanternSwitch', '开关提灯'),
  ('World/TVSwitch', '开关电视'), ('World/TVLoop', '电视节目声（循环）')]),
 ('制作 / 锻造 / 储物', [
  ('World/WorkbenchAssemble', '工作台组装'), ('World/ForgeStrike', '锻造锤击'), ('World/FurnaceLoop', '熔炉燃烧（循环）'),
  ('World/HandcraftRub', '桌边手作摩擦'), ('World/CraftFinish', '制作完成'), ('World/StorageMove', '箱子存入/取出物品'),
  ('World/ChestOpen', '打开家里储物箱'), ('World/LootBag', '拾取袋子/战利品'), ('SFX/ShopPurchase*', '商店购买/升级成功'),
  ('World/CoinGain', '卖鱼得金币（待定，见最上方）')]),
 ('拾取 / 采集', [('SFX/SoftPickup*', '拾取软物品'), ('SFX/WoodPickup*', '拾取木头'), ('SFX/StonePickup*', '拾取石头'),
  ('SFX/StoneHit*', '镐子敲石头/采矿'), ('SFX/PickaxeInspect*', '检视镐子')]),
 ('狩猎 / 射击 / 动物', [('SFX/RifleShot', '开枪（主）'), ('SFX/RifleShot1', '开枪变体 1'), ('SFX/RifleShot2', '开枪变体 2'),
  ('SFX/RifleInspect*', '检视猎枪'), ('World/AnimalHit', '击中动物'), ('World/AnimalRustle', '草丛里动物窸窣'), ('World/SeagullCall', '海鸥叫')]),
 ('移动 / 碰撞', [('SFX/StepGrass*', '草地脚步'), ('SFX/StepSand*', '沙地脚步'), ('SFX/StepStone*', '石面脚步'), ('SFX/StepWood*', '木板脚步'),
  ('World/JumpPush', '起跳离地'), ('World/BodyLand', '落地（土地）'), ('World/ClimbGrip', '攀爬抓握'),
  ('World/WaterEntry', '跳进水里'), ('World/WaterStroke', '游泳划水'), ('World/ImpactWood', '撞到木头'), ('World/ImpactStone', '撞到石头'),
  ('World/ImpactMetal', '撞到金属'), ('World/BoatSlap', '船拍浪'), ('World/BoatScrapeLoop', '船擦岸/擦礁（循环）')]),
 ('界面 UI', [('World/UIConfirm', '确认/选中/成功'), ('World/UIBack', '返回/关闭'), ('World/UIReject', '拒绝：金币不够/无效'),
  ('World/UIPage', '日志、工具包切换标签'), ('World/UINotify', '顶部弹出提示消息'), ('World/BagOpen', '打开背包（待定，见最上方）'),
  ('World/BagClose', '关闭背包'), ('World/BookOpen', '打开日志'), ('World/BookClose', '合上日志')]),
 ('传送门 / 秘境', [('World/PortalEnter', '走进传送门、画面变黑（待定，见最上方）'), ('World/PortalPass', '穿过传送门落地，金色光环闪光'),
  ('World/MechanismClick', '普通机关/宝箱扣件/灌水失败'),
  *[(f'World/Realm{t}{a}', f'{n}：{d}') for t, n in (('Wind', '风之回廊'), ('Forest', '森光庭院'), ('Wreck', '沉潮机库'), ('Stars', '星轨圣所'))
    for a, d in (('Step', '机关一步/拾得回响贝'), ('Solve', '阶段完成/整关解开'), ('Echo', '回响重现'))]]),
 ('剧情短句', [('World/StingerDiscover', '海图、星章反转、终章开箱'), ('World/StingerTender', '“归来”、终章最后一镜'), ('World/StingerRelic', '每个“第 N 盏灯”')]),
 ('海上事件', [('World/SeaFogHorn', '远处港口雾笛'), ('World/SeaWhaleSpout', '鲸鱼喷水'), ('World/SeaGlow', '夜光海闪烁')]),
 ('环境 / 天气循环', [('World/RainLoop', '下雨（室内会变闷）'), ('World/ForestLoop', '森林环境'), ('World/WreckLoop', '沉船/机库环境'),
  ('World/WindCorridorLoop', '风之回廊风声'), ('World/StarsLoop', '星轨圣所环境')]),
 ('背景音乐（区域）', [(f'Music/{m}', d) for m, d in (('Sailing', '外海开船'), ('Fishing', '停船钓鱼'), ('Home', '家、菜园、标题菜单'),
  ('HarborShop', '港口与钓具店'), ('Exploration', '岸上各岛探索'), ('RealmWind', '风之回廊'), ('RealmForest', '森光庭院'),
  ('RealmWreck', '沉潮机库'), ('RealmStars', '星轨圣所'))]),
]


def files(pattern):
    folder, stem = pattern.split('/')
    names = sorted(p for p in (FA / folder).iterdir() if p.suffix.lower() in ('.wav', '.ogg') and
                   (p.stem == stem or (stem.endswith('*') and p.stem.startswith(stem[:-1]) and p.stem[len(stem) - 1:].isdigit())))
    return [f'{folder}/{p.name}' for p in names]


def audio(src, label=''):
    return f'<span style="display:inline-block;margin:2px 6px 2px 0"><small>{html.escape(label)}</small><br><audio controls preload="none" src="{src}"></audio></span>'


def main():
    used = set(); rows = []
    r5 = json.loads((ROOT / 'Logs/V044/audio/r5/r5.json').read_text(encoding='utf-8'))
    pend = '<h2>一、待定项（每项选一个）</h2><table cellpadding=6>'
    for cue, scene in (('CoinGain', '卖鱼得金币：在你选的 A 基础上加“叮当”尾音'), ('PortalEnter', '走进传送门、画面变黑：新方向（柔和吸入气流 + 玻璃星尘上扬 + 像落入水面般闭合）'),
                       ('BagOpen', '按 Tab 打开背包：新方向（帆布掀开+皮带扣 / 手账翻开式布面摩擦），与关背包 D 成对')):
        cand = sorted(k for k in r5 if k.startswith(cue + '_'))
        cur = files(f'World/{cue}')[0]
        pend += f'<tr><td style="max-width:280px"><b>{cue}</b><br>{scene}</td><td>{audio(REL + cur, "当前游戏内")}<br><label><input type=radio name="pick-{cue}" value="当前">保留当前</label></td>'
        for k in cand:
            m = r5[k]; L = k.split('_')[-1]
            pend += f'<td style="max-width:230px">{audio("r5/" + k + ".ogg", L)}<br><label><input type=radio name="pick-{cue}" value="{L}">选 {L}</label><br><small>{html.escape(m["note"])}（{m["source"]}）</small></td>'
        pend += f'<td><label><input type=radio name="pick-{cue}" value="都不好">都不好</label><br><input name="note-{cue}" placeholder="备注" size=14></td></tr>'
    pend += '</table>'
    body = '<h2>二、全部音效审查（每行：保留 / 需改进 + 备注）</h2>'
    for cat, items in CATS:
        body += f'<h3>{cat}</h3><table cellpadding=5 border=1 style="border-collapse:collapse;background:#fbf5e8">'
        for pattern, scene in items:
            fs = files(pattern); used.update(fs)
            if not fs: continue
            name = pattern.split('/')[1].rstrip('*'); key = 'audit-' + pattern
            players = ''.join(audio(REL + f, f.split('/')[1].split('.')[0] if len(fs) > 1 else '') for f in fs)
            body += (f'<tr><td style="width:170px"><b>{name}</b>{f"<br><small>{len(fs)} 个变体随机播放</small>" if len(fs) > 1 else ""}</td><td style="width:260px">{scene}</td><td>{players}</td>'
                     f'<td style="white-space:nowrap"><label><input type=radio name="{key}" value="保留">保留</label> <label><input type=radio name="{key}" value="需改进">需改进</label><br>'
                     f'<input name="note-{pattern}" placeholder="备注（哪里不好）" size=22></td></tr>')
        body += '</table>'
    missing = [f'{p.parent.name}/{p.name}' for d in ('World', 'SFX', 'Music') for p in (FA / d).iterdir()
               if p.suffix.lower() in ('.wav', '.ogg') and f'{d}/{p.name}' not in used]
    if missing: print('NOT LISTED:', missing)
    head = ('<meta charset=utf-8><title>V044 音效全量审查</title><body style="font:15px sans-serif;background:#f4ead6;padding:20px">'
            '<h1>V044 音效全量审查（第五轮）</h1><p>戴耳机听。先选最上方 3 个待定项，再逐行把每个音效标为“保留”或“需改进”，需改进请写一句原因。'
            '选择与备注自动保存在本浏览器；全部完成后点底部“导出结果”，把文本复制发回。</p>'
            '<p>NPC 配音另见 <a href="../npc/voices.html">NPC 配音试听页</a>。程序内合成的提示音（区域标题风铃、成功/传说提示音、远处雷声）不在文件里，未列出。</p>')
    foot = '''<div style="position:sticky;bottom:0;background:#e9dcc0;padding:10px;margin-top:20px"><button onclick="exportAll()">导出结果</button> <button onclick="if(confirm('清空全部选择与备注？')){localStorage.removeItem(K);location.reload()}">清空</button> <span id=saved></span><br><textarea id=out rows=8 style="width:95%" placeholder="点“导出结果”后复制这里的文字"></textarea></div>
<script>
const K='v044-review';const st=JSON.parse(localStorage.getItem(K)||'{}');const save=()=>{localStorage.setItem(K,JSON.stringify(st));document.getElementById('saved').textContent='已自动保存 '+Object.keys(st).length+' 项';};
document.querySelectorAll('input[type=radio]').forEach(r=>{if(st[r.name]===r.value)r.checked=true;r.addEventListener('change',()=>{st[r.name]=r.value;save();});});
document.querySelectorAll('input:not([type=radio])').forEach(t=>{if(st[t.name])t.value=st[t.name];t.addEventListener('input',()=>{st[t.name]=t.value;save();});});
function exportAll(){const lines=[];document.querySelectorAll('tr').forEach(tr=>{const r=tr.querySelector('input[type=radio]');if(!r)return;const n=r.name;const pick=st[n]||'（未选）';
 const note=tr.querySelector('input:not([type=radio])');const nv=note&&st[note.name]?' | 备注: '+st[note.name]:'';lines.push(n.replace(/^(pick|audit)-/,'')+' = '+pick+nv);});
 document.getElementById('out').value=lines.join('\\n');document.getElementById('out').select();}
</script>'''
    OUT.write_text(head + pend + body + foot, encoding='utf-8'); print('rows ok ->', OUT)


if __name__ == '__main__':
    main()
