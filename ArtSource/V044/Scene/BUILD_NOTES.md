# V044 四个 AI 地标接入

复用 `ArtSource/V044/AI/hunyuan/2026-10-28/` 中已生成的 ancienttree、windmill、galleon、lighthouse GLB；本轮接续没有再次调用建模生成服务。

- 输出：`Assets/Resources/FishingV035/{AncientTree,Windmill,WindmillRotor,Galleon,Lighthouse}.fbx`。
- 贴图、材质、LOD：`Assets/Resources/FishingV044/Scene/`；每个模型1024像素绘制贴图，Android ASTC 6×6。
- 可编辑来源：本目录四个 `.blend`，贴图打包入文件；风车来源包含已拆出的叶片。
- 原定位标记来自 `ArtSource/V044/Backup/Scene/` 的五个原始 FBX。保留这些输入，导出脚本才能在新检出中复现原庭院、原坐标和叶片来源。
- 古树11500面；风车塔身和叶片合计9500面；沉船14000面；灯塔6500面（导出阶段预算）。最终重导入统计见 `export-validation.json`。

使用工程既有 Blender 5.2 后台流程：

```text
blender --background --factory-startup --python-exit-code 1 --python Tools/Art/build_v044_scene.py -- AncientTree Windmill Galleon Lighthouse
blender --background --factory-startup --python-exit-code 1 --python Tools/Art/build_v044_scene.py -- --lod AncientTree Windmill Galleon Lighthouse
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite FishingV044SceneArt
```

导出保留原 FBX 对象/网格名和标记的世界坐标。Unity 的既有导入路径带 X 轴反射，灯塔运行位置由 `LighthouseLens` 标记确定。旧灯塔几何从庭院合并网格中移除；其余庭院和光源标记保留。古树/沉船碰撞与可通行路线由安装器处理；复杂根部和舱室使用简化碰撞。

最终实景截图在 `Docs/Images/V044/`。沉船局部细节、市场船尖刺及旧场景穿插仍需后续精调；结构检查与编辑器截图不等于头显实机验收。
