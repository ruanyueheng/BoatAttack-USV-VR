# 电脑 / PICO 双模式工作流

> 当前进展（2026-09-30）：v0.9.0 已加入 Android-only PICO OpenXR 候选配置、实体抄鱼/收纳与世界空间控制台，详见 [v0.9.0 说明](FISHING_V09_CATCH_LOOP.md)。下文旧日期内容保留为历史记录；真机、岸上 VR 与完整信标操作仍未验收。

## 当前实际状态（2026-09-23）

- 开发分支：vr-usv-dev；原始提交：6acf5f77。
- 共享输入已通过原版 HumanController 接入 Boat.Setup 的玩家控制链，不需要再挂第二个推进脚本。
- 默认编辑器和 Windows 使用 Desktop；Android 使用 XR，没有 XR runtime 时输入保持中性，不假装是有效 VR。
- Auto 只自动选择输入源，不切换相机、不安装 SDK、不启动 loader。
- 新增地图/交互按钮只是事件接口；尚无新地图界面或任务交互。
- 原始 Engine 不支持倒车，S/负向摇杆不会产生倒车推力。这轮没有改变其物理算法。
- 没有 XR Origin、双眼水面配置、座舱相机或 APK；不能报告“PICO 已配置好”。

## 版本纠正与回退

上一轮把 Unity 2022.3 与 URP 12.1 配套的建议不准确。机器上的 2022.3.62f1 自带 URP 14.0.12。
目标候选为 Unity 2022.3.62f1 / URP 14.0.12，必须通过实际导入、Shader 和 Addressables 验证后再锁定。
当前 ProjectVersion 仍是原版 2020.3.23f1，manifest 已撤回本轮的猜测式修改并恢复到 HEAD：URP 10.7.0。
不要把配置文件存在、SDK 已复制或纯 C# 测试通过误解为 Unity 已成功迁移。

本轮尝试复制的 PICO 6.1.1 SDK 和单独抽出的样例 prefab 已移到项目外 IntegrationStaging。
原因：本地 6.1 硬件要求文档列 PICO Space Pro / OS 6；不能仅凭包名断言 PICO 4 Ultra 兼容。
单独复制示例 prefab 也会遗漏 Starter Assets 等依赖，不采用该方式创建正式 rig。
后续按 PICO 4 Ultra 的实际固件版本选官方支持的 SDK，通过 Unity Package Manager / PICO MCP 安装并校验。

## 没有头显时

编辑器成功导入和编译之后：
1. BoatAttack > Development > Input > Desktop (no headset)。
2. BoatAttack > Development > Open Island (save prompt)。
3. 点击 Play，沿原航线验证驾驶。
4. W / 上 / Space 前进，A/D 或左右转向，左 Shift 减速，R 复位，F/Escape 暂停与恢复。
5. 普通手柄：右扳机前进、左摇杆转向、左扳机制动、Start 暂停、Select 复位。
6. E/Enter（手柄 X）预留交互，M（手柄 Y）预留地图；当前只是事件，不代表新玩法已经实现。

设置资产是 Assets/Resources/USVRuntimeSettings.asset。菜单修改 editorMode，不改 Windows/Android 默认模式。
暂停在 Update 中响应，因此 Time.timeScale=0 时仍可恢复；离开窗口后油门与转向清零。

## 有 PICO 时（尚待配置与验证）

1. 确认设备是 PICO 4 Ultra，记录 PICO OS 版本，开启开发者模式和 USB 调试并授权这台电脑。
2. 先用官方兼容 SDK 最小场景验证双眼图像、头部追踪、两个手柄、暂停/恢复。
3. 仅 Android 启用合适 loader；Windows 默认不启动 XR，避免电脑调试依赖头显。
4. 将 XR Origin 座舱与原第三人称相机通过明确模式切换，保留桌面相机；不得用“关闭所有相机”破坏水面反射/深度相机。
5. 再接入原海岛、水面、浮力、输入、World Space UI。
6. 使用实际 ARM64 Android 构建测试，不能拿编辑器截图或串流体验代替独立 APK。
7. 手柄映射候选：左摇杆纵向油门、右摇杆横向转向、左扳机制动、左主按钮复位、右主按钮暂停、右扳机交互、右次按钮地图。
8. 当前 XR 输入要求双手均有追踪才输出驾驶轴；丢失一只手不会继续推进，惯性仍由物理处理。

## 真机舒适性 / 性能验收

计划使用稳定座舱参照、可选减弱船身俯仰/横滚传递、平滑加减速；不得覆盖头显真实头部追踪。
VR 不沿用原版随速度扩大 FOV 和 Cinemachine 主动摆动 HMD 的逻辑。
平面反射、水面中间纹理、固定注视点渲染、Multiview、Vulkan/GLES 必须分别测试，不承诺把所有“优化”同时开启就更快。
帧率目标按设备实际支持的显示模式确定并测量，不能把尚未核实的刷新率写成验收事实。

## 验证命令（都不会启动 Unity 或安装 SDK）

PowerShell：

    & "E:\University\Homework\虚拟现实作业\BoatAttack\Tools\Check-Environment.ps1"
    dotnet run --project "E:\University\Homework\虚拟现实作业\BoatAttack\Tools\InputTests\InputTests.csproj" --configuration Release

环境脚本只确认文件存在与版本差异；纯 C# 测试覆盖模式选择、按键边沿、死区、异常数值和失焦。
完整 Unity 编译、Windows build、Addressables build、APK 安装与设备运行仍要另行通过。
