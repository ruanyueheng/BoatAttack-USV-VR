# 玩家反馈：P0 美术修正

2026-10-26；保留既有资源路径、FBX 的 `.meta` 和所有物品编号。

- AK 使用用户原始 `ArtSource/HuntingRifle/NewAK.glb`，原件 SHA-256 不变。运行模型由 50000 降至 12000 三角面，保留 UV、原贴图和同一握点/枪口点。模型边界最大变化 0.595 mm；运行模型与物品检视仍共用同一 FBX。
- 果树主干使用同轴截面，半径从地面的 0.22 m 单调减小到 3.3 m 高度的 0.065 m；树冠、分枝、果实和摇树机制保留。
- 萝卜采用本地已归档的 Kenney Food Kit 2.0 `radish.glb` 外形（CC0），幼苗、成长、成熟三阶段分别为 416、664、664 三角面。四株布局、原作物编号及 Coastal 材质不变，根部按土面埋入。
- 移除菜园路径边石和外围整圈装饰石，保留真实地形、碎石路径底材、栅栏、花园棚及灯塔。新的 `HomeLandscaping` 为 36880 三角面；40 根栅栏柱使用单一尖端和三角扇，避免旧零半径顶盖产生退化面并被 Unity 丢弃。

原始来源：`ArtSource/V019/Kenney/food-kit/Models/GLB format/radish.glb`。本次独立归档所需的模型、调色板和 CC0 许可到 `ArtSource/Feedback-20261026/Sources/`，复建不依赖整份未提交食物包。源萝卜 SHA-256 与所有输出哈希见 `ArtSource/Feedback-20261026/P0Art-manifest.json`。

复建入口仍为 `Tools/Art/replace_hunting_rifle.py` 和 `Tools/Art/build_v035_assets.py` / `v035_home_geometry.py`。本次仅重建 `OrchardTree`、`Turnip0/1/2`、`HomeLandscaping` 五个家园模型。禁止为此重建全场景或覆盖玩家存档。

已检查 Blender 前后预览、FBX/GLB 再导入、UV、材质、有限顶点与三角面预算。临时工程和预览位于 `Logs/P0Art/`。

`FishingP0ArtValidation.Prepare()` 仅定向导入六个 FBX，并刷新 `HomeLandscapingCollision`、`OrchardTreeCollision`、`Turnip2Collision`，不保存场景。专项入口 `Run()` 检查哈希、面数、树干截面、萝卜绑定和旧石头位置清空。还须运行狩猎、农场及 V039 家园专项，复看实际握枪、摇树与田间画面；Blender 预览不替代这些检查。
