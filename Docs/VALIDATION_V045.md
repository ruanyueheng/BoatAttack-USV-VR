# v0.45.0 交付与PICO实机验证（2026-10-28）

## 最终安装包

| 交付 | 路径 | 验证 |
|---|---|---|
| Windows程序 | `Builds/FishingPrototype-v0.45.0/FishingPrototype.exe` | 原生构建通过；88场景发布包冒烟通过 |
| Windows便携包 | `Builds/FishingPrototype-v0.45.0-Windows.zip` | 210个文件逐项SHA-256与程序目录一致 |
| PICO安装包 | `Builds/FishingPrototype-v0.45.0-Android/FishingPrototype.apk` | ARM64、v2签名、70个ZIP条目全部可读；设备上安装文件SHA-256相同 |

Android为0.45.0 / versionCode45 / minSdk29 / targetSdk33，使用与第44版相同的开发签名。原存档schema20不变。最终哈希清单为 `Builds/v0.45.0-builds.json`。

Windows ZIP SHA-256：`1199397A203E584120D6A1A15FB40D87C6F9D246DA36B2D345072769235F9F65`。

APK SHA-256：`F15F22CF73E12E86E25F08B06400EC080CB432E96E5E606678E8BD17CF9BF7FC`。

## 第44版在PICO上一直加载的检查

在用户已连接的PICO 4 Ultra（A9210）、PICO OS5.15.8 / Android14上实际复现。原应用进入Unity与OpenXR会话创建后，没有完成场景首帧；用户确认一直加载或黑屏。系统记录加载超时。没有把`am start`返回`Status: ok`当成游戏已启动。

设备系统报告中的原生堆栈，经本机同版本Unity符号解析，显示：

- 主线程等待 `PersistentManager::ReadObject`、`ShaderNameRegistry::FindShader`；
- `Loading.AsyncRead`长时间忙于 `ZipFile::inflateRead/read/tell` → `FileSystemAndroidAPK` → `AsyncReadManagerThreaded`；
- 原包使用 `BuildOptions.None`，场景数据为两千多个散装APK条目；资源总量约953MiB，其中一个合并草地资源约132MB。包结构与实际卡点均在本地核实。

仅把散装Data条目改为ZIP_STORED的诊断包仍未解决，未作为最终方案。最终将正式Android构建改为 `BuildOptions.CompressWithLz4`，产生未再套ZIP压缩的 `assets/bin/Data/data.unity3d`（671775198字节），支持分块读取。没有改变OpenXR/PICO SDK版本、Vulkan或固定注视点设置。该变更在同一台设备上消除了加载卡住，之后实际显示双眼场景。

最终打包脚本加入LZ4容器存在及不受外层ZIP压缩的检查，防止后续构建意外退回旧布局。

## 实机结果

- 安装最终APK并启动，双眼可见海湾、船、钓竿与手部；保存并查看实际设备截图。
- 用户明确确认：“画面和两只手柄正常”。包含转头与两手柄移动的现场反馈。
- 冷启动复测：日志23:36:18.663开始Unity初始化，23:36:22.624进入XR FOCUSED，23:36:25.961游戏控制器初始化完成，23:36:27.028材质预热完成。约7.3秒初始化、8.4秒完成预热，仅代表本次设备和缓存条件。
- 应用在随后的系统菜单切换／焦点恢复后仍能显示游戏；日志无本应用C#异常或Java致命异常。
- 读取头显内已安装的`base.apk` SHA-256，与本地最终APK逐字一致。实机记录见 [V045_PICO_DEVICE.json](V045_PICO_DEVICE.json)。

![第45版PICO实际双眼画面](Images/V045/pico-final-stereo.png)

原始设备日志、系统报告和未采用诊断包保存在忽略目录 `Logs/V045/PICO/`，没有上传系统报告或用户房间画面。图中仅为游戏画面。

## 其他检查与保留项

九个场景专项3159条CHECK、4728项纯规则、70项输入检查通过。四境完整机关与首次回响跨剧情播放检查通过。VR相册、翻页／握边、箱盖／梯子手势、市场声音、居民驻足点与掌心地图修复见 [整合核对](CLAUDE_INTEGRATION_V045.md)；计数见 [V045_TESTS.json](V045_TESTS.json)。

发布包测试还抓到两项旧遗漏并修复：无图形模式下预热误调用渲染；拿壶／打水的旧验收站位和接近过程等待已过时。最终Windows发布包88场景冒烟通过，真实存档未用于自动验收。

本次实机通过的是启动、双眼图像、头部及双手柄跟踪和焦点恢复。尚未认证全程72/90Hz、所有新手势的舒适度或四秘境耳机听感；原205处交叠、市场船近看、井绳全程手摇、云形重复等仍列为未完成，不宣称所有Claude计划均已实现。
