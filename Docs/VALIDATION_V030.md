# 第30版验证记录

2026-10-16，版本0.30.0。全部自动化使用隔离验收存档；正式v13/v12存档及备份的前后校验通过。

## 功能与回归

- 本地规则检查3,831项、输入检查70项通过。
- 8组Unity专项/回归通过：V030、第一人称、提灯、V029、腰挂与睡眠、钓获事务、人物、全局Smoke；另通过导入/输入配置及三个原场景的结构检查。
- 三种海底拾获均走实际抄网、背包提交和持久化防重复路径，再显示共享卡片。验证图标、幽默文字字形和地区标题避让；满包仍保留原鱼获流程。
- 信封邮戳与牛皮纸已渲染；全部来信文字不缺字、不截断。标题、正文和落款使用现有中文手写字体。
- 人物使用9S服装、头发、眼罩与贴图，适配现有骨架/动作，约1.67m、17,895三角面。袖口内复用已校准的CC0五指网格并使用深色手套材质。第一/第三人称、实际持物、俯仰、蹲下、游泳、攀登与睡眠均覆盖验证。
- 新灯12,000三角面；腰挂竖直、衣物间隙、挂带到提手连接和腰带表面连接均通过。金属挂钩实际网格有独立检查。F同时开关真实暖光与灯窗发光，附近材质照明对比通过。
- 工具摆位同时约束腕部和掌心可达，没有拉长骨骼或放宽原有穿透、接触与视口阈值。

## 最终安装包

| 文件 | 大小（字节） | SHA-256 |
|---|---:|---|
| Windows ZIP | 427720479 | 2D09FBA83BB9A2B25AF038F36AB6F2447004A7300FAAB5D7ED9CD0BD8D10617D |
| PICO APK | 280998218 | ABA880DB86AA1E2176D8AC668F0F4739E34951964C2CE5852976DABCB7F14E1C |

Windows ZIP的210个文件与最终构建目录逐一校验一致；APK的935个条目CRC可读、包名com.ruanyueheng.tidefin.vr、0.30.0/code30、ARM64、v2签名与第29版相同的更新证书均通过。构建后已恢复Windows编辑器目标。清单位于Builds/v0.30.0-builds.json，包检查位于Logs/v030-package-verification.json。

正式Windows程序的58个隔离场景检查通过；最终58张1600×900完整界面截图与HUD证明标记通过。五张总览及三种拾获、信封、信纸、提灯和人物重点画面已复看。游戏窗口已自动关闭。最终截图清单：Logs/desktop-acceptance-827cda8aa0804ddaa55e50a8e369ba1a/capture-20261016-104309-315de064/manifest.json。

- [旧鞋卡片](Images/V030/boot-fullhud.png)、[罐头卡片](Images/V030/can-fullhud.png)、[宝匣卡片](Images/V030/treasure-fullhud.png)
- [信封邮戳](Images/V030/envelope-fullhud.png)、[手写来信](Images/V030/letter-fullhud.png)
- [人物与腰挂灯](Images/V030/character-daylight.png)、[夜间腰灯](Images/V030/character-waist-lantern.png)
- [总览1](Images/V030/fullhud-sheet-1.jpg)、[2](Images/V030/fullhud-sheet-2.jpg)、[3](Images/V030/fullhud-sheet-3.jpg)、[4](Images/V030/fullhud-sheet-4.jpg)、[5](Images/V030/fullhud-sheet-5.jpg)

## 修正记录与边界

首轮FBX导出发现单位尺度差异，已修正。原9S仅三条指骨链，已改为适配现有完整五指表面；重新校准人物尺寸和工具可达域，原接触阈值保持。正式截图发现地区提示遮挡钓获卡片，已修正显示优先级并重建两端、重新采集。旧验收器仍要求手持灯的条件已更新为默认腰挂状态，最终58场景通过。

用户两个原件逐字节复制保存在ArtSource/V030，SHA-256记录见source.json；桌面原件未修改。只提交第29/30版白名单，工程原有其他本地修改保留。

未进行PICO真机追踪、双眼画面、实际设备帧率、佩戴舒适度或长时间游玩验收。构建、模拟姿态和桌面截图不等同于硬件认证。
