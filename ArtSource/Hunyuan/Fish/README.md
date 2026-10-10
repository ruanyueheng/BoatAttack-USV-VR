# 第15版混元鱼种资产

用户于 2026-10-11 提供 8 个手动导出的混元 GLB，原位置为项目同级的 `混元3D/`，完整原件现在保存在 `Originals/`，以 Git LFS 跟踪。未改写原始 GLB；每个原件的 SHA-256、原始/运行面数及展示长度记录在 `manifest.json`。这些是风格化游戏鱼，不用于生物学鉴定。

运行副本在 `Assets/Resources/FishingFish/`：每种一份 FBX、1024×1024 的颜色贴图及透明图鉴缩略图。使用现有 FishingPalette 光照、夜景和立体渲染路径；大尺寸法线与 ORM 贴图保留在原件中，不进入运行包。珍珠变体共用网格和贴图，材质作暖金色偏移。GLB 导入时的节点旋转、反向鱼头、重复顶点和 UV 均在转换中处理。

再生成（Blender 5.2）：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/Art/import_hunyuan_fish.py
```

脚本包含单网格、长度轴、真实三角面数和贴图来源断言。Unity 的 FishingLivingCoast 验收检查实际加载的 FBX、贴图、图鉴缩略图和最终米制尺寸。侧面图与真实手持截图用于检查减面后的外观。

导入策略参考 [Unity OnPreprocessModel](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetPostprocessor.OnPreprocessModel.html)。生成平台及导出由用户完成；本仓库不声称取得平台条款以外的额外授权。
