# 动作音效与区域配乐交接

更新日期：2026-10-22。Unity 2022.3.62f1，当前玩法版本 0.35.0。

当前增补：已接入独立 BGM / 环境 / 动作混音，降低引擎与风浪基础增益，并新增 **8 条 Meowa 镐子/枪检视、活鱼挣扎及 AK 单发音效**。SFX 当前总数为 **58 = 既有 34 + 前批 16 + 新批 8**；AUDIO-MIX 与扩展后的 ACTION-AUDIO 原生专项通过。操作说明、资源/积分、当前有效日志及人工验收边界见 [声音混音与检视增补](AUDIO_MIX_AND_INSPECTION.md)。本次消耗 80 积分、前后累计 240；尚未上传 GitHub。

最新主线整合与本轮验证见 [v0.37 整合记录](V037_AUDIO_INTEGRATION.md)。以下保留前一批九首 BGM 与 16 条生成音效的交接记录。其中“50 条”及当时日志为历史批次状态；当前数量及检视/混音状态以增补文档为准。

## 前一批交接记录（保留）

本轮已归档并接入用户提供的 **9 首区域 BGM**，另新增 **16 个 Meowa 动作音效**。REGIONAL-MUSIC 与 ACTION-AUDIO 原生 Unity 专项均已通过，纯 C# 4272 项已重跑通过；人工耳机听感和实体头显验收尚未进行。详细参数、来源、有效日志与补测范围见 [区域音乐与 Meowa 记录](REGIONAL_MUSIC_AND_MEOWA.md)。

## 9 首音乐的实际分区

| 运行资源名 | 使用范围 |
| --- | --- |
| Sailing | 外海开船、漂航及海上停留；排除已进入的钓位和港口 |
| Fishing | 实际停船钓位，包括等鱼、遛鱼和待处理鱼获 |
| Home | 回潮渔屋室内、家岛、菜园及桌边手作；标题菜单也复用此曲 |
| HarborShop | 港口、钓具店实体室内、店员与引路人对话 |
| Exploration | 岸上各岛、科考岸、沙滩与其他岸上探索；没有为每座外岛另配一首 |
| RealmWind | 风之回廊 |
| RealmForest | 森光庭院 |
| RealmWreck | 沉潮机库 |
| RealmStars | 星轨圣所 |

区域由实际船/人物位置、停船和秘境状态判定，选择导航目标不会立刻换曲。当前没有独立潜水状态或潜水专曲；游泳归入对应岸上区域，家岛附近仍可沿用 Home。

普通区域候选持续约 1.35 秒后确认，采用 **4 秒等功率交叉淡化**。过渡尚未结束时只保留最新的已确认目标，完成后再切下一曲，不回收仍可听见的 deck。门口黑屏过渡保留当前选择；序章接管时，区域音乐用约 0.75 秒淡至静音。

每个 deck 使用两个 AudioSource 提前安排下一轮，共四个声音源。通过真实 DSP 时钟调度，所有 source.loop 均为 false：

- 普通五首：循环重叠 3.5 秒，播放终点提前裁去尾部 0.18 秒。
- 四首秘境：循环重叠 6.5 秒，尾部裁去 0.65 秒，以长叠化处理原素材未完整收束的结尾。
- 首次从原曲开头播放；后续从清单指定的 loopStart 回接。Fishing 为 0.35 秒、RealmForest 为 1 秒，其余为 0 秒。
- 这些是运行时首尾叠化，不是重新创作完整尾奏，也未宣称逐拍无缝；最终听感仍需耳机与头显试听。

音乐接入“总音量 / 音乐 / 静音”设置。对话压至基础音乐增益的 0.55，钓鱼或持有待处理鱼获压至 0.70，约 0.6 秒平滑调整。暂停、背包和海图保留音乐进度，不因打开面板重新从头播。

旧 AppSettings 上的 MainTrack 仅按精确音源和曲目身份暂停租借；区域音乐停用后恢复该曲的原播放位置。不同曲目、其他管理器及 AppSettings 子物体声音不属于此次租借范围。

## 新增 16 个 Meowa 音效

本轮为八类声音各生成两个变体，原始输出每个 2 秒；剪除静音后运行片段约 0.248–2 秒。

| 文件名前缀；每组 1/2 两个变体 | 当前成功事件入口 |
| --- | --- |
| RodCast | 成功抛竿 |
| FloatSplash | 浮标完成飞行、首次进入等鱼阶段的落水 |
| NetFishPlace | 左手检视鱼放入抄网的动画完成点 |
| FarmTill | 翻土成功 |
| FarmPlant | 播种成功 |
| FarmWater | 浇水成功 |
| FarmHarvest | 收获成功 |
| ShopPurchase | 用品购买、装备升级、猎枪购买成功；升级原有泛用提示音已替换以免叠加 |

八类片段接入音效通道，两个变体交替使用；缺少导入素材时不临时合成替代。本轮 ACTION-AUDIO 已检查八类真实成功事件，以及不足款购买、已有猎枪再次购买、关闭柜台拒绝、未成熟/重复收获、重复浇水、取消抛竿、取消/重复入网和静音等失败/抑制路径。

原有 34 个动作片段继续保留：四种地面的脚步、拾取/采矿、枪声、实际收线/放线、手持活鱼拍声。当前 SFX 目录共 **50 个音频片段 = 原有 34 + 新增 16**，不是 50 个本轮生成结果。

脚步仍按人物真实移动距离计步，撞墙原地、暂停、空中、游泳、攀梯和传送不发步声；鱼轮按实际线长变化发声，活鱼拍声按检视动画周期去重。新增声音不会替换玩法交易和存档规则。

## 积分、素材与许可证

- Meowa 本轮预算 500 积分，实际消耗 **160 积分 = 8 组 × 20 积分**，得到 16 个文件；本轮预算尚余 340。记录中的账户余额为 725 → 565，不能把预算余量当账户余额。
- 生成项目、提示词、结果地址、成本及下载源见 [Meowa sources](../ArtSource/Audio/Meowa/sources.json)；原 MP3、派生 WAV 哈希和剪辑参数见 [Meowa manifest](../ArtSource/Audio/Meowa/manifest.json)。
- 用户 9 首音乐的原始 MP3 保留在 ArtSource/Audio/UserMusic/Originals，未覆盖原件；原件与派生母带哈希见 [用户音乐清单](../ArtSource/Audio/UserMusic/manifest.json)。没有将用户 BGM 或本地工程上传给生成服务。
- BGM 派生为 44.1 kHz 立体声 PCM WAV，运行导入为 Streaming/Vorbis。采用带静音门限的 active RMS 常量增益匹配到约 -21 dBFS，并保留峰值余量；这不是 LUFS 测量，也不是动态压缩。
- Meowa 派生为 44.1 kHz 单声道 PCM WAV，短音效预载；处理包括去直流分量、剪静音、常量增益和短边缘淡化。
- 用户提供音乐和账户生成音效 **没有被重新标为 CC0**。当前清单未收录九首 BGM 的独立发行许可文本或 Meowa 账户适用条款快照；发行时保留并补齐原提供方/账户的许可记录，不把积分扣费或文件下载等同于许可证明。
- 前一批第三方音效作者、CC0 与枪声 CC BY 3.0 归档说明见 [既有音效 CREDITS](../ArtSource/Audio/CREDITS.md)。该页描述前一批来源；本轮新增素材状态以本文和独立清单为准。随包保留 [Attribution.txt](../Assets/Resources/FishingAudio/Attribution.txt) 与 [MeowaAttribution.txt](../Assets/Resources/FishingAudio/MeowaAttribution.txt)。

## 验证状态与复验

- 2026-10-22 纯 C# **4272 项重跑通过**，包含新增 56 项音乐数学测试，覆盖等功率、短曲窗口、区域防抖与重定向。
- **REGIONAL-MUSIC PASS**：[有效日志](../Logs/usv-validation-3f8466aa64f94dbbb1455daf82e60319.log)。九区域映射、4 秒淡化、防抖、暂停/背包不重启、风境首个真实 DSP 循环、序章接管和精确旧音乐租借均通过。风境尾衔采集 294 个帧级观测点；序章淡化实际经过 0.256 秒，同步当前 DSP 后区域 voice 增益平方和为 0.739。后者是增益检查，不是音频波形响度测量。
- **ACTION-AUDIO PASS**：[最终有效日志](../Logs/usv-validation-b31d5edb03b74bcfa0adffd1562b7e26.log)。50 个片段全部可解码、有限且非静音，八类新增事件及上述失败/取消去重、原有脚步/收放线/鱼拍声与音量/静音通过；包含用品、升级与猎枪购买的真实成功/失败。正式存档与备份未变。
- 本轮音乐专项使用安全入口放置夹具，不宣称完成四岛步行路线或秘境解谜。两项自动专项不替代人工耳机循环听感、实际输出波形或实体头显验收。
- **MOUSE-FISHING PASS**：[本轮有效日志](../Logs/usv-validation-a1898964508241d0b5d06deb9e2430ec.log)。抛线、鼠标控制杆向、实际收放线与线轮旋转、左手活鱼检视、入网及一次性存储均通过。本轮没有声称实体输入或头显人工验收。

保存并关闭使用本工程的 Unity 编辑器后，可由主任务执行：

    dotnet run --project Tools/FishingTests/FishingTests.csproj
    .\Tools\Run-UnityValidation.ps1 -Suite FishingRegionalMusic -DisableBurst
    .\Tools\Run-UnityValidation.ps1 -Suite FishingActionAudio -DisableBurst
    .\Tools\Run-UnityValidation.ps1 -Suite FishingMouseFishing -DisableBurst

Streaming 音乐禁止用 AudioClip.GetData 做解码检查；音乐专项核查元数据、实际播放状态与 DSP 排程。短音效的解码检查与此不同。首轮曾因 FishingRegionalMusic.cs.meta 的 GUID 长度异常启动失败，修正后的有效结果以本节两份 PASS 日志为准。
