# 第29版验收

当前版本0.29.0；测试均使用临时隔离存档。

## 已通过

- 纯规则3820项、输入策略70项。
- V029专项：真实Input System鼠标按下、跨帧蓄力、释放进入Waiting；倍率边界和无效输入；五个新模型、贴图与缩略图；烤鱼体积；各区物种配置；旧存档补齐三工坊、重复加载不复制；逐个通过正常E入口打开西湾工作台、熔炉、锻造台；43种物品均实际可见且检视不消耗库存；夜间提灯；游动半径变化；30秒自然更新出现非强制跃水。
- 第一人称专项：双掌握点、真实骨架可达、俯仰、光学过渡、第三人称和恢复边界。
- 提灯专项、海风烧烤/海漂物/海鸥专项、工坊、钓获事务、V027家园和UI回归。

最终3500面模型的Smoke预算、Hunting命中/拾取/复活及实际界面图标检查通过；没有放宽几何、臂长或库存阈值。V029新增实际8倍相机投影及快捷位保持检查通过。

## 最终交付包

| 文件 | 大小 | SHA-256 |
|---|---:|---|
| Windows ZIP | 418751582字节 | F8914DB9E2209B3099838369587F1AA14B0D2FF03937FED62365666E8559ED76 |
| PICO APK | 272149654字节 | 91B17D2B62ADA1BA9814629CB5B3ED6576F875C45C0240BA635E7F95EA561D72 |

- Windows ZIP的210个文件与最终构建目录逐一比对一致。
- APK的927个归档条目可读，版本0.29.0/code29，包名com.ruanyueheng.tidefin.vr，ARM64，v2签名及与第28版相同的更新证书均通过。首包发现构建器硬编码code28，已更正并重新构建；该首包不作为交付包。
- 正式Windows程序的53个隔离验收场景启动/状态检查通过：Logs/desktop-acceptance-79fe759679134361b54940abe4bca7d3/player.log。
- 构建后已恢复Windows编辑器目标。包清单见Builds/v0.29.0-builds.json；独立验证见Logs/v029-package-verification.json。
- 主要最终日志：Logs/v029-final-build.log、Logs/v029-android-version29.log、Logs/v029-hold-final.log、Logs/v029-release-validation.log；签名及版本证据为Logs/v029-apk-signature.txt和Logs/v029-apk-badging.txt。

## 图像范围

已复看独立Blender模型图、Unity实际世界/手部渲染、三工坊、整鱼检视、提灯开关对照、2倍/8倍视野和海鸥叼鱼。部分图例见Images/V029。

后台首次截图的黑屏INVALID图保留为失败记录。用户随后明确授权显示窗口，正式Windows程序已完成最终53张1600×900完整IMGUI界面截图，全部HUD证明标记通过；五张总览和钓获、手册、望远镜、提灯、工坊全尺寸画面已复看。倍率提示在亮地面上对比不足，已加昼夜主题底板，重建两端并重新采集最终53张，未发现新的视觉阻断。游戏窗口已自动关闭。

最终截图清单位于Logs/desktop-acceptance-c2ddcaa6e49e4f1bbbd5ea7b6a73affb/capture-20261016-043213-244dfcae/manifest.json，复看记录位于Logs/v029-fullhud-visual-review.json。最终构建记录为Logs/v029-optics-final-build.log，原生截图与状态验收为Logs/v029-visible-final-capture.log。

- [钓获提示](Images/V029/catch-feedback-fullhud.png)
- [手册握姿](Images/V029/handbook-fullhud.png)
- [倍率提示](Images/V029/binocular-fullhud.png)
- [提灯夜景](Images/V029/lantern-fullhud.png)
- [截图总览1](Images/V029/fullhud-sheet-1.jpg)、[2](Images/V029/fullhud-sheet-2.jpg)、[3](Images/V029/fullhud-sheet-3.jpg)、[4](Images/V029/fullhud-sheet-4.jpg)、[5](Images/V029/fullhud-sheet-5.jpg)

## 修正与失败记录

- 首轮直接验证蓄力通过；扩大到真实鼠标事件后，发现编辑器回调的输入边沿不代表运行帧。改用临时InputSettings副本和运行时Update探针，不改项目输入设置。
- 西湾发现实际缺陷：熔炉可达但维修记录抢走互动目标。修复共享目标选择后，三种设施均通过正常交互。
- 第一人称回归发现缩小后的手册掌心略低于屏幕；抬高并校准双侧握姿，保持原可见性和骨架阈值。
- 原始导入模型超出既有5000面海洋预算；导出降为3500面并保留原贴图，原件完整保留。未放宽预算断言。
- 视觉检查发现新海鸥的喙与旧叼鱼挂点不一致；按新网格喙部位置校准，并修复随机游动状态下俯冲结束的回升轨迹。

## 限制

没有PICO真机追踪、帧率、双眼画面或佩戴舒适度验证。自动化姿态与截图不作硬件认证。未重置或直接编辑用户的正式存档。
