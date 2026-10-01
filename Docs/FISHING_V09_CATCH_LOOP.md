# v0.9.0 — 实体抄鱼、快速收纳与 XR 码头控制台

本轮沿用“驾船捕鱼 + 岛屿调查”的方向，不增加枪械、鱼群物理、地图、泛化任务或第二套经济。保留已有休息/蓄力/冲刺/跃水机制，深化鱼到船边之后的交互。

## 本轮范围与不等价声明

已实现：稳定待抄、网口覆盖后抬网、普通鱼倾网快速入箱、可选手持检查、传说鱼测量后确认放生、近场单鱼状态、世界空间交易/升级/导航/暂停控制台，以及 Android-only PICO OpenXR 接入配置。

**本轮没有完成**：PICO 实机验证、岸上 VR 移动、VR 背包、手动插入信标线圈与旋钮交互。原桌面信标任务保留，新的入箱结算仍能记录真正的北岛捕获来源；不能把“支持 XR 输入 / 成功打包 APK”当成整条信标路线已可在头显完成。

## 鱼获处理：不需要三只手

1. 斗鱼仍是右手持竿、左手摇轮；已有冲刺/跃水规则不重复增加。
2. 到船边进入稳定待抄状态：生产流程暂停待抄倒计时，不再要求同时收线。鱼和提示保持可见，竿可回架。
3. 左握键拿起靠近左侧的短柄抄网（网口中心距握柄 1.2m）；XR 待抄位置在左侧，鱼箱接收圈前移，避免长柄网需要第三只手或过度后伸；网口覆盖鱼体、稳定片刻后小幅抬起，才创建待处理鱼获。
4. 普通鱼有两条路径：
   - **快速收纳**：保持鱼在网里，把网移到箱口上方并轻倾；短暂稳定后直接倒入。满箱时仍保留鱼，不丢弃、不虚增图鉴或任务进度。
   - **可选观察**：右握键近距拿鱼，查看外观和重量；放到鱼箱接收圈松手即可入箱，其他位置松手回安全托盘。
5. 传说鱼不出售：拿起或使用“Store / measure”测量，再用 Release 并再次确认，才放生、记录和发放原有赏金/纪念。倾网快捷入箱不适用于传说鱼。

首次发现、珍珠变体或体型纪录会提示观察，但普通鱼不被迫重复整套仪式。网捕和阶段切换可请求短震动，不要求大幅挥动或快速甩动。

### 桌面等价操作

- `E`：在待抄阶段开始辅助抄网动画（仍通过同一套网口/抬升几何判定）；有待处理鱼时拿起/放回。
- `F`：普通鱼入箱；传说鱼测量，**不会自动卖出或直接放生**。
- `X`：请求放生，3 秒内再按确认。
- `R`：安全返港；待处理鱼仍保留，可在港口整理鱼箱。
- `Tab`：港口交易台；`I` 背包、空格跳跃、Ctrl 蹲行、S 倒船等 v0.8 操作继续保留。
- 有待处理鱼时不允许新抛竿或普通起航，避免覆盖未结算鱼获。暂停、失焦和追踪丢失会停用抓取并将鱼停放在安全位置。

桌面的辅助动画不是物理鼠标或头显追踪证据。

## 结算、任务与存档

`FishingCareer` 持有至多一个 `PendingCatchData`，其中有唯一鱼获 ID、原捕鱼区域/位置和测量状态。网口、手持模型、UI 只提交动作，不拥有库存或奖励。

- 抄入网中不会自动入箱、加金币、发赏金或推进信标捕获凭证。
- 入箱才产生普通鱼库存，并按**捕获时**保存的区域/位置判断北岛主线凭证；带鱼返港不会把真实捕获来源改成港口。
- 普通放生保留图鉴/体型纪录和普通放生计数，不支付售鱼收入，不计作订单货物或入箱凭证。
- 传说鱼必须先测量再确认放生；保留原赏金与金帆纪念，永远不进入普通售卖鱼箱。
- 所有成功结算都用同一 ID 去重，反复触发、满箱拒收或恢复存档不能复制鱼或奖励。
- 新档为独立 `boatattack-fishing-v7.json`；只读迁移优先 v6、再 v5、最后旧 v1 路径，不覆盖旧版本档。
- 待处理鱼退出重进后恢复到船边安全处理位置，不恢复不可靠的手柄瞬时姿态。
- `hasPendingCatch` 明确区分真实待处理鱼与 JsonUtility 对空内联对象生成的空结构。

## 世界空间控制台

一块固定在船只参考系的面板，右手指向、扳机选择，包含：

- Accept / mark order：标记现有常驻捕鱼订单；不是新建另一套强制接单系统。
- Deliver order / Sell unreserved fish：复用原有交付与保留委托鱼货规则。
- Upgrade line：复用原有价格/等级，购买后竿上等级标记变化。
- Next waypoint / Anchor / sail：只循环港口与已解锁渔点，给出左右转向角和距离；不把头显玩家导向尚不可下船的岸上航点。
- Store / measure fish / Release fish：处理当前鱼获；售卖、交付、升级和放生需要确认。
- Pause / resume：无需键盘暂停和恢复。

价格、鱼箱和待处理 ID 变化会使旧确认失效。禁用按钮也消费指向它的点击，不把事件穿透成抛竿。UI 占用扳机后重置工具按键状态，离开 UI 时仍按着扳机不会误抛。

控制台用 ASCII 标签和 Unity 内置字体，避免在未核验中文字库的 Android 设备出现方框。中文桌面界面仍保留。旧只读 XR 仪表不再和新面板重叠；现有鱼行为/张力提示在新面板显示。

## XR 接入候选与设备前提

- Unity **2022.3.62f1 / URP 14.0.12** 不变。
- 官方 PICO OpenXR Plugin，package.json 为 **1.4.1**，固定 Git 提交 `3aa3e62bff41df618529eeb60ff02c29a515dafe`。
- Unity OpenXR **1.8.2**，XR Management **4.4.0**；依赖锁文件一并提交。未导入项目外面向 OS 6 的 SDK 包。
- 仅 Android 指定一个 OpenXR loader，启用 PICO 基础扩展、PICO 4 / 4 Ultra 手柄 profile；Windows 保持无 loader 自动启动。
- 使用真正的 XROrigin 和 Input System TrackedPoseDriver。只移动稳定船只参考系，不手工覆盖 HMD 局部跟踪姿态，不传递船体俯仰/横滚。
- 初始 Android 候选为 **ARM64 / IL2CPP / Vulkan / MultiPass**，包名 `com.ruanyueheng.tidefin.vr`。这是可构建起点，不是已经通过性能验收的最终渲染方案。
- 官方仓库 README 对 v1.4 系列列出 **PICO OS 5.13.0+** 要求；本机未检测到已授权连接设备，实际固件/双眼画面/手柄映射仍需核验。
- 本地开发未使用 PICO Platform 网络服务，不伪造 App ID。配置仅将 SDK 新建资产里的 null App ID 归一化为空串，避免其预处理 `.Trim()` 空引用；若启用了应用授权检查而缺少真实 App ID，构建会拒绝，不绕过授权。未启用 SDK 的自动精彩片段录制选项。

参考来源：
- [官方 SDK 固定版本](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/tree/3aa3e62bff41df618529eeb60ff02c29a515dafe)
- [package.json](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/package.json)
- [官方 README 的系统版本要求](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/README.md)

## 水面与近场约束

没有为新增鱼获注册 Gerstner 水面采样点，没有扩大全海湾物理对象数量。鱼获仅一个近场运动学刚体/触发体，入箱后成为数据与原有静态陈列，不给船体或相机施加冲击。

现有水系统在 stereo camera 下跳过平面反射和焦散；玩法使用清晰的鱼体、浮标、鱼线、文字与短震动，不依赖这些桌面特效来判断待抄位置。入网与倒箱判定使用船只相对空间，航行/波浪的整体位移不算一次“快速挥网”。

## 构建与复跑

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingXRSetup
./Tools/Run-UnityValidation.ps1 -Suite FishingCatchCapture
./Tools/Run-UnityValidation.ps1 -Suite FishingExpeditionCapture
./Tools/Run-UnityValidation.ps1 -Suite FishingShore
./Tools/Run-UnityValidation.ps1 -Suite FishingField
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite Regression
./Tools/Run-UnityValidation.ps1 -Suite PlayMode
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke -Player Builds/FishingPrototype-v0.9.0/FishingPrototype.exe -Session v090-review
./Tools/Run-FishingAndroidBuild.ps1
```

Android 工具拒绝非 ASCII 项目路径，包装脚本会创建并验证一个临时盘符别名，结束后仅移除本次拥有的别名，**不搬动、不删除工程**。不用手工把大项目挪出当前目录。

Unity 在别名与原路径、Android 与 Windows 之间切换后，可能留下失效的局部类脚本导入缓存。构建和编辑器预检会核对并强制刷新 `FishingPrototype` 的 MonoScript 绑定；找不到真实场景控制器或存在缺失脚本时直接失败，而非打出空场景后声称成功。

## 验收口径

纯规则测试覆盖未结算鱼获、唯一结算、满箱恢复、放生/测量、稳定待抄、几何抄网、倾网快速入箱与按键恢复。Unity 场景测试使用生产网具 Transform、箱口几何、实际 UI Collider 射线和确认命令；输入姿态由测试提供，不是假装连接了头显。

**APK 成功、相机渲染图和这些自动测试，均不能替代真实 PICO 验收。** PR 保持草稿。人工需确认双眼画面、文字大小、左右手可达性、抓取/倾网/测量、交易、暂停和追踪恢复，并实测性能。完整的岸上 XR、背包与信标插槽/旋钮留待独立迭代。


## 本轮验证与交付记录（2026-09-30）

- 纯捕鱼/背包/鱼获事务/几何/输入恢复规则：**962 项通过**；独立输入/渲染策略：**62 项通过**。
- Unity `FishingCatchCapture`：通过。涵盖真实网具 Transform、待处理鱼持久化、满箱保护、可选观察、倾网快速收纳、名义坐姿可达范围、世界空间 UI Collider 射线、确认购买、暂停恢复和目标导航过滤。
- 原 `FishingExpeditionCapture`、`FishingShore`、`FishingField`、`FishingSmoke`、`Regression`、原竞速 `PlayMode` 回归通过；北岛入箱凭证与独立任务提示保留。
- 非 Development Windows v0.9.0 构建成功，独立 Player 的 Acceptance Smoke 通过。
- Android **ARM64 / IL2CPP** APK 构建成功。检查 APK 元数据：包名 `com.ruanyueheng.tidefin.vr`、versionName `0.9.0`、minSdk 29、targetSdk 33、ABI `arm64-v8a`，包含 PICO VR/头部追踪声明。**没有连接设备，未安装或声称真机通过。**
- Windows：`Builds/FishingPrototype-v0.9.0/FishingPrototype.exe`。
- Android：`Builds/FishingPrototype-v0.9.0-Android/FishingPrototype.apk`。
- 构建、原始日志和诊断文件不提交仓库；仅上传源码、必要配置与两张目视核对的世界空间相机渲染图。

可在本工作区复核的最终日志：
- 网具/控制台：`Logs/usv-validation-bfe634572c13485aaf638de0515dfeea.log`
- 信标任务：`Logs/usv-validation-2ff90b6da53d47b6ae89b0945ff283eb.log`
- 登岸：`Logs/usv-validation-cf6995f1fbc049edac55d0094c1a1353.log`
- 背包/陆地：`Logs/usv-validation-21fa9c1ddec04f6f90e70cf599a3955a.log`
- 捕鱼冒烟：`Logs/usv-validation-fba8b6ab5d9e443883ba6fc13cdd7b2d.log`
- 组件回归：`Logs/usv-validation-513ab905afb24940a27c824281e5ad92.log`
- 原竞速流程：`Logs/usv-validation-9b97045565654a12bb5b843d5d0c6b12.log`
- Windows 构建：`Logs/usv-validation-4fc9060530bf4a0eb77b9e40d9b26616.log`
- Windows Player：`Logs/desktop-acceptance-36294aea04a84d3d853f7d8722c6d6d5/player.log`
- Android 构建：`Logs/usv-validation-c7f5e1fafed84ee98ff970e79db72616.log`
- 回到原项目路径后的结构检查：`Logs/usv-validation-87cfe0a8fbca404ab8ecd914e0a80302.log`

额外修复了旧 Gameplay Ingredients 欢迎窗口在隐藏批处理域重载时抛出空引用的问题：只关闭其自动欢迎入口，手动菜单仍可用；不屏蔽测试异常。ProjectSettings 只保留 Android 必需参数和版本号变化，没有混入 iOS、VisionOS、Switch 等无关重序列化。Android 的 Burst AOT 文件为工具升级后的等价策略表示，未更改 Burst 包版本。

![世界空间控制台：已检查文字与布局；射线姿态来自测试，不是真机操作照片](Images/CatchLoop/01-dock-console.png)
![待处理近场鱼获与抄网：相机渲染，不代表头显或软体物理验收](Images/CatchLoop/02-pending-fish.png)
