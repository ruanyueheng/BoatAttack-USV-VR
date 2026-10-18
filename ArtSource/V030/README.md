# 第30版用户资源

- `lantern-original.glb`：用户桌面的提灯.glb，原样保留；转换后的12,000三角面模型、PBR贴图和物品图位于Assets/Resources/FishingUserTools。
- `9s-original.zip`：用户提供的9s.zip，原样保留。采用其中gas gas gas.fbx及其原贴图；GLB包含辅助球且骨架没有完整行走动画。适配后17,895三角面，使用项目原有CC0 Quaternius骨架与Idle/Walk/Run动作，保留新人物袖口，手套内复用已校准的CC0五指网格并改为深色手套材质，以保持真实工具接触。
- `9s-adapted.blend`与`lantern-adapted.blend`：独立后台制作的编辑源文件，没有改写桌面原件。
- `character-import.json`与`lantern-import.json`：资源转换记录。

复现：先在独立Blender后台进程运行Tools/Art/import_v030_character.py，再运行Tools/Art/import_user_lantern.py -- --v030。人物脚本会从原始ZIP安全解压需要的源文件。Tools/Art/build_v030_stationery.py生成本项目原创牛皮纸纹理与海岛邮票/邮戳。

人物表面由用户提供，来源压缩包未附授权文件；CC0仅指已有Quaternius骨架和动作。没有下载其他模型，没有执行压缩包内代码。
