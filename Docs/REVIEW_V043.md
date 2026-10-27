# 第43版整合审阅与验证

## 最终改动

基于主线 `698f5a44`，合入第42版反馈修复、同学的PR #54（`f6ad50c9`）以及旧存档兼容修复，版本为 `0.43.0`。PR54作者提交保留在本地合并历史中，没有在GitHub自动合并其草稿PR。第42版功能与资源详见 [验证记录](VALIDATION_V042.md)，浇水音效原始交接详见 [WATERING_AUDIO_V041.md](WATERING_AUDIO_V041.md)。

本轮审阅沿实际调用检查存档恢复、备份写入、野外箱奖励、动物冷却/捕捉、手部平滑、镜头显示恢复，以及循环声源的类别、静音和复用。按单机游戏、本地三槽存档、有限声源池的使用范围判断。

## 已修复的问题

### 旧档被第42版误判为损坏

`FishingHunting`原先以`FishingBackpack.PickupCount`作为冷却数组长度。第42版新增8个箱子后，资源点从8增至16；第40–41版正常保存的8项冷却数组因长度不符被拒绝。主档和备份使用相同校验，因此会同时显示“损坏”。实际文件仍然保存在正式存档目录。

修复将动物冷却槽数与资源点数分离，保留第42版的16项格式，同时接受完整的旧8项数组。校验通过后复制并补零，保留原有金币、物品、日历、已捕猎记录和冷却期限，不修改输入对象或源文件。非法长度、天数/分钟不匹配、非有限计时和未知版本仍然拒绝。无需升级存档格式到20。

新增回归在修复前确认失败；修复后涵盖schema9–19、精确冷却边界、不变的输入、再次保存、非法数组和NaN。Unity专项另检查原始schema18封装、升级后的主档、保留原始备份及损坏主档时的旧备份回退。

### 与手部平滑相容的检视验收

步枪专项曾在检视旋转后8帧、切第三人称后8帧和俯仰后4帧直接检查稳态接触。这些帧数不能保证第41版引入的IK过渡结束。改为等待实际0.6秒后检查；40mm位置、2度旋转及骨长、碰撞、相机近裁面标准保持不变。正常过渡与其他工具接触由第一人称/手部专项覆盖。

## PR54整合

- 水流循环显式请求Effects，默认循环继续使用Ambient；沿用第42版水壶的AuthoredMotion，避免手臂被重复平滑。
- 声源只在租约/片段/类别变化时重新注册，池数量保持有界，音量由统一注册表乘一次。
- 80ms起声、120ms收尾及提高的水流/完成增益由原生场景专项检查；取消、失败和静音不改变农事交易结果。
- `WaterSprinkleLoop`是可选入口，缺文件回退已有真实`WaterPourLoop`，没有把预留接口描述为已生成新素材。

## 验证记录

本轮结果保存在本地忽略目录`Logs/V043/`；个人存档、恢复副本和完整日志不提交GitHub。

- 领域规则4727项通过，输入/渲染策略70项通过；失败复现与修复记录为`domain-before.txt`、`domain-after.txt`、`input.txt`。
- V043专项对显式备份目录中的28份实际存档/备份全部验证通过，并通过游戏菜单加载schema18进度。测试只写隔离档，原备份逐字不变。复现入口：`Tools/Run-UnityValidation.ps1 -Suite FishingV043`；提供本地副本时使用`FISHING_SAVE_AUDIT_DIR`，不设置时运行合成旧档回归。
- WORLD-AUDIO、ACTION-AUDIO、AUDIO-MIX、V033三槽/设置及V037水壶手部专项已通过；水流在Ambient=0仍播放，Effects=0静音且交易只提交一次。
- Hunting、HomeEcology、V042新箱/剧情回归及14项组件回归全部通过；对应`FishingHunting.txt`、`FishingHomeEcology.txt`、`FishingV042.txt`、`Regression.txt`。步枪检视/第三人称/俯仰在正常IK稳定后通过原接触标准。
- 从已提交Git源码独立导出再次通过4727项领域规则、70项输入检查，未借用工作区其他修改；12个新增/修改LFS对象的大小与SHA-256均匹配提交指针。记录为`committed-domain.txt`、`committed-input.txt`、`lfs-verified.json`。
- 最终Windows Player的88场景与8张新增玩法图均通过原生1600×900及完整HUD探针校验；重点复看钓鱼、低头、手作、三槽界面、水壶携带/灌水/浇水。原生记录为`native-acceptance.txt`、`native-feedback.txt`，重点联系表`native-review.png`。这些自动预置场景使用临时档，不冒充用户实档。
- Windows、Android、结构检查、Windows ZIP逐文件哈希比对、APK条目和ARM64库检查全部通过，见`builds.txt`。APK签名v2通过，与此前本地调试证书一致，包名`com.ruanyueheng.tidefin.vr`，versionName`0.43.0`、versionCode43，minSdk29 / targetSdk33；设备安装未做。
- 最终保留核对确认原有28份正式存档/备份和71个无关本地文件SHA-256均不变；另将已验证的更早手动备份复制到原本空着的槽3，没有替换槽1/2。具体恢复清单、时间和哈希仅留本机`local-save-recovery.json`、`preservation-final.json`。

## 本地安装包

- `Builds/FishingPrototype-v0.43.0-Windows.zip`，210个文件；SHA-256 `0AFF12F5B4F077D4D0D06B8FCAAE03D2F7C68A31D134A8550E3145075FC9AF77`。
- `Builds/FishingPrototype-v0.43.0-Android/FishingPrototype.apk`；SHA-256 `8AD1252A0DA2532F987BA17D97794741C77BEEF0039EC5D7D858BBF68AB7EE10`。
- 构建清单`Builds/v0.43.0-builds.json`，本机签名/包信息记录为`apk-signature.txt`与`apk-badging.txt`。请运行第43版读取旧档，旧第42版可执行程序仍保留供追溯。

## 审阅结论

发现的旧档兼容阻塞已修复，并以真实旧档副本及菜单加载验证。PR54功能在本地合并版本中通过其原生专项，且保留第42版手部修复。当前没有确认的新增合并阻塞，可提交源码PR供评审；以下硬件与长期使用边界继续保留。

## 边界

没有PICO实体手柄、追踪、佩戴舒适度或实体扬声器/耳机主观听感结果；自动操作不替代长期人工游玩。本轮没有重跑第42版固定18段性能对照，其GC频率未改善和首次约130ms间隔仍列为已知边界，不把旧成绩算作第43版新结果。

正式存档已在任何恢复动作之前完整复制并记SHA-256。既有槽不会因修复而被清空；个人记录绝不进入提交。初始71个无关本地修改保持，提交按白名单整理。

本地Windows ZIP及Android ARM64 APK随本版构建，生成包不加入源码PR；GitHub PR供审阅，未经用户要求不自动合并。
