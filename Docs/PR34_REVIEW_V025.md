# PR #34 审阅与 v0.25 补全记录

What this change does:

[PR #34](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/34) 在 v0.24 上增加可购买猎枪、按物种区分的掉落与存档、锻造台、可达山脚煤矿、煤和毛皮灯笼，并调整店员模型。审阅沿着实际商店、快捷栏、工具、交互、存档、资源生成与测试调用链进行，假定单机玩家、固定 12 只可狩猎动物、最多 32 个工坊设施。

## 版本与合并

- 审阅日期：2026-10-14（Asia/Shanghai）。
- PR 基线：`c518a2804c8efc180e6f754a01a22f8c2b2afc75`。
- PR HEAD：`e4933f88abaaed3f95368d3e4517935adc962684`，90 个改动文件。
- 本轮读取 GitHub 状态为 `OPEN / MERGEABLE / CLEAN`，Windows、Ubuntu 的纯 C# 工作流全部通过；CI 不构建 Unity Player，也不覆盖实体头显。
- 本地 HEAD 已包含整个 PR HEAD，不再挑选部分代码或遗漏随 PR 提交的资源。已有未提交素材和配置另行保留。

## 必须修复

1. **[P2] 正常退出会丢失最近自动保存之后的刷新进度**（原 PR `Assets/Scripts/Fishing/FishingRifleHunting.cs:89`、`FishingPrototype.cs:431`；现修复 `FishingPrototype.cs:431`）。
   - **What this is:** 领取猎物后，存档记下 300 秒恢复时间，活动期间每帧扣减。
   - **Problem:** 猎物组件在交易、领取或计时结束时保存；另外 `FishingWorkshopWorld.Update` 每 10 秒保存整份进度。正常退出和应用挂起没有最后一次保存，因此最近自动保存之后的几秒计时会丢失。初次审阅遗漏了工坊的全局自动保存，现已校正；不能把实际最多约 10 秒的尾差描述为整段等待重置。
   - **Fix:** 中央生命周期在失焦、应用挂起、退出时保存已初始化的真人游戏进度。自动化测试仍保持隔离；新增 `FishingHuntingValidation.cs:157` 回归在隔离存档中模拟 250 秒活动，再触发真实失焦回调，要求磁盘剩余 50 秒。
   - **If we skip it:** 玩家正常挂起或退出仍会丢失最后几秒进度。该项已在 v0.25 工作区修复。

2. **[P2] 猎物刷新在手柄失去跟踪和商店冻结期间仍计时**（原 PR `Assets/Scripts/Fishing/FishingRifleHunting.cs:87`；现修复 `FishingRifleHunting.cs:92`）。
   - **What this is:** 已领取猎物通过独立组件推进有效游玩时间。
   - **Problem:** 原组件检查暂停、剧情、失焦和 XR 笔记本，却不检查 XR 的实时跟踪或桌面商店冻结。头显/手柄仍连接但停止跟踪时，刷新计时会继续；打开桌面实体商店时也会继续。
   - **Fix:** 刷新沿用当前 head/controller 跟踪读取，任一手柄跟踪失败、头部被实体遮挡、应用失焦或商店占用时暂停计时。
   - **If we skip it:** 玩家尚未恢复可操作状态就提前获得刷新。该项已在 v0.25 工作区修复。

3. **[P1] 发布包会因被剔除的枪口轨迹着色器中断初始化**（原 PR `FishingRifleHunting.cs:59`）。
   - **What this is:** 猎枪组件在主控制器启动时，按字符串寻找 `Universal Render Pipeline/Unlit` 并创建轨迹材质。
   - **Problem:** 编辑器中可找到这个着色器，但实际 Windows 发布包启动检查得到 `ArgumentNullException: shader`。该着色器没有资源依赖，构建剔除后查找返回空值，主控制器无法完成初始化。纯规则与编辑器专项都不能证明发布包已可运行。
   - **Fix:** 复用项目已放在 Resources、其他效果也在使用的 `FishingPalette` 着色器，并启用其 `_Unlit` 参数；加入材质资源绑定断言，再重新构建并执行实际 Player 验收。
   - **If we skip it:** 发布包可能显示场景却无法进入完整游戏。该项已修复，本轮发布包必须以修复后的重构建结果为准。

## 本轮 PICO 功能补全

4. **猎枪原本明确只支持桌面，无法满足本轮双端试玩要求**（原 PR `FishingRifleHunting.cs:35`、`Docs/HUNTING_RIFLE.md`；现补全 `FishingRifleHunting.cs:37`、`:160`、`:210`）。
   - **What this is:** 原 PR 文档明确注明 XR 禁止开火，代码也用 `!XRSettings.isDeviceActive` 排除了整个猎枪展示与射击。这是原 PR 已声明的范围限制，不能据此声称同学实现违反自己的说明。
   - **Problem:** PICO 可以购买并装备猎枪，却无法呈现或使用，形成电脑和头显之间真实功能缺口。
   - **Fix:** 复用独立右掌跟踪和原笔记本扳机边沿入口。枪的右握点贴合真实手柄；XR 子弹沿实际枪口方向射出，左手仅在真实掌心接近前握点时参与握持。实际右扳机必须按下；菜单、剧情、失焦、跟踪失效、头部/枪口遮挡均禁止射击。受阻枪在 XR 收起，不通过虚拟挪枪绕过实体墙。领取提示采用 A，满包提示采用 B。
   - **If we skip it:** 新 PICO 包仍缺少同学加入的核心狩猎功能。该项已补全；实体设备的握枪角度、震动与 45 米瞄准仍需要头显试玩确认。

## 已逐项核对的功能

| 功能 | 实际生产入口与检查 | 结论 |
|---|---|---|
| 柜台购买猎枪 | `FishingTackleShop.cs:86` → `FishingAnimalLoot.cs:44` | 90 金币；缺钱、满包、重复持有均不扣钱；购买后进入快捷栏并选中 |
| 桌面第一/第三人称狩猎 | `FishingRifleHunting.cs:160`、`:210`，`FishingPrototype.cs:320` | 保留同一枪械、握点和 0.65 秒单发间隔；从真实枪口检测阻挡 |
| 按物种掉落 | `FishingAnimalLoot.cs:9`、`:61` | 兔/狐毛皮、龟壳、海鸥羽毛分开；一次完整事务，满包保留尸体 |
| 猎矛兼容 | `FishingHunting.cs:22`、`FishingFieldWorld.Hunting.cs:50` | 兔肉 ×2、狐肉 ×3 的主线产量保留，追加相应毛皮 |
| 海鸥与海龟 | `FishingEcology.cs:51`、`:123`、`:156` | 稳定目标 ID；死亡停动画；海面/空中遗体落到可达岸边 |
| 刷新与保存 | `FishingAnimalLoot.cs:54`、`:70`，`FishingModel.cs:551` | 固定人口模型复用；未领取尸体不被刷新吞掉；重复/异常收据拒绝 |
| 新锻造台 | `FishingCrafting.cs:45`，`FishingWorkshopWorld.cs:78`、`:124` | 公共与可制作台均存在；完整设施放置/回收与存档路径 |
| 石镐、铁锭、铁镐 | `FishingCrafting.cs:46`，`Tools/FishingTests/ForgeTests.cs` | 站点限制和成本事务正确；满包回滚；无免费产物 |
| 山脚煤矿 | `FishingWorkshopWorld.cs:46`，`FishingWorkshopWorld.Coal.cs:9`、`:26` | 使用可达真实岩壁；保留资源 ID 与 90 秒冷却，不修改山体碰撞 |
| 毛皮灯笼 | `FishingCrafting.cs:49`，`FishingFieldWorld.Tools.cs:131` | 煤 ×2 + 兔毛/狐皮/旧毛皮 ×1；真实手持点光源；旧贝壳灯笼配方保留 |
| 分类肉烤制 | `FishingCrafting.cs:40` | 分类肉与旧兽肉继续进入熔炉，保留燃料和成品领取事务 |
| 物品图标与 UI | `FishingItemIcons.cs:11`，`FishingV023Art.Icon` 原图优先 | 原实物图标保留；新增物品使用无字图形；此前本地新增 XR 图标验证完整保留 |
| 存档编号兼容 | `FishingBackpack.cs:7`、`:80`，`FishingModel.cs:507` | 主线 0–25 未重编号；旧本地 v8 的 23/24/25 在加载副本中迁移，不改变调用者数据 |
| 店员资源 | `FishingShopCashier.cs:8`、`:24` | 沿用 FantasyGirl 正式路径、预放置/重复 Bind 保护；LOD、贴图和猎枪 LFS 资源已实际落盘 |

保存、制作、掉落的主要边界已有可执行回归，固定规模下没有发现需要新增架构或依赖的原因。

## 验证与边界

- 本轮已运行纯规则回归：**3601 项通过**，包含狩猎收据、物种、满包、旧存档、计时边界、锻造与三种毛皮灯笼事务。
- 新增 Unity 狩猎回归覆盖笔记本打开时直接调用也不能开枪，以及挂起前剩余刷新计时的落盘；原本地新增图标检查原样保留。
- Unity 新 XR、狩猎、锻造、第一人称及 v0.24 五个专项分别在独立存档通过；校准与发布材质修复后，新 XR 与狩猎专项再次通过。
- 修复后 Windows 与 Android ARM64 / IL2CPP 两端均构建成功。实际 Windows 发布包完成 48 个场景的状态检查与 48 张 1600×900 完整 HUD 截图验收，修复后的初始化没有再次出现空着色器异常。
- APK 版本名 0.25.0、versionCode 25，848 个 ZIP 条目可完整读取，ARM64 Unity / IL2CPP / OpenXR 库齐全，v2 签名验证通过。USB 调试设备随后断开，本轮没有把 APK 安装、真实跟踪或佩戴舒适度标为已通过。

Verdict: 原 PR 无合并冲突。合并发布应带入已修复的 1、2、3、5；本轮 PICO 包还应包含 4，以 Unity 专项和修复后实际双端构建结果验收。

Not checked: 实体 PICO 的射击指向、握枪舒适度、手柄震动和长时间刷新；未添加弹药、装填或锻造小游戏，沿用同学已声明的单发无限弹药及即时配方范围。

## 独立 PR 审查追加修复

5. **[P2] 旧 v8 已领取猎物缺少计时字段，升级后永久隐藏。** 独立审查在 PR36 指出：旧收据默认 activeRecovery=false、respawnAt=0，两个恢复路径都不会处理它。修复仅在 TryRestore 已校验的收据副本上，为这种已领取收据启动完整 300 秒活动计时；不改传入 DTO、未领取遗体、已有活动计时余量或旧日历期限。新增真实混合旧存档回归先红后绿，验证 299 秒后重载保留 1 秒、暂停和睡眠不跳过等待、完成后不重复领取。规则回归由 3593 增至 3601 项通过；随后重新构建两端并提交更新。
