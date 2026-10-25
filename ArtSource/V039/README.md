# V039美术源文件

- `Hunyuan/GrilledSeaBream_Hunyuan_Source.glb`：腾讯混元3D V3.1生成原件，50000三角面；与用户提供的附件逐字节一致。
- `Hunyuan/GrilledFish-game.glb`：6000三角面的游戏用导出，保留UV和贴图；Unity使用`Assets/Resources/FishingV039/`内的FBX、材质和图片。
- `v039-development.blend`：实时BlenderMCP生产源，只包含本版场景修订、混元原件和游戏适配三个场景，图片已打包。
- `Scenery/`：13个场景/桥梁模型的导出哈希与标记。生成代码为`Tools/Art/build_v039_assets.py`，复用V035网格工具和修订后的地形、栏杆几何。
- `Meowa/`：原始滋滋声MP3、提示词与积分记录。运行WAV为单声道44100Hz、3.96秒循环。

使用混元1次已有额度、Meowa20已有积分。网页生成记录在`Docs/Images/V039/`。未将手工鱼草稿纳入正式资源。
