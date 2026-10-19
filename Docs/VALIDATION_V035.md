# 第35版验证与交付

版本0.35.0，2026-10-19。建模参考图与实际游戏画面分别记录；自动断言不代替人工审美、真实手柄和头显体验验收。

## 已通过的源场景与规则检查

- 4161项钓鱼/种田规则检查和70项输入/渲染策略检查，包含既有鼠标控竿、鱼获交易与新增农田规则。
- V035手作：筛具与接盘居中关系、六贝独立运动、落砂到接盘、固定粒子池、连续供线、完整一圈两掌握持、源手臂可见、取消及焦点边界。
- V035-FARM：实际码头登陆，步行到屋内/菜园，E领取种子、翻土、播种、浇水，磁盘保存重载，三次真实睡眠成长、收获、井水补充、售卖及暂停守卫。继而回归家中与公共六处设施、家具交互及睡眠。
- MOUSE-FISHING与ANGLING：合并主分支PR45后保留鼠标抛竿、反向控竿、动态鱼目标、左手检视、实体抄网与待决定鱼获所有权；JSON验证使用本版v14格式。
- V019：四岛实际登岸、NPC交谈、真实路线进境、四种谜题、回响仅结算一次、原路返回、终章与存档；另检查风车绳梯、口粮、游泳返岸及驾驶。
- FIRST-PERSON：同源骨架、手部/工具接触、身体遮挡与视角回归。
- V033：三槽手动/自动分离、覆盖确认、失效/未来格式拒绝、海上与家中恢复点、设置、海图及连续人物动作。
- 完整试玩Smoke：四种分辨率HUD安全区、鱼种/网具、港口与经济、传说鱼、天气、生态、日历与备份恢复。

本轮自查修复了风车阳台挡梯口、家岛地表法线、浅海底登岸候选、菜园围栏通路、低处分畦接触点和低分辨率钓鱼面板边界。历史V019缩略图生成保留在美术工具中；游戏回归改为只读检查现存图标的有效PNG与可见像素，避免运行测试时重写工程。

## 建模交付

77个原创建模网格，1,030,912个独立三角面；这不等于运行时同屏面数或头显性能结论。GLB内嵌三张PBR图像；源Diffuse为1254像素，Normal及打包材质图为2048像素。Unity动画、植被风和游戏状态由运行时代码负责，GLB本身为静态资产库。

实际GLB重导入逐模型检查了网格数量、三角数、UV、材质、有限坐标与包围盒，最大世界包围盒误差0米；77个模型按实际边界排布，无画廊重叠。FBX哈希与游戏导入文件逐一比对。见[导出报告](../ArtSource/V035/portable-validation.json)和[重导入报告](../ArtSource/V035/roundtrip-validation.json)。

## 构建与独立程序验收

Windows与PICO ARM64构建均通过。Windows便携包的210个文件逐一与构建目录核对SHA256；APK清单为`com.ruanyueheng.tidefin.vr`、版本0.35.0/code35，含ARM64运行库，v2签名通过且证书与V034一致。

| 产物 | SHA256 |
|---|---|
| Windows.zip | `F3A278DD235D8E64DEA358ADC1A432B5787DFBC7D639983494B05AE67E650B35` |
| Android APK | `EDC8B277930E7D936A0192CED4EFC174599C6B3B9D9EC37B66908DBA3A0F88D1` |

独立Windows程序的78案例Smoke通过；1600×900和1280×1024分别完成78张全HUD截图，帧末探针均通过。验收使用隔离资料，检查普通存档与设置未被覆盖。自动探针只验证截图包含完整界面；本轮另外复看了四境、家屋、菜园、手作和观星画面，未把全部旧场景逐图认定为人工验收。公开[验证摘要](Images/V035/validation-summary.json)与[截图来源](Images/V035/capture-source.json)保留实际结果，原始运行日志留在本机。

最终实景检查额外修正了桌边手作缓存未站稳眼位的问题：电脑启动时站起，镜头持续跟随同源眼位，蹲姿启动回归通过。菜园状态栏避让地点标题，通知在全HUD截图中恢复普通位置；家岛地点名与烧烤设备提示也已校正。

## 实际游戏相机画面

以下先展示最终独立程序的完整界面截图；小紫点是帧末截图探针。

![最终家岛菜园，完整HUD](Images/V035/player-homegarden-wide.png)

![成熟作物与收获提示](Images/V035/player-farmharvest-wide.png)

![最终居中摇筛与双臂](Images/V035/player-sandsieve-wide.png)

![5:4窗口摇筛](Images/V035/player-sandsieve-5x4.png)

![最终绕绳](Images/V035/player-ropewinder-wide.png)

![实际观星视野](Images/V035/player-starobservation-wide.png)

以下为源场景的实际游戏相机渲染，环境视角临时隐藏人物以便检查；这些图片没有完整HUD，手作连续动作图展示实际运动中间帧。

![家屋与菜园](Images/V035/home-approach.png)

![家屋内部](Images/V035/home-interior.png)

![白垩浮岛](Images/V035/sanctuary-0-overview.png)

![根峡光路](Images/V035/sanctuary-1-overview.png)

![两层沉船](Images/V035/sanctuary-2-overview.png)

![螺旋观星台](Images/V035/sanctuary-3-overview.png)

![居中筛具、贝与双臂](Images/V035/sieve-motion-12.png)

![绕线与握持](Images/V035/winder-motion-12.png)

## 复现与限制

`Tools/Run-V035Validation.ps1`运行当前回归集合；美术安装入口是`Tools/Run-UnityValidation.ps1 -Suite FishingV035Art`，不会重建原始游戏场景模板。纯规则入口是`dotnet run --project Tools/FishingTests/FishingTests.csproj -c Release`。自动化使用隔离存档。

实体PICO佩戴、双手追踪舒适度及长时间帧率尚未检查；不能以桌面截图或APK结构验证替代。种田首版为12块菜畦与一种可循环作物，不包含多作物、季节枯萎和自动喷灌。
