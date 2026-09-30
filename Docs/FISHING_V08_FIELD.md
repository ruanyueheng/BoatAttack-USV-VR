# v0.8.0 — 随身行囊与陆地探索

本轮在已合并的 v0.7.1 上实现背包、跳跃/蹲行、陆地动物、工具拾取和倒船。Unity 仍为 **2022.3.62f1**，不更换 URP、XR loader、场景或地形。

## 交互设计

借鉴《我的世界》的 **网格储物区 + 9 格快捷栏 + 材料堆叠**，采用现有海湾游戏的墨绿/琥珀色界面；未复制其贴图、模型或整套生存系统。陆地操作采用常见第一人称探索游戏的空格跳跃、按住 Ctrl 蹲行、Shift 快走。由于 E 已用于拾取/任务，背包使用 I，避免改变原有任务键。

### 按键

- `I`：打开/关闭背包；背包打开时世界暂停，`Esc` 也可关闭。
- `1–9`：选择快捷栏；岸上可用鼠标滚轮切换（Alt 操作界面时不切换）。
- `E`：附近拾取/任务互动。已有信标任务优先，满包拾取不会误触后续调查。
- `F`：使用选中的自然手册、望远镜或提灯。**F 不再暂停；暂停使用 Esc。**
- `T`：看向陆地动物并记录；船上仍是原有海洋生物观察。
- `Space`：陆地跳跃；必须先释放，再按下一次，不能空中连跳或按住连跳。
- `Ctrl`：按住蹲行；`Shift`：陆地快走。
- 船上 `W / S` 前进/倒航，`A / D` 转向，`Shift` 刹车。手柄左扳机倒航、左肩键刹车。
- `G` 上下船，`C` 第一/第三人称，鼠标转头与 Alt 释放游标沿用 v0.7.1。
- `Tab` 港口交易台；升级用界面按钮（`U` 仍可升级鱼线）。原数字 1/2/3 升级键让给快捷栏。

## 背包与工具

**27 格储物区 + 9 格快捷栏**，共 36 格。材料上限 64/格，工具 1/格。先点击来源格，再点击目标格可移动、合并或交换；右键目标格分出一半；Shift+点击在储物区/快捷栏之间快速转移。关闭背包不会把物品留在一个可能丢失的“鼠标暂存槽”。本轮没有丢弃、合成、耐久、饥饿或交易物资系统。

- 开局自然手册：`F` 记录看向的附近动物。
- 西湾科考岸：可拾取望远镜，第一人称 `F` 放大/放下，观察距离由 8m 扩为 22m，`T` 记录。
- 北岛潮池：可拾取提灯，`F` 点亮/熄灭，有实际暖色局部灯光和手持模型。
- 四处登岸点共 **8 个固定 ID 拾取物**，还包括海贝、漂流木。靠近并看向物品后 E 拾取。
- 背包容量不足时整笔拾取失败，地面物品继续可见、领取位不被消耗。拾取成功后不会因重新登岸/读档重复生成。
- 鱼货仍在原有船上鱼箱，旧捕鱼容量、委托和出售逻辑不变。鱼竿与抄网仍属于船上钓具，不强行塞进随身行囊。

## 人物与动物

人物站立高 1.75m，蹲下碰撞体高 1.08m，眼高随之变化；第三人称模型同步压低。头顶受阻时松开 Ctrl 也保持蹲下，移出障碍后才能站起。跳跃高度约 1.1m；有重力、顶碰取消上升、斜坡/水边检查，不开启游泳或无限空中跳跃。暂停/失焦/界面恢复时需要先释放 Space，避免误跳。

陆地新增 **野兔、赤狐、陆龟，共 8 只**。使用本项目原创顶点色低多边形网格，在登岸点周边采样干燥可站立地面，有限范围游荡，有移动起伏/转向表现，不能跨过水域/建筑。默认 3.5m 警戒，快跑扩大到 7m，蹲行缩小到 1.5m；惊动后会在安全范围内避开。它们是非战斗探索对象，不支持捕猎、驯养或真实刚体推挤。

观察必须满足距离、视线方向和场景遮挡检查。三种动物的发现记录保存在背包右侧“野外手册”，重复记录不发放重复奖励。原有海洋生态/日志不受替换。

## 真正的倒船

`Engine.Accelerate` 接受负油门，鱼船与原版 HumanController 都不再钳掉负值。S 不再调用“把速度移向零”的制动代码，而是施加反向力；前进时按住 S 会先抵消惯性，然后持续倒航。倒航推力为正向的 55%，约 4m/s 后停止继续施加倒航推力（不是瞬时速度钳位）。显式 Shift 制动仍只作用于水平速度，保留浮力。

## 存档升级与边界

- 独立 `boatattack-fishing-v6.json`，包含完整槽位、快捷栏选择、8 位领取记录和 3 位陆地观察记录。
- 新档不存在时优先读取 v5 主档/备份；v5 不存在时才读取旧 v1 路径。旧文件只读、不覆盖。
- 新档或备份存在但损坏时不偷偷回滚到旧版进度；保留已有警告/恢复策略。
- v1–v5 迁移保留鱼货、金币、任务等原有状态，增加一本自然手册，不虚构拾取或动物记录。
- v6 拒绝缺失/错误数量槽位、未知物品、超限堆叠、非法选择和未知掩码；模型边界对旧版 JsonUtility 的空数组表示兼容。
- 自动化与截图使用隔离临时档。截图中的装备用明确的演示 fixture 填充，不是完整路线实玩证明。

## 回归与复跑

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingField
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingShore
./Tools/Run-UnityValidation.ps1 -Suite FishingExpeditionCapture
./Tools/Run-UnityValidation.ps1 -Suite Regression
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke -Player Builds/FishingPrototype-v0.8.0/FishingPrototype.exe -Session v080-field
./Tools/Run-UnityValidation.ps1 -Suite FishingFieldCapture
```

新增纯规则测试覆盖事务式入包、拆分/交换/快速转移、3000 次随机操作的数量守恒、满包拒收、拾取去重、非法存档、迁移、跳跃边沿和正反推力。`FishingField` 通过虚拟 Keyboard → Input System → 生产 Shore.Tick/CharacterController 检查跳跃、蹲下与头顶净空；也检查实际世界拾取/观察、Unity JSON、S/Shift/F 来源映射和真实浸水引擎/Rigidbody 从静止倒航。

**验收边界：** 自动化不是物理键鼠/Windows 游标认证，不是完整海陆路线或持续性能测试。背包与跳跃/蹲行是桌面功能，未为头显增加腾跃、世界空间背包 UI 或设备绑定；PICO 真机未验收。PR 保持草稿，不自动合并。

## 人工验收清单

1. 运行新 `Builds/FishingPrototype-v0.8.0/FishingPrototype.exe`，确认版本，不误开旧程序。
2. 从静止按 S 能后退；前进中长按 S 能越过零速进入倒航；Shift 仍独立刹车。
3. 低速到橙旗 G 登岸，空格跳跃落地；Ctrl 改变视高/速度，低顶下不能起身；Alt-Tab/暂停/背包后不会误跳。
4. I 打开背包，验证缩放、点击移动、右键分半、Shift 快速转移、1–9/滚轮选槽，关闭时无丢物。
5. 西湾拾取望远镜、北岛拾取提灯，F/T 使用；满包时物品保持在地上。
6. 走近/快跑/蹲行接近三种动物，对照行为；有墙遮挡时不能记录；保存重启后物品与观察仍在，已领取物不重生。
7. 重跑原捕鱼、信标任务、鱼箱交易，确认数字键不再误买装备。

## 本轮验证记录（2026-09-30）

- 纯捕鱼/背包/移动/推进规则：**881 项通过**；独立输入/渲染策略：**62 项通过**。
- Unity FishingField / FishingFieldCapture：通过虚拟键盘跳跃、蹲行/净空、真实场景拾取与观察、遮挡拒绝、20 秒步进游荡边界、满包保护、JSON 迁移和实际浸水倒航检查。
- Unity FishingSmoke、FishingShore、FishingExpeditionCapture、Regression：通过；旧鼠标门控回归继续通过。
- 非 Development Windows v0.8.0 构建成功；新构建的独立 Acceptance Smoke 通过。
- 可复查的本地日志（忽略、不上传）：`usv-validation-89beb3786fdf43b59ecafa9edddf1195.log`（Smoke）、`usv-validation-8c4a5e5c43424fc2a66d8135546134bb.log`（Shore）、`usv-validation-25497cfefce444788e3b945dcb69fb42.log`（Expedition）、`usv-validation-08d1d6be587145b88a37b26c2fde0b80.log`（Regression）、`usv-validation-be89d17b67194cddb661abfa56f0fdf6.log`（最终 Windows 构建）、`desktop-acceptance-6f874bd959b74ef391eab9525b1f1941/player.log`（最终 Player Smoke）。

### 图形验收限制

隐藏 Windows 图形窗口返回黑色 backbuffer，完整 HUD 探针检查明确失败（`Logs/field-capture-5da1379c07234f08a4d22c183e3d61dc`）。**这次失败没有被当成成功截图，也没有上传黑帧。背包界面的真实点击、显示与缩放仍待人工验收。** 人工希望显示测试窗口时，可以自行运行 `./Tools/Run-FieldCapture.ps1 -Visible`；该显式截图模式只用隔离演示存档，检查完整 HUD 探针，不证明物理操作完成。默认不主动弹出可见窗口。

下面仅为 Unity 相机渲染的角色/动物外观参考，**没有 HUD，不代替背包界面、实际输入或完整路线验收**。


![蹲行角色外观，相机渲染，无 HUD](Images/FieldExploration/crouch.png)
![第一人称观察野兔，相机渲染，无 HUD](Images/FieldExploration/rabbit.png)
![赤狐外观，相机渲染，无 HUD](Images/FieldExploration/fox.png)
![陆龟外观，相机渲染，无 HUD](Images/FieldExploration/tortoise.png)

最终实景检查日志：`Logs/usv-validation-b826acf342f64a04940cb34014fbc7a5.log`。外观图片已逐张目视检查；动物近景使用能看见目标的相机视角，不把固定偏移相机被地形挡住的空画面当作生物截图。
