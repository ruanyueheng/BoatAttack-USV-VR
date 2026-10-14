# 手镐模型替换与握持修正

已使用用户 `pickaxe_free_asset.blend` 的镐头和木柄替换石镐、铁镐的旧外观；没有导入展示背景。保留木纹、金属表面、法线贴图和实际模型缩略图。名字、配方、煤矿产量和存档编号保持兼容。

握柄在身体侧前方，不再挤到眼旁。掌心方向以真实柄轴为依据，肘部保留双段骨骼关节，前臂分担适量旋转，避免手腕蒙皮独自承担全部扭转。源手臂段长和比例不改变；真实镐端与采矿接触点共用坐标。

最终专项检查：站立/蹲下，上下 30 度视线，第一/第三人称，同源手掌握点与木纹 UV，源骨骼长度及眼旁近裁剪面。待机实测腕部方向弯折约 33 度、肘内角约 78 度；不能仅凭手掌到握点的距离判定姿态正确。

Unity `FishingPickaxe`、`FishingForge`、`FishingHunting` 专项通过；3600 项纯规则断言通过。采煤流程验证石镐 / 铁镐实际动作和原有产量，未改动猎枪。使用 Unity 2022.3.62f1、隔离存档、模拟输入及临时 `-DisableBurst`；不等同于手持设备或人工手感验收。

最终材质/姿态/主线兼容检查日志：Pickaxe `Logs/usv-validation-04383be9124140668ef87183717f6b58.log`；V023 `Logs/usv-validation-02cbd9acdc244e8e8e5e5fc9849c45fa.log`。木纹 UV 的实际采样通过，不仅检查材质有没有绑定。

导入命令：`Tools/Run-UnityValidation.ps1 -Suite FishingUserToolsInstall`；握持验证：`-Suite FishingPickaxe`。参考图在 `Logs/UserPickaxe`，本次只处理镐子，提灯仍待新的模型附件。
