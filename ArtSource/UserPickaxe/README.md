# 用户提供的手镐

原文件来自用户附件 `E:/pickaxe_free_asset.blend`，本目录保留未修改副本。仅提取 `Circle` 镐头和 `Circle.001` 木柄，排除摄影背景、灯光与摄像机。不修改原 .blend，也不替换角色骨架。

导出入口：`Tools/Art/import_user_pickaxe.py`。真实镐头/木柄经统一轴向和单位处理，网格高 0.58 米；游戏中的紧凑比例为 0.85。镐柄下段设独立握点，尖端坐标见 `import-manifest.json`。Blender 导出约 3136 三角形，Unity 三角化后 3116 三角形。

保留原木纹及金属材质，烘焙 Base Color、Metallic/Smoothness、Tangent Normal 图集，并提供与实际模型相同的缩略图。UV 层删除前复制图集坐标、删除后回写，避免 Unity 显示整柄单色金属。

资源位于 `Assets/Resources/FishingUserTools`。导入配置由 `FishingUserToolAssets.Build` 生成；石镐和铁镐共用这份用户提供外观，原游戏名称、配方、产量及存档物品编号不变。提灯不在本次替换范围，等待用户重新提供可导入模型。

来源和使用许可由用户确认，本项目不擅自为该第三方模型新增授权声明。
