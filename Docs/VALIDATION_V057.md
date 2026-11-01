# 0.57 验证记录

日期：2026-11-01，Asia/Shanghai。

## 已验证

| 检查 | 结果与证据 |
| --- | --- |
| 纯捕鱼规则 | 4,847 项通过，`Logs/V057/rules.log`；新增空中敲击与重复接触拒绝检查 |
| 纯输入规则 | 74 项通过，`Logs/V057/input.log` |
| Unity V057 | `Logs/V057/validation-release.log` 完整 PASS；锯齿/锤面触料、动作极值、双臂不交叉、整锤视野与眼位距离、斧铲耙回归、四处科考岸、32台设施/炉内产物安全迁移、真实水路与碰撞；海图在 `validation-11.log` 已重烘 |
| 原生 OpenXR 手作 | `Logs/V057/native-play-final.log` PASS；真实 A 入场、右握把及追踪推拉产出木板×4，空中敲击保持0次，接触敲击产出铁镐头和枪械铁件，B取消 |
| 原生 OpenXR 航行 | `Logs/V057/native-voyage.log` PASS；从沉船泊位实际登船，以油门、舵与刹车穿过原截面及旧大陆区域，再驶回沉船；未在航行途中传送船体 |
| 输入与航行留档 | `Logs/V057/NativeXR/*.json`，含五个航点、逐段船位/速度与各小游戏状态 |
| 相关回归 | V034手作及工具检查通过；V056综合检查通过，含船体、XR展示/舒适策略、锻造/狩猎、提灯、家园/农场、集市/动物栖地及捕鱼交互；驱动记录 `Logs/V057/FishingV034-driver.log`、`FishingV056-driver.log` |
| Windows成品包 | 210个文件与ZIP逐一哈希相同；88场景无图形逻辑冒烟通过，`Logs/V057/release-smoke.log`。该项不作渲染验收；画面依据上面的图形Unity/OpenXR实际截图 |
| PICO成品包 | 0.57.0／versionCode57，包名 `com.ruanyueheng.tidefin.vr`，ARM64、LZ4外层免压缩、完整归档、v2签名及与0.56相同的升级证书均通过；`apk-badging.txt`、`apk-signature.txt`、`previous-apk-certificate.txt` |
| 构建源与工程 | 501个C#文件在两端构建间逐字节一致；PlayerSettings仅有预期版本字段变化，Unity换行规范化已单独核验；E盘原路径最终Structure通过。`Logs/V057/source-verification.json`、`final-structure.log`、`closeout-status.txt` |

模拟器使用 Unity 官方 OpenXR Mock Runtime 与 Conformance Automation，XRSettings.isDeviceActive=true；PICO画质、真实游戏输入链、隔离档，Automated=false。场景夹具仅布置站位和材料，小游戏开始、运动和领取使用真实输入。无用户正常档被测试夹具替换。

## 画面

- [桌面锯木](Images/V057/saw-raised.png)
- [桌面抬锤](Images/V057/forge-raised.png)
- [原生XR锯木](Images/V057/v057-saw-stroke.png)
- [原生XR接触锻造](Images/V057/v057-forge-impact.png)
- [原生XR空中锤击不计数](Images/V057/v057-forge-airborne.png)
- [原生XR驶过原障碍区域](Images/V057/v057-voyage-2.png)

## 交付状态

已完成两端构建、归档检查和最终核验。安装包：

- `Builds/FishingPrototype-v0.57.0-Windows.zip`，793,452,500字节；SHA-256 `F0BFA8A7972C3102BA98FC69A9B22209176F15D36F2AD675CA4B2F117CAF5B3E`。
- `Builds/FishingPrototype-v0.57.0-Android/FishingPrototype.apk`，760,954,169字节；SHA-256 `7ED02DB596AC12A50D7EE738F527EAF84898A6646158642F73C7C6A539B8FC6B`。
- 升级证书SHA-256 `9e285a6d982524c420722d7ebc157fa0643b62c65eca5a0285c5311e000b5437`。

旧V027整套检查仍依赖已被后续版本替换的房屋山墙和对象名称，失败记录予以保留，未用放宽断言掩盖。本版独立检验32台设施迁移，并通过当前V056家园回归。旧安装入口产生的Android旧版本标记已恢复；正式构建入口同步为57并在成品APK中确认。本版按文件白名单提交，既有无关工作区修改保留。

未验证实体 PICO 的手柄配重、佩戴舒适度、立体画面和设备帧率。上述桌面镜像不作为真机性能认证。
