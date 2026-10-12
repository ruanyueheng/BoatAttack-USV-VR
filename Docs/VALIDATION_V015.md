# v0.15.0 桌面验收记录

日期：2026-10-11（北京时间）。基于已合并第14版，使用隔离测试存档。当前交付目标是 Windows 桌面试玩；PICO 真机不作为本版桌面验收前置条件。

| 检查 | 已执行结果 |
|---|---|
| .NET FishingTests | **1,831 通过**：15分钟整日、跨夜恢复、暂停停表、v14恢复日期迁移、重载防重复获肉、异常存档、挥竿力度、蓄力/飞行/落水时钟及既有规则 |
| .NET InputTests | **62 通过**：输入隔离及渲染策略 |
| Unity FishingLivingCoast | 通过：实际加载的12种鱼、8种混元网格/贴图/缩略图、米制尺寸、检视范围、夜景、猎矛接触，以及动物5分钟后真实回到栖息点且不额外发肉 |
| Unity FirstPerson / Interaction | 通过：同源手部握持、可达域、第一人称视野、接触结算、取消与失焦边界 |
| Unity Field / Catch | 通过：动物/背包、实际抄网、检视、显式装箱与放生；XR姿态输入为模拟 |
| Unity FishingSmoke / Structure | 通过：捕鱼至交易/存档、UI中央留空、输入区域与多画幅、天气/生物、脚本导入与场景结构 |
| Windows Build | **成功**：`Builds/FishingPrototype-v0.15.0/FishingPrototype.exe` |
| Windows Player Smoke | **30个夹具场景通过**：生产规则、隔离存档及状态检查 |
| 完整HUD截图 | **1600×900 与 1280×1024 各30张通过**，每张核验完整HUD标记；已检查两种画幅联系表及图鉴/甩竿/日历大图 |
| PICO实体输入 | 未执行；当前没有连接设备，未认证真实挥动、握点、立体显示或帧率 |

可复现桌面入口：`Tools/Run-V015Validation.ps1 -Capture`。它沿用既有Unity与Player验证工具，不构建Android。纯C#规则检查也由现有GitHub Actions分别在Windows和Linux执行。

## 图形与资产

8种混元鱼实际为3,498–3,500三角面/种；最长展示长度0.38m；1024像素颜色贴图及256×144透明图鉴预览。青花鲭原件1,500,000面，其余约50,000面，原始GLB完整保留在Git LFS。面数由Blender转换和Unity最终加载网格分别核验，完整清单见 [fish-budgets.csv](Images/V015/fish-budgets.csv) 与 `ArtSource/Hunyuan/Fish/manifest.json`。

![混元鱼种](Images/V015/HunyuanFish.jpg)

![简洁海湾手册与真实模型图鉴](Images/V015/Catalog.jpg)

![常驻HUD与较小快捷栏](Images/V015/HUD.jpg)

![前挥与浮标飞行](Images/V015/CastFlight.jpg)

[全部宽屏场景](Images/V015/all-scenes-wide.jpg) · [全部方形画幅场景](Images/V015/all-scenes-square.jpg) · [方形图鉴](Images/V015/Catalog-square.jpg) · [夜鱼图鉴](Images/V015/NightCatalog.jpg) · [实际手持青花鲭](Images/V015/HeldMackerel.jpg)

60张原始PNG的分辨率、阶段、HUD标记和SHA-256在 [capture-evidence.json](Images/V015/capture-evidence.json)。构建目录大小、启动器和实际游戏程序集散列在 [build-artifacts.json](Images/V015/build-artifacts.json)；完整运行目录需一起保留。

## 验证边界

- FBX最初只把单位/轴向转换留在对象层级，直接读取网格会得到约1/100尺寸且鱼体横躺；导入器已将转换烘进顶点/法线，重新导入与实际手持渲染均通过。
- 最后一轮方形截图曾在提灯持物可见性断言上退出，未采信该轮。保留断言，重新完整执行30场景后通过；中断原因尚未复现，不能将重跑通过视为真实输入手感证明。
- 验证在本地现有工程中执行。本机原有地图、店员、URP/XR配置与生成元数据改动保留且未混入提交；ProjectSettings仅提交版本号。未对全新Unity缓存的检出再执行完整导入。
- 自动测试与定格截图不能替代键鼠手感；PICO握点、挥动阈值、长时舒适性和帧率留待用户需要头显时验证。当前以Windows桌面为交付目标。
- 本版提交PR审查，不自动合并。
