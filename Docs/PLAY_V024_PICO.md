# 第24版 PICO 4 Ultra 安装与试玩

2026-10-14，版本0.24.0。APK位于 `Builds/FishingPrototype-v0.24.0-Android/FishingPrototype.apk`，大小248,741,215字节（约249 MB / 237 MiB）。应用名称为BoatAttack，包名 `com.ruanyueheng.tidefin.vr`。这是可独立安装的离线VR试玩包，使用开发签名。

## 安装

1. 用USB连接PICO 4 Ultra，在头显内允许USB调试。本次已读取到已授权设备：PICO A9210，系统5.15.5，Android 14，ARM64。
2. 在PICO开发者中心选中连接的设备，使用安装应用功能选择上述APK，等待安装完成。
3. 在头显的应用列表中找到BoatAttack并启动。首次运行会播放序章，右手A可跳过。电脑版和头显的进度保存在各自设备上。

可选命令行安装方式：先把PowerShell切换到BoatAttack目录，再调用Unity自带的ADB完整路径。这样无需预先配置ADB环境变量：

```powershell
& "$env:ProgramFiles\Unity\Hub\Editor\2022.3.62f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r ".\Builds\FishingPrototype-v0.24.0-Android\FishingPrototype.apk"
```

## 手柄操作

| 操作 | 手柄输入 |
|---|---|
| 航行前进 / 倒车 | 左摇杆前 / 后 |
| 船舶转向 | 右摇杆左 / 右 |
| 刹船 | 左扳机 |
| 航行加速 | 前推左摇杆并按住摇杆按键 |
| 岸上移动 | 左摇杆 |
| 岸上转身 | 右摇杆，30°分段转向 |
| 上下船 | 左手X；靠近泊位并先降低航速 |
| 切换快捷栏装备 | 左手Y |
| 与附近对象互动 | 右手A |
| 使用岸上装备 | 右扳机；采矿可使镐尖接触矿石并挥动 |
| 打开 / 关闭中文手册 | 右手B |
| 点击手册与世界空间按钮 | 右手射线指向按钮，按右扳机 |

驾驶需要两个手柄均处于追踪状态。手册首页提供背包、制作、日历、上下船和校准眼高入口；港口事务页的“下一航点”可循环至回潮渔屋。靠近床后，可在手册日历页选择休息至清晨。到钓具店柜台附近，按A与汐禾交谈，选择商品介绍或查看商品。

捕鱼沿用现有真实手柄交互：右手靠近钓竿并按握持键拿起；左手靠近摇柄、按握持键转动收线。船边控制台提供抛锚、鱼获收纳、测量、放生、交易和暂停操作。首次进入后可通过手册校准眼高。

## 打包检查

- Unity 2022.3.62f1，Android ARM64 / IL2CPP，Vulkan / Single Pass Instanced。
- PICO OpenXR Plugin 1.4.1，Unity OpenXR 1.8.2；启用PICO 4与PICO 4 Ultra手柄profile。
- APK版本名0.24.0，versionCode 1，minSdk 29，targetSdk 33。
- Manifest包含 `pvr.app.type=vr`、必需的头部追踪声明与手柄标识。
- APK包含Unity、IL2CPP和OpenXR的ARM64原生库；启动配置指定 `UnityOpenXR` 预初始化。
- APK压缩包完整性与v2签名验证通过。无需额外OBB文件。
- 已切回Windows编辑器构建目标，保留原有项目、XR、渲染设置和Windows试玩包。

SHA-256：

```text
83f39e7142696bd6cd24cc100041820994ccac6bb319aa8b0c38865a6a2b63fc
```

2026-10-14已通过ADB安装到连接的PICO设备，返回Success；设备包信息确认版本0.24.0、ARM64、包名com.ruanyueheng.tidefin.vr。未启动游戏，未认证双眼画面、真实手柄校准、持续帧率和佩戴舒适度。可在头显应用列表打开BoatAttack，先检查头部追踪、手册射线、船舶驾驶与上下船，再进行完整试玩。

本地构建与检查证据：`Logs/usv-validation-7be7b798771948579636fc154dec9eed.log`、`Logs/v024-android-badging.txt`、`Logs/v024-android-manifest.txt`、`Logs/v024-android-signature.txt`、`Logs/v024-android-archive.json`。Windows目标恢复与XR配置检查：`Logs/usv-validation-41b0074c62a941f3bd7821230a101997.log`。

项目使用固定SDK版本；系统要求来源：[官方PICO OpenXR SDK说明](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/README.md)。本次修正Android包装脚本对Unity Hub的误判，实际项目占用检查仍由共享构建脚本的工程锁负责。
