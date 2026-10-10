# v0.13.0 — 第一人称握持、低头视野与女店员

基线：`origin/main@2d633c5d`（已合并 PR #22 的 v0.12 文件树）。Unity **2022.3.62f1**，URP **14.0.12**；继续使用 v8 存档，不重置玩家进度。

## 本版改进

### 手与物品

- 使用源人物真实掌部蒙皮标定掌心，修正仅按骨骼中点估算造成的偏差。
- 石镐/铁镐、双筒望远镜、手册和提灯分别使用握点、掌面方向及手指弯曲量。
- 镐柄与镜筒轴向不同，不再复用同一种握拳方向；物品仍独立于解算后的手腕，避免反馈抖动。
- 岸上平视时把手物放到可见的下方操作区，并在真实臂长范围内调整，不靠伸长骨骼或取消接触守卫。
- 增加真实蒙皮顶点的握持检查、前物镜遮挡检查、俯仰与不同画幅检查。

### 第一人称身体与望远镜

- 完整源身体继续负责第三人称和阴影；桌面本地使用同源手臂/下身呈现，隐藏近眼头颈与上身。
- 对独立下肢网格的腰口添加仅本地可见、随髋部移动的封口，避免隐藏上身后看进裤腰内部。
- 望远镜支持携带 → 举镜 → 光学观察 → 放下。观察时不把双手一起放大到镜片前；第三人称仍显示举镜实体。
- 桌面垂直 FOV 为 68°，光学观察为 38°，约 0.28 秒过渡；暂停、换装备和退出适用场景会恢复正常状态。
- 没有新增覆盖世界深度的手部相机，不修改 HMD 投影。取舍、参考游戏与官方资料见 [调研说明](FIRST_PERSON_RESEARCH_V013.md)。

### 原分支女店员接入

原 PR #21 的 FantasyGirl 资源未进入 v12。Claude 的 v13 草稿提供了 CoastalClerk 替代角色；本次按用户要求从 `origin/feature/v011-cashier-integration@8f609ab5` 恢复原 `Assets/Resources/FishingCashier/FantasyGirl` 资源，不整体合并旧分支。

- 保留原两级模型（69,999 / 22,000 三角面）、URP 材质及颜色、法线、金属度/光滑度贴图；Git LFS 实体按 SHA-256 校验。
- 将原 prefab 的脚本引用改为 v13 现有 `FishingShopCashier.cs.meta` GUID，避免旧 GUID 造成 Missing Script；保留 v13 脚本自身 GUID。
- 沿用 `clerkSpaces[0]`、1.72 m 身高、180° 朝向及 `Bind/Start` 去重；交易、钱包、v8 存档与场景布局仍使用 v13 现有实现。
- 原模型为静态放松站姿，没有骨骼动画、对话树或 NPC AI。CoastalClerk 草稿资源保留在本机，不包含在本次 PR 中，也不生成第二名店员。
- 原素材作者/许可证仍未提供，见 [原来源声明](../ArtSource/FantasyGirl/README.md)。本地接入不宣称原模型具有 CC0 许可；公开上传或分发前需补齐授权。

## PR #20 / #21 逐项核对

| 内容 | v13 处理 |
|---|---|
| #20 蹲姿降幅、低位拾取自动蹲下 | 保留 v12 的安全修正版与真实帧等待，不退回旧实现 |
| #20 接缝/台阶与登陆 | 保留连续步行局部补查、严格登陆支撑及边界守卫 |
| #20 降低持竿遮挡 | 保留并继续校准；补充真正参与本地绘制的网格检查 |
| #21 实体商店、通路、第十航点 | 已在 v11 主线接入，本版继续保留 |
| #21 商品文字、余额/差额、售鱼提示 | v12 已采用，本版保留原单钱包逻辑 |
| #21 经济测试 | 原测试继续执行，没有另建一套经济系统 |
| #21 女店员、Resources 加载与去重 | 恢复同学分支 FantasyGirl 资源，适配 v13 脚本 GUID、加载链与原站位 |
| #21 原导入/预览工具 | 不原样恢复会切换场景/退出编辑器的旧工具；使用独立 Preview Scene 的限定资源构建器 |
| #21 文档中提及的点头/动画 | 最终原 PR 并没有实现，不能当作本版漏掉的既有功能 |

#20、#21 为关闭但未直接合并；其有效内容通过主线和 #22 选择性吸收。不要整体覆盖旧分支，否则会倒退已经修复的边界。

## 工作目录整理

本机正式开发目录统一为 `BoatAttack/`，可执行版统一为 `BoatAttack/Builds/`。

- v11、v12 的完整可执行目录已移回 `Builds`；较早版本和预览集中到 `Builds/Archive`，不删除独有历史版本。
- 17 个旧子任务工作树及 v12 临时工作区共 **18 个**，均先备份独有内容/二进制补丁、逐文件校验 SHA-256，并创建 Git 提交锚点，再清理重复工作树与可重建缓存。
- 开始整理前主目录的 v10 未提交修改保留为补丁和命名 stash，没有覆盖到 v13。
- 其他参考项目、SDK、设计稿保留在作业目录 `References/`；问题截图保留在 `Archive/Feedback-v012/`。
- 本机完整移动/恢复记录在作业目录 `Archive/Maintenance-20261009/manifest.json` 和 `目录说明.md`。这些本地备份不上传 GitHub。

## 验收记录（持续更新，以最终结果为准）

Claude 草稿阶段的历史记录（模型为 CoastalClerk）：

- 女店员 Unity 资源构建、Resources 路径、重复生成、站位、朝向、尺寸、LOD 和材质验证通过：`Logs/usv-validation-d6aba2628dcf4ccbac8dc0cfa15a184d.log`。
- 第一人称正式专项通过：`Logs/usv-validation-5c836e028e8b4f069812428530d1e85b.log`。
  - 石镐、铁镐各 1,501 个手部顶点，深入柄内超过 8 mm 的采样点均为 **0**。
  - 望远镜 3,002 个手部顶点，深入超过 8 mm 的采样点为 **4**，前物镜区域为 **0**；不将这项结果写成“所有顶点零穿模”。
  - 16:9、16:10、21:9、5:4 的平视握持、俯仰、举镜/放下、第三人称及暂停恢复检查通过。
- 第一轮完整回归：`Logs/v013-validation-1201ed6bbac5404d81f11fa8b531e38a/results.json`，**20 项中 19 项通过**；`FishingInteraction` 入箱夹具失败，因此当轮 Windows 构建与 Player 捕获均按门禁跳过。该历史轮次不能称为完整通过。后续已有箱沿表面投影修正，本次在不改动该修正的前提下重新验证通过（见下方）。

### 2026-10-10 原店员接入验收

- 同学原分支 5 个 LFS 二进制资源均按上游 SHA-256 校验，保留原资源 GUID；仅将 prefab 的脚本引用适配到 v13。无 Git 未合并项，其余 188 个已记录的代码文件未被本次修改。
- 规则检查：捕鱼/经济 **1,442 项通过**，输入 **62 项通过**。
- 店员资源专项通过：`Logs/usv-validation-f8eef7d561fe4baf8a86035454cf956d.log`。包括单实例、原站位、面向顾客、两级 LOD、完整三贴图和材质。
- 交互专项通过：`Logs/usv-validation-c086262b1fe4497ab39d7534ed81f0b4.log`。既有箱沿修正通过 12° 横倾下的入箱、取消、重试、只结算一次及 v8 存档回归。
- 本轮相关 Unity 回归、构建结果见 `Logs/v013-cashier-20261010/results.json`；此轮没有重跑地图烘焙及其他未改动的全量套件，不把之前失败轮次改记为通过。
- Windows 构建入口增加原店员资源预检，避免缺失 prefab/材质时打出没有店员的成品。

- Windows 构建成功：`Builds/FishingPrototype-v0.13.0/FishingPrototype.exe`，日志 `Logs/usv-validation-37c8d981552a4dac8682fde19021e1e9.log`。
- Player **23 场景冒烟通过**，包含店员面部视野/视线和正常存档、备份、偏好设置隔离检查：`Logs/desktop-acceptance-c2d3df8cb9364122828b500de852ed7d/player.log`。
- 实际商店相机图已生成并检查：`Logs/first-person-4136f56ebb53479d97a9cd0b518a33e5/12-shop-attendant.png`，原紫发店员可见且面向柜台。完整场景资源/视线检查及第一人称专项通过，日志见 `Logs/v013-cashier-20261010/ShopCamera-settled.log`。
- 补拍前第一人称复测两次在手册 65° 俯视处失败；夹具将俯仰后的真实等待由 1 帧改为 3 帧，保留 35 mm 阈值并输出误差，最终该项为 30.68 mm。没有改动生产握持逻辑或移除断言。
- **完整 HUD Player 捕获未通过**：本轮隐藏窗口 1280×720 捕获缺少 HUD 探针，保留失败日志 `Logs/desktop-acceptance-3bb995650f5a454bafeae90758ab3ea1/player.log`；其余两种画幅未继续。相机图不包含 IMGUI，不能冒充完整 HUD 截图。原 `Capture` 命令仍保留可见窗口模式，供手动运行验收。
- 本版通过功能分支提交 Draft PR 审查，不自动合并。无关本地改动保留；修改前备份与上游资源清单在作业目录 `Archive/V013-cashier-before-20261010-0948/`。

## 复现

先关闭使用本项目的 Unity 编辑器及 FishingPrototype，再在正式项目中执行（其他项目编辑器可保持运行）：

```powershell
# 常规快速回归
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-V013Validation.ps1 -Stage Quick

# 完整回归；全部逻辑/Unity 检查通过后才构建，再运行 Player
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-V013Validation.ps1 -Stage Full

# 仅在重新加工女店员素材时重建限定目录内的材质和 prefab
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite FishingCashierBuild
```

`Full` 最多包含 25 步：两套纯规则、18 个 Unity 套件、Windows 构建、Player smoke，以及 1280×720、1280×1024、1680×720 各 **23 个场景**的完整 HUD 捕获。每轮使用唯一日志和 JSON 汇总；脚本读取源码版本/场景数，避免误启动 v12 的 exe。

`FishingMap` 会重新烘焙地图；验收前备份现有资源，审阅后恢复非本次功能的重烘焙/重序列化噪声。测试专用的握点搜索代码已移出 Assets，正常回归没有跳过穿透断言的环境开关。

## 仍需单独验收

PICO 头显、真实控制器握持、人工键鼠完整流程、所有动画角度的像素接缝、长时间帧率和晕动感。桌面自动捕获不等于这些项目已经通过。

## PR 审查证据

![原店员在实际柜台中的相机图（不含完整 HUD）](Images/V013/shop-attendant.png)

此图来自本轮实际场景相机捕获，不是素材展示渲染；仅证明店员外观与站位。Unity 专项和 Player 冒烟结论见上方，完整 HUD 捕获失败仍保留。
