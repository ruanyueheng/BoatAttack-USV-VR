# 狩猎与锻造整合验收

基线：GitHub main v0.23，`b726e38efc248a4ef6f245780ee9e46748e8a657`。
编辑器：Unity 国际版 `2022.3.62f1 / 4af31df58517`，未升级 Unity 6。
日期：2026-10-14。

- 纯规则回归：3569 项通过（`Logs/final-rules-v023.txt`）。
- 输入/渲染策略：70 项通过（`Logs/final-input.txt`）。
- Unity Hunting：通过，日志 `Logs/usv-validation-ebc3c0481a5548d39869fbda6c0b9bee.log`。
- Unity Forge：通过，日志 `Logs/usv-validation-1ec9da06460148fc9d8406bf02af8fd7.log`。
- Unity V023：通过，日志 `Logs/usv-validation-275db563a57e4aed865847050bcc1a2b.log`。
- v0.20/v0.21 合并阶段的 Workshop/V021 回归也通过；最终 v0.23 另外核对了新版物品模型和双手握持。

专项使用生产场景、模拟输入及隔离存档，未操作玩家真实存档。狩猎验证真实枪口射线、兔子/海鸥击杀与领取，锻造验证真实石料采集、制作事务、重复领取保护及磁盘存档。V023 验证渔屋、休息、海洋图鉴、开场电影和原主线装备握持；新增物品使用其自身的贴图模型/无字简图验证，未要求伪造主线照片资源。

本机原生 Burst 缓存存在 DLL 加载问题，运行时专项临时使用 `-DisableBurst`，未修改永久 Burst 设置。上述检查不等同于人工手感、头显或发布构建验收。猎枪暂为桌面单发、无限弹药；锻造暂为即时配方，未加入敲击小游戏。

预览见 [狩猎说明](HUNTING_RIFLE.md) 与 [锻造说明](FORGE_PROTOTYPE.md)。收银员增大与肩肘姿势修改保留在 `FishingCashier/FantasyGirl` 模型和 `FishingShopCashier.cs` 中。

后续山脚煤矿/毛皮灯笼/活动时间刷新更新：3593 项规则断言通过，最终 Hunting 与 Forge 专项再次通过。新增内容见 [煤矿、灯笼与刷新说明](COAL_LANTERN_WILDLIFE.md)。
