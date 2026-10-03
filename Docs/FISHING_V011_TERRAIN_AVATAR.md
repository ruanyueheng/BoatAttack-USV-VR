# v0.11.0 地形地图、同源人物与交互

## 基线与 PR 取舍

基于 `main 193cb346`（v0.10，PR #18）。择优吸收 [PR #12](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/12)（`83e15c1`）的地形校验、二进制 `.asset` 的 `text=auto` 保护及天气／景物跟随人物，不引入旧 v0.4.3 Walker。

移植 [PR #17](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/17)（`730de9e`）的商店 prefab、柜台购买与第 10 航点，重新接入 v0.10 的自由岸线、四处原有登陆点、八项调查、工坊 15 配方和 v8 存档，保留港口购买。未直接合并旧分支。店员／顾客仅为空站位，不是 NPC AI；原工程 Unity Companion License、人物 CC0 和字体许可各自保留。

## 地图与人物

- 真实 Terrain、静态 mesh、初始化礁区、栈桥、工坊与商店生成离线高程底图。北向上，底图和图标共用米制投影；提供比例尺、未知边界、商店／工坊和至多六个近资源标记。正确处理静态批处理子网格、渲染矩阵和 Renderer 范围，不增加常驻俯拍相机。**不表示水深或安全航线**。
- 参考 [Brackeys/Minimap](https://github.com/Brackeys/Minimap) 教程与 Unity 2022.3 官方 API；不宣称使用和平精英内部实现。
- 第一／第三人称共用 Quaternius BeachCharacter 的 FBX、蒙皮和材质；第一人称只隐藏头部 Renderer，不再创建旧 FishingGlove。使用真实 38 指骨、双臂 IK 和独立掌心目标，保留 Legacy Idle／Walk／Run。人物来源见[署名文件](../ArtSource/Characters/ATTRIBUTION.md)。
- 眼位根据同源头部／眼睛几何校准；第一人称与身体使用同一竖向参考系，避免镜头平滑后把手臂留在画面外。桌面捕鱼身体朝向作业侧，双视角共用紧凑钓具；XR 仍保留自己的追踪参考系和挂载尺寸。
- 手册、望远镜、提灯、镐、钓竿、摇轮、抄网和鱼获分别设置握点。动作采用程序化姿态与时序叠加，**不等同于完整动作捕捉或 NPC 制作／交易动画**。

## 交互规则与操作

真实 E／F／鱼获请求锁定目标、装备和档案；起手不发奖，接触许可只消费一次。暂停、失焦、换装备、目标消失、满包或读档不会重复结算。设施成功消耗后保留收手阶段；工坊和商店继续原有 UI 事务。

普通鱼与传说鱼均通过双次 X 确认放生；传说鱼必须先测量。松手点位于实际手臂可达域，落水是结算成功后的独立无碰撞表现，不要求手臂伸到远处水面，也不重复发放赏金。

- **W/S、A/D、Shift**：前进／倒航、转向、刹车。
- **G、空格、Ctrl**：安全上下船、跳跃、蹲下。
- **C、V**：切换视角、回正；岸上移动鼠标转头，**Alt** 释放鼠标操作界面。
- **E、I、1–9、F**：互动、背包、快捷栏、使用。低处采集需要等蹲姿完成并真正靠近目标。
- **M、范围按钮、N**：收展地图、缩放、切换航点。
- 商店柜台 **E/Tab** 开关，**1/2/3** 购买升级，**Esc** 关闭，**I/J** 转背包／日志，不穿透快捷栏。
- 待处理鱼 **E** 检查、**F** 入箱、**X** 两次确认放生。

## 验证与审查

| 范围 | 实际结果 |
|---|---|
| .NET 捕鱼／存档／地图／动作时序／验收规则 | 1315 项通过 |
| 输入与渲染策略纯规则 | 62 项通过 |
| Unity 新功能 | Terrain、ShopInstall、Map、Character、Interaction、Shop 通过 |
| Unity 既有玩法 | Shore、Field、Workshop、Expedition、CatchCapture 通过 |
| Windows | v0.11.0 构建成功，独立程序实际启动 |
| 完整 HUD | 1280×720、1280×1024，各 15 个场景通过，含岸上双视角和商店 |
| 代码审查 | 发现的桌面放生可达性阻塞已修复，并新增普通／传说鱼接触结算回归；未保留该已知阻塞 |

地图新增真实 GPU 像素／批处理范围、非可读网格、inactive 几何和 Terrain holes 回归；已查看实际底图。人物检查包含同源材质／骨骼、真实蒙皮变形、握点可达性、视口位置、跨帧恢复和双视角。商店检查实际登陆／步行／回船、输入模态、精确扣款和 v8 工坊数据持久化。已查看独立 Player 的关键地图、持物、人物与商店截图；相机 RT 图不冒充完整 HUD。

验证使用临时隔离档案。部分人物／鱼获事务夹具固定船体以排除救援后的浮力瞬态；实航与抄网物理由独立套件验证，不把这些夹具当作船体稳定性证明。日志留在本地 `Logs/`，不上传玩家存档。

## 实际 Player 画面

以下来自独立 Windows Player 的完整 HUD 捕获，不是编辑器场景视图。

![地形小地图](Images/v011/terrain-map.png)

![第一人称同源手臂与钓具](Images/v011/first-person-fishing.png)

![岸上第一人称](Images/v011/shore-first-person.png)

![岸上第三人称](Images/v011/shore-third-person.png)

![实体钓具店柜台](Images/v011/shop-counter.png)

## 试玩与复验

构建输出：`Builds/FishingPrototype-v0.11.0/FishingPrototype.exe`。直接双击即为正常游戏；不传验收参数不会进入自动测试模式。请保留整个构建目录。

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release
powershell.exe -NoProfile -File Tools/Run-V011Validation.ps1 -Stage Full
```

`Quick` 运行六个新功能套件，`Full` 追加旧回归、构建与两种分辨率的 Player 捕获；`-StartAt` 可从指定步骤续跑。脚本不调用 Generate、不清理玩家档案。

**验收边界**：PICO 真机、实体键鼠全流程及持续性能未认证。本轮不扩展 PICO 岸上移动、背包和工坊交互；船上 XR 保持兼容接线，但不将模拟姿态测试当作真机通过。
