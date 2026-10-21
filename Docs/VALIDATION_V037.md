# V037 集成验证记录

记录日期：2026-10-21。基于主线 V035 与本地 V036 改进，使用 Unity 2022.3.62f1 的独立副本验收；正常游玩存档和用于截图的原工作区保持独立。

## 本轮检查

纯规则 4250 项、纯输入 70 项已重新运行通过。V037 使用 `Tools/Run-V037Validation.ps1` 组织手部、门体、天气、UI 和既有交互回归；下表链接为本轮实际运行的断言摘要。

| 检查 | 状态 | 范围 |
|---|---|---|
| 水壶真实蒙皮与手工交互 | [通过](Validation/V037/FishingV037Hands.txt) | 20 个持壶相位的五指接触、提手和壶体净空、原始骨架、取壶/携带/补水/浇水/归还、摇筛与摇柄双腕可达性 |
| 四入口与归航出口 | [门体通过](Validation/V037/FishingV037Portal.txt) / [路线通过](Validation/V037/FishingV036Portal.txt) | 原场景门体、四境完整进出路线、薄雾、粒子、减少特效、生命周期和实际构图 |
| 主题 UI | [通过](Validation/V037/FishingV037UI.txt) | 三套手册纸面与独立暖色设置背景、七类实际页面、标题、夜色、输入穿透；设置正文对比度白天7.22:1、夜晚9.79:1 |
| 雷雨 | [通过](Validation/V037/FishingV037Weather.txt) | 天空、环境与水面变暗、实际闪电亮线、雷声与音量、暂停/失焦/晴天/减少特效时恢复 |
| 家园与电视 | [通过](Validation/V037/FishingV036.txt) | 沙发坐姿、电视屏幕、独立悬灯、镐头可见范围、空闲存档轮询 |
| 储物、果树、航线 | [储物](Validation/V037/FishingV036Storage.txt) / [果树](Validation/V037/FishingV036Orchard.txt) / [航线](Validation/V037/FishingV036Performance.txt)通过 | 双向物品守恒、每日果实收取、真实摇树输入、路线等价与分帧工作量 |
| 工具与冒烟 | [镐头](Validation/V037/FishingPickaxe.txt) / [提灯](Validation/V037/FishingLantern.txt) / [垂钓](Validation/V037/FishingAngling.txt) / [鼠标钓鱼](Validation/V037/FishingMouseFishing.txt) / [狩猎](Validation/V037/FishingHunting.txt) / [冒烟](Validation/V037/FishingSmoke.txt)通过 | 原角色腕臂、源工具、输入与物品结算、鱼获/交易/存档/天气生命周期回归 |

## 验证方法

手部逐指比较源模型两骨权重预测与 Unity `BakeMesh` 的实际蒙皮位置，要求误差小于 1 mm；每根手指必须靠近提手，且不得进入柄芯或壶体。掌心到目标的误差不能替代这些检查。拿起中段也采样，避免只检查已经携带的状态。新增断言已实际发现空握、穿入与拇指离柄，失败结果不会列为通过。

最终这轮水壶五指到提手中心线均约 11 mm，匹配 10 mm 提手半径及 1 mm 接触余量；未检出进入提手核心或壶体内部的手指顶点。两骨预测与实际蒙皮最大误差约 0.031 mm。四指按指尖到指根的顺序闭合，额外要求中节弯曲超过 15 度，防止只有近端接触、末端仍伸直；实际中节弯曲约 47–74 度，掌心侧图确认回勾与拇指对握。摇筛改为斜侧握，完整运动中双腕挂点误差小于 0.1 mm。预热后的接触求解平均 2.434 ms、托管分配 0 字节；这是本机编辑器样例，不代表 PICO 帧率。

同步离屏摄影会占用真实时间，手部测试仅在采样期间临时限制 `Time.maximumDeltaTime`，结束或异常时恢复。产品动作计时与完成条件保持原规则。

门体、天气和手册 UI 使用实际源场景及离屏相机截图。UI 分别检查航海纸、植物标本纸和工坊纸；雷雨额外检查画面中真实闪电亮线。最新主菜单、三个设置标签、存档槽、覆盖确认和启航画面使用实际 Windows Player 全屏截图，各比例抽查7张，共14张；没有把全部176张都写成人工逐张审阅。数据和截图只证明各自测试覆盖的状态。

最新菜单保留实时港湾，用连续羽化薄幕承托标题与选项，移除了独立纸面和角饰拼接。设置页新增内置 imagegen 生成的 `DreamSettings.png`，原图1672×941、运行导入上限1024；星月、玫瑰与藤蔓围绕控件留白展开，按九宫绘制扩展留白，保留5:4屏幕的完整角落图案，并随昼夜调整色调。[素材说明与提示词](../ArtSource/V037/README.md)已保留。存档深色面板按钮及启航黑幕提示也修正了文字颜色。

风境出口避开钟架并同步去回路线，星境入口移到天文台侧边、同步导航与实际交互；实际站立眼高图与美术相机图分别保存，光幕中央雾层遮住背后的环和镜筒。

![实际倾壶时的掌心与回勾指节](Images/V037/hand-grip-palm-13.png)

![星境入口实际站位](Images/V037/portal-approach-3.png)

![风境归航门](Images/V037/portal-exit-0.png)

![航海页与可读页脚](Images/V037/notebook-12.png)

![实际雷雨与闪电](Images/V037/weather-lightning.png)

## 构建与边界

最终源码版本为 `0.37.0 / versionCode 37`，Windows 与 ARM64 Android 已在最新菜单、设置与按钮修正后重新构建通过，见 [Windows](Validation/V037/FishingBuild.txt)、[Android](Validation/V037/FishingAndroidBuild.txt)。最终 Windows Player 88场景冒烟通过，1600×900和1280×1024各88场景完整HUD截图通过，见[截图清单摘要](Images/V037/player-capture-validation.json)与[运行摘要](Validation/V037/FishingPlayer.txt)。Windows编辑器目标已恢复，三场景[结构检查](Validation/V037/Structure.txt)通过。

本地交付文件位于 `Builds/FishingPrototype-v0.37.0-Windows.zip` 与 `Builds/FishingPrototype-v0.37.0-Android/FishingPrototype.apk`。Windows便携包210个文件逐项比对SHA-256；APK 1306个条目可读，ARM64、包名、版本及v2签名通过，与V036使用同一更新证书。文件大小、完整SHA-256及检查范围见[包核验清单](Validation/V037/package-verification.json)。构建包留在本地Builds目录，不纳入源码PR；第36版旧包与旧截图保留其原版本标记。

![最终实时港湾主菜单](Images/V037/player-startmenu-wide.png)

![最终暖色水彩设置](Images/V037/player-menucontrols-wide.png)

![5:4屏幕完整保留星球与玫瑰](Images/V037/player-menucontrols-5x4.png)

尚无实体 PICO 的追踪、触觉、佩戴舒适度或长时间帧率实测。窄柄接触使用胶囊近似，其他专用挂点的源码审阅不代表所有物件已完成真实硬件全姿态无穿模验收。
