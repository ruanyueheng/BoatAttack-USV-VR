# v0.46.0 PICO 4 Ultra 迁移

本版 Android 包与 Windows 0.46.0 共用同一套捕鱼玩法、存档、场景和 UI 数据。PICO 端使用 OpenXR 单通道立体渲染、PICO 4 Ultra 控制器配置、Vulkan、ASTC、固定注视点和 LZ4 分块资源。

本轮修复了造成 PICO 交互缺失的输入差异：PICO 4 Ultra 的握把按键以模拟 `grip` 为主，游戏现在统一用模拟握把阈值并保留数字按键回退；扳机同样支持模拟值回退；左菜单键同时读取 Unity 通用菜单键和 PICO `menu` 输入。抓取物品、提灯、掀箱盖、手册握书/翻页、果树、攀爬、秘境和船控共用这一层。

交付包：

- `Builds/FishingPrototype-v0.46.0-Android/FishingPrototype.apk`
- SHA-256：`870DEBE90CA10D9B95122803D2767037431393AA5CC6AFC85E3522AC41AB4A74`
- ARM64，versionCode 46，Vulkan，ASTC，LZ4 分块资源；APK 内 `assets/bin/Data/data.unity3d` 保持未被外层 ZIP 压缩。

本机验证通过脚本编译、Addressables、Android Gradle/IL2CPP、APK 条目读取和 LZ4 布局检查；当前没有连接 ADB 设备，因此未在本轮自动安装或实机运行头显。
