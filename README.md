# BoatAttack USV VR

一款以海岛生活为主题的第一人称钓鱼与探索游戏，基于 Unity Boat Attack 开发，支持 Windows 桌面和 PICO VR。

驾驶钓艇穿行海湾，寻找不同天气与潮汐下的鱼群，靠岸探索岛屿和秘境，再把鱼获与旅途收集的材料带回自己的海边家园。

## 游戏内容

- **出海钓鱼**：蓄力抛竿、咬钩提竿、张力控制、遛鱼与抄网；收集鱼种图鉴，完成委托并升级船具与钓具。
- **海岛探索**：驾驶、登岛、游泳和攀爬，探索风铃岛、潮林岛、沉船沙洲与星望遗迹，解开四境机关。
- **家园生活**：种植作物、照料果树与动物、储物、手工制作、锻造和烹饪。
- **海湾故事**：与居民交谈，收集信件与旧海图，体验海上事件、灯塔值夜和寻宝航程。
- **季节与昼夜**：天气、潮汐、日历、节庆与限定商船共同影响海湾生活。
- **VR 交互**：握持钓竿与工具、转腕收线、抓取鱼获、翻阅手册，并提供转向、移动与手部校准设置。
- **航程存档**：提供三个存档槽，以及独立的自定义日期与物资模式。

## 开发环境

| 项目 | 版本 / 平台 |
| --- | --- |
| Unity | 2022.3.62f1 |
| 渲染管线 | Universal Render Pipeline 14.0.12 |
| 输入 | Unity Input System、OpenXR |
| 目标平台 | Windows、Android / PICO 4 Ultra |
| 大文件管理 | Git LFS |

## 获取与运行

先安装 Git LFS，再克隆项目：

```sh
git lfs install
git clone https://github.com/ruanyueheng/BoatAttack-USV-VR.git
cd BoatAttack-USV-VR
git lfs pull
```

在 Unity Hub 中添加仓库根目录，使用 **Unity 2022.3.62f1** 打开，等待依赖解析与资源导入。

打开 `Assets/scenes/Fishing/fishing_Island.unity`，进入 Play Mode 即可体验钓鱼与海岛探索。`Assets/scenes/main_menu.unity` 为原 Boat Attack 菜单入口。

模型、纹理和音频使用 Git LFS 管理，建议通过 Git 克隆项目，确保资源已完整下载。

## 基本操作

| 操作 | Windows | PICO VR |
| --- | --- | --- |
| 行走 / 驾驶 | WASD | 左摇杆 |
| 船只加速 / 刹车 | Ctrl + W / Shift | 左摇杆按下或左握把 / 左扳机 |
| 互动 / 交谈 | E | A |
| 上下船 | 低速靠岸后 G | 低速靠岸后 X |
| 查看手册 | J | B 或左菜单键 |
| 暂停 / 设置 | Esc | 手册中的设置页 |

钓鱼、手工和工具使用会随当前情境变化，完整按键可在游戏内手册的「操作说明」中查看。VR 的移动方式、转向和手部角度可在设置中调整。

## 构建

通过 Unity Hub 安装 Windows Build Support 和 Android Build Support（含 SDK、NDK、OpenJDK）。关闭当前项目的 Unity 编辑器后，在仓库根目录使用 PowerShell 执行：

```powershell
# Windows
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild -TimeoutSeconds 3600

# Android / PICO
./Tools/Run-FishingAndroidBuild.ps1
```

构建输出位于 `Builds/`，不纳入源码仓库。Android 脚本会为包含中文的项目路径创建临时英文盘符映射。

## 项目结构

```text
Assets/            场景、游戏脚本、美术与音频资源
Packages/          Unity 包依赖与水体渲染包
ProjectSettings/   Unity 工程设置
Tools/             构建、性能检查与回归测试工具
ThirdParty/        第三方资源许可
```

使用 .NET 10 SDK 可运行不依赖 Unity 或头显的规则测试：

```sh
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release
```

## 致谢与许可

本项目基于 [Unity Technologies / Boat Attack](https://github.com/Unity-Technologies/BoatAttack)，使用 [Unity Companion License](LICENSE.md)。原项目及第三方资源的作者、来源与许可见 [CREDITS.md](CREDITS.md) 和 [ThirdParty/](ThirdParty/)。

第三方美术、字体和音频分别遵循各自许可；仓库公开不代表所有资源均可任意再分发。
