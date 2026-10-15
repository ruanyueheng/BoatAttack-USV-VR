# 第27版验证记录

2026-10-15。Unity 2022.3.62f1 / v0.27.0。最终运行内容提交为 `d4b1b484`；两个安装包均包含山墙、旧设施边缘及提示遮挡修复。

## 检查结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 规则 | 3647 通过 | `Logs/v027-rules-final.log` |
| 输入与渲染策略 | 70 通过 | `Logs/v027-input.log` |
| 最终 GLB 重新导入 | 3 网格 / 26269 三角面 / 顶点配色完整 / 2862152 字节 | `ArtSource/V027/export-validation.json` |
| 最终 V027 Unity 专项 | 通过；含前后山墙内侧、完整足迹与 32 台设施迁移、住宅/工坊/游泳、XR 门禁和功能页 | `Logs/usv-validation-e4dc9f9d1525496281b5a52f0b6bcf11.log` |
| 水面航线遮挡 | 正式游戏相机、真实规划航线，纸面内部像素差为零 | 同上日志的 `[NOTEBOOK UI]` |
| 捕鱼、锻造、工坊、第一人称、V024 兼容 | 五项独立回归通过 | `Logs/v027-related-regressions.log` |
| 最终 Windows 构建 | 通过 | `Logs/v027-final-windows-build.log` |
| 最终程序截图 | 50 场景，全部 1600×900，完整 HUD 探针通过 | `Logs/desktop-acceptance-bb242b09af89425fafd90b7bbdaf00d9/capture-20261015-031032-1c622f90/manifest.json` |
| 最终程序 Smoke | 50 场景、隔离存档和生产规则检查通过 | `Logs/desktop-acceptance-f1b5f2106ee84c8e98f81bcf9175aa12/player.log` |
| 最终 ARM64 APK | 0.27.0 / code 27；880 条目完整；v2 签名有效，更新证书与第26版相同 | `Logs/v027-final-android-build.log`、`Logs/v027-apk-signature.txt` |
| Windows 便携 ZIP | 210 个文件逐一与最终构建一致，排除 DoNotShip 调试目录 | `Logs/v027-package-verification.json` |
| 编辑器目标恢复 | Windows；导入、输入配置和三个场景结构通过 | `Logs/v027-final-structure.log` |
| PR / CI | [PR #37](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/37)，三项 P2 已修复；运行内容提交的四项 CI 通过 | `089dc0b9`、`44de9dfa`、`d4b1b484` |

## 修正与回归范围

- 修正屋顶和瓦片绕序，补齐屋顶内侧及有厚度的前后山墙。新增室内向前、向后的真实 MeshCollider 探针；最终 Windows 室内图不再透天。
- 修正功能页纸面层级、返回首页后的副标题、字体测量和码头按钮中心。共享世界空间 UI 在海面航线之后绘制，中文与返回入口受专项检查。
- 清理与新岛重叠的独立岩壁和地形底座，使用运行时地形副本；同步水深，补回地图中的钓具店。
- 迁移保留设施 ID、朝向、材料、待领成品和加工进度。32 台容量、逻辑框外中心、实际采样外扩区、中心有支撑但旋转角点悬空均有回归；复用正常摆放的四角规则。重复载入不反复移动，影响范围外设施不变。
- 最后视觉复查发现验收模式中的地名卡遮住提示第二行，调整地名卡最小高度后重建两端、重新完成 50 场景截图与 Smoke。
- 不改存档格式，不重置正常玩家进度。自动验收使用隔离存档，六个固定工坊沿用第26版兼容范围。

## 最终实际画面

场景和 UI 来自 Unity 正式游戏相机或实际 Canvas；桌面图来自最终 Windows 程序。已检查五张联系表与重点大图，并在提示排版修复后复验受影响的室内画面。

- [小岛实景](Images/V027/home-island-overview.png)、[木屋正面](Images/V027/cottage-front.png)、[木屋侧面](Images/V027/cottage-side.png)
- [室内山墙](Images/V027/desktop-HomeInterior.png)、[工作台界面](Images/V027/desktop-HomeWorkbench.png)
- [VR 手册首页](Images/V027/ui-page-0.png)、[VR 背包](Images/V027/ui-page-1.png)、[VR 航海图](Images/V027/ui-page-12.png)
- 50 场景联系表：[1](Images/V027/desktop-contact-1.jpg)、[2](Images/V027/desktop-contact-2.jpg)、[3](Images/V027/desktop-contact-3.jpg)、[4](Images/V027/desktop-contact-4.jpg)、[5](Images/V027/desktop-contact-5.jpg)。[截图清单副本](Images/V027/desktop-capture-manifest.json)保留采集时的原始字段，人工图像检查记录在本文。

## 交付包

| 文件 | 大小（字节） | SHA-256 |
| --- | --- | --- |
| `Builds/FishingPrototype-v0.27.0-Windows.zip` | 395594620 | `1b2312c36bb3e928f4907228f5b4b96cc1872a42f26b32339eb025e40fb7a9e0` |
| `Builds/FishingPrototype-v0.27.0-Android/FishingPrototype.apk` | 253077205 | `15e19f188485bbc7e38ff4dc4ec5053a913e52064df143216c84490951e2390a` |

[SHA-256 清单](../Builds/FishingPrototype-v0.27.0-sha256.json) · [完整本机包检查](../Logs/v027-package-verification.json) · [试玩与操作](PLAY_V027.md)。安装包和本机日志不放入 Git，校验值和主要截图随 PR 保存。PR 保持打开，未自动合并。

## 尚未实测

ADB 未发现连接设备。PICO 4 Ultra 的安装启动、双眼清晰度、真实控制器追踪、持续帧时和佩戴舒适度仍待设备验证；桌面截图和模拟输入不能替代这些检查。新存档包含第26版起的六个固定工坊，不应回退给第25版及更早版本读取。自动回归与审查不能保证不存在任何 bug。

