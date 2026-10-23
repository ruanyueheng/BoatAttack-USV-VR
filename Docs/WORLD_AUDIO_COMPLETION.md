# 世界交互与环境音效补齐

更新：2026-10-23。基线为 v0.37，Unity 2022.3.62f1 / 4af31df58517。本轮按用户要求仅保留本地，不更新 GitHub 或 PR #48。

## 完成内容

通过用户指定的 [Meowa 项目](https://meowa.ai/canvas?projectId=proj-1792601700097-ukfrrsxf)实际生成并下载 **44 个原始 MP3：33 个短音效、11 个循环环境音**。另外从倒水原声剪辑出 WaterFinish，得到 **45 个运行资源**；连同既有 58 个片段，动作/世界音效资源总数为 **103**。九首 BGM 未替换，仍使用此前本地提交的“旧曲完全淡出 → 短静音 → 新曲淡入”，同曲首尾循环叠化不变。

| 事件组 | 新增声音及触发条件 |
| --- | --- |
| 碰撞、移动与船体 | 木/石/金属碰撞、船体擦碰循环、船拍水、落地、入水、游泳划水、攀梯、成功上下船；按实际运动、接触及材质判断，带冷却 |
| 钓鱼与狩猎 | 咬钩、提竿中鱼、断线、鱼跃水花、鱼获存放、猎物命中、拾取战利品；保留已有鱼轮、检视鱼及枪声 |
| 农场与水井 | 水壶拿放、摇井绳、提桶、实际倒水及成功结束、果树摇动/落果/拾果；取消操作不冒充完成 |
| 制作与生活 | 锻造敲击、工作台装配、熔炉燃烧、制作完成、储物移动、灯笼开关、烹饪、手作、电视、沙发/睡眠布料 |
| UI、对话与秘境 | 确认、返回、拒绝、传送及机关反馈；以现有交互成功入口为准 |
| 生物与区域环境 | 稀疏海鸥鸣叫、附近活动动物/草叶声、雨、森林、沉船、风廊、星轨环境循环 |

这不是新增换弹、箱盖动画或畜牧玩法；没有对应真实玩法事件的声音不伪造触发。相同素材可供多个相近事件复用，资源数不等同于独立玩法数。

## 混音与生命周期

- 新增 FishingWorldAudio 四个分部，将真实成功事件与声音调度分开，不更改购买、掉落、制作或存档交易规则。
- 固定池共 30 个 AudioSource：动作 10、UI 4、环境点声 4、循环 12；不会无限创建，忙碌声源不被抢占，重复请求有冷却。
- 循环需要持续的活动租约，操作结束、离开范围或暂停后淡出停止；距离与低基础增益限制叠加。静音、失焦与暂停会重置基线，恢复时不补播积压事件。
- 暂停中的储物/制作成功反馈走专用 UI 池，仍受动作音量、总音量和静音控制。睡眠布料声仅由真实睡眠动作触发。
- 室内降低外部风、雷、海岸及雨的存在感；低通仅用于独立环境声物体，不给共享原型根物体加滤波，BGM 不被室内低通误伤。
- 增加波浪采样点的幂等释放及注册表压缩，避免音效组件销毁后遗留采样范围；保留既有波形计算。

## 素材、成本与复建

- 原件、网页可见结果地址与提示词：[sources.json](../ArtSource/Audio/MeowaWorld/sources.json)；实际输出与 SHA-256：[manifest.json](../ArtSource/Audio/MeowaWorld/manifest.json)。原始 MP3 位于同目录 Originals，运行 WAV 位于 Assets/Resources/FishingAudio/World，均使用 Git LFS。
- 本批合计 **445 积分**，含恢复任务前已生成的木碰撞 5 积分；恢复时余额 490，继续消耗 440，最终网页余额 **50**。已存在的木碰撞直接复用。未充值、未开会员，也未上传用户 BGM 或本地工程。
- 供应商部分请求为 6 秒的结果实际约 5 秒；清单保留请求与实际文件信息，不把请求时长当成成品时长。
- 派生为 44.1 kHz 单声道 PCM16。去直流、剪静音、固定增益保留峰值余量；短音效采用边缘淡化，循环离线处理首尾接缝。短片约 0.149–1.589 秒，循环约 3.88–4.88 秒，WaterFinish 为 0.18 秒。带静音门限的 active RMS 目标约为短声 -21、循环 -26 dBFS，**不是 LUFS 测量或动态压缩**。
- 账户生成结果没有被声明为 CC0。分发时保留提供方/账户适用许可记录及随包 Attribution.txt；扣费与下载本身不是许可证明。
- 本地网页完成凭证：Logs/Meowa/04-world-audio-completed.png；44 段原声选段试听：Logs/Meowa/world-audio-preview.wav。这些 Logs 文件不进入 Git。

从已归档原件复建（无需再次访问网页或消耗积分）：

```powershell
& E:/blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Art/prepare_world_audio.py
```

## 最终验证

纯 C# **4353 项通过**。以下均为本轮最后一次原生 Unity 检查的 PASS 日志：

| 专项 | 日志 |
| --- | --- |
| WORLD-AUDIO | [45 段解码、真实交互、混音及采样生命周期](../Logs/usv-validation-66df85337c7d4affa75f9c5a5d6d9403.log) |
| ACTION-AUDIO | [既有动作音效回归](../Logs/usv-validation-08671676947f409e9ff3c6f7a855c4c6.log) |
| AUDIO-MIX | [音量及检视回归](../Logs/usv-validation-49d9c9f1957844d2adf085b5fc48b0ea.log) |
| HUNTING | [枪械与猎物回归](../Logs/usv-validation-dd1294c206754851b9c8c48d78ec9199.log) |
| MOUSE-FISHING | [抛竿、收放线、检视与入网回归](../Logs/usv-validation-643c8b40b9cc43608df469e0dc393392.log) |
| REGIONAL-MUSIC | [区域音乐与循环回归](../Logs/usv-validation-23ab2e8307624c469a33a02c45f31e38.log) |

新专项覆盖全部 45 段可解码、非静音及有限采样，声源上限/冷却/分类/静音，暂停中的储物与制作反馈，室内风声压低且 BGM 不受滤波，水壶取水及浇水取消/成功/去重，波浪采样范围删除与其余数据保留。测试不保存场景，正式存档、备份与偏好保持不变。

自动检查不等于人工耳机/实体头显听感验收，也未遍历所有碰撞几何、动物路线或物理输入。最终响度和短循环听感仍需实际游戏试听。未修改场景、Unity/包版本；无关 Unity 重序列化改动留在工作区，未收入本轮提交。

复验前保存并关闭使用此工程的 Unity 编辑器，然后运行：

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj
.\Tools\Run-UnityValidation.ps1 -Suite FishingWorldAudio -DisableBurst
.\Tools\Run-UnityValidation.ps1 -Suite FishingActionAudio -DisableBurst
.\Tools\Run-UnityValidation.ps1 -Suite FishingAudioMix -DisableBurst
.\Tools\Run-UnityValidation.ps1 -Suite FishingHunting -DisableBurst
.\Tools\Run-UnityValidation.ps1 -Suite FishingMouseFishing -DisableBurst
.\Tools\Run-UnityValidation.ps1 -Suite FishingRegionalMusic -DisableBurst
```
