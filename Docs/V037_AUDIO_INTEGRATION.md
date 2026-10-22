# v0.37 主线与音频整合

2026-10-22；Unity 2022.3.62f1 / 4af31df58517。

基于 GitHub main `9e2b9ca`（PR #47），合入本地 `d9f82d7`、`f566625`、`ac86e92` 的动作音效、九首区域 BGM、独立混音和检视音效。整合分支为 `feature/v037-audio-integration`，不直接推送或自动合并 main。

## 保留与兼容

- 保留 v0.37 场景、模型、纸面 UI、雷雨、手部交互、果园和储物功能；没有回退场景、升级 Unity 或包版本。
- 保留九首 BGM、58 条 SFX、跨区/循环叠化，以及 BGM / 环境 / 动作三路音量。音频来源、积分和许可边界见 [音频说明](AUDIO_MIX_AND_INSPECTION.md)。本次同步没有再生成音频或消耗积分。
- XR 储物页保持 18/19，声音混音迁至 20，避免页码覆盖。
- v0.37 新雷声归环境音，基础增益 0.12；保留闪电、延迟雷声、失焦/暂停取消及原有较低风雨增益。
- 浇水音效仍只在新版真实手持水壶接触并完成提交时触发。持壶期间拒绝装备检视，归还后恢复检视，不自动丢弃水壶。
- 新版位置型碰撞网格没有 UV。脚步识别按实际碰撞落点读取邻近可视三角形的材质/UV，使用缓存的二维空间网格，不修改地形或碰撞，也不把整个家岛强制归为同一材质。
- 用户 BGM 准备脚本默认从仓库归档原件重建；可在 Blender 参数 `--` 后指定 `--source-dir`，不再依赖固定盘符。
- 原有 49 个本地 Unity 导入/渲染文件修改已备份并恢复，未混入 PR。恢复点为本地 `backup/audio-before-main-sync-20261022` 与 `backup/uncommitted-unity-before-sync-20261022`；原 stash 仍保留。

## 本轮实际验证

纯 C# 测试 **4353 项通过**，含原音频规则及上游新增储物规则；83 个 Unity 专项的参数/入口/成功标记保持一致。

下列 Unity 原生专项均在合并后的 v0.37 通过，唯一日志位于本机忽略目录 `Logs`：

| 专项 | 日志 |
| --- | --- |
| AUDIO-MIX | usv-validation-ceb7b7dbff1e4156b291a78206fe705c.log |
| ACTION-AUDIO | usv-validation-0e9c1eb86eb3482f9d10ebe58d6cae03.log |
| V036-STORAGE | usv-validation-47c2a915322f4195bac2c920cef4e9bb.log |
| V037-WEATHER | usv-validation-b40c72005ee9413d9711051ba1da845f.log |
| V037-HANDS | usv-validation-8b90cfee552844c182696a1dcca3fc3f.log |
| MOUSE-FISHING | usv-validation-11485884ed2f470e9eafde995477768d.log |
| HUNTING | usv-validation-48688d79d42e423a8eac5f4d46d604e4.log |
| REGIONAL-MUSIC | usv-validation-1ecb11f82c164666899d1f7ead250888.log |

音乐真实跨过首轮尾衔，记录 292 个帧级重叠观测点。天气专项截图改存唯一 Logs 子目录，不覆盖上游作者截图。

验收边界：测试运行于保留原本地导入/渲染差异的工作区，不冒充另一台机器的全新干净检出验证；正式玩家存档和音量偏好受到隔离保护。未进行 Android 构建、实体 PICO、物理输入、耳机主观听感或完整步行路线验收，因此 PR 保持草稿等待人工确认。
