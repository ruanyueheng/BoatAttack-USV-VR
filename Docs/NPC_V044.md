# NPC 设计 V044 —— 居民的“生活感”

> 作者：Claude（npc 代理），2026-10-28。代码：`FishingResidentBrain.cs`（行为）、`FishingResidents.cs`（对话）、`FishingResidentLines.cs`（台词表）。
> 资源：`Assets/Resources/FishingV044/Residents`（模型+动作）、`Assets/Resources/FishingV044/Voice`（台词表 `lines.txt` + 配音）。
> 验收：`Tools/Run-UnityValidation.ps1 -Suite FishingV044Npc`，证据在 `Logs/V044/npc/`。

## 1. 调研结论（参考其他游戏）
| 做法 | 来源 | 本作落地 |
|---|---|---|
| 日程 = 地点 + 朝向 + 动作，按天气、剧情进度切换 | 星露谷物语日程数据：每条记录是格子、朝向和要播放的动画（[Schedule data](https://stardewvalleywiki.com/Modding:Schedule_data)、[Schedule Viewer](https://www.nexusmods.com/stardewvalley/mods/19305)） | 每位居民在原地循环做“身份工作”，台词随章节变化（暂不让 NPC 走动，避免穿模和卡住） |
| 居民有自己的事，与玩家无关时也在“生活” | 动物森友会里村民会自己互动、写信（[Wikipedia](https://en.wikipedia.org/wiki/Animal_Crossing_(video_game))） | 工作循环之间随机插入小动作：叉手、喝水、点头 |
| 头部注视有角度限制，超出范围就回正，不必转身 | GordianXI 约 ±45°（[issue](https://github.com/jimmy58663/GordianXI/issues/402)）；NVForever 人物最多偏转 60–80°、上 30° 下 25°、颈部占 0.4、0.25s 临界阻尼（[PR](https://github.com/LootJunkieAzza/NVForever/pull/132)）；UE5 IK 指南认为目标不重要时应放弃注视，眼/头/颈/脊按比例分担转角（[StraySpark](https://www.strayspark.studio/blog/procedural-animation-ik-ue57-guide)）；Unity 论坛：把头当成以身体朝向为零点的炮塔（[讨论](https://discussions.unity.com/t/clamping-rotation-ai-turning-their-heads-toward-player/828433)） | 头部偏转 ±70°，抬头 20°、低头 25°；颈 35% / 头 65%；0.22s 平滑；注意到玩家前先延迟 0.4s；在身后 110° 以外不看 |
| 只有对话时才转身 | 同上；OpenMW 里打招呼时转向玩家（[issue](https://gitlab.com/OpenMW/openmw/-/issues/5065)） | 对话开始 0.25s 后（头已经先转过去）身体平滑转向玩家；结束后停 1.6s，再转回原朝向，继续原来的工作 |
| 玩家靠近时偶尔说一句闲聊（bark） | 原神：大部分 NPC 在玩家附近时会随机播放带配音的闲聊（[Fandom](https://genshin-impact.fandom.com/wiki/NPC)）；Valve 的动态对话（bark）会结合上下文、记忆和冷却（[GDC 2012 Ruskin](https://www.gdcvault.com/play/1015528/AI-driven-Dynamic-Dialog-through)、[slides](https://cdn.akamai.steamstatic.com/apps/valve/2012/GDC2012_Ruskin_Elan_DynamicDialog.pdf)） | 9m 内每人 25–45s 冷却，全局最少间隔 6s；台词按章节解锁；头顶有夜航玻璃气泡，并配语音 |

## 2. 行为状态机（FishingResidentBrain）
- **工作**：每人有一组工作动作（铃音：修理、挂铃；青禾：浇水、播种、收获；阿砾：修理、搬拿、开箱；望星：提灯观星、叉手；林舟：叉手望港）。循环动作随机持续 N 秒；单次动作播完一遍。约 30% 的概率插入小动作。用 PlayableGraph 做 0.35–0.4s 交叉淡入淡出。
- **注视**：只转头和颈，身体朝向不变（验收里会检查）。修理、浇水这类低头干活的动作，头只转 55%。
- **对话**：说话时播放说话动画，静默时播待机；玩家选回应选项时点头；对话结束时说一句带配音的告别。
- **性能**：屏幕外的居民不做动画求值；25m 以外每 3 帧求值一次；Animator 设为 CullCompletely；LOD0 ≤15k 三角面，LOD1 约 4k；1024 图集，一种材质。

## 3. 模型
沿用 v022 的 Quaternius CC0 头部和服装（[Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html)、[Modular Outfits Fantasy](https://quaternius.itch.io/modular-character-outfits-fantasy)），但**保留蒙皮骨骼**（旧版被烘成 A 字静态网格，这是动作僵硬的根本原因）。动作来自同骨架的 CC0 [Universal Animation Library 1/2](https://quaternius.itch.io/universal-animation-library)，以 Humanoid 方式重定向。生成脚本：`Tools/Art/build_v044_residents.py`；原始下载在 `ArtSource/V044/AI/quaternius/2026-10-28/`。

## 4. 对话
台词表 `lines.txt` 是唯一数据源。分 4 个阶段（未读海图 / 已读海图 / 收回 1–3 段 / 四段齐全），另有“本岛回响已收回”的感谢台词。每人都有口头禅（铃音“听——”、青禾“慢慢来”、阿砾“嘿嘿”、望星“按星图来说”、林舟“嗯，老规矩”），并和回响人物有关系（阿铃的孙女、阿根的徒弟、被救孩子的儿子、沈望潮的记录员、送沈望潮出海的老友）。“聊聊故事”会依次推进；第二次问时居民反问一个问题，给出两个回答选项，每个回答都有配音的回应。字幕按配音时长逐字显示，按 E 补全。存档结构没有改动。

## 5. 配音
`Tools/Art/build_v044_voice.py`：用 edge-tts 生成后，两遍 loudnorm 归一到 -16 LUFS / -1.5 dBTP，输出单声道 44.1k Ogg。只重新生成有改动的行（按哈希判断）。每位居民头部挂一个 3D AudioSource（对数衰减 1.5–14m）。试听页：`Logs/V044/npc/voices.html`。

| 角色 | 音色 | rate / pitch |
|---|---|---|
| 铃音 | zh-CN-XiaoyiNeural | +6% / +2Hz |
| 青禾 | zh-CN-XiaoxiaoNeural | -12% / -3Hz |
| 阿砾 | zh-CN-YunxiNeural | +10% / 0 |
| 望星 | zh-CN-YunjianNeural | -8% / -4Hz |
| 汐禾 | zh-TW-HsiaoChenNeural | 0 / 0 |
| 林舟 | zh-CN-YunyangNeural | -15% / -10Hz |
