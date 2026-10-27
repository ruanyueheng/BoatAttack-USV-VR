# v0.41.0 交接文档（Claude 修改记录，交给 Codex 续做）

> 本文件只描述 **Claude 在 2026-10-27 做的改动**。Codex 的第 40 版工作（未提交的 WIP）未被删除或重写，全部保留在工作区。
> 为避免混淆：所有 Claude 改动的代码处都带有 `[Claude v0.41]` 注释，可直接 `grep -rn "Claude v0.41" Assets/Scripts` 定位。

## 0. 基线与备份

| 项 | 位置 |
|---|---|
| Codex v0.40 WIP 原样备份（Claude 改动前的全部脚本） | `Builds/Archive/Codex-v040-WIP-20261027/Scripts/` |
| Codex v0.40 对 v0.39 提交的 diff | `Builds/Archive/Codex-v040-WIP-20261027/tracked-changes.patch` |
| 改动前 git 状态 | `Builds/Archive/Codex-v040-WIP-20261027/status.txt` |
| Codex v0.40 WIP 构建（对照用） | `Builds/FishingPrototype-v0.40.0/` |
| **Claude v0.41 构建** | `Builds/FishingPrototype-v0.41.0/FishingPrototype.exe` |

git HEAD 仍为 v0.39 提交 `6e7ae238`，Claude 没有做任何 commit。
对比 Claude 改了什么：`diff -r Builds/Archive/Codex-v040-WIP-20261027/Scripts Assets/Scripts`。共 13 个文件：新增 `FishingStoryCapture.cs`、`FishingFrameProbe.cs`；修改 `FishingPrologue.cs`、`FishingCharacter.Arms.cs`、`FishingHUD.Kit.cs`、`FishingHUD.Story.cs`、`FishingFieldWorld.Tools.cs`、`FishingShore.cs`、`FishingVoyageWorld.cs`、`FishingRealmWorld.cs`（1 行，触发回响过场）、`FishingPrototype.Story.cs`（1 行，过场按键）、`FishingPrototype.cs`（版本号、两个命令行开关、挂载两个工具）。最后三个文件的单行改动没有加注释标记。

## 1. Claude 改动清单（已完成、已验证）

### 1.1 剧情与世界观重写 + 过场 CG 系统
**文件**：`Assets/Scripts/Fishing/FishingPrologue.cs`（整体重写，公开 API 保持兼容：`Open/Age/Shot/ShotCount/ShotSeconds/CinemaCamera/CinemaScreen/Begin()/Tick()/Present()/Finish()/Suspend()`）

- 新世界观"潮汐回响"：回潮海四座灯塔（风/林/潮/星）以歌声指路 → 三十年前"大寂潮"使灯火熄灭、鱼群离散 → 祖父、老村长兼守灯人**沈望潮**出海寻灯未归 → 玩家收到海风邮戳来信返乡接任村长 → 书桌发现祖父撕成四页的旧海图 → 四境回响逐盏点亮灯塔 → 组装远航罗盘、白帆拱湾开启"宝藏"：航海日志、旧灯芯和一包种子 → 四灯重燃，玩家继承"海贼王"称号（保留了用户要求的称号，重新解释为"敢于远航也记得回家的人"）。
- 7 段过场：`Prologue`(6 镜)、`Chart`(3)、`Wind/Forest/Tide/Star`(各 2)、`Finale`(4)。
- 镜头类型：手绘插画 CG（`FishingV031/Chapters` 4×2 图集与 `FishingUIPrototype/IslandPostcard`，缓慢推拉平移）与引擎实时运镜混合；宽银幕黑边、章节标题、逐字字幕、渐隐转场、每段独立合成配乐。
- 操作：E/空格 先补全字幕，再按进入下一镜；Esc 跳过整段；XR 右手 A 跳过。
- 触发（`Queue()` 排队，在玩家空闲、无对话/菜单/钓鱼进行时自动播放；自动化/验收模式不播放）：
  - 发现旧海图：`FishingVoyageWorld.ReadChart()`，仅首次 `DiscoverChart()==true`
  - 回响重现：`FishingRealmWorld.Interact()` id==3，仅该岛首次 Cleared
  - 宝藏终章：`FishingVoyageWorld.OpenTreasure()` 成功后
- 文案同步：`FishingVoyageWorld.cs` 中旧海图提示与宝藏信件改为沈望潮的航海日志。
- 桌面按键：`FishingPrototype.Story.cs` 第 34 行改为调用 `Prologue.Advance(esc)`。

### 1.2 动作僵直：手臂 IK 平滑过渡
**文件**：`FishingCharacter.Arms.cs`（`Arm` 新增过渡状态字段、`ApplyArm` 头部、新增 `BlendSolve()`、`ApplyFingers()` 由原函数尾部拆出，逻辑不变）

- 根因：原 IK 目标按帧开关，出现即瞬间吸附、消失即瞬间弹回动画，这是"动作很僵"的主要原因。
- 现在：桌面模式 IK 权重渐入约 0.16 秒、渐出约 0.24 秒；同一帧目标跳变超过 6cm（换工具、切动作）时，用约 0.1 秒半衰期的衰减偏移过渡（惯性化）。XR 追踪手仍 1:1，不做任何平滑。
- 调试开关：命令行 `-fishing-no-ik-blend` 可关闭此功能做 A/B 对比。

### 1.3 UI 美化
- `FishingHUD.Kit.cs` `ReadingVeil()`：原羽化半透明遮罩（边缘发糊）改为清晰圆角纸卡：投影 + 94% 纸面 + 内描金线。所有提示条、目标卡、标题卡统一生效。
- `FishingHUD.Story.cs` `DrawRegionTitle()`：地区标题卡从屏幕中央的大卡（516×174）改为顶部紧凑条（420×88），不再遮挡准星和画面中心。

### 1.4 第一人称遮挡
- `FishingFieldWorld.Tools.cs` 自然手册：不阅读时放低到腰前（原来常态端在胸前，挡住约 40% 画面），翻开时平滑抬到原阅读位置。
- 望远镜：`BinocularScale` 0.52 → 0.42，携带时位置下移；观察（目镜）模式不变。

### 1.5 美术：登岛栈桥
- `FishingShore.cs` 新增 `BuildPlankedJetty()`：原来是一整块棕色长方体，现在保留它作为**隐形碰撞体**（行走和物理不变），视觉换成带缝隙、轻微错位和深浅交替的横向木板、两侧纵梁和入水桩柱。

### 1.6 工具（仅命令行开启，使用临时存档，不影响玩家存档）
- `FishingStoryCapture.cs`：`-fishing-story-capture <绝对目录>` 逐段播放全部过场并截图（21 张），完成后退出。
- `FishingFrameProbe.cs`：`-fishing-frame-probe <绝对文件>` 记录可见窗口下帧时、GC 次数与超过 40ms 的尖峰；非验收模式下 70 秒后自动退出。
- 两个开关都已加入 `FishingPrototype.cs` 的 `Automated` 判定。
- 版本号：`FishingPrototype.Version` 改为 `0.41.0`。

## 2. 验证结果（Claude 实测）

| 项 | 结果 |
|---|---|
| 编译与 Windows 构建 | 通过（`FishingPrototypeEditor.Build`） |
| 88 个验收场景截图（`-fishing-acceptance-capture`） | v0.41 **88/88 通过** |
| 剧情过场截图 | 7 段共 21 镜全部生成，PASS |
| 帧时：自由游玩静置 60 秒，1080p | 平均 4.9ms，p99 8.4ms，最大 54ms（1 次） |
| 帧时：开船巡游（港口→家→风铃岛），1080p 可见窗口 | 平均 4.55ms，p99 6.4ms，**0 次超过 33ms** |
| Editor 校验套件（`Run-UnityValidation.ps1` 各 Suite） | **未运行**，见第 4 节 |
| PICO 真机 | **未测试** |

## 3. Claude 发现、但没有修好的问题（请 Codex 处理）

1. **v0.40 回归：钓鱼时右手袖口出现深色尖块。** v0.39 没有，v0.40 WIP 中出现（对比 `03-Fighting` 截图）。疑似钓竿握姿或重烘的 `CompatibleInteractions` 动画改变了右前臂的姿态/滚转。
2. **低头视角穿模**（`LookDownStanding/LookDownCrouched`）：画面右上方有深色尖角网格。Claude 试过剔除肩部/上臂三角面，会在钓鱼画面露出袖子内侧锯齿，已撤回；也试过把工具跟随俯仰上限从 65° 降到 35°，截图无变化，已撤回。两项都已恢复为 Codex 原代码。
3. **Codex 的帧测脚本无效**：`Tools/Run-FishingPerformance.ps1` 用 `-WindowStyle Hidden` 启动，GPU 不渲染（测出平均 0.68ms/帧，不可信）；巡游在 "World shoreline capture did not become ready" 处失败。
4. **GC 偏多**：约每秒 4 次 GC0（已开启增量 GC，桌面上没看到卡顿）。主要怀疑 IMGUI HUD 每帧拼接字符串，PICO 上可能出现周期性掉帧。
5. **美术仍待改进**：第三人称角色是 NieR 9S 黑衣，与低多边形卡通风格冲突；珊瑚像橙色叉子；钓具店柜台和家中部分道具是纯色方块。
6. v0.40 新增的左上角红字主线提示与漂浮的"水族箱安装点"文字压在画面上，建议并入右侧目标卡。

## 4. 给 Codex 的注意事项

- `FishingV023Validation.cs` 和 `FishingXRPresentationValidation.cs` 引用了 `FishingPrologue`：API 名称都保留了，但 `ShotSeconds` 从 6 变为 6.5，镜头内容也变了。如果这些校验比对固定截图或文案，需要更新预期。
- `FishingFirstPersonValidation` 可能检查手册的手持位置：现在不阅读时手册在腰前。
- IK 平滑会让手在目标出现后的前 0.16 秒内尚未贴合工具。如果有校验在**目标首帧**断言手掌贴合，请等待几帧，或使用 `-fishing-no-ik-blend`。
- Android 包没有重新构建。
