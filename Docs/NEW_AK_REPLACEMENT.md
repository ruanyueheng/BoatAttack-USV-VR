# 用户新 AK 模型替换

2026-10-24；Unity 2022.3.62f1 / 4af31df58517。用户现已授权将本批音效及 AK 修改提交到独立分支 `feature/ak-world-audio-20261024`；此前仅本地保存的限制解除。通过草稿 PR 审阅，不自动合并主线。

## 资源与兼容

- 用户提供的 `新ak.glb` 原样归档为 [NewAK.glb](../ArtSource/HuntingRifle/NewAK.glb)，SHA-256 为 `475d45ae14c309cc5590c00550b9ee8e70a892347007aa37c78ece5bc06743cb`。源文件本身不被修改，也不另行声明为开源许可。
- 保留源网格 50000 个三角面，统一比例缩放至资源空间枪长 0.66 米；当前正常持枪与检视统一使用 1.18 倍，最终约 0.78 米。较初次替换的 0.82 倍 / 0.54 米增大约 44%，没有沿单个轴拉伸，也不添加运行时 glTF 包。
- 转为既有 `FishingHunting/Rifle.fbx`，保留模型、预制体、材质、Diffuse 贴图 GUID 和资源加载路径。射击与背包检视共享同一个新网格及材质，旧模型源文件和 Git 历史仍保留。
- 从 GLB 的三张 4K PBR 贴图提取 2K 运行贴图：Diffuse 为 sRGB；法线作为 NormalMap；glTF 金属/粗糙贴图转换为 URP 金属/光滑贴图（RGB = 原 B，A = 1 − 原 G，线性）。保留原材质的双面显示，不额外添加发光效果。
- 左手移动到新模型前护木下方；右手仍握手枪式握把。枪口点放在新枪管端口稍前方。保留原射程、射速、购买价格、制作配方、掉落、枪声、检视声音与防穿墙逻辑。
- 右手握后柄、左手托前护木，握点间距约 22.4 厘米；两手采用独立手指弯曲和柄面接触约束，65% 前臂分摊旋转，避免全部拧在腕部。正常枪位放低并略向内横持，使枪托远离眼睛，保留中央视野；第一/第三人称共用同一真实网格与骨骼。
- 检视不再缩成 34 厘米单手托举，而以同样约 78 厘米双手横持，枪身向视野中部靠拢。桌面检视允许上下各 25°、左右各 20° 摇转（基础枪身偏航 -80°），不再允许将长枪反转穿入脸部的 360° 转动。此限制只用于猎枪，其他物品原有检视转动不变；XR 仍由实际控制器姿态决定。
- 碰障检测采用实际网格 14 个方向极值点，而非空包围盒角；保留眼睛到枪口的扫掠、枪口净空及第一命中规则。它仍是稀疏扫掠，不等于完整网格体积碰撞，极细障碍的局部可视穿插不作全面保证。

## 复建

转换脚本：[replace_hunting_rifle.py](../Tools/Art/replace_hunting_rifle.py)。输出及哈希见 [NewAK-manifest.json](../ArtSource/HuntingRifle/NewAK-manifest.json)。

```powershell
& E:/blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Art/replace_hunting_rifle.py
```

然后在关闭该项目交互编辑器的情况下，用固定 Unity 版本执行 `BoatAttack.EditorTools.FishingHuntingAssets.Build`，配置法线/线性贴图、URP 材质并更新预制体。旧 `import_hunting_rifle.py` 仅适用于历史 Blender 模型，不用于本次 GLB 复建。

本地预览位于 `Logs/NewAK/source-side.png`；游戏画面位于 `Logs/Hunting/first-person-rifle.png`、`new-ak-inspection-0.png` 等。Logs 不进入 Git。

## 验证边界

纯 C# 4353 项在本次比例/握姿调整后重跑通过。Windows 应用控制阻止了 `dotnet run` 启动生成的原生 apphost，最终使用已安装的 .NET 主程序执行同一已构建 `Tools/FishingTests/bin/Debug/net10.0/FishingTests.dll` 成功；没有修改系统策略。Unity 导入成功日志：`Logs/new-ak-import-8bee44ad7f92409fba4ea02cee05b78d.log`。

- 比例/握姿调整后的 [HUNTING PASS 日志](../Logs/usv-validation-9743617f1e2e42229602024ef8f7dc60.log)。
- 比例/握姿调整后的 [ACTION-AUDIO PASS 日志](../Logs/usv-validation-c575507663584759badb5a78d0b39ace.log)，枪械检视与单发音效未因换姿失效。

HUNTING 专项检查新网格三角数/尺寸/朝向、PBR 资源及颜色空间、78 厘米的统一比例、两手接触约束和前臂旋转分摊、真实腕/手掌与目标的匹配、检视端点及外部越界角限幅、实际网格顶点与镜头间距、站立/蹲伏/第三人称、16:9 / 5:4 / 21:9、±30° 俯仰、近墙禁止射击、实际兔子/海鸥命中与掉落、暂停/笔记本禁用和刷新存档。零角检视最近网格深度约 0.33 米；第一人称原近裁剪距离保持 0.04 米，没有靠修改相机/FOV 或拉长角色骨骼来规避检查。

测试使用隔离存档，不保存场景；不代表实体头显、实体控制器或所有角度/障碍物均已人工验收。项目原有的 Unity 重序列化与画质设置改动不收入本轮提交。

已知独立问题：退出播放时，旧水面系统可能在 `GerstnerWavesJobs.Cleanup` 释放后再次执行 `Water.LateUpdate`，抛出 `HeightJob.OutPosition has been deallocated`。模型替换前的日志 `Logs/usv-validation-dd1294c206754851b9c8c48d78ec9199.log` 已有相同异常；本轮没有扩改水面模块，不以专项 PASS 宣称整个工程无运行错误。
