# 第32版验证记录

日期：2026-10-17（Asia/Shanghai）。Unity 2022.3.62f1 / URP 14.0.12。所有自动玩法检查使用隔离存档。

## 已确认

- 纯规则3865项通过；输入/呈现策略70项通过。
- V032：实时展示船、暂停期间启航移动、恢复原船位置与存档、实际CG入口、主音量、设置输入边界、真实相机视野、画质阴影开关、两岛实体地面与实际下船、拱岩预算、地图烘焙来源通过。
- V019：四个新秘境的实际行走、操作、可解性、奖励只结算一次、返回岛屿、对话与游泳攀爬通过。最后加入的实体液压浮桥再次通过同套回归。
- V022：17个航点从渔港均有可通行水路，并逐段验证船体净空；跨岛路线、旧边界开通及信件流程通过。
- V031、第一人称、Catch、FishingSmoke通过；覆盖手册/提灯、人物视角、实际抄网、待处理鱼获持久化及防重复收益、商店和天气环境恢复。
- 首个Windows构建成功，其65场景完整HUD截图通过，用于发现与修正画面。最终Windows包再次通过70场景完整HUD截图与70场景Smoke；每张1600×900且通过帧末探针检查。
- Tripo原件467496面，优化后19995面；原始纹理、拱洞轮廓、FBX导入轴向与真实三角网格碰撞已核对。

测试命令入口：`Tools/Run-V032Validation.ps1`、`Tools/Run-FishingBuilds.ps1`、`Tools/Run-FishingAcceptance.ps1`。单项通过的实际日志索引在 `Logs/v032-verified-suites.json`。模型摘要在 `ArtSource/V032/arch-validation.json`。

## 验证范围

编辑器相机图不包含IMGUI，不能替代实际游戏窗口的HUD证据。独立Windows验收通过帧末截图与4像素探针检查完整画面，验收条幅在截图时收起，原始清单记录场景、尺寸与存档路径。

没有进行PICO真机佩戴、控制器追踪、立体画面、帧率和舒适度验收；Android构建成功也不代表这些真机项目通过。保留既有用户素材、无关本地修改和正式存档。

## 最终交付

- Windows便携包：210个文件逐一与构建目录比对，SHA256 `7E9160624EED671E8BF5C90BCA25FC21C62D55C24B04FA1489F4025743C3A2BD`。
- PICO APK：版本0.32.0、versionCode32、ARM64；ZIP条目和v2签名检查通过，签名证书与V031一致。SHA256 `BFC489A98707DDB3DE872DED5DDB8B43353A88CB7B924E3AA815C3948E187737`。
- 最终双端构建：`Logs/v032-smooth-builds.txt`；Windows目标已恢复，临时R盘别名已清除。
- 最终完整窗口：`Logs\desktop-acceptance-6f6f4a91d9f24f91ab84dcc61132b558/capture-20261017-081936-27b0016d`，70场景，每张1600×900。精选画面在 [V032截图目录](Images/V032)，原图清单带sourceDirectory记录。
- 最终运行：`Logs/v032-smooth-native.txt`、`Logs/v032-smooth-smoke.txt`；关闭后的普通存档与偏好边界检查通过。
- 收尾证据：`Logs/v032-closeout.json`；包摘要：`Builds/v0.32.0-builds.json`。

![实时启航菜单](Images/V032/start-menu.png)

![按键图示](Images/V032/settings-controls.png)

![新岛与Tripo拱岩](Images/V032/white-sail-cove.png)
