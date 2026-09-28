# v0.4.2 聚焦 HUD 与航海图审查

日期：2026-09-28。基线：已合并第四版审查的 `main`（`c1162229`）。本次保留原有 v0.4.2 改动，在其上修复并补充回归；分段读取、不开并行代理、各验证进程依次运行。

## 变更范围

- 常驻信息移到屏幕边缘，中央保留钓鱼视野；委托/传说详情默认关闭，以 **H** 展开。
- 航海图固定北向、以玩家为中心、保持统一比例；**M** 收起/展开，范围按钮循环 80/160/320 m，点击港口或渔区标记选航点。
- 超出范围的标记保留方位；虚线仅表示目标方向，不是避障航线，也不是实测海岸地图。
- 港口交易台改为靠港低速时用 **Tab** 或按钮显式打开，与详情卡互斥；**E** 快速交易、数字键升级仍可使用。
- 停船时自动收起地图和可选卡片；起航/救援回港恢复地图并关闭卡片。
- 输入拦截依据实际可见的地图、详情、交易台、观察按钮；桌面 HUD 不在活动 XR 设备上绘制。
- Unity、包版本、场景、Prefab 和存档格式均不修改。仍是桌面原型，不代表完成 PICO 适配。

## 审查修复：捕获卡不能吞掉后续操作反馈

原实现只要捕获卡还在显示，就隐藏所有普通消息。捕获后立即换饵时，4 秒的换饵提示会在 5 秒捕获卡消失之前过期；其他操作反馈也会被延迟或隐藏。

先将原有显示判断抽成与实际绘制共享的 `NoticeText`，加入真实捕获后立即换饵的回归。修复前 Unity 冒烟确实失败：`Fresh catch toast suppresses subsequent bait feedback.`

修复后给捕获消息加明确标记，只去除捕获卡的重复文案。后续 `Notice` 默认清除该标记，同一帧内的新操作反馈也能显示；存档警告继续优先显示。不依赖中文前缀匹配或浮点时间先后判定。

## 验证记录

- 原改动基线：钓鱼纯逻辑 **323 项通过**（含新增海图投影），输入/帧预算纯逻辑 **62 项通过**，Unity `FishingSmoke` 通过。
- 缺陷复现：新增捕获后换饵回归在修复前失败，退出码 1。
- 修复后钓鱼纯逻辑：**323 项通过**；输入/帧预算纯逻辑：**62 项通过**。
- Unity **2022.3.62f1** `FishingSmoke`：**通过**，包括新增消息回归及完整捕捞/交易/传说/存档流程。
- v0.4.2 Windows Release 构建：**通过**；旧版构建目录保留。
- 本次新构建的独立 Windows Player：**通过**，以 `-batchmode -nographics -fishing-autoplay` 运行，确认版本与新增回归标记，退出码 0。
- 新增运行回归：四种视口下地图展开/收起、详情/交易/观察按钮显示条件、交易台三种订单高度的点击拦截；暂停禁止切换、停船收起卡片、离港禁止打开交易台。

### 串行复现命令

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false -p:UseSharedCompilation=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false -p:UseSharedCompilation=false
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild -TimeoutSeconds 3600
```

本地证据记录在忽略目录 `Logs/codex-v042-fishing.txt`、`Logs/codex-v042-input.txt`、`Logs/codex-v042-smoke.txt`、`Logs/codex-v042-build.txt`、`Logs/codex-v042-player-*.log`；缺陷复现对应 `Logs/usv-validation-73e673523adc4f4b991ed8ddb6176934.log`。

等待前一个命令结束后再运行下一个。构建产物为 `Builds/FishingPrototype-v0.4.2/FishingPrototype.exe`，旧版目录保留；日志、构建产物与自动测试存档不提交。

## 验收边界与合并前清单

自动化直接调用游戏操作，HUD 回归检查共享布局和显示状态，不会模拟真实键鼠，也不能证明字体渲染、操控手感或帧率。

- [ ] 在 1280×720、1366×768、宽屏实际查看完整 HUD：文字不裁切、鱼获提示不遮挡、中央钓鱼操作可辨认。
- [ ] 实际点击地图、观察、详情、交易/升级按钮，确认不会误抛竿/收线；测试 M/H/Tab、暂停/图鉴恢复及右键环顾。
- [ ] 查看三行订单、满级升级、满鱼箱及捕获后换饵/起航的反馈。
- [ ] VR loader、控制器/头显交互、双目显示、真机舒适性和持续性能另行验收。

人工验收完成前保持 Draft PR，不自动合并。
