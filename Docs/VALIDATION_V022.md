# v0.22 验证记录

日期：2026-10-13。Unity 2022.3.62f1，Windows D3D11，Blender 5.2.2 LTS；自动流程全部使用隔离存档。

## 通过的检查

- 无硬件捕鱼规则：2797项；输入/渲染策略：70项。
- 第22版真实Unity组件专项：199项，包含14个航点、四条跨岛航线、每段船体余量、三面旧竞速墙解除、15个固定导览渲染器、第一/第三人称画面、抵达、读信开关、翻盖、抽纸、跳过、原告示牌恢复、手持物及近眼身体遮挡、桌面/房间步进守卫、重复奖励、救援、维修记录、实际XR手册按键边沿复位、五居民LOD/贴图、12鱼种图片及真实网格。
- 第21版回归：真实加速与热量、调查航点、物理抄网失败后的重试、放生一次结算和船舶专精。
- 第18版回归：四岛连续步行、真正手部接触读信/开箱/转盘、信标奖励、商店路径及船体稳定性。
- FishingSmoke：捕获、测量、图鉴、出售、升级、委托、存档/备份、天气生命周期、相机遮挡与两种视角。
- 最终Windows Player/content构建成功；打包后所有既有桌面验收场景和生产规则检查通过。
- 五居民FBX经Unity重新导入，源图集与材质保留；授权原件哈希及下载包CRC核验通过。临时四鱼照片按真实网格在Blender重渲染，透明背景且鱼鳍/尾部完整留白。
- 回归重烘焙后的地图恢复为测试前副本，三份文件逐字节一致。原玩家进度未作为夹具使用。

## 可复现入口与日志

入口：`Tools/Run-V022Validation.ps1`。

- 完整回归：`Logs/v022-full-validation.log`。
- 最终专项：`Logs/v022-pr-fixes-special.log`，底层日志 `Logs/usv-validation-2695d36858bb48a6a04ff9d315d2ff0c.log`。
- 最终构建：`Logs/v022-pr-fixes-build.log`，底层日志 `Logs/usv-validation-5391957b63ee4c30b4380d269edfb77e.log`。
- 最终打包验收：`Logs/v022-pr-fixes-player.log`；隔离输出 `Logs/desktop-acceptance-d26834f8d5e84cff83905fab4837cd77/`。
- 旧流程日志：V021 `Logs/usv-validation-764d9e61c65040208316b910053bcbdd.log`，V018 `Logs/usv-validation-73d9b19d00eb4dbfb41274de4b4a9054.log`，Smoke `Logs/usv-validation-587debb2ea144225863acf9328f6cbaa.log`。
- 美术：`Logs/v022-residents-final.log`、`Logs/v022-fish-portraits.log`，源件清单在 `ArtSource/V022/`。

日志和Windows包保存在本地，不提交到仓库。下面是实际相机/资产渲染，导航与信纸图没有把它称为完整HUD截图。

![水面导览](Images/V022/navigation-first-person.png)

[第三人称](Images/V022/navigation-third-person.png) · [信封浮起](Images/V022/letter-rise.png) · [翻盖](Images/V022/letter-flap.png) · [抽出信纸](Images/V022/letter-extract.png) · [阅读](Images/V022/letter-read.png)

![居民外观](Images/V022/Residents.png)

## PR反馈回归

PR #32的首轮自动审查提出两项P2，均已修复：J→海岸调查→西湾新增已读维修记录的重读入口，重读不修改任务事实或奖励；全部阅读按钮使用统一可读条件，待处理鱼获期间禁用并显示先结算提示，不会先关闭日志再拒绝显示信件。实际生产事务生成待处理鱼、打开日志、尝试重读、保持日志/鱼获及结算后恢复阅读，共增加六项组件断言，最终199项通过。修复后Windows包重新构建并完成打包验收。

## 修复与范围边界

早期导航验证拒绝穿过旧竞速围墙，定位后在当前捕鱼场景解除三墙，仍逐段检查真实岸线。早期Unity临时相机导出空白鱼图被像素检查拒绝，最终改为Blender真实几何渲染；鼓鼓鱼初版照片顶部刺有裁切，已按高宽共同适配取景并检查四张照片的完整留白。阅读早期被旧告示牌与手持图鉴遮挡，最终相机图和组件断言均确认已清除。

未执行PICO真机、Android构建、实体按键手感与长时间性能验收。XR守卫和按键复位由真实组件检查支持，不能代替设备验证。前四种鱼最终混元模型仍待用户生成，本版照片来自原有网格。
