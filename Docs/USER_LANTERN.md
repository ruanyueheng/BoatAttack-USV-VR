# 灯笼替换、握持与照明

已用用户新 `lantern.blend` 替换旧提灯外观、地上可拾取的提灯和物品栏缩略图。保留模型的横柄、框架、纸面和 PBR 贴图，排除无关文件。原提灯物品编号、所有配方与存档不变，煤 + 毛皮仍可制作同一物品。

装备后按 F 同时切换灯罩自身发光和暖色点光照明。灯亮时照亮附近真实地形/工坊物体；灯灭、换装备、打开背包或进入其他抑制手持的状态时，不留下隐形点光源。原灯具的强点光调柔至强度 1.35、范围 8 米，不投射额外实时阴影。

手握实际横柄，而不是旧模型虚空中的握点。右臂使用原角色骨架，前臂分担旋转，灯体保持竖直放在视野右下方。最终站立/蹲下、上下 30 度视线的实测腕部弯折约 21.6°，肘内角约 113.6°；未拉伸骨骼，也未替换成独立假手。

Unity 国际版 2022.3.62f1 验证：

- `FishingLantern` 通过：真实手柄姿势、近裁剪面、灯罩发光与点光源、开关和换物品清理。
- 隔离点光后仍测得纸面亮度从 0 升至约 0.736；不是仅验证属性值。
- 实际海岸材质的受光目标亮度由约 0.050 升至约 0.476，确认灯能照亮周围。
- `FishingForge`、`FishingPickaxe`、`FishingV023` 回归通过。
- 纯规则回归重跑 3600 项通过；首次运行既有随机钓鱼测试 `V4 reeling through a leap snaps the line` 偶发失败。本次未改动该钓鱼规则或测试文件，保留失败与重跑日志以备诊断。

日志：`Logs/usv-validation-d1ea9ebf2b0a4d568039109836fa2f64.log`、`Logs/usv-validation-05ed08b0674f423a89796aae03e48cc8.log`、`Logs/user-lantern-rules.txt`、`Logs/user-lantern-rules-retry.txt`。使用隔离存档、模拟输入与临时 `-DisableBurst`；不等同于人工手感或头显验收。

![白天握持](Images/UserLantern/daylight.png)

![夜间发光](Images/UserLantern/illuminated.png)

导入入口：`Tools/Run-UnityValidation.ps1 -Suite FishingUserLanternInstall`；专项验证：`-Suite FishingLantern`。
