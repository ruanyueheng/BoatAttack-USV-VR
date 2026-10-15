# V028 用户镐子模型与握持

从 PR #38 逐项接入用户提供的 `pickaxe_free_asset.blend` 的镐头、木柄和 PBR 贴图，应用于石镐和铁镐。源文件保存在 `ArtSource/UserPickaxe`，运行资源位于 `Assets/Resources/FishingUserTools`。展示背景未导入。

源模型缩至 0.58 米高，运行时比例 0.85。握柄位于身体侧前方，掌心方向依据真实柄轴；前臂分担旋转，保留原角色手臂骨段长度。真实镐尖和采矿接触许可使用同一校准坐标。

石镐、铁镐物品编号保持不变；制作配合熔炉、锻造台、工作台分工。未覆盖 V027 视觉布局和奶油色界面。

验证入口：`FishingPickaxeValidation` 检查站立与蹲下、上下 30 度视线、第一与第三人称、木纹 UV、掌心握点、腕肘范围和近裁剪；`FishingForgeValidation` 检查实际开采及制作链。当前结果以 V028 验收日志为准，PICO 实体挥镐仍需头显检查。
