# 0.49 PICO 实机排障记录

日期：2026-10-29。

已完成：
- 用户开启PDC设备常亮后，确认screenoff=65535、sleep=-1、sleep_by_static=0，连续检查mWakefulness=Awake。
- 新增仅ADB boolean intent `fishing-pico-tour=true` 启用的原生XR巡检入口。使用temporaryCachePath独立档，检查焦点、XR、眼高校准与水深Ready后才推进；脚本命令依次打开8个手册页、走3个钓点的抛竿/搏鱼/几何抄网/入箱、检查重复结算与存档回读。它不等于真实手柄握持验收。
- 修正自动化在XR下仍进入真实手柄门禁的问题；真人输入保护保留。几何抄网辅助仅允许隔离测试档。
- 捕鱼回归74项CHECK通过：`Logs/usv-validation-10ec3dd29ad0403bb7661990609c0515.log`。最初一次因Bee缓存引用旧R盘失败，恢复同一项目盘符后通过。
- ARM64/Vulkan/ASTC/LZ4 Android构建通过：`Logs/usv-validation-568d5ff33283464b9687049e33115432.log`。
- `adb install -r`已更新到0.49.0/code49，APK64个条目全部CRC可读，含ARM64运行库，LZ4数据容器外层ZIP无重复压缩。设备base.apk与本地SHA256相同：`51d0cf67cf3ae50f5f03da7d9d9611171d95588d4330efda8832ef366efc4dea`。
- 玩家手动/自动存档前后SHA256完全一致，未用玩家进度测试。

尚未完成：
PICO场地/透视设置`com.pvr.seethrough.setting`反复自动重启并占用VR画面，`sys.pxr.boundary.ready=0`。游戏Activity可以启动，但没有获得VR焦点，0.49的巡检未进入，不能报告实机PASS。系统设置72FPS不属于游戏；此前0.47约30FPS风险仍未闭合。

已尝试正常返回、退出设置和重启游戏。用户不在头显旁，要求远程跳过确认。官方企业API有SFS_BOUNDARY_CONFIRMATION_SCREEN；先用独立命令行程序只读查询，因设备Binder上下文报Bad file descriptor失败，未修改该开关。后续创建并安装普通Activity临时工具的动作被自动审批拒绝，仅返回blocked by policy，未提供更详细理由。该动作未执行，也未通过其他途径绕过拦截。

收尾：保留用户要求的常亮，游戏恢复普通启动。未留待未来获得焦点时自动开始的测试意图。完整安装清单见`Builds/v0.49.0-pico.json`。
