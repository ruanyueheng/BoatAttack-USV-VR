# v0.7.0 — 鱼竿、第一人称双手和登岸视角

## 参考检索（2026-09-30）

实际通过 Sketchfab 公共 API 检索可下载鱼竿模型，并目视检查前两款缩略图。以下为参考候选，未下载或导入其网格；许可证为检索时 API 返回的 CC Attribution，真正复用时还需检查具体版本并署名：

- [Fishing Rod, Rigged and Animated — Ergin ERYILDIR](https://sketchfab.com/3d-models/78991bf44cf54acb8660bef317d72c7a)：23,316 面，具有纺车轮、金属导环、分段握把和动画，结构参考价值最高。
- [Fishing Rod - h8pitts](https://sketchfab.com/3d-models/b379727f867d4109835aff0334904d66): 4,728 faces; red-and-black slim rod, low-poly reference; CC Attribution.
- [Fishing rod — 3DDomino](https://sketchfab.com/3d-models/c26db0cf3ab04060bfc7d14434a1542f)：68,093 面，仅查阅 API 元数据，不能声称已详细审美评估。

上述缩略图只保存在忽略的 Logs/hand-research 中，不随项目转载。实际交付为本项目原创脚本生成的配色统一模型，不需要购买第三方资产或注册账户。

VR 交互参考和证据边界：

- [Real VR Fishing 官方 Steam 页面](https://store.steampowered.com/app/1266470/)：描述抛竿、等待咬钩和收线过程，以及拟真阻力。借鉴将操作分成握持、抛竿、收线几个阶段；官方营销文案不证明具体内部实现。
- [Half-Life: Alyx 官方 Steam 页面](https://store.steampowered.com/app/546560/)：明确描述翻找货架、操纵工具、投掷瓶子等环境交互。借鉴“手与物体动作对应”的原则，不复制游戏模型或声称复现其完整物理系统。
- [Unity XR Hands Interaction Demo 2.5](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.5/manual/samples-hands-interaction-demo.html)：文档展示 pinch、poke、ray、socket 及控制器/手追踪模式切换。当前实现不包含无控制器光学手追踪。
- [XR Grab Interactable](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.5/manual/xr-grab-interactable.html)：参考主/副 attach transform、抓取姿态与释放生命周期设计。本补丁保持现有 provider-neutral Unity XR 输入架构，没有安装新的 XR loader 或完整 XRI 样例。

## 已实现

### 鱼竿
- 锥形碳色竿身、软木分段握把、锁紧环、八级导环与绑线、纺车轮机身、线杯绕线、导线架、独立摇柄。
- RodBody 12,440 三角面 / 一个 renderer，ReelCrank 120 三角面 / 一个 renderer。
- Blender 源文件：ArtSource/FishingGear/FishingGear.blend；生成脚本：Tools/Art/generate_fishing_gear.py。
- 右手握持、左手随摇柄转动、抛竿姿态；鱼线起点在 LateUpdate 后重新绑定到实际竿尖，避免相机转动时脱节。

### 双手及操作
- 原创带腕带、护垫和袖口的手套；每只手一个 SkinnedMeshRenderer、15 个指节，加一个根骨骼。
- 手指弯曲驱动真正的网格变形，不是两个固定图片或手掌方块。
- 桌面：停船时右手持竿，左手持摇柄；Landing 阶段左手改持抄网；岸上 E 保留原任务解析，加入指向反馈，成功取得维修线圈后短暂展示在手上。
- 桌面工具动作与现有 Cast / Hook / Scoop / Expedition 状态共用，不再另开一套奖励或存档逻辑。
- XR 控制器：通过 Head/LeftHand/RightHand 的实际跟踪姿态计算世界空间双手；右 grip 近距取竿/松手放回，右 trigger 驱动原抛竿/提竿/抄鱼，左 grip 在摇柄附近转动才收线。停住手、离开范围或丢失跟踪停止收线；恢复后必须先释放握键。
- 有效抓取/工具动作请求短振动反馈，是否支持由设备决定。

### 登岸转头
旧代码只在右键按住时采样鼠标，且未锁定游标。本轮改为岸上默认鼠标自由转头，不再依赖右键。没有证据将用户遇到的所有相机问题都归因于单一条件，因此仍需实机手动检查。

- G 登岸后直接移动鼠标转头，WASD 仍相对相机朝向移动。
- 按住 Alt 释放鼠标；松开恢复转头。切换恢复首帧不读取旧 delta，防止跳视角。
- v0.7.1 修正：地图是非模态导航 HUD，展开时仍可转头；Alt 操作地图。暂停、失焦、日志/详情或校准拨盘释放游标；上船恢复原船上右键环顾。
- 俯仰限制 ±65°、水平可连续绕转；不改写 XR 跟踪相机。

## 验证与明确限制

已通过的检查会在下方记录，不把桌面自动化冒充 PICO 真机验收。

仍需手动检查：真实鼠标捕获/Alt 释放、不同分辨率与缩放下的 HUD、完整步行路线、PICO 控制器握持方向与摇柄阈值、振动及帧耗。

本轮没有：全身人体重拓扑/完整 IK；物理手掌碰撞和任意物体通用拾取；无控制器指骨追踪；VR 岸上移动。岸上 E 仍是原有范围内语义交互，线圈展示是成功领取后的反馈，不是新增自由抛掷物理系统。抄网的 XR 操作仍使用扳机时机确认，不宣称已实现真实兜鱼碰撞。

既有地表灰白问题仍在，未混入另一条海岸地形 PR 的改动。

## 本轮自动验证记录

- 纯规则：**833 项通过**（新增 19 项手部输入/鼠标捕获策略），原输入/渲染策略套件另行运行。
- FishingExpeditionCapture：通过；验证岸上无右键也能转动、转头不移动角色、暂停不转动、非有限输入拒绝；双手 16 骨骼、实际手指网格变形、第一/第三人称显隐及全部旧信标任务回归。
- FishingSmoke：通过，原捕鱼、交易、天气、相机、HUD、存档流程未回归。
- 上述测试中的相机输入为程序化输入，不等于人工鼠标或头显操作。
- 原模型 FBX 在 Unity 中导入并经真实相机渲染。截图使用实际视角，不是 Blender 效果图。

![第一人称持竿与摇柄](Images/FirstPersonHands/04-first-person-rod.png)
![岸上双手](Images/FirstPersonHands/05-shore-hands.png)
![维修线圈领取后的手持反馈](Images/FirstPersonHands/06-held-repair-coil.png)
![左手抄网、右手保持握竿](Images/FirstPersonHands/07-net-grip.png)

本轮实景截图/任务日志：Logs/usv-validation-1db1ddbd19444d3981b406b3abd2f287.log；捕鱼冒烟日志：Logs/usv-validation-4920781978764f1282d1faf945cbc6b1.log。日志为本机文件，不上传。

复跑：

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingExpeditionCapture
./Tools/Run-UnityValidation.ps1 -Suite FishingShore
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke -Player Builds/FishingPrototype-v0.7.0/FishingPrototype.exe -Session v070-hands
```

补充验证（2026-09-30）：
- 独立输入/渲染策略：62 项通过。
- FishingShore：四岸点登岸、步行、碰撞、调查、上船与救援通过，日志 Logs/usv-validation-be4bb299d0e9472cad9d6b620502456b.log。
- 非 Development Windows v0.7.0 构建通过，日志 Logs/usv-validation-bf6f7fc146e44759b2a02ada4c0d8e7a.log。
- 可执行文件：Builds/FishingPrototype-v0.7.0/FishingPrototype.exe；旧版不覆盖。

后续鼠标回归发现与修复见 [v0.7.1 审阅说明](FISHING_V071_LOOK_FIX.md)。此前基于角度 helper 的测试没有覆盖真实输入门控，不应作为鼠标成功验收证据。

独立 v0.7.0 Windows Player 的验收 Smoke 已通过（隔离存档与生产规则检查）；本轮日志 Logs/desktop-acceptance-67a20de4bc3148d7ab24e35aa4d18336/player.log。不是物理键鼠或 PICO 认证。
