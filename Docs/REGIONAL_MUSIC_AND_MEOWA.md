# 区域音乐与 Meowa 音效记录

更新日期：2026-10-22。本文记录本轮素材、运行时设计和验收边界；快速交接见 [音频交接](AUDIO_AND_MUSIC_HANDOFF.md)。

当前已准备并接入 9 首用户 BGM 和 16 个真实 Meowa 输出。2026-10-22 的 REGIONAL-MUSIC、ACTION-AUDIO 原生专项均通过，纯规则 4272 项已重跑通过；下文记录实际检查范围与日志，人工耳机听感和实体头显仍未验收。

## 用户提供的 9 首 BGM

原始文件按原文件名归档在 ArtSource/Audio/UserMusic/Originals；派生母带位于 Assets/Resources/FishingAudio/Music。清单记录原件/母带 SHA-256、时长、声道、增益及循环参数：

- [归档清单](../ArtSource/Audio/UserMusic/manifest.json)
- [运行时 Manifest](../Assets/Resources/FishingAudio/Music/Manifest.json)
- [本地准备脚本](../Tools/Art/prepare_user_music.py)

| zone / 资源名 | 原始曲名 | 区域 | 时长约秒 | loopStart 秒 |
| --- | --- | --- | ---: | ---: |
| 0 / Sailing | Open Sea Drift & Voyage | 外海航行 | 30.772 | 0 |
| 1 / Fishing | Serene Waters Fishing Spot | 固定钓位、遛鱼及待处理鱼获 | 30.746 | 0.35 |
| 2 / Home | Fisherman's Hearth Island Home | 小屋、家岛、菜园、手作及标题菜单 | 30.772 | 0 |
| 3 / HarborShop | Harbor Tackle Shop BGM | 港口、钓具店与港店对话 | 30.772 | 0 |
| 4 / Exploration | Tropical Shoreline Discovery | 岸上各岛与科考海岸共用 | 30.328 | 0 |
| 5 / RealmWind | Breeze Corridor of Windmill Island | 风之回廊 | 30.772 | 0 |
| 6 / RealmForest | Emerald Mangrove Garden | 森光庭院 | 28.761 | 1 |
| 7 / RealmWreck | Echoes of the Sunken Hull | 沉潮机库 | 30.772 | 0 |
| 8 / RealmStars | Celestial Sanctuary of Star Trails | 星轨圣所 | 30.772 | 0 |

全部母带为 44.1 kHz 立体声、16-bit PCM WAV；Unity 导入采用 Streaming/Vorbis、质量 0.85、不强制单声道、不预载整段音频、后台加载。原 MP3 保留，解码为 WAV 不意味着恢复有损编码已丢失的细节。

准备过程去除直流偏移，以带门限的 active RMS 做常量增益匹配，目标约 -21 dBFS，并受峰值余量限制。清单实际母带峰值约 0.44–0.47。这里没有执行 LUFS 测量或动态压缩，也没有向 AI 服务上传原音乐、续写尾奏或改写曲式。

## 实际区域判定与淡化

[控制器](../Assets/Scripts/Fishing/FishingRegionalMusic.cs) 在实际场景初始化时绑定；[纯音乐规则](../Assets/Scripts/Fishing/FishingMusicMath.cs) 管理等功率、循环窗口与稳定候选。

判定优先级为：序章独占 → 标题 Home → 秘境过渡保留当前选择 → 实际秘境 5..8 → 小屋/菜园/家岛 Home → 实体商店或店员/引路人对话 HarborShop → 停船钓位/船上待处理鱼获 Fishing → 实际港口 HarborShop → 其他岸上 Exploration / 海上 Sailing。

使用 Home.Inside、Farm.Nearby、Realms.Inside/Island、Anchored、AtHarbor 和人物位置，不能用导航选择替代真实到达。游泳没有独立的潜水状态：仍归对应岸上区域，家岛近海可能继续沿用 Home，不虚构水下专曲。

- 区域候选连续驻留 1.35 秒后确认。无效或缺失的时间样本不完成候选驻留。
- 正常跨区采用顺序淡化：旧曲 2 秒淡出至精确零增益并停止全部旧 voice，再经过约 0.18 秒 DSP 准备静音，由新曲用 2 秒淡入。不同区域曲目不重叠；旧曲淡出期间以最新已确认区域替换排队目标。新曲淡入期间再次换区则在该阶段结束后继续顺序切换。
- 两个 deck，每个各两条 AudioSource，总计四条。跨区与本曲循环分开计算增益，避免回收仍可听见的音源。
- DSP 预排程提前量 0.18 秒；source.loop 均关闭，由 PlayScheduled / SetScheduledEndTime 安排。
- 同区暂停、背包与海图不重启音乐。音乐沿用总音量、音乐滑块和静音开关。
- 对话 duck 为 0.55，钓鱼或待处理鱼获为 0.70，约 0.6 秒平滑变化。序章接管则约 0.75 秒淡至区域静音，避免与序章已有配乐同时占据前景。

## 循环处理与不完整秘境结尾

| 曲目组 | loopCrossfade | tailTrim |
| --- | ---: | ---: |
| Sailing / Fishing / Home / HarborShop / Exploration | 3.5 秒 | 0.18 秒 |
| 四首 Realm 曲目 | 6.5 秒 | 0.65 秒 |

第一次从曲首播放；播放终点是原曲时长减 tailTrim。另一条 voice 在终点前 loopCrossfade 秒启动，后续从 loopStart 处接入，运行长度为“原曲时长 − loopStart − tailTrim”。刚结束的 voice 在静音间隔中预排下一轮。

因此四首秘境不需要等到原曲末尾的未完成收束点才硬切，而是在此前用 6.5 秒叠化交接；这不是修补成完整尾奏，也没有承诺节拍严格对齐。淡化平方功率已通过纯规则测试，风境首轮 DSP 尾衔也已在原生专项检查；其余三境的完整循环听感以及人工耳机试听仍不能由风境结果代替。

本地试听文件不额外打包为游戏曲目：

- [风境尾衔预览](../Logs/UserMusic/RealmWind-loop-preview.wav)
- [16 个 Meowa 片段串联试听](../Logs/Meowa/meowa-action-preview.wav)

Logs 下的预览用于本机试听，可能不随源码交付；准备脚本可重建，运行清单和 Resources 母带才是游戏输入。

## 旧 MainTrack 的精确租借

旧 Assets/Audio/Music/MainTrack.wav 仍由 AppSettings prefab 引用。区域音乐就绪后，只检查 AppSettings.Instance 自身的 AudioSource，匹配 prefab 预期 clip 或 MainTrack 身份，并仅暂停正在播放的匹配源。

租借保存音源与 clip 身份；组件停用或销毁后，对仍是同一 clip 且确实被本组件暂停的源执行 UnPause。不会遍历并停止所有音乐，也不改写原 clip、音量、循环标志或播放位置；其他管理器、子物体及不同曲目排除在外。九首资源清单不可用时不接管旧音乐。

本轮原生专项实际运行了 AppSettings 精确租借夹具：匹配 MainTrack 被暂停，另一 clip 与管理器子物体音源持续播放；区域音乐停用后 MainTrack 原位恢复，其他音源未被改写。该用例本次没有因缺少管理器而跳过。

## Meowa：8 类 × 2 变体，160 积分

来源：[本轮 Meowa 项目](https://meowa.ai/canvas?projectId=proj-1792601700097-ukfrrsxf)。使用项目拥有者账户的 sound effect 模式，每类请求两个 2 秒变体，temperature 为 0.3。

| 类别 / 两个运行资源 | 声音内容 | 已写入的事件挂钩 |
| --- | --- | --- |
| RodCast1 / RodCast2 | 挥竿、破空与细线擦响 | 成功抛竿 |
| FloatSplash1 / FloatSplash2 | 小浮标落水、细水滴 | 飞行结束后首次等鱼落水 |
| NetFishPlace1 / NetFishPlace2 | 湿鱼轻落网绳 | 左手放鱼入抄网动画完成 |
| FarmTill1 / FarmTill2 | 锄头闷击、松土碎粒 | 翻土提交成功 |
| FarmPlant1 / FarmPlant2 | 细种子、纸袋与覆土 | 播种提交成功 |
| FarmWater1 / FarmWater2 | 小水壶细流、土叶水声 | 浇水提交成功 |
| FarmHarvest1 / FarmHarvest2 | 萝卜根茎脱土与叶片 | 收获提交成功 |
| ShopPurchase1 / ShopPurchase2 | 硬币、木台与钱袋 | 用品、装备升级、猎枪购买成功 |

每类 20 积分，共 8 × 20 = **160 积分**，未用满用户授权的 **500 积分**预算；本轮预算余 340。sources/manifest 记录的账户余额是 **725 → 565**，与预算余量是两个不同数字。本轮没有为用户提供的九首 BGM 再花生成积分。

[生成源记录](../ArtSource/Audio/Meowa/sources.json) 保存项目、提示词和真实结果 URL；[处理清单](../ArtSource/Audio/Meowa/manifest.json) 保存 16 个源/母带哈希、原时长、剪辑时长、声道、采样率和增益。原始下载在 ArtSource/Audio/Meowa/Originals，运行 WAV 在 Assets/Resources/FishingAudio/SFX。

[本地准备脚本](../Tools/Art/prepare_meowa_audio.py) 只解码真实下载的 Meowa MP3，不制造合成替代品。派生为 44.1 kHz 单声道 16-bit PCM，去直流、剪除静音、常量增益匹配并加约 3 ms 入端/25 ms 出端淡化，峰值不超过约 0.82；成品时长约 0.248–2 秒。Unity 中以短音效单声道 PCM 预载，保持原件及记录供重建。

[FishingActionAudio](../Assets/Scripts/Fishing/FishingActionAudio.cs) 负责两变体交替、音效设置和静音过滤；农田声音放在成功交易之后。本轮原生 ACTION-AUDIO 已验证八类真实成功事件，以及无款购买、重复买枪、柜台关闭、未成熟/重复收获、重复浇水、取消抛竿、未完成/重复入网和静音等路径不会补发成功声音。装备升级的旧泛用提示音已替换，避免成交声双叠；NetFishPlace 本次检查的是左手检视入网路径，不能据此宣称所有 XR 放置分支已实测。

## 素材与许可证边界

用户提供 9 首音乐用于本工程接入，记录未把它们声明为 CC0，也未归档逐首独立发行许可文本。保留用户原件、来源信息和原提供方授权记录；是否具备其他发行/转授权用途，不能由文件可播放或归档哈希代替。

Meowa 输出来自项目拥有者账户，生成提示和下载证据已保留；当前未归档账户/提供方适用许可条款快照，不能从扣除积分推断为 CC0 或已核实商用授权。随包保留 [MeowaAttribution.txt](../Assets/Resources/FishingAudio/MeowaAttribution.txt) 及后续补齐的授权记录。生成时没有上传用户 BGM 或本地工程文件。

前一批 34 个音效的来源另见 [CREDITS](../ArtSource/Audio/CREDITS.md) 与 [manifest](../ArtSource/Audio/manifest.json)：Kenney / Fantozzi 条目按归档声明保留 CC0，枪声保留 Vincent Sevedge 的 CC BY 3.0 归档作者、许可链接及改作说明，随包继续保留 [Attribution.txt](../Assets/Resources/FishingAudio/Attribution.txt)。这套既有声明不能自动套用给新音乐和 Meowa 输出。

## 2026-10-22 验证记录

后续按用户要求，跨区域改为“旧曲完全消失后新曲才出现”，不再采用两曲同时叠化。同曲循环仍保留 3.5 / 6.5 秒首尾叠化。下列早期日志为此前实现的历史记录；本轮顺序切换结果以最新专项日志为准。

本轮顺序切换专项已通过：[日志](../Logs/usv-validation-5d6d9ffdcc964aa582fc7109147eda71.log)。真实 DSP 观测确认旧曲逐渐减小、所有旧源在零增益停止、静音边界后新曲逐渐出现，全程无跨曲重叠；连续“港口→航行→钓位”在淡出期间更新到钓位，未排程或播放中间航行曲。九区域、同曲首轮尾衔、暂停/背包、序章及旧音乐恢复继续通过。测试不代替耳机或实体头显听感验收。

切换修改后 [AUDIO-MIX 复验](../Logs/usv-validation-d9c9d39cc8e344c99575dbf42a108c9a.log) 亦通过，三路独立音量、环境静音、XR 声音页及原音量偏好保持正常。

纯 C# 规则 **4272 项重跑通过**，包含 56 项音乐数学测试。音频准备清单、代码复查与以下两项原生运行检查已完成。

### REGIONAL-MUSIC PASS

[有效日志](../Logs/usv-validation-3f8466aa64f94dbbb1455daf82e60319.log)，对应 [专项代码](../Assets/Scripts/Editor/FishingRegionalMusicValidation.cs)。

- 九轨 Streaming/立体声/时长元数据、实际九区域状态、驻留防抖、4 秒跨区淡化及暂停/背包不重启通过。
- 风境实际播放跨过首轮尾部，两个 voice 按 6.5 秒重叠预排程；记录 **294 个帧级观测点**，确认播放状态、增益平方和约 1、范围有界，并在尾部后排好下一轮。这不是 294 个 PCM 音频采样，也不等于逐轨人工无缝听感验收。
- 序章接管从当前混音连续淡至静音。日志实际 DSP 经过 **0.256 秒**，先前一帧功率 0.777、同步当前 DSP 后增益平方和 **0.739**，随后四源静音。这里检查的是音源增益曲线，不是测量输出音频 RMS。
- AppSettings 上精确 MainTrack 租借与停用恢复实际通过；不同 clip 与子物体音源保持播放，本次未跳过该夹具。
- 正式玩家存档与备份逐字节未变，测试结束恢复隔离进度与设置。秘境入口用安全 Relocate 夹具，不宣称完成四岛连续步行路线或解谜。

### ACTION-AUDIO PASS

[最终有效日志](../Logs/usv-validation-b31d5edb03b74bcfa0adffd1562b7e26.log)。

- **50 个片段 = 原有 34 + Meowa 16** 均可解码、数值有限且非静音；新增八类各有两个不同导入变体。
- 实际翻土、播种、浇水、成熟收获、用品购买、抛竿、浮标接水和左手放鱼入网成功声音通过。款项不足、未成熟或重复收获、同日重复浇水、取消抛竿、取消或重复入网不误报声音。
- 原有真实脚步、收线/放线、鱼拍声节奏与入网/暂停静音仍通过；新增声音遵守音效音量和静音设置。
- 玩家正式存档与备份未变。
- 用品、装备升级与猎枪购买各自验证真实扣款/库存/等级和一次音效；无款、已有枪但余额仍足够、关闭柜台等失败不扣款、不加物品、不误响。

### MOUSE-FISHING PASS 与人工验收边界

[本轮有效日志](../Logs/usv-validation-a1898964508241d0b5d06deb9e2430ec.log)：抛线、鼠标控制杆向、收放线/线轮旋转、左手持活鱼检视、入网和一次性存储通过，未因新增声音改变玩法交易。人工耳机试听的逐曲循环衔接、音量平衡与实体头显验收仍未完成，不由上述自动专项代替。

Streaming 音乐专项没有调用 AudioClip.GetData；短音效的解码检查与此不同。后续重跑命令与历史旧批次结果见 [音频交接](AUDIO_AND_MUSIC_HANDOFF.md)。首轮曾有 .meta GUID 长度错误导致启动失败，修正后已取得以上有效 PASS，不沿用失败启动作为最终结果。
