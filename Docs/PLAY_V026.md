# 第26版试玩说明

版本：0.26.0。保留既有角色、岛屿、制作配方和玩家进度。

## 本版变化

- 重制工作台、熔炉、锻造台、木堆、纤维、石料、铁矿、煤矿、黏土、砂堆共10种模型。各地摆放、手持物品与图标共用对应资源；新增运行时资源约0.94 MB。
- 制作页增加设施、产物和材料图片，保留材料数量和中文名称；熔炉显示烧制进度及待领取成品。
- 游泳改为完整骨架俯身、抬头、划水和交替打腿，静止时保持浮泳；视点保持稳定。回到低岸后恢复站姿。
- 回潮渔屋右侧摆放工作台、熔炉和锻造台，均可使用原有制作系统。为操作区腾出通道。
- 收音机播报当前和明日天气；航线书桌展开海图；落地灯可开关室内照明；床仍可休息至清晨。
- 修复旧存档中初始登岸点仅剩锻造台的问题：每次加载分别检查三种公共设施，缺失时安全补齐，保留已存在的设施、背包和熔炼队列。

## 怎么体验

在航海图选择“回潮渔屋”，到专用泊位下船后沿栈道进屋。靠近设施或家具，按屏幕提示交互：Windows 为 **E**，PICO 为 **A**。三台设施从左到右为工作台、熔炉、锻造台。熔炉加入材料与燃料后，关闭界面继续加工，再回来领取。

公共工坊位于西侧初始登岸点附近；已有存档加载后自动补齐，不需要新建存档。若手持物品遮挡观察，可切换到空快捷栏。

游泳时朝低岸移动并按空格攀回岸边；PICO 朝可攀低岸移动时沿用自动攀岸。其他操作见[第25版手柄说明](PLAY_V025_PICO.md)。

## 运行文件

- Windows：[直接运行](../Builds/FishingPrototype-v0.26.0/FishingPrototype.exe)
- Windows便携包：[下载ZIP](../Builds/FishingPrototype-v0.26.0-Windows.zip)，解压后运行其中的 `FishingPrototype.exe`。
- PICO ARM64：[安装APK](../Builds/FishingPrototype-v0.26.0-Android/FishingPrototype.apk)

PICO包名保持 `com.ruanyueheng.tidefin.vr`，版本号26；更新安装时保留数据。

## 验证与图像

统一回归入口：`Tools/Run-V026Validation.ps1`；涵盖V026、XR呈现/舒适性、锻造、工坊、第一人称及住宅/商店通路。验证使用临时独立存档。

Windows验收增加家内全景和家中工作台制作页，共50个场景；自动检查普通存档及备份未变化。模型自检入口：`python Tools/Art/validate_v026_art.py`。

模型预览：

![第26版设施与资源模型](Images/V026/coastal-props.png)

已通过：3647项纯规则测试、70项输入测试、10种模型资产自检，以及上述6项Unity回归。场景检查包括仅剩锻造台的旧档、重复补齐、家内三台实际制作和熔炼领取、墙体阻挡、PC/XR同入口、码头游出和攀回。实际头腰轴与水面夹角约19°，眼位约在水面上0.24米。

场景截图：[家中全景](Images/V026/home-front.png)、[工作台](Images/V026/home-Workbench.png)、[熔炉](Images/V026/home-Furnace.png)、[锻造台](Images/V026/home-Forge.png)、[游泳侧视](Images/V026/swimming-side.png)、[游泳斜视](Images/V026/swimming-oblique.png)。这些是Unity场景渲染；完整HUD另在Windows运行包中检查。

回归摘要见 `Logs/v026-regression-runner.log`，纯规则与输入结果见 `Logs/v026-rules.log` 和 `Logs/v026-input.log`。

最终Windows程序通过50场景和50张1600×900完整HUD截图校验，测试前后普通存档及备份不变。已检查全部场景联系表，并放大检查家内、制作、游泳和重点界面；[家中完整HUD](Images/V026/home-full-hud.png)、[图示制作页](Images/V026/crafting-full-hud.png)。最终验收记录见 `Logs/v026-capture-runner.log`。

最终便携ZIP已同步家中区域标题修正，210个文件逐一与已验收Windows构建核对一致。PICO APK的868个归档条目完整，包含ARM64运行库、版本号26和有效v2签名，与第25版签名证书相同。文件大小和SHA-256见[最终包校验信息](../Builds/FishingPrototype-v0.26.0-sha256.json)，完整记录见[交付检查](../Logs/v026-package-verification.json)。真机安装尚未验证。

## 已知边界

旧存档可升级到本版；新增六个固定设施后，第25版及更早版本无法完整读取此存档，请继续使用第26版。灯光开关只保持本次运行状态，重新进入游戏默认开灯。

编辑器几何和输入检查、构建成功均不代表PICO真机佩戴验收；实际手柄、双眼画面、帧率与舒适度仍需设备确认。
