# 第31版验证记录

2026-10-17，v0.31.0。自动功能验证与截图均使用隔离存档，继续沿用正式v13存档格式。

## 功能检查

- .NET规则检查3,845项、输入检查70项通过。
- 8组主要Unity专项/回归通过：V031、第一人称、提灯、玩家边界、实际交互、腰挂与睡眠、V030、完整Smoke；其中V031在最终收尾后再次运行通过。
- 第31版专项覆盖开始菜单暂停/恢复、既有进度不重置、七章与四页鱼种目录、中文标题字形、日夜星月、灯塔9度/秒环扫和暂停、选中提灯手持/换物品腰挂、翻书进入阅读页、收起恢复，以及徒手低位采集和持镐采矿的自动蹲姿与唯一结算。
- 实际交互检查同时约束掌心与手腕的真实可达距离，保持原臂长和俯身上限；采矿、拾取、满包、取消、目标消失和鱼获结算回归通过。
- 提灯实际蒙皮有2,685个手部顶点参与检查，五指最近接触点在1mm内；未检测到深入金属8mm以上的顶点。环形提手使用闭合网格射线内外判定，空心部分不被当作金属。
- 可开合书体验证薄Y轴及封面从铰链向+X伸展；FBX导入烘焙轴向，避免反向翻转。翻书时收起日常HUD，旧宝匣展示不会遮挡手册，也不改变背包数量。

## 画面与安装包

最终Windows ZIP与PICO ARM64 APK均已构建并校验；构建后已恢复Windows编辑器目标。Windows便携包的210个文件与构建目录逐一SHA-256一致；APK的949个条目CRC可读，包名com.ruanyueheng.tidefin.vr、0.31.0/code31、ARM64、v2签名及与第30版相同的更新证书均通过。

| 文件 | 大小（字节） | SHA-256 |
|---|---:|---|
| Windows ZIP | 436087865 | 633E6BD577604CF5A48C8D166237191174C03D63FAD0F3EF1AA8E5859E04F961 |
| PICO APK | 292968138 | 0A870B9E48A30E6B19E3C6487BCC7E12B8F49C7CC866E0756C8728C3D0E17B78 |

最终Windows程序65个隔离场景检查通过，65张完整1600×900截图的HUD证明标记与尺寸均通过，并复看五张总览和关键页面。正式v13/v12存档及备份的元数据保护检查通过。完整截图清单：Logs/desktop-acceptance-2fa50dd4b1d6433788da09fc1077b54c/capture-20261016-194328-db60e6ca/manifest.json。截图来源为实际游戏窗口，未以相机离屏图替代完整界面。包校验：Logs/v031-package-verification.json；构建清单：Builds/v0.31.0-builds.json。

- [开始菜单](Images/V031/start-menu.png) · [控制页](Images/V031/controls.png) · [鱼获图鉴](Images/V031/journal.png)
- [传说](Images/V031/legend.png) · [自然](Images/V031/nature.png) · [调查](Images/V031/survey.png) · [日历](Images/V031/calendar.png)
- [群岛](Images/V031/islands.png) · [秘境](Images/V031/realms.png) · [委托实测](Images/V031/order-native.png)
- [翻书](Images/V031/guide-opening.png) · [家岛月色与环扫](Images/V031/moonlight.png) · [提灯近景](Images/V031/lantern-close-warm-contact.png)
- 总览：[1](Images/V031/overview-1.jpg) · [2](Images/V031/overview-2.jpg) · [3](Images/V031/overview-3.jpg) · [4](Images/V031/overview-4.jpg) · [5](Images/V031/overview-5.jpg)


已在独立试玩窗口实测控制页全屏/窗口、F11往返、Alt+Enter；[全屏证明](Images/V031/fullscreen-native.png)和[窗口证明](Images/V031/window-native.png)包含原生窗口尺寸差异。菜单、七章手札、提灯、翻书和家岛星月环扫均进行了画面复看。

## 修正与限制

修正了提手握点沿用旧模型、玻璃与环境反射偏绿、采集仅覆盖Pickup而未覆盖Harvest、采矿只测掌心未测手腕、书体导入轴向、翻书HUD重叠、宝匣临时展示残留和灯束过硬等问题。旧玩家边界夹具按既有游泳行为和实际支撑重新隔离，未放宽生产碰撞、臂长或穿透阈值。

额外运行的旧独立镐具舒适腕角检查在俯仰-30度返回57.3度，未满足其40度阈值；该项未计为通过。闲置镐握持角不是本次调整项，实际采矿接触专项已通过。

未完成PICO真机追踪、双眼观感、设备帧率、佩戴舒适度或长时间游玩验收。构建、桌面实测与模拟姿态不等同于头显认证。
