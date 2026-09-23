# 集成状态与下一次接续

检查日期：2026-09-23。不要将本文件作为已完成运行验收的证明。

## 已完成

- 创建 vr-usv-dev 分支，保留原版基准提交。
- 共享输入状态、键鼠/普通手柄输入、Unity XR 手柄输入、模式选择及输入处理器。
- 将 HumanController 接到共享路由，继续使用原 Boat/Engine，避免两个控制器同时施加推进力。
- 编辑器菜单与默认 Desktop / Windows Desktop / Android XR 设置资产。
- 18 项纯 C# 回归测试通过；Unity 相关适配器尚未经过 Unity 编译与运行验证。
- 检查 2022.3.62f1 的 Unity、Android SDK、NDK、OpenJDK 路径存在。
- ADB devices 检查成功，未发现已连接设备。
- 本轮未经验证的依赖改动撤回；SDK 和示例 prefab 移出活动 Unity 工程。

## 尚未完成

- Unity 2022.3 / URP 14 导入升级；当前版本标识和依赖仍是原始版本。
- PICO 4 Ultra 固件与 SDK 支持矩阵核对。
- PICO loader、XR Origin、座舱相机、World Space UI 与双眼水面。
- 原版水面、RenderPass 和 Addressables 在目标编辑器下回归。
- Windows 可执行程序、Android ARM64 APK，以及真实运行验收。
- 新玩法；所有候选只是设计，不默认实施救援主线。

## 具体阻碍

- Unity 自动启动调用被执行策略拒绝；没有成功运行 Unity，不能靠更换启动器绕过。
- 当前可见工具只有 PICO 知识图谱，没有 pico_xr_package / pico_xr_status 等 Unity MCP 编辑器工具。
- GUI 连接也不可用（auth token unavailable），没有用其他技术绕过 UI 限制。
- 没有已连接的 ADB 设备，不能核对固件或实测 APK。

## 继续顺序

1. 用户打开 Unity 编辑器并连接 Unity/PICO MCP；保留首次导入 Console 和包解析错误。
2. 在开发分支内进行可回退的 Unity 2022.3 / URP 14 迁移，用 Package Manager 逐项修依赖。
3. 先验收原海岛 PC Play Mode -> Addressables -> Windows build。
4. 按 PICO 4 Ultra 实际系统选 SDK，先最小 XR 场景再海岛，最后 APK 真机验收。
5. 将最终解析成功的依赖锁文件纳入版本管理（原 .gitignore 忽略它，目前不产生伪造锁文件）。
6. PC 与 VR 用同一地图/任务/船只逻辑，不维护两个玩法分支。
