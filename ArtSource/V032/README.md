# 第32版模型来源与复现

拱岩由用户授权在其已登录的Tripo Studio工作台生成。初次v3.1任务139845a1-0be6-41a7-aaed-7b7d50df5dc9使用50积分，但该账号免费方案不能导出此版本；随后v2.5任务847923ee-0faa-4f09-b43d-de08f94c61f7使用35积分并成功导出。未购买会员或新增订阅，操作后可见余额225。

用户提供下载文件 `tripo_convert_ed6dad44-5908-430b-bb89-cf6023289cd0.glb`，经GLB元数据核对匹配上述v2.5任务和内嵌贴图。原件保留为 `OriginalCoastalArch.glb`，SHA256记录于 `arch-validation.json`。

- 原件467496三角面；合并接缝重合点并减面后19995三角面；原始1K JPEG保留。
- 游戏使用 `Assets/Resources/FishingV032/CoastalArch.fbx` 与 `CoastalArchAlbedo.jpg`，保持真正开孔的三角网格碰撞。
- `coastal-arch-game.glb` 为便携优化模型；`coastal-arch-v032.blend` 为编辑源；两张同机位预览用于核对轮廓与开孔。
- `Tools/Art/prepare_v032_arch.py` 需独立 Blender `--background --factory-startup --python-exit-code 1` 进程运行。默认生成原件检查图，添加 `-- --optimize` 后生成游戏资产及验证摘要。脚本不会打开或改写用户正在编辑的场景。
- 原件来自当前Tripo免费账号，网页方案标明公开模型、仅限非商业用途。本工程为用户课程作业；该模型不能标为CC0。
- 新岛植被及营地复用工程内已归档的Kenney CC0资源。补充检索确认了 [Quaternius自然素材包](https://quaternius.com/packs/ultimatestylizednature.html) 的CC0来源，本版未重复下载该素材包。

本版运行时地形与光影由Unity实现，保留原有四座任务岛、存档、地图和交互系统。
