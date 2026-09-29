# 怪鱼航线 — 桌面试玩（当前 v0.5.0）

新增 **实体角色、G 下船/登船、四区海岸调查**，详见 [v0.5.0 海陆探索说明](FISHING_V05_SHORE.md)。岸上 WASD 行走，靠近调查台 E 记录，返港 E 领奖。

历史版本：本次聚焦 HUD、航海图与验证边界见 [FISHING_V042_REVIEW.md](FISHING_V042_REVIEW.md)，第四版成长系统审查见 [FISHING_V04_REVIEW.md](FISHING_V04_REVIEW.md)。天气、生物与新模型见 [FISHING_LIVING_BAY.md](FISHING_LIVING_BAY.md)。第三版基础玩法、UI 与 PICO 适配边界见 [FISHING_V03.md](FISHING_V03.md)。第二版记录保留在 FISHING_V02.md。

本版本是基于 BoatAttack 的独立捕鱼灰盒，不是《How to Fish》的资源移植。鱼、鱼竿、抄网、鱼箱、市场小站和提示音均由本项目代码生成；海岛、船只、水面和原有环境素材沿用 BoatAttack，保留其授权与署名。

## 如何运行

- Unity 2022.3.62f1：菜单 **BoatAttack > Fishing > Open prototype**，然后按 Play。
- 或直接打开 `Assets/scenes/Fishing/fishing_Island.unity`。
- Windows 试玩程序构建位置：`Builds/FishingPrototype-v0.5.0/FishingPrototype.exe`。运行时必须保留相邻 Data 文件夹和 DLL，不能只复制 exe。
- 普通启动是玩家模式；没有自动驾驶/自动收鱼。命令行自动演示只用于验收和截图。

## 第一段体验

1. 从青色市场附近出发，驶向黄色浮标圈。
2. 减速到 10.8 km/h 以下，在渔区按 **E** 停船。
3. 点击鼠标左键抛竿，听到咬钩提示后再次点击提竿。
4. 按住左键收线，张力过高时松开；不要一直放线。
5. 鱼到船边后，指针进入绿色区域时按 **E** 抄鱼。
6. 普通鱼进入初始六格鱼箱（可升级至十格）；传说鱼测量后放生领奖。可以继续抛竿，或 **Q** 收起工具继续航行。
7. 返回青色市场，低速靠近后按 **E**：订单已集齐则交付并领奖，否则出售非预留鱼货。按 **Tab** 打开交易台查看订单和三条升级路线；首级鱼线需要 30 金币。

## 控制与便利操作

- W / 上箭头 / 空格：前进；A、D / 左右箭头：转向。
- S / 下箭头 / Shift：刹车。当前发动机不支持倒车。
- E：附近场景交互（渔区停船、抄鱼、港口交付订单或出售）。
- 鼠标左键：抛竿、提竿、按住收线；松开放线。
- Q：取消当前捕捞并起航。
- T：看向 28m 内的野生生物进行观察；日志可切换自然观察页。右上工具栏可打开日志、设置和切换视角。
- B：换饵（捕捞进行中不可换）；N：切换航点；J：图鉴与订单日志；O：在港口尝试提交订单。
- 1 / 2 / 3：在港口升级鱼线、卷线器、鱼箱；U 仍是升级鱼线的快捷键。X 全部出售会一并卖掉预留的委托鱼。
- R：救援回港，已入箱鱼和金币保留。这是灰盒便利功能，尚未做航行成本平衡。
- C：第一/第三人称切换；按住右键环顾；V 视线回正。
- H：展开/收起委托与传说详情；Tab：靠港低速时打开/收起交易台。两种面板互斥、默认关闭。
- M：展开/收起航海图；点击范围按钮切换 80/160/320 m，点击地图标记选航点。北向固定、目标线仅指示方位，不负责避礁。停船自动收起地图，起航恢复。
- F / Esc：暂停/恢复。关闭窗口退出桌面试玩。

## 已实现范围

- 原海岛上的三个渔区和三种普通鱼：珊瑚鱼、飞梭鱼、鼓鼓鱼；另有满足海域/夕照/鱼饵/装备条件才能遇见的金帆旗鱼。
- 等待、咬钩、收线/放线、抄鱼、成功/挣脱的完整状态。
- 稀有变体、克级体重与个人纪录、委托预留/交付、普通/全部出售，以及鱼线、卷线器、鱼箱三条各两级升级路线。
- 本地进度保存及备份恢复；保存的是真实库存/金币数据，鱼箱里的模型只是表现。
- 独立相机与 HUD、原创建模和程序化提示音。
- 原竞速默认行为保留；新增 Boat 的运行时竞速参与开关，捕鱼不伪装成无限圈比赛。

## 当前有意简化的部分

- 这是桌面灰盒，不是完成的 PICO VR 游戏。没有配置新的 XR loader、头显相机或抓取系统。
- 鱼线、鱼挣扎与入箱动作使用简化规则和受控动画；不是全物理绳索、自由抓取或复杂鱼群生态。
- 上岸已支持四处安全登陆点及八项调查；暂不含游泳、攀爬、多人、武器、首领、船上自由行走和完整剧情。
- 鼠标/键盘是本版主要控制；手柄适配存在部分按键通路，尚未人工验收。
- 捕鱼站保持水平位置/航向，但保留垂直水面浮力；仍需真机舒适性测试后才能设计 VR 站位。
- 原水面采样注册表长期累积/清理风险仍见 PROJECT_HEALTH_CHECK.md；没有给每条装饰鱼注册浮力采样点。

## 保存位置

Windows 默认在 Unity 的 `Application.persistentDataPath` 下，文件名 `boatattack-fishing-v1.json`，旁边有 `.bak` 备份。v0.5.0 内部格式为 version=4，可迁移 version=1/2/3，并持久化海岸调查与奖励记录。请勿用旧版程序写回第四版存档，回退前先保留副本。自动测试和截图用独立测试路径，不读写玩家进度。UI 会提示保存权限错误；不要把存档或用户目录提交到仓库。

## 重建与验证

```powershell
# 无 Unity/无头显的逻辑测试
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false

# 已提交生成后的场景，普通克隆无需重复 Generate。
./Tools/Run-UnityValidation.ps1 -Suite FishingGenerate
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite PlayMode
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild -TimeoutSeconds 3600
```

FishingSmoke 在真正的 Play Mode 中自动完成三种鱼捕获、入箱、出售、升级、进度重读和备份恢复。它通过显式测试驱动调用游戏操作，不等于真人键鼠、VR 或帧率验收。

构建先生成 Addressables 内容，再打包只以捕鱼场景启动的 Windows Release Player（v0.3.1 起关闭 Development）。构建、截图和本地日志不进入 Git；朋友通过源码和上述命令重建。


## 实现与构建说明

- 本版是独立编辑器场景和独立 Windows 试玩程序，尚未把捕鱼按钮接入原竞速的游戏内主菜单。
- 捕鱼渔区采用地表射线和水平净空检查布置，并保留区域间距；这不能代替所有航线的人工关卡测试。
- 桌面目标帧率上限为 60；不是已经完成性能基准，也不代表 PICO 能达到同样表现。
- 首次完整构建触发既有 URP 14/Burst 设置的序列化升级，并生成空 XR 配置；没有升级 Unity/包版本或配置 PICO loader。
- 临时日志、截图、可执行程序和 Addressables 构建状态不提交到 Git。不要从 Windows 包直接推断 Android 或 VR 兼容性。


## 第二版历史验收（2026-09-27，第三版见 FISHING_V03.md）

- 73 项捕鱼/鼠标、手柄与环顾恢复逻辑断言、62 项原输入/渲染策略断言；GitHub CI 执行同一组代码。
- 捕鱼 Play Mode 自动验收覆盖三类鱼、即时入箱、防重复抄取/出售、升级、存档和坏主存档的备份恢复。
- 原竞速的两次比赛启动/返回菜单、14 项组件回归及场景结构检查独立运行。
- 独立 Windows Player 使用 Direct3D 11 正常渲染，已查看捕鱼与渔港截图。首次隐藏窗口截图为黑屏，因此不拿隐藏截图作为画面验收；使用实际显示窗口复核。
- 最终自动演示没有模拟真人手部或 OS 键鼠操作，操控手感、游戏平衡、长时间稳定性和 PICO 真机仍须用户试玩。
- 抄鱼成功立即提交库存，避免同帧再次抛竿丢失鱼获；鱼箱模型复用同一份鱼网格，避免反复入箱累积网格资源。
