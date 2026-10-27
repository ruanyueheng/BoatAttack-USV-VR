# v0.41 浇水声修复交接

日期：2026-10-27。Unity 保持 2022.3.62f1 / 4af31df58517。

## 同步与备份

工作分支：`feature/v041-watering-audio-20261027`。已快进同步 GitHub 主线 `698f5a4`（PR #53，v0.41），它已包含之前 PR #49 的音效与 AK 修改；没有用旧代码覆盖主线的水壶握姿、烧烤、故事或交互更新。

同步前的提交保存在 `backup/before-v041-water-audio-20261027`；原本未提交的 Unity 修改保存在 `backup/local-unity-before-v041-20261027`，对应 stash `b33152e35111589d935647c67de520f9e4cf77d1`，已在同步后恢复且备份仍保留。LFS 连接中断后的半更新状态另存于 `backup/interrupted-v041-checkout-20261027`；复用缓存完成同步，没有硬重置或删除用户修改。

## 原因与修改

问题不是缺少声音文件。既有 `WaterPourLoop` 是之前真实 Meowa 输出，但水流循环统一注册成 Ambient，降低风浪/环境音时也会把浇水压低或静音。其原增益仅 .05，统一环境淡入又消耗了约 .42 秒，而浇水动作实际水流窗口仅约 1.19 秒。

- `MaintainLoop` 增加可选通道，默认仍是 Ambient，保留其他环境循环调用的行为。
- 水流明确使用 Effects；池内声源在起租、换片段或切换通道时绑定一次，不每帧重复注册，也不重复乘音量。
- 动作循环约 80ms 淡入、120ms 收尾；环境循环保留原有慢淡化。
- 浇菜水流基础增益 .05 → .11，向水壶补水 .04 → .08；成功结束的 `WaterFinish` 请求增益 .35 → .6。
- 取消/失败不增加水量、不播完成声；静音、暂停、失焦、有限声源池等约束保持。
- 预留 `WaterSprinkleLoop`，真实新文件未接入时明确回退为既有 `WaterPourLoop`。没有创建占位声音，也没有宣称新片段已经生成。

## 素材与网页状态

本轮目前没有新增音频文件、没有消费 Meowa 积分。仍使用先前生成并归档的真实原声：

- 原件：[WaterPourLoop.mp3](../ArtSource/Audio/MeowaWorld/Originals/WaterPourLoop.mp3)。
- 运行素材：[WaterPourLoop.wav](../Assets/Resources/FishingAudio/World/WaterPourLoop.wav)、[WaterFinish.wav](../Assets/Resources/FishingAudio/World/WaterFinish.wav)。
- 来源/提示词/处理哈希：[sources.json](../ArtSource/Audio/MeowaWorld/sources.json)、[manifest.json](../ArtSource/Audio/MeowaWorld/manifest.json)。

用户已经分享了 Meowa 项目标签页，但当前聊天没有可用的专用浏览器控制接口；原 Windows UI 路径又无法可靠核验浏览器网址，被安全检查停止。已检查 Edge 扩展与原生连接组件正常，并重建电脑控制会话，但不能将这些检查等同于网页操作已恢复。未绕过安全检查，未变更浏览器权限/注册表/系统策略，未上传本地文件。

网页控制恢复后可在同一 Meowa 项目生成约 3–4 秒、干声无音乐的细密浇水壶洒水落土声，适合短循环；保存原件及可见结果来源，再接入预留资源名 `WaterSprinkleLoop`。生成与实际听感验收尚未完成。

## 验证

- v0.41 纯 C#：4683 项通过，使用已安装的 .NET 主程序运行测试 DLL，没有更改 Windows 应用控制策略。
- [WORLD-AUDIO PASS](../Logs/usv-validation-044cf1440d6e48f59d35b0c7b593c6f2.log)：45 个既有片段解码、声源上限、通道切换与复用、静音、暂停，以及真实拿壶/打水/取消/重试/浇菜/完成去重均通过。
- [ACTION-AUDIO PASS](../Logs/usv-validation-c3e5a77eb660475dafc003ec42b3a427.log)：原动作与检视/钓鱼/购买音效回归通过。
- [AUDIO-MIX PASS](../Logs/usv-validation-1dc386f1ee034779bd46c9472dc1fb98.log)：音量滑块、独立分类及混音回归通过。
- 新增回归确认：Ambient=0 时实际浇水仍启动并在短水流窗口达到目标增益；Effects=0 时水流及尾声静音，但种植交易仍只成功一次；关闭 Effects 不连带关闭 Ambient。
- 低层包络测试显式调用真实 `TickLoops` 的 80ms 步长，避免 EditorApplication.update 与 LateUpdate 顺序造成夹具误判；另外的真实农事测试正常观察生产时序和有效音量，不修改生产逻辑以迁就测试。
- 正式存档、备份与音量偏好在隔离测试结束后保持逐字节不变；场景没有保存。

此结果不是实体耳机/扬声器或 PICO 4 Ultra 的主观听感认证，也不包含本次延期的卡顿/远景优化。新的 Meowa 素材仍待网页连接恢复后生成。
