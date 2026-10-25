# V038 验证记录

日期：2026-10-24。Unity 2022.3.62f1；自动检查使用临时隔离存档，不操作正常游玩存档。

## 已复现的旧问题

- 第37版原手部专项仍会通过，但读取实际水壶 FBX 后发现原握点离源网格最近约0.1837米；旧专项没有验证胶囊是否对应真实提手。
- 新增“注满后关闭水阀”的检查在旧代码上失败：水位保持而桥面碰撞关闭。
- 新增持镐远端检查在旧求解方式上失败：食指中节约2.64度、指尖离柄约0.1049米。修正按关节分组后，还发现原镐柄挂点缺少指根前距，已同步修正呈现与实际采矿接触调用者。

## 本轮验证

| 项目 | 状态 |
|---|---|
| 纯规则4356项、输入70项 | [规则通过](Validation/V038/v038-rules.txt)、[输入通过](Validation/V038/v038-input.txt)，含关阀保持通路、真正排水关闭通路 |
| 持壶、持镐、秘境水域与缓存 | [V038整合通过](Validation/V038/FishingV038.txt)，[液面贴壁终验通过](Validation/V038/FishingV037HandsFinal.txt) |
| 四秘境、采矿/采集 | [四境通过](Validation/V038/FishingV036Portal.txt)、[实际交互通过](Validation/V038/FishingInteraction.txt) |
| 垂钓、鼠标钓鱼、狩猎、提灯 | [垂钓](Validation/V038/FishingAngling.txt)、[鼠标钓鱼](Validation/V038/FishingMouseFishing.txt)、[狩猎](Validation/V038/FishingHunting.txt)、[提灯](Validation/V038/FishingLantern.txt)通过 |
| 航线、外海游泳、冒烟与结构 | [航线](Validation/V038/FishingV036Performance.txt)、[游泳](Validation/V038/FishingV038Swimming.txt)、[冒烟](Validation/V038/FishingSmoke.txt)、[结构](Validation/V038/Structure.txt)通过 |
| Windows与ARM64 Android | [Windows](Validation/V038/FishingBuild.txt)、[Android](Validation/V038/FishingAndroidBuild.txt)最终重建通过 |
| 安装包与独立 Player | [210文件哈希、APK版本/ARM64/v2签名](Validation/V038/package-verification.json)通过；最终Windows程序[88场景冒烟](Validation/V038/FishingPlayer.txt)通过 |

稳定握持测量见[性能记录](Validation/V038/grip-performance.txt)。源场景回归由 `Tools/Run-V038Validation.ps1` 组织，新增实际网格和皮肤检查编入既有手部、农场、镐头专项。

共11个不同Unity入口完成回归，液面边缘收尾后追加手部专项终验并重新构建两平台。水壶用实际FBX提手核对握点，多相位检查五指实际蒙皮、壶体净空与水平液面。持镐覆盖石镐/铁镐、站立/蹲下与三种视线俯仰，四指中节约42–74度，五指皮肤均靠近实物柄，未检出穿入木柄核心。

船舱水覆盖浮桥所有浮筒，检查双侧双向连续步行、关闭满水阀、真实下水游泳与跳跃攀回侧甲板。壶内液体检查1、6、12格余量与0、25、50、85、135、180度共18种组合；液面保持世界水平，边缘贴合源壶内壁、内底或口沿。液面高度按余量标定，精确升数守恒和流体力学没有纳入本次模拟。

本机同一实际源场景测量：第37版稳定握持约2.487毫秒，第38版终验约0.041毫秒，30次预热调用托管分配0字节；握柄半径变化会使缓存失效。未把该局部CPU数据写成全游戏帧率提升。

最终交付位于 `Builds/FishingPrototype-v0.38.0-Windows.zip` 和 `Builds/FishingPrototype-v0.38.0-Android/FishingPrototype.apk`。APK版本为0.38.0、versionCode38，使用与V037相同的更新证书。旧版本包、原存档及原有无关工作区修改保留。

![实际持镐的四指围合](Images/V038/pick-grip-close.png)

![实际提手与壶内水平液面](Images/V038/can-grip-palm.png)

![沉船内部有深度的积水](Images/V038/hold-water.png)

边界：有界水体和壶内液体采用几何近似；没有实体 PICO 的追踪、佩戴体验与长时间帧率实测。编辑器 CPU 均值不能替代整机帧率结论。
