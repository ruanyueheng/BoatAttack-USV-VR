# v0.12.0 — PR #20 / #21 审查与玩家修复

基线：已合并的 v0.11，`4fccfc763efb0af1e6f6bd182c775d7cb61ed4d6`。固定 Unity **2022.3.62f1**，URP **14.0.12**；存档 schema 仍为 v8，未重置玩家存档。

## 两条 PR 做了什么

- [PR #20](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/20)，`c91159ff`：修正视觉蹲姿降幅（0.42 m → 与胶囊一致的 0.67 m）；普通低位物品按 E 可自动蹲下；桌面持竿握点降低；用局部地面补查和抬升胶囊改善窄缝／低台阶；增加玩家专项和原始说明。没有替换源人物模型，也没有关闭碰撞。XR 手柄挂载参数未变。
- [PR #21](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/21)，`8f609ab5`：包含同样的玩家修复；另有 FantasyGirl 收银员的源素材、LOD、纹理、材质、预制体、导入／预览工具，以及商品名称、装备说明、购买余额和售鱼收入提示、商店经济测试。可进入商店本身已经在主线 v0.11 中，不能把整条分支的历史提交都算成本次新增。

## 合并决定

**不直接合并两条原 PR，改为提交本版修正 PR。**

1. 在真实 Unity PlayerLoop 中，从站立开始低位拾取，然后下一生产帧设为 0.4 s：原代码在 `Update` 消费接触许可时仍读取上一帧站姿骨架，合法拾取被取消。原专项以小时间步手动 Tick、同时允许生产 Update Tick，未覆盖这个边界。
2. 原版把球形补查加入共享 `GroundBelow`，连登陆用的四个精确支撑采样也被扩大。实际物理夹具中，**26 cm × 26 cm 的孤立方桩**被错误判为满足四向登陆支撑。
3. 代码审查还发现蹲姿预测只按剩余权重缩放竖直量、却再次扣完整后移量；以及先强制蹲下可能拒绝原本站着可取的台面物品。球扫掠边缘接触法线也不等于真实表面坡度。
4. PR #21 的 `ArtSource/FantasyGirl/README.md` 明确写明未提供许可证／作者归属。本轮没有权利证明，不能将源文件或派生文件继续纳入新发布版本。其新增独立预览工具还存在非批处理调用会切场景并退出的缺陷；本版不采用这些工具。

## 本版采用与补修

- 保留 PR #20 的三项主要修复与原作者提交记录。
- 自动蹲下后等待真实骨架姿态就绪，再推进动作接触时钟；等待期间焦点、暂停、模态、目标、装备、岸船状态的取消守卫仍生效。
- 蹲姿的竖直／前后预测使用同一剩余权重；优先保留可达且无遮挡的姿态。
- 普通 `Ground`／登陆／设施放置恢复严格的中心射线。仅连续步行使用胶囊半径 60%（当前 18 cm）内的局部射线补查；采用实际表面法线，不再把 sphere contact normal 当地面坡度。
- 地面与台阶净空查询使用可复用 NonAlloc 缓冲；饱和时保守拒绝，不依赖不完整碰撞结果放行。
- 保留真实胶囊、墙体、低顶、陡坡、水层、触发器过滤、接触可达性／视线以及一次性库存事务。
- 从 PR #21 只采用 `FishingHUD.Shop`、`FishingText`、两处交易提示及 `ShopEconomyTests`。没有新增第二套钱包、收费逻辑或收银员 AI。
- 原 `FishingPlayer` 专项的拾取时钟改为仅由生产 Update 推进；新增独立 `FishingPlayerBoundary` 套件，退出时恢复 PlayerLoop、时间步、组件启用状态和临时场景物体。

## 已验证的红／绿对照

日志在忽略目录 `Logs/`，不是将旧报告当成本轮通过证明：

- 原 PR #20 玩家专项：`usv-validation-66804f2195a347bc8232e3e38692d907.log`，通过低位拾取、4 cm 缝／20 cm 台阶、完整墙／水边和默认持竿视野检查。默认姿态中央区域身体顶点为 **2423 → 0**；这是几何代理指标，不是所有视角的像素遮挡保证。
- 补充边界红测：`usv-validation-82e6268bf8bb4a6c82d2eb4be807b8fb.log`，卡顿拾取和窄桩登陆失败，其余 5 组通过。
- 首轮修复后：`usv-validation-415e02b38ced4d18a87ab86601f183c3.log`，最初 7 组全部通过：低檐／模态拒绝、接触取消与重复输入、低帧率生产次序、沿薄墙跨缝和拐角、低顶台阶及蹲行正对照、远岸／水面保护、窄桩与宽平台正反对照。
- 扩展回归又发现起身途中瞬时可达不代表最终站姿可达。现对当前输入所对应的最终姿态做只读预测；`usv-validation-0b55a949d83242068bbfe37eb366e70e.log` 的 **9 组全部通过**，新增起身中重拾与低栏杆后台面拾取，均由生产 Update／LateUpdate 推进。
- 第一轮全套复测共 18 步，17 步通过、`FishingWorkshop` 失败，因此当轮构建／Player 捕获被正确跳过。失败断言仍写死旧的 0.42 m，而人物已改为 0.67 m；按实际胶囊高度差修正测试后，`usv-validation-5bb0ba17c172475493fcfaca99a28afc.log` 的工坊采集／制作／熔炼／放置／存档回归通过。未删除断言或放宽接触安全守卫。
- 捕鱼纯规则：原 PR #20 **1315** 项；加入 PR #21 经济测试后 **1426** 项。输入／渲染策略纯逻辑 **62** 项。这些不等同于 Unity 或设备验证。

## 最终整套验证

`Logs/v012-validation-a2b375ade8d7473492a16e5e74074a58/results.json`：**21 步通过，0 失败**。

- 捕鱼纯规则 **1426** 项，输入／渲染策略纯逻辑 **62** 项。
- **16 个 Unity 套件**：FishingPlayer、FishingPlayerBoundary（9 组）、FishingCharacter、FishingInteraction、FishingShore、FishingShop、FishingTerrain、FishingMap、FishingField、FishingWorkshop、FishingExpedition、FishingCatchCapture、FishingSmoke、Structure、Regression、PlayMode。
- **Windows64 Release 构建通过**：`usv-validation-7a14dfab03cd4eec8131faf47eaceb3e.log`。
- **真实 Player 两分辨率各 15 场景完整 HUD 捕获通过**，不是隐藏窗口／仅相机 RT：`desktop-acceptance-19a2905b5adc4f568252c066ead42603`（1280×720）、`desktop-acceptance-f817d08edad043689a9f2f74cc04ebae`（1280×1024）。两个 manifest 都确认 version=0.12.0、图像尺寸和 fullHudProbeVerified；共 30 张图做了截图索引检查，并单独检查持竿、商店和岸上第三人称画面。未将自动 manifest 的 `physicalInputTested=false` 改成真机通过。
- 构建引起的 Unity 自动重序列化、地图烘焙时间戳等不混入提交；完整副本保存在本地 `Logs/UnityGeneratedSnapshot`。地图 PNG 内容未改变。
- 4 个 PowerShell 验证／验收脚本语法检查通过。旧 `Run-V011Validation.ps1` 的 Player 路径改为读取当前版本，避免新版构建后误启动旧 exe。

### 本版 Windows Player 窗口截图（自动场景）

![v0.12 桌面持竿与完整 HUD](Images/v012-fishing-720.png)

![v0.12 商店商品与余额提示](Images/v012-shop-720.png)

## 复现命令

在本版独立工作区运行，先关闭其他 Unity／FishingPrototype 进程：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-V012Validation.ps1 -Stage Quick
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-V012Validation.ps1 -Stage Full
# 单独复现边界
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite FishingPlayerBoundary
```

`Quick` 包含两套纯规则、玩家专项／边界、人物、接触交互、岸上和商店套件。`Full` 再检查地形、地图、野外、工坊、探险、抄鱼、捕鱼 smoke、原项目结构／回归／PlayMode；无失败时才构建 Windows Player，然后执行 1280×720 与 1280×1024 各 15 场景完整 HUD 捕获。每步记录唯一日志、退出码和 PASS，并写 `Logs/v012-validation-*/results.json`；失败后不能把跳过的构建／捕获称为通过。

`FishingMap` 会重烘焙地图资源，所以必须在隔离副本执行；本脚本不调用 `FishingGenerate` 或重装商店。地图和 Unity 自动重序列化改动需审阅，不应混入纯修复提交。

## 试玩与验收边界

Windows 输出：`Builds/FishingPrototype-v0.12.0/FishingPrototype.exe`，必须保留整个输出目录。常规启动沿用已有存档；自动验收使用 `temporaryCachePath` 的隔离档案。

- **尚未验收**：PICO 头显实机、实体键鼠全流程、每个美术接缝、所有环顾角度、持续性能和晕动感。
- 保留真实手掌在画面下方；不是隐藏全部身体来规避遮挡。
- 过高台阶、过窄通道和真实墙体仍然阻挡，不能承诺任何建模交界都可穿越。
- FantasyGirl 授权待素材提供方补充证明；不将“用户提供素材”视为再分发授权。
