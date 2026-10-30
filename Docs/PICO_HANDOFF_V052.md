# PICO 0.52 开发接手

仓库是私有的；先由仓库所有者邀请协作者，使用自己的 GitHub 账号登录。PR 未合并前检出 `feature/v052-pico-experience`，合并后使用 main。

1. 安装 Git、Git LFS，以及 Unity Hub 中的 **Unity 2022.3.62f1**。勾选 Android Build Support、Android SDK & NDK Tools、OpenJDK。
2. 在英文路径克隆，例如 `C:/Projects/BoatAttack-USV-VR`，避免 Android 构建的中文路径限制。不要只下载源码 ZIP。
3. 执行：

```powershell
git lfs install
git clone --branch feature/v052-pico-experience https://github.com/ruanyueheng/BoatAttack-USV-VR.git
cd BoatAttack-USV-VR
git lfs pull
```

4. 在 Unity Hub 添加克隆的工程根目录。首次打开等待 Package Manager 和资源导入完成。依赖版本由 Packages/manifest.json 和 packages-lock.json 固定，PICO OpenXR SDK 会按固定 Git 提交下载，不需要复制原电脑的 Library。
5. 打开 `Assets/scenes/Fishing/fishing_Island.unity`。桌面可直接运行；PICO 真机使用 Android 平台、ARM64/IL2CPP，沿用已提交的 OpenXR/PICO 配置。
6. 头显打开开发者/USB 调试，用数据线连接并在头显允许调试。`adb devices` 应显示 device，再运行构建入口：

```powershell
./Tools/Run-FishingAndroidBuild.ps1
```

构建脚本调用 FishingXRConfiguration.BuildAndroid，自动构建 Addressables 和 APK。运行前关闭占用该工程的 Unity 编辑器。APK 位于 `Builds/FishingPrototype-v0.52.0-Android/FishingPrototype.apk`，可用 `adb install -r` 覆盖安装并保留本机存档。应用包标识为 `com.ruanyueheng.tidefin.vr`。

协作时每人使用独立 feature 分支提交 PR，避免同时编辑同一场景/预制体。不要提交 Library、Temp、Builds、Logs、UserSettings、个人密钥或头显授权信息。

## 当前验证

0.52 已完成 Windows/Android 构建，并安装到原开发者的 PICO。纯规则 4815 项、输入 74 项及 Unity 场景检查通过；OpenXR 模拟手柄已验证扳机抛竿、摇杆按压收竿、菜单恢复和快速回家。详细边界见 VALIDATION_V052.md。新电脑首次导入和同学的实体 PICO 尚未验证，需要各自检查。

右握把拿竿，右扳机按住蓄力、松开抛竿；按下右摇杆收竿起航；B 手册首页快速回家。待处理鱼须明确存放或放生。
