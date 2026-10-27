# 第42版资源修订

人物保持原配色。没有替换脸部、发型、服装纹理或已校准的五指手套。

- `Character/9s-sleeve-fixed.blend`：从既有 `ArtSource/V030/9s-adapted.blend` 修复衣袖。将衣袖误绑在手腕/手指上的权重转移给同侧前臂，并将过大的袖口拟合到前臂附近、平整腕侧开口。原手套权重和几何保持不变，身体网格顶点数与拓扑保留。重建入口：`Tools/Art/repair_v042_character.py`；输出运行资源仍为 `Assets/Resources/FishingCharacter/9SCharacter.fbx`。具体计数和哈希见 `Character/sleeve-repair.json`。
- `Furniture/cedar-furniture-v042.blend`：沿用现有海岸材质图集，为书桌加入独立木板及细缝，为柜门加入木条和黄铜合页；保持原占地范围和交互锚点。重建入口：`Tools/Art/build_v042_furniture.py`；仅输出既有 `HomeDesk.fbx`、`HomeCabinet.fbx`，不重建家岛。
- 七段剧情音乐沿用 Claude 第41版的合成算法，改在离线生成，避免播放时在游戏主线程计算整段PCM。重建入口：`Tools/Art/bake_story_audio.py`；输出 `Assets/Resources/FishingV042/Story/*.wav`。

模型脚本应在独立的 Blender 5.2 后台进程中运行，保留已打开的用户场景。配乐脚本使用既有 Python/NumPy 环境。Unity 导入后的真实姿态、手部接触、场景画面及打包验证另记在最终版本验证文档中，脚本自检不替代它们。
