# v0.60.0 — 拿持、家园与 PR #67 整合验收

本版在 `main` 的 `d12f48da` 上完成上一轮未提交的家园与拿持改进，并通过 merge commit 保留 PR #67 的原始提交 `6d891ed9daa2850dc2efba49a0768e91904aecd3`。两套 V059/V060 场景和原生 OpenXR 验收入口并存，Android 版本号为 60。

## 最终行为

- 斧、铲、耙和镐使用各自握点、工具轴和实际握柄手指表面；活捉网使用单独的握柄目标，去除重复占位物。捕捉前刷新当前网口，右手失去追踪即隐藏网具。
- 家园储物箱从 36 格扩为 144 格，背包保持 36 格。桌面有四页，VR 每页六格；第 144 格支持存取。存档版本 21，旧版本 15–20 的合法 36 格箱子经校验后补空格，保留所有原物品；破损数组拒绝加载。
- 原水族箱移至渔屋东侧岛上，保留鱼和交互，按家园岛的实际碰撞面落地并清出花丛。
- 女店员保留原外观，增加独立前臂骨骼、四种待机活动、招呼和交谈/倾听切换。
- 井水动作降低摇把并检查整圈可达范围，改进第一人称取景和近眼遮挡。第三人称显示完整身体；动作中双向切换视角及取消、完成均恢复正确遮罩。
- 保留 #67 的左右手任意筛柄抓取、追踪恢复须松手、真实摇筛交易、锯木开缝/断开、锻铁变形/火花、有效接触音效，以及五类住所门口安置、容量/筛选提示和动物空间叫声。

## 验证记录

以下均运行于整合后的源代码，使用 Unity 2022.3.62f1 的生产场景 `Assets/scenes/Fishing/fishing_Island.unity` 和隔离验收存档。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 游戏规则 | 4,861 通过；包括旧满箱迁移、全部新增格、末格存取、交易复制与破损存档拒绝 | `dotnet run --project Tools/FishingTests/FishingTests.csproj --no-restore` |
| 输入规则 | 74 通过 | `Tools/InputTests/InputTests.csproj` |
| Runtime / Editor 编译 | 通过；最终源已实际由 Unity 编译及双端构建 | `Tools/Check-FastCompile.py`；场景运行也完成实际 Unity 编译 |
| V060 完整场景 | 通过；包含 V058、65 类物品、旧箱 JSON、室外鱼缸、店员与井水全过程 | `Logs/usv-validation-c12f36bbe2a64a7fa588997759856488.log` |
| PR #67 的 V059 完整场景 | 通过；音效、筛具、工件与动物专项 | `Logs/usv-validation-c7a4f7a30e584895b664b96e8e705739.log` |
| 活捉网及生态回归 | 通过；故意破坏旧网口位置后仍先刷新当前姿态，再实际捕获野兔 | `Logs/V060/FishingHomeEcology.log` |
| 战斗与枪械场景 | 通过；实际持枪、射击与回港清理 | `Logs/V060/final-FishingCombat.log` |
| 工具回归 | 通过 | `Logs/V060/FishingFeedbackTools.log` |
| 世界声音回归 | 通过；验收结束后普通存档、备份与偏好逐字节保持不变 | `Logs/V060/FishingWorldAudio.log` |
| V060 原生 OpenXR | 31 组通过；真实跟踪姿态、网具失追踪/恢复、箱子末格取物、岛上步行和鱼缸入口 | `Logs/V060/NativeXR/native-v060.json` |
| PR #67 原生 OpenXR | 25 组通过；左右手任意筛柄、失追踪、真实完成交易，以及锯/锤接触、声音、网格、双向粒子和空挥拒绝 | `Logs/V060/NativeXR/native-pr67.json` |

Jev 辅助核查使用 `jev-latest`，实际模型 `jev-1.14.0`。它对有源码支持的失追踪隐藏判断返回 0.94；对迁移保留判断返回 0.51，未据此接受迁移结论，另以源码、规则和真实 JSON 场景验证。概率不代表测试通过率。响应保存在忽略目录 `Logs/V060/jev-integration-check.json`，未保存密钥。

## 设备范围

`adb devices` 未检测到连接的头显。原生 Mock OpenXR 可以验证 PICO 画质下的控制器输入路径和画面；实体 PICO 的佩戴舒适度、双眼立体效果、声音听感和持续性能仍需人工实测。测试不读取或重置普通玩家存档。

## 画面证据

[原生 OpenXR 握姿、箱子末页与室外鱼缸](Images/V060/native-grips-and-home.jpg)为生产镜头同位置的单眼镜像（80°视场，不采用头显双眼投影），保留实际场景与界面。网具近景通过抬起控制器并低头取得；没有变更网具模型或绕过碰撞。它们不能替代双眼头显验收。

店员保留原模型的静态姿态，以自然近身幅度增加活动与交谈。源网格的手和衣饰共用拓扑，因此不采用会拉长腰饰的大幅抬臂。最终真实蒙皮前臂峰值位移为 **0.0528 m**，区域平均峰值 **0.0141 m**；800个源腰饰点最大位移 **4.87 mm**，相邻三角边最大相对伸缩 **1.85%**，脚部最大位移约 **0.007 mm**。已复查动作中段和实际交谈近景。

[店员动作中段](Images/V060/cashier-activities.jpg)、[原生 VR 中的店员](Images/V060/native-cashier.jpg)和[井水视角检查](Images/V060/well-views.jpg)已目视复查。原生店员验收使用实际面部坐标和 XR origin 求头部输入，并逐次采样检查面部位于视域且视线无遮挡，证据为 `Logs/V060/NativeXR/native-v060.json`。最初朝向错误的一组补拍未用作该结论。

## 最终构建与交付

构建源码提交：`c1b13b72cbfc64280c3a864926d2acfaccd747c6`。Windows 与 Android 均重新构建于最终自然姿态修正之后；使用 `Tools/Run-FishingBuilds.ps1` 同步生成。

| 产物 | 大小 | SHA-256 |
| --- | --- | --- |
| `Builds/FishingPrototype-v0.60.0-Windows.zip` | 794865551 字节；210个文件 | `9b82954ddcd688423cd3dd19eacfc8eb99a2cb7cd4fcf3a1bef8e20e0d85eb53` |
| `Builds/FishingPrototype-v0.60.0-Android/FishingPrototype.apk` | 762594809 字节 | `56d81884b7f76e0be51ece4e9118dbd25f92d98586f6aa00149c32e4ba239bca` |

最终 Windows Player 独立启动 smoke 通过（`Logs/desktop-acceptance-9d900535efae4ccaa231bc27e2cd55fd/player.log`）。两端构建和结构检查见 `Logs/V060/builds-final.log`；最终 APK 的归档 CRC、ARM64 IL2CPP、UnityFS LZ4/外层 stored 容器、六项整合运行时代码标记、版本0.60.0/60、包名及 v2 签名均已核对，文件校验值与构建清单一致。

签名证书 SHA-256：`9e285a6d982524c420722d7ebc157fa0643b62c65eca5a0285c5311e000b5437`，与本机已有 v0.58.0、v0.57.0 和 v0.50.0 APK 一致。PR #67 文档中的证书来自同学的构建环境，未用它替换本机升级签名。详细机器可读记录：`Builds/v0.60.0-builds.json`、`Logs/V060/build-verification.json`。安装包属于生成产物，不提交到 Git。
