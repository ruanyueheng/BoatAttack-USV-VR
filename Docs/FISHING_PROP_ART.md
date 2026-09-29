# v0.6.1 港口精细模型接续与验证（2026-09-29）

## 找到的断点与接续范围

工作区已有 `Tools/Art/generate_harbor_props.py`、`ArtSource/FishingProps/HarborProps.blend` 和 10 个 FBX，但运行时代码仍生成基础几何占位物。本次接续范围为这批港口任务道具，不推断其他会话的全部需求。

- 接入信标塔身、可转动灯头、独立透镜和安装后的维修线圈。
- 接入三类物资箱（鱼纹、双环、条纹）、箱上待拾取线圈、维修记录板和双环航标。
- 航标整体朝向信标，移除重复的旧程序化圆环；任务点、互动范围、存档格式不变。
- 保留线圈拾取隐藏、修复显示安装件、透镜亮起、灯头跟随校准角度的逻辑。
- 使用共享 HarborPalette 材质和现有 URP 顶点色 shader；不添加碰撞体或实时灯光。
- 增加仅适用于 FishingProps 的导入策略和稳定 GUID，不修改原船艇配置。
- 新增 validate_harbor_props.py，回读所有 FBX 并生成独立 Blender 素材预览。
- 版本升至 0.6.1，构建输出使用新目录，保留旧 0.6.0 试玩包。

## 许可证状态

上一轮 Editor 报无有效许可证；本轮 Unity Hub 已在运行，重新启动 Editor 后日志确认 `Successfully updated license`，项目已成功编译与运行。没有购买许可证、修改账户或绕过授权检查。

## 本轮验证

- 首轮任务验证真实失败：测试按旧占位物名称 Prismatic lens 找透镜。改为检查导入网格 BeaconLens，保留亮灯断言。
- 新增 10 个 FBX 的 Unity 检查：单网格/单 renderer、顶点颜色、单 submesh、每模型少于 14,000 三角面、米制尺度、无碰撞体和 shader 支持。
- 增加修复前安装线圈隐藏、修复后显示、灯头目标方向检查。
- Unity 2022.3.62f1 FishingExpeditionCapture：通过。包括模型导入、任务交互、步行、校准取消/确认、短航行、捕获记录及存档恢复。
- Unity FishingSmoke：通过，覆盖原捕鱼、HUD、相机、交易、天气与存档流程。
- FishingBuild：通过，生成非 Development Windows v0.6.1 试玩包。
- 纯捕鱼规则：814 项通过；纯输入/渲染策略规则：62 项通过。
- Blender FBX 回读：10 个模型通过，报告见 ArtSource/FishingProps/Preview/roundtrip-report.json。

本地日志（不上传）：
- 任务/模型：Logs/usv-validation-18bb9bf3943e4e47a15c1c32560ae4b5.log
- 捕鱼：Logs/usv-validation-d0cf63f1dcad40b4ae38e201b047cf51.log
- 构建：Logs/usv-validation-cd0917238f4f4c8eadc6976f612f0862.log

## 实际 Unity 相机截图

![维修箱与线圈](Images/HarborProps/01-component-crate.png)
![修复前：倾斜灯头与暗透镜](Images/HarborProps/02-beacon-broken.png)
![修复后：灯头对准、透镜发光与安装线圈](Images/HarborProps/03-beacon-restored.png)

已目视检查上述道具材质、朝向及修复前后差异。这些是隔离自动验收中的定向相机截图，不是完整 HUD 或物理键鼠输入证据。箱体下方地表在本分支截图中仍为灰白；海岸/地形 PR #12 不属于本次模型补丁，不在此合并或宣称修复。

ArtSource/FishingProps/Preview/harbor-props.png 是独立 Blender 素材渲染，不是游戏截图。透镜为不透明风格化发光材质，不是物理透明折射。

## 复跑命令与人工发布门槛

```powershell
./Tools/Run-UnityValidation.ps1 -Suite FishingExpeditionCapture
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke -Player Builds/FishingPrototype-v0.6.1/FishingPrototype.exe -Session v061-art
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python Tools/Art/validate_harbor_props.py
```

完整港口→各岸点→北岛→返港键鼠路线、持续帧耗、PICO 真机仍未验证；PR 保持 draft，不以绿色自动化代替人工发布门槛。Builds、Library、Logs、账号与许可证信息均不上传 GitHub。

模型由本地几何脚本生成。本次没有新增外部参考图或复制商业游戏素材；先前脚本提到的参考图记录未在工作区找到，不补造来源。

补充：新 v0.6.1 Windows Player 的独立验收 Smoke 已通过（12 个案例及隔离存档/生产规则检查）；日志为 Logs/desktop-acceptance-8a710aa9c8cc47cfa869c06400764ce2/player.log，不代表物理输入认证。
