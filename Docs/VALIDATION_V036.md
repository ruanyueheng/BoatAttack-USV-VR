# V036 集成验证记录

记录日期：2026-10-21。本文依据本次保存的实际日志与当前源文件整理；不把编辑器断言、构建成功、玩家画面检查和实体 PICO 验收混为同一结果。

本页保留第36版历史结果；随 PR 归档的[证据摘要与来源](Validation/V036/README.md)不代表第37版重跑。原始完整日志和安装包仍保存在本地 `Logs/`、`Builds/`。

纯规则 [4250 项](Validation/V036/rules.txt) 与纯输入 [70 项](Validation/V036/input.txt) 已通过。

## 已通过的检查

| 检查 | 实际证据 | 覆盖内容 |
|---|---|---|
| V036 家园、坐姿、灯笼与镐头 | [最终断言摘要](Validation/V036/FishingV036.txt) 中 `[V036] PASS` | 沙发入口选择、坐下/起身、电视画面与开关、5 盏独立悬挂灯笼、夜间家园补光、平视时完整镐头可见、200 次空闲存档轮询 0 托管分配 |
| V036 深井与水壶完整流程 | [最终断言摘要](Validation/V036/FishingV036Farm.txt) 中 `[V036-FARM] PASS` | 真实井口开洞、超过 5 米的井壁与井底、井沿以下的水面、木架取壶/归还、可达提手、水桶入水与提桶灌壶、壶内水位、倾壶水流与土面水花、取消与完成结算、JSON 往返 |
| 储物箱与物品守恒 | [最终断言摘要](Validation/V036/FishingV036Storage.txt) 中 `[V036-STORAGE] PASS` | 双向整组存取、先合并后使用空格、任意一侧满时保持原组、非法槽位、真实界面存取、家具避让、存档往返、快照隔离、旧存档迁移 |
| 果树与地面拾取 | [断言摘要](Validation/V036/FishingV036Orchard.txt) 中 `[V036-ORCHARD] PASS` | 树干接近、3 次真实往返动作、追踪丢失后重新松握、剔除头部/摇杆/转向伪动作、落地后拾取、禁止重复落果、每天每树一次、背包满不结算、无伤碰头插曲 |
| 四条秘境路线的入口与终点出口 | [断言摘要](Validation/V036/FishingV036Portal.txt) 中 `[V036-PORTAL] PASS` | 拱框和半透明动态光幕、门体不阻挡行走、入口不误触返程、归航门位于终点平台且不依赖奖励领取状态 |
| 信纸与共用纸面 UI | [断言摘要](Validation/V036/FishingV036UI.txt) 中 `[V036-UI] PASS` | 纸纹与角饰共用缓存、信件布局、文字高度、装饰不挡输入、实际相机边界、阅读姿态锁定；标题/设置/图鉴的最终玩家截图仍需另查 |
| 航线搜索 | [断言摘要](Validation/V036/FishingV036Performance.txt) 中 `[V036-PERFORMANCE] PASS` | 与 V035 参考算法的路线等价、分帧工作量、复用搜索数组、旧路线在新结果完成前保留、目标变更/关闭引导/回港取消旧搜索 |
| V035 农田回归 | [最终断言摘要](Validation/V036/FishingV035Farm.txt) 中 `[V035-FARM] PASS` | 翻土、播种、3 个浇水夜晚、成熟、一次性收获与种子返还、打水、实际背包产物售卖、暂停不改农田、实岛与海图一致 |
| V035 手工互动回归 | [最终断言摘要](Validation/V036/FishingV035.txt) 中 `[V035] PASS` | 摇筛/摇柄的手部可达性、动作中的握点、视野、取消后恢复与特效清理 |
| 镐头、垂钓、鼠标钓鱼 | [镐头](Validation/V036/FishingPickaxe.txt)、[垂钓](Validation/V036/FishingAngling.txt)、[鼠标钓鱼](Validation/V036/FishingMouseFishing.txt) 均有各自 `PASS` | 原角色腕/臂与工具、钓鱼和输入回归 |
| 独立存档与继续/读取 | [最终断言摘要](Validation/V036/FishingV033.txt) 中 `[V033] PASS` | 手动档与自动档独立、继续保留当前进度、读取恢复磁盘进度、覆盖确认、家岛安全落脚点 |
| 原型运行冒烟 | [最终断言摘要](Validation/V036/FishingSmoke.txt) 中两条 `[FISHING] PASS` | 鱼获、视角、相机遮挡恢复、暂停、装备、订单、升级、图鉴、导航、天气、生物观察、存档及备份恢复 |
| 素材导入 | [断言摘要](Validation/V036/FishingV036Art.txt) 中 `[V036-ART] PASS` | 独立悬挂灯笼、全新象牙白水壶、深井网格与碰撞 |

上述测试使用临时隔离存档，不替换正常游玩档。历史失败或中断日志不作为通过依据；这里只引用有最终 `PASS` 的运行。

## 存档、物品与 PICO 输入核查

当前存档版本为 15。`IslandFruit` 追加在既有 `FieldItem` 尾部，既有编号保持不变。箱子使用独立 36 格背包结构，存档同时复制随身背包、箱子与 5 棵树的采摘日；读取拒绝超限物品、未来采摘日与伪造旧版新数据。

`FishingBackpack.TransferTo` 先确认目标能完整接收，再清空源格；目标无法接收时不发生部分转移。落果只有在落地、接近与背包接收都成功后才记录当天采摘并隐藏。未拾取落果不单独存档，重新读取后可以再次摇落；已成功拾取的当天记录保存。

摇树和摇筛读取右手柄原生追踪位置，避免玩家移动和转向冒充摇动。摇树同时要求手在树干附近、握把先松后握、有效往返幅度与时间间隔；失焦、追踪丢失、遮挡、菜单、其他动作和手持水壶等状态会阻止或重置相应动作。震动仅在设备支持脉冲时发送，并限制强度、时长和频率。碰头时 PICO 保留原生头部追踪，用短震动和边缘提示反馈；电脑可在“减少特效”中关闭轻微抖动。

水壶拿起/放回使用真实提手接近条件，PICO 持壶位置和朝向跟随右手。打水、浇水在动画完成时才结算，取消、追踪丢失、改变装备或离开动作范围不提前扣水或给水。源代码与编辑器日志可以确认这些条件；手柄物理手感仍需实体设备确认。

## 航线性能测量

本次编辑器实测结果见[原始测量输出](Validation/V036/route-performance.txt)，对应[断言摘要](Validation/V036/FishingV036Performance.txt)。

| 指标 | 实测 |
|---|---:|
| 搜索网格 | 161,721 格 |
| 本次路线完成所需搜索片段 | 3 |
| 单片最大展开节点 | 32 |
| 单片最大测得耗时 | 1.538 ms |
| 4 次预热后重新规划的托管分配 | 0 字节 |

算法采用 1.5 ms 软截止时间，允许当前一次物理/节点步骤结束后退出，并硬限制每片最多展开 32 个节点。因此 1.538 ms 不是违反 1.5 ms 硬时限，也不是整帧耗时。测量覆盖一个真实家岛航线样例与预热后重新规划，不代表长时间航行、所有航线或 PICO 帧率保证。

## 美术来源与本次替换

屋檐与室内横梁使用独立的 [Brass Diya Lantern](https://polyhaven.com/a/brass_diya_lantern)，作者 Bhargav Kubal，来自 Poly Haven，采用 [CC0](https://polyhaven.com/license)。保留链条、黄铜笼体和星形镂空，游戏导入版本为 12,742 三角面、3 个共用材质、1K 贴图；没有复用人物手持提灯。下载索引及原件保留在 [素材来源目录](../ArtSource/V036/README.md)。

水壶为本项目原创重建的象牙白釉面、浅黄铜与鼠尾草绿星月枝叶款，7,956 三角面、4 个共用材质，保持现有提手与壶嘴交互锚点。游戏读取 `FishingV036/StarlightWateringCanView`，已替换被用户否定的旧水壶。可编辑 Blender 源文件及便携 GLB 均保留在素材目录。

家岛井口的地面已实际开洞；石质井壁与井底延伸至局部约 -5.4 米，水面位于局部 -0.35 米、低于井沿约 1.23 米。水桶在提起前浸入实际水面下，井内不再保留草地色封底面。

## 最终构建与独立程序

Windows 便携包和 ARM64 PICO APK 均已重新构建，结果见[最终构建摘要](Validation/V036/builds.txt)与 [SHA-256 清单](Validation/V036/builds.json)。Windows ZIP 的 210 个文件逐项与构建目录一致；APK 版本为 `0.36.0 / versionCode 36`，v2 签名、ARM64 原生运行库与归档读取校验通过，签名证书与 V035 相同。

最终 Windows 程序通过 [88 场景 Smoke](Validation/V036/player-smoke.txt)。[1600×900](Validation/V036/player-capture-wide.txt) 和 [1280×1024](Validation/V036/player-capture-5x4.txt) 各保存 88 张完整 HUD 截图，并核对版本、像素尺寸及帧末界面标记。最终[捕获清单](Images/V036/player-capture-validation.json)与[交付证据](Validation/V036/final-delivery-evidence.json)对应同一批构建。

复看了开始菜单、操作/画面/声音设置、图鉴、来信、两侧储物格、坐姿电视、白釉壶携带与倾壶、提桶注水、落果、秘境门和夜间家园。5:4 下的地区三行说明已加高，终点导览标记改为“出口”。粉色小点仅属于隔离验收的帧末截图标记，正常游戏不显示。

![最终操作设置](Images/V036/player-menu-controls-wide.png)

![最终信件](Images/V036/player-letter-wide.png)

![双向储物](Images/V036/player-storage-wide.png)

![象牙白釉星月水壶](../ArtSource/V036/starlight-watering-can-preview.png)

![真实深井与井水，源场景实际游戏相机](Validation/V036/well-clear-water-depth.png)

![实际程序中的倾壶与水流](Images/V036/player-watering-pour-wide.png)

![秘境终点出口与完整说明](Images/V036/player-portal-exit-5x4.png)

## 验收边界

没有实体 PICO 头显及手柄触觉强度、真实追踪手感、佩戴舒适度或长时间稳定帧率实测。井水采用实际深井几何与动态水面，未运行流体体积模拟。CPU 航线样例改善和桌面截图不能证明所有设备、路线和时段的卡顿均已消失。

另按用户偏好使用 Jev（请求别名 `jev-latest`，实际 `jev-1.13.0`）核对说明与证据的对应：真机与“彻底消除卡顿”的结论被判为证据不足；储物测试描述出现低置信度判断，已回到确定性日志与源代码核查，不以模型概率替代测试。原始结果保留在[语义核对记录](Validation/V036/jev-delivery-claim-audit.json)。
