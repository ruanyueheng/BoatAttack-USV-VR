# V0.44 音效设计与素材记录

更新：2026-10-28。调研原文与全部出处链接见 [AUDIO_V044_RESEARCH.md](AUDIO_V044_RESEARCH.md)；试听与投票页：`Logs/V044/audio/listen.html`（选择自动存入浏览器 localStorage，底部“导出结果”生成可复制文本）。

## 情景 → 声音设计原则

| 情景（游戏里这一刻） | 原则 | 主要参考 |
|---|---|---|
| 咬钩：浮漂被鱼猛拽入水，玩家要立刻提竿 | 三层：瞬态（入水“咚”）+主体（水花）+尾（气泡/上行提示音）；比环境高约 10 dB；3D 挂在浮漂上；与“轻碰浮漂”的声音区分开 | 动森 NH 盲玩分析：轻碰/咬钩/收线三种声音，[Audiokinetic 博客](https://www.audiokinetic.com/en/blog/animal-crossing-is-playable-by-the-totally-blind/) |
| 遛鱼：鱼线张力 | 连续循环“绕线轮棘轮+线摩擦”，张力驱动音高与音量；高张力叠警告；断线“啪” | 未找到可靠出处，经验原则 |
| 传送门进入/穿越 | 进入：上扬吸入（≤1.2 s）；落地：干脆起音+约 1.5 s 闪光尾，对齐光环；用星光/铃+海浪扣海岸主题 | 未找到专门出处，经验原则 |
| UI 确认/返回/拒绝/翻页/通知 | 同一音色家族；确认上行、返回下行、拒绝同音重复；80–250 ms；比玩法事件低 6–10 dB；翻页用真实纸声 | [Cozy UI SFX 合成开发日志](https://yetiancai.itch.io/cozy-ui-sfx-190/devlog/1730801/synthesizing-game-ui-sound-effects-with-code) |
| 起跳 | 短（80–150 ms），布料+鞋底轻擦，避免低频“噗”；落地声按地面材质区分（已有） | 旷野之息拟音做法（见调研文） |
| 打开背包 | 帆布/皮革拟音+极轻单音；开与关成对（开亮、关沉） | [Unpacking 拟音访谈](https://www.asoundeffect.com/unpacking-game-audio/) |
| 开宝箱 | 两段式：铺垫（吱呀/搭扣）→揭晓（轻铃），稀有度靠层数和音高 | 经验原则 |
| VR 空间化 | 世界音效单声道 3D；UI 才用 2D；避免长时间贴头声 | [Meta VR 音频混音](https://developer.oculus.com/design/audio-intro-mixing) |

## 当前状态

- **用户保留并已在游戏中**：RareShimmer、CoinGain、StingerDiscover/Tender/Relic、四秘境 12 条、关背包（候选 D）、开日志（候选 A）、合日志（候选 D）。CoinGain 另给一个升级候选 `CoinGain_U1`（上行连串细铃）。
- **第三轮待投票**：BitePing、LineTensionLoop、PortalEnter、PortalPass、ChestOpen、JumpPush、BagOpen、UIConfirm、UIBack、UIReject、UIPage、UINotify，每项 3–4 个候选 + 当前版。生成脚本 `Tools/Art/candidates_v044_r3.py`，候选及自检图在 `Logs/V044/audio/r3/`，每个候选的设计思路与来源在 `r3/r3.json`。
- UIBack/UIReject 候选由 UIConfirm 同音色降调派生，选定后两者应与 UIConfirm 选同一字母以保持家族一致。

## 素材来源与许可

| 来源 | 位置 | 许可 |
|---|---|---|
| Meowa（项目账户生成） | `ArtSource/Audio/V044/2026-10-28-meowa/`（mp3 原件 + meowa.json：提示词、URL、积分） | 账户生成；条款快照未存档，发行前需补 |
| Pixabay | `ArtSource/Audio/V044/2026-10-28-pixabay/`（sources.json：页面与 CDN 地址） | Pixabay Content License，免署名 |
| Kenney RPG Audio / Impact Sounds | `ArtSource/Audio/Downloads/*.zip`，用到的原件在 `V044/2026-10-27/Originals` | CC0 |
| 程序合成 | `Tools/Art/synth_v044_audio.py` | 本项目原创 |

Meowa 积分：本轮消耗 145（余 55），逐条记录在 `ArtSource/Audio/V044/credits.md`。

未使用：Sonniss GDC 包（整包过大，官网未查到分类单独下载）、Freesound（需登录且 CC-BY 需署名管理）、BBC Sound Effects（仅限非商业，发行前须替换）、Zapsplat/Mixkit。现有 Pixabay+Meowa+分层合成已覆盖每项 3–4 个候选；若用户对某项仍不满意，再按需去这些库定点找。

## 验证

- AUDIO-MIX、WORLD-AUDIO 原生专项在加入 Voice 类别与 76 条世界音效后均 PASS（`Logs/usv-validation-75f589963d8740339ec81a1a5a9a1e8d.log`、`Logs/usv-validation-86fe4754ecb44cb39d58a5618a92af4f.log`）。
- 每个新音效的波形+频谱自检图在 `Logs/V044/audio/*.png`、`candidates/*.png`、`r3/*.png`；自检项：无削波、峰值 ≤ -3 dBFS、无直流、首尾无爆音。循环候选的接缝用等功率交叉淡化处理。
- 未做：真人耳机/头显试听（由用户投票完成）。

## 第五轮代选（用户授权，待复核；旧版在 Logs/V044/audio/old/r5/，候选全在 review.html）

| 音效 | 代选 | 理由 |
|---|---|---|
| CoinGain | r5/T1（A + 5 个渐疏细碎叮当，0.3s） | 保留用户选的 A，尾音清脆不抢戏 |
| PortalEnter | r5/N4（纯 44.1k 分层：气流吸入+玻璃星尘上扬+水面闭合） | 全频不闷、无电子感；E1/E2 方向已否决，AI 草稿版高频受限 |
| BagOpen | r5/N4（真实布料“嚓”+皮带扣） | 真实拟音、短促明亮，与关背包 D 同属布/皮质家族 |

已代改：27 个老素材（脚步/采石/拾取）削波修复到 ≤-3 dBFS+2ms 淡入；16 个多变体音效组内响度对齐（备份 Logs/V044/audio/old/sfx/）。
未完成（按收尾要求停止）：Stable Audio 候选、SeaMarketLoop/SeaMarketTrade 未生成；review.html 其余 110 行未逐行代改。
