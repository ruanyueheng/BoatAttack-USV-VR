# 声音混音与检视音效增补

更新日期：2026-10-22。工程仍使用 Unity 2022.3.62f1，当前已合并上游玩法版本 0.37.0。合并后的八项原生专项及 4353 项纯规则均通过，详见 [v0.37 整合记录](V037_AUDIO_INTEGRATION.md)。下方早期日志保留原批次的验证历史。

当前 SFX 共 **58 条 = 既有 34 条 + 前批 Meowa 16 条 + 本次 Meowa 8 条**。九首区域 BGM 的文件及分区沿用 [前批区域音乐记录](REGIONAL_MUSIC_AND_MEOWA.md)，没有为这次检视修改重新生成 BGM。

## 本次新增的八条声音

| 资源键；每类 1/2 两个变体 | 实际触发语义 |
| --- | --- |
| PickaxeInspect | 石镐、铁镐成功进入检视后，真实模型显示到手时播放一次握持/转物声 |
| RifleInspect | 猎枪成功进入检视、模型实际显示到手时播放一次；不假装换弹、拉栓或开火 |
| FishInspect | 左手及右手活鱼检视的真实动作峰值；替换当前使用的旧 FishFlop 声音银行 |
| RifleShot | 原有合法射击成功出口的单发 AK 风格枪声；两个新变体交替使用 |

工具检视没有每帧发声：进入或换物只排队一次，待真实 HeldTool 可见才消费。重复请求同一检视、持续旋转模型不重播；空槽、无效槽、暂停和到手前取消不发声。取消检视立即停止专用检视音源，不打断共用物品音源上的其他动作声。普通检视入口沿用现有 Z / 背包检视；桌面按住右键转动，Z / F / Esc 收起。

活鱼继续采用既有检视时钟：左手使用 InspectionMotionTime，右手使用受暂停约束的持有时钟；约每 0.785 秒的有效动作峰值最多发一次，不补播暂停或错过的拍点。原始两秒素材保留，从最强的 10 ms 能量块前 35 ms 开始选取最多 0.65 秒的一次短扑腾，并加入 55 ms 尾部淡出；即使以最低 0.95 倍音高播放，也在下一峰前自然结束。运行时仍先清除上一段尾音以防特殊长帧，不让声音不断堆叠。放入抄网、放下或暂停停止手持挣扎声，不改变待处理鱼获的所有权和一次性入箱/放生规则。

FishInspect1/2 到齐时完全替换运行中的三条旧 FishFlop，不混播旧银行。RifleShot1/2 优先于旧单文件 RifleShot，仍只在原 TryFire 通过冷却、持枪、枪口空间和视线检查后发一枪；检视枪本身绝不开枪。旧 FishFlop1/2/3、RifleShot 仍保留作兼容回退，旧枪声和新枪声均缺失时才使用原程序备用声；只销毁自建备用片段，不销毁导入资源。

## 三路混音及环境基础增益

总音量为公共主音量；以下三路可独立设置，继续使用既有 Fishing.V032 音量偏好，不重置已有滑杆数值：

| 通道 | 包含范围 |
| --- | --- |
| BGM 背景音乐 | 区域音乐、标题复用曲与序章音乐 |
| 环境音 | 船引擎、水声、原岛海浪/栈桥/流水、风雨及上游雷声 |
| 动作音效 | 脚步、拾取、采集、鱼轮、检视、活鱼、枪声及成交等操作反馈 |

环境基础增益在应用用户环境滑杆之前降低：

- 引擎：原作者音量 ×0.20，上限 0.08。
- 船体水声：原作者音量 ×0.18，上限 0.055。
- 旧岛 ShoreWaves / ShoreRocks / Jetty / FlowingWater 循环：原作者音量 ×0.20，上限 0.055。
- 风声：基础 0.025，随雨量最多增加 0.05；原风雨公式的 0.07＋雨量×0.13 不再使用。
- 雷声：保留上游闪电、延迟雷鸣与暂停/遮蔽守卫，基础增益由 0.42 调低为 0.12，并归入环境音；环境音为零时其实际音源音量也为零。

只有本钓鱼场景内的音源被登记，其他场景及 DontDestroyOnLoad 音源不改类别或源音量；原音量、静音和 ignoreListenerVolume 会在本控制器退出时恢复。引擎现在随环境音滑杆调整，不再占用动作音效通道。工具检视使用独立 Effects 音源，基础增益 0.16、片段播放增益 0.7。

桌面在标题菜单选择“设置 / 声音混音”，或 Esc 暂停后进入“声音混音：BGM / 音效 / 环境”。总音量、BGM、环境音、动作音效分别有滑杆；背景声过响时先降低环境音，不必连同脚步、拾取和枪声一起压低。桌面设置区域使用 690 高虚拟面板，保留底部说明和返回空间。

PICO / XR：右手 B 打开潮汐手册 → 设置 → 声音混音（内部页码 20；18/19 保留给储物箱存入/取出），射线指向后按右扳机选择。总音量按钮依次循环 0 / 25 / 50 / 75 / 100%；BGM、环境和音效分别用加减按钮每次调整 10%；另有全部静音和返回设置。与桌面共用设置并自动保存。

## 来源、处理与积分

- [MeowaInspection sources.json](../ArtSource/Audio/MeowaInspection/sources.json)：本批项目、用户授权、四类完整提示词、真实下载 URL、80 积分及账户余额记录。
- [MeowaInspection manifest.json](../ArtSource/Audio/MeowaInspection/manifest.json)：八条源 MP3/派生 WAV 哈希、采样率、声道、时长、剪辑范围与增益。
- 原始文件保留在 ArtSource/Audio/MeowaInspection/Originals；运行资源为 Assets/Resources/FishingAudio/SFX 下上述八个 WAV。
- [随包来源说明](../Assets/Resources/FishingAudio/MeowaInspectionAttribution.txt)。生成素材不重新标为 CC0；发行仍需保留账户/提供方适用许可记录。
- 本次实际消耗 **80 积分 = 四类 ×20**，得到八条；账户余额 **565 →485**。加前批 160，累计 **240 积分**，不是本次花费 240，也不是将账户余额当预算余量。未充值或订阅，未把用户 BGM 或本地工程上传给服务。

每条原始生成结果为两秒；处理为 44.1 kHz 单声道 PCM WAV，去直流、剪静音、活动段 RMS 常量增益匹配并限制峰值，加入短边缘淡化。未重新合成替代片段；这不是 LUFS 响度测量或动态压缩。

从仓库根目录处理已下载的这八条真实原件，使用包含 aud 的 Blender Python：

    & 'E:\blender\blender.exe' --background --python Tools/Art/prepare_meowa_audio.py -- --inspection

该 --inspection 模式读取 MeowaInspection，而不是前批 Meowa 目录；要求八条原 MP3 齐全，保留原件哈希并拒绝覆盖不匹配的音频 GUID。它只导出已有原件，不发起生成或花费积分；普通系统 Python 若没有 aud 不能直接替代。

## 已验证范围与边界

- **AUDIO-MIX 原生 PASS**：[fd5656ae8bc545e2a8575271ca591543](../Logs/usv-validation-fd5656ae8bc545e2a8575271ca591543.log)。覆盖混音通道、较低的环境基础增益、独立音量及 PC/XR 设置入口/布局相关运行时断言。
- **ACTION-AUDIO 原生 PASS**：[最终 c591c306135f4ff5b61ddfc1eb77403d](../Logs/usv-validation-c591c306135f4ff5b61ddfc1eb77403d.log)。58 条资源可解码、有限、非静音；实际模型可见时的石镐/铁镐/枪检视、取消/暂停/旋转去重、左右手活鱼分散峰值、新鱼/枪银行优先、鱼声在下一峰前完整播完、原脚步/拾取/收放线和八类前批成功/失败事务均通过。隔离存档夹具恢复，玩家正式存档与备份未改变。
- 本轮没有取得新的 PC batch 截图；日志中的原生场景断言和模拟输入不是全新截图或人工画面验收。没有完成耳机听感、真实输出波形检查、主观音量平衡或实体 PICO 听感验收。
- **HUNTING 原生 PASS**：[2cb8c19d63934d8496770397fdee9a7d](../Logs/usv-validation-2cb8c19d63934d8496770397fdee9a7d.log)。真实开枪、单发冷却、命中兔子/海鸥、障碍物与枪口遮挡、分类掉落和重复收取保护未回归。
- **REGIONAL-MUSIC 原生 PASS**：[a02e161941784855befb511ff9f114a1](../Logs/usv-validation-a02e161941784855befb511ff9f114a1.log)。九区域选择、音量与静音、4 秒跨区、风境实际循环衔接、序章交接和旧音乐精确恢复通过。
- 纯规则本轮重跑 **4272 项通过**。上述自动检查不代替主观耳机听感或实际头显验收。

关闭占用本项目的 Unity 编辑器后，由主任务统一运行，不与交互编辑器争用锁：

    .\Tools\Run-UnityValidation.ps1 -Suite FishingAudioMix -DisableBurst
    .\Tools\Run-UnityValidation.ps1 -Suite FishingActionAudio -DisableBurst
    .\Tools\Run-UnityValidation.ps1 -Suite FishingHunting -DisableBurst
    .\Tools\Run-UnityValidation.ps1 -Suite FishingRegionalMusic -DisableBurst

本机 [八条新音效串联试听](../Logs/Meowa/meowa-inspection-preview.wav) 和 [Meowa 成品/余额截图](../Logs/Meowa/03-inspection-and-ak-generated.png) 位于被 Git 忽略的 Logs，准备脚本可重建音频试听。

本增补已纳入 v0.37 音频整合分支；发布结果与人工验收以该分支 PR 为准，不自动合并主线。
