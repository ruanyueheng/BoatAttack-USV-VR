# v0.14.0 验收记录

日期：2026-10-10。所有自动游戏流程使用临时隔离存档。下列“通过”仅对应列出的证据，不等于实际键鼠、手柄或 PICO 真机验收。

## 结果

| 检查 | 结果与范围 |
|---|---|
| .NET FishingTests | **1,813 通过**：旧规则 + 日历/季节跨界、夜鱼条件、12 种发现位、v8→v9、捕猎去重/满包、烤肉事务、体感抛竿松键/丢追踪、控制器提示 |
| .NET InputTests | **62 通过**：输入隔离与既有渲染策略 |
| Unity Structure | 通过：脚本编译、场景引用、基础结构 |
| FishingLivingCoast | 通过：12 种真实网格、最大体型屏幕占比、日夜提灯渲染、真实猎矛接触获肉、同源追踪手超臂长、4 个海岸网格与 64 个资源接近点 |
| FishingFirstPerson | 通过：真实蒙皮握持、五指接触、近眼身体、望远镜与多个画幅 |
| FishingInteraction | 通过：接触事务、取消/失焦/换槽/目标消失、设施预览/落位、捕鱼检视与结算 |
| FishingPlayer / FishingPlayerBoundary | 通过：低处拾取、持竿视野、接缝/台阶、实体墙与水边约束、9 个对抗性边界案例 |
| FishingShore / FishingTerrain | 通过：岸上完整既有路线与 terrain / collider 对应 |
| FishingWorkshop | 通过：自然岸线、上下船、采集、制作/熔炼/领取、拥有设施落位与回收、存档 |
| FishingCatch | 通过：实际抄网变换、待处理鱼获、装箱/放生、世界空间射线与确认、Android OpenXR 配置（姿态为模拟） |
| FishingField / FishingCharacter / FishingShop | 通过：动物/装备、人物骨架、店员与升级交易/存档 |
| FishingExpedition | 通过：维修线圈、步行、信标校准、北岛捕获凭证、备份恢复 |
| FishingSmoke | 通过：基础鱼钓获到交易、传说跃水/放生、天气生命周期、UI 输入区域、相机遮挡与存档回归 |
| Windows Build | **成功**：`Builds/FishingPrototype-v0.14.0/FishingPrototype.exe` |
| Windows Player Smoke | 27 个夹具场景通过，生产规则和存档隔离检查通过 |
| 完整 HUD Capture | **1600×900 + 1280×1024 各 27 张通过**，全部图片通过 HUD 校验标记；已查看全场景联系表及日历/图鉴/商店/持物/夜景大图 |
| Android Build | **成功**：ARM64 / IL2CPP / Vulkan / Single Pass Instanced APK；ZIP 核对只有 `arm64-v8a`，含 Unity OpenXR 与 PICO OpenXR 库 |
| PICO 4 Ultra 实机 | **未执行**：`adb devices -l` 无设备。帧率、温升、双眼视觉、震动、实际手柄挥动和长时舒适性尚未证实 |

两种画幅的全 HUD 原始 PNG 及详细日志在 `Logs/desktop-acceptance-9649e436c30949c989ea87825d5618ca` 和 `Logs/desktop-acceptance-97c3f9f25e6b48c88d71cf667c1c0fbb`。可复现的代码、压缩审查图片和清单进入 Git，临时日志、缓存及构建包不进入 Git。

## 画面与几何指标

- 12 种鱼的基准长度和实际三角面数：[fish-budgets.csv](Images/V014/fish-budgets.csv)。新增 8 种均 ≤1,706 三角面，全部 ≤3,640；1 网格/共享材质。最大尺寸的普通鱼不进入 15 cm 近眼区，专项记录全部通过。
- 昼/夜/提灯在同一相机下的亮度采样为 **0.5272 / 0.1453 / 0.2049**。这是渲染图像的相对数值，不是现实照度；暗夜与提灯的差别由实际帧验证。
- 石镐/铁镐各 1,501 个手部顶点，深入柄内超过 8 mm 的点为 4 个；五个手指均贴近柄面且包到背侧。没有宣称任何角度像素级零穿模。
- 四个登陆区按 0.5 m 网格取样，共 1,347 个有支撑点，1,194 个胶囊可站立点；其余点可能位于合法墙体/石头/树木，并非全部认定为错误。详见 [coastal-clearance.csv](Images/V014/coastal-clearance.csv)。原有旧港绕行、码头接缝和不穿墙检查单独执行。未承诺整个自由地图的任意坐标都已人工走查。
- 字体含完整 OFL 版权/授权记录，脚本校验所有当前代码中的中文和 ASCII 字形；约 315 KB。最终 UI 使用字体本身的 Medium 字重，避免合成粗体挤压汉字内白。

## 审查图片

![第 14 版日历与白底界面](Images/V014/Calendar.jpg)

![12 种原创低面鱼模型](Images/V014/FishCatalog.jpg)

![夜间提灯与实际 HUD](Images/V014/LanternCoast.jpg)

[全部 27 个场景联系表](Images/V014/all-scenes.jpg) · [实际检视](Images/V014/InspectCoral.jpg) · [握镐](Images/V014/StonePickaxe.jpg) · [暗夜](Images/V014/NightCoast.jpg) · [图鉴](Images/V014/Journal.jpg)

## 构建与遗留边界

构建大小和 SHA-256 见 [build-artifacts.json](Images/V014/build-artifacts.json)。Windows EXE 是启动器，因此同时记录实际 `Assembly-CSharp.dll` 的散列；完整可运行目录需要一起保留。

- 初次隐藏窗口截图回读为黑图，离屏相机方案缺少 IMGUI 标记，均判失败且未采用；最终恢复正常屏幕截图，在可见验收窗口拿到两个画幅的 54 张合格实画。没有删除标记断言来制造通过。
- 中途回归发现设施检查方向/距离不一致、旧验证仍要求 v8、Catch 无图形设备启动问题，均修复后重跑通过。
- 混元未登录/无凭据，未实际生成或下载模型；交付原创可用网格、可复制提示词及资产替换流程。详见 [调研](RESEARCH_V014.md)。
- 编译与运行检查在保留既有改动的本地工程中执行，并非全新 clone；旧地图/未引用资源未纳入本 PR，干净检出后的构建由 CI/审查者再次确认。
- 本机原有的地图重烘焙、CoastalClerk 资源、展示脚本和导入元数据改动保留，不混入本版提交；配置只提交渲染模式和版本号的有意变化。
- PR 保持 Draft，等待 PICO 真机与人工自由探索验收，不自动合并。

## PICO 合并前验收

安装 APK 后以双手柄完成：坐姿/站姿校准 → 左摇杆走动与 30° 转向 → 近墙淡出/实体障碍 → 上下船 → 体感和 A 键各抛竿 10 次 → 丢追踪后松键恢复 → 摇轮、抄网、装箱/放生 → 岸上拾取/挥镐/猎矛 → 手册背包满栏换物、烤肉领取 → 暗夜提灯 → 退出重进恢复日期/动物/熔炉。

同时观察左右眼水面/反射/天空是否一致，手的比例与 grip offset 是否需要微调，持续 20 分钟记录 CPU/GPU 帧时间、内存和温升。若运行于 72 Hz，应以 13.89 ms 帧预算检验；90 Hz 对应 11.11 ms。这些是目标预算，不是已测成绩。按结果调整固定 0.85 渲染比例与 LOD；不能只凭低面鱼就断言整个海岛稳帧。
