# v0.4.3 桌面验收交接

## 范围与边界

v0.4.3 增加默认关闭的桌面验收入口；不接入 XR、不升级 Unity、不扩充玩法。
**自动状态验证、完整 HUD 截图与真实键鼠手感是不同证据，不能互相替代。**

普通启动不挂载验收组件。显式 `-fishing-acceptance` 后，存档写入 Unity temporaryCachePath 下的 `FishingAcceptance/<session>/`：
- `playthrough.json`：真实操作的连续进度，同一 session 退出重进可续玩。
- `fixture.json`：F5/F6 预置场景，切换时重新生成，绝不覆盖 playthrough。
- 验收模式不写普通玩家的舒适/静音偏好；临时缓存可能被系统清理，不应作为正式存档。

## 运行（仓库根目录，串行）

```powershell
# 先构建 Unity 2022.3.62f1 Windows player
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild -TimeoutSeconds 3600
# 无图形状态检查：12 场景、真实规则、隔离存档、偏好、冻结/恢复
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke
# 以下各条会打开可见游戏窗口；不要最小化，必须逐条完成
./Tools/Run-FishingAcceptance.ps1 -Mode Capture -Width 1280 -Height 720 -Session capture-720
./Tools/Run-FishingAcceptance.ps1 -Mode Capture -Width 1366 -Height 768 -Session capture-768
./Tools/Run-FishingAcceptance.ps1 -Mode Capture -Width 1920 -Height 810 -Session capture-wide
# 手工验收；退出后再运行同一命令续玩
./Tools/Run-FishingAcceptance.ps1 -Mode Manual -Session manual-v043
```

脚本拒绝已有玩家进程；自动运行要求退出码 0 + 本轮 PASS。截图还要求 12 张文件、请求分辨率和 HUD 像素标记全部有效。证据在独立的 `Logs/desktop-acceptance-*/`（Git 忽略），manifest 的 physicalInputTested/visuallyReviewed 默认 false，自动截图不会伪造人工验收结论。

F5/F6 切换 12 个场景；F7 解除/恢复捕捞计时定格；F8 保存完整 HUD；F9 返回自己的实玩进度。默认 FreePlay 使用真实输入。预置状态包括咬钩、高张力、抄网、捕获反馈、传说跃水、三行委托、满鱼箱、满级装备、详情、图鉴、暂停。预置状态切换会重置位置和场景，不算完成了出港航行或捕捞。

## 仍须人手验收（未勾选不代表通过）

- [ ] 三个分辨率下实看中文裁字、遮挡、抄网提示、三行订单及满级按钮。
- [ ] 鼠标地图点击/缩放不误抛竿、不误收线；验收横条不穿透工具输入。
- [ ] M/H/Tab/J、暂停恢复、切出再切回、按住鼠标/手柄恢复时不误触。
- [ ] 同一 FreePlay session 完成“出港 → 捕获 → 交付 → 升级 → 退出重进读档”。
- [ ] 记录实际操作的设备、分辨率、问题/反馈；声音、键鼠手感和帧率须另外确认。

PICO 4 Ultra / VR 头部跟踪、手柄、APK 和真机性能仍不在本 PR 验收范围。优先完成以上桌面反馈，再单独推进最小 VR 闭环。

## 本轮验证记录

### 2026-09-29 提交前验证

环境：Windows、Unity 2022.3.62f1；基于 `origin/main` 的 `f8f1f692`。所有 Unity / Player 验证串行执行。

- 输入与帧预算纯逻辑：**62 项通过**。
- 钓鱼纯逻辑：**382 项通过**，其中新增 **59 项**验收参数、路径隔离、场景目录与生产规则预置状态断言。编译警告按错误处理。
- Unity `FishingSmoke`：**通过**，覆盖原有捕捞、交易、传说、天气生态、HUD 与存档/备份回归。
- Unity `FishingBuild`：**通过**，生成 `Builds/FishingPrototype-v0.4.3/FishingPrototype.exe`；旧版构建保留。
- 新构建 Player 的 `Run-FishingAcceptance.ps1 -Mode Smoke -Session pr-finalize-v043`：**通过**，退出码 0，并具有本轮 PASS 标记。覆盖 12 场景、计时冻结/恢复、捕获后反馈、四视口验收横条输入拦截，以及预置/续玩存档、普通玩家存档元数据和偏好隔离。
- 新构建 Player 的旧版 `-batchmode -nographics -fishing-autoplay`：**通过**，退出码 0、v0.4.3 版本及完整流程 PASS 标记齐全；日志确认未启动验收组件。
- PowerShell 脚本语法与新增 **7 个 Unity meta GUID** 的唯一性检查：**通过**。

本轮可在本地查看的证据（均被 Git 忽略，不上传构建、日志或玩家存档）：

- `Logs/usv-validation-dd11308b5e424cd4bad02b45d1b06dd4.log`：Unity FishingSmoke。
- `Logs/usv-validation-ae8b3132c1a747e9850447e50affa5f2.log`：Windows Release 构建。
- `Logs/desktop-acceptance-282059e5b2ac4f1aafbb064939edff99/player.log`：独立 Player 验收状态检查。

- `Logs/fishing-finalize-autoplay-50e0f090f12341a1987aff6acf76697a.log`：独立 Player 旧版自动回归。

本轮**未运行**三分辨率完整 HUD 截图、真实键鼠/手柄、声音/帧率和 PICO/VR 验收；也未重跑原竞速专用 `Structure` / `Regression` / `PlayMode` 套件。上述人工清单保持未勾选。远端 CI 结果以 PR checks 为准，不以本地通过代替。
