# 第 14 版设计依据与混元资产流程

核对日期：2026-10-10。以下页面用于设计参照；没有复制商业游戏的模型、贴图或 UI 素材。玩法参数是本项目的选择，不声称来自这些游戏的内部实现。

## 设计参照

- **DREDGE**：[开发商/发行商 Steam 页面](https://store.steampowered.com/app/1562430/DREDGE/)明确区分白天航行与夜间薄雾中的危险，鱼获出售支持船只升级。本项目采用可读的暮光过渡、暗夜与夜钓鱼群，维持舒缓基调，不加入恐惧值和强制惩罚。
- **Animal Crossing: New Horizons**：[Nintendo 官方介绍](https://www.nintendo.com/us/store/products/animal-crossing-new-horizons-switch/)说明生物出现受季节和一天中的时段影响，岛上采集物可用于 DIY。这里把海域、四季、昼夜作为鱼群条件；采用暖白卡片、深青正文和琥珀色提示来减少界面压迫感。配色、字号与间距为原创设计。
- **Stardew Valley**：[官方网站](https://www.stardewvalley.net/)作为经营、采集和生活节奏的风格参考。项目选择四季各 28 天、每个游戏日 20 分钟、按暂停状态停表；这些数值是本版的独立设定。没有访问玩家系统时间，也不离线推进。
- **Raft**：[开发商 Steam 介绍](https://store.steampowered.com/app/648800/Raft/)列出采集、建造、动物和烹饪循环。本项目采用“猎矛近距接触 → 生肉入包 → 燃料烤制 → 手动领取”，复用现有熔炉事务；取消动作和满包不会杀死动物或丢材料。陆龟与海洋伙伴保留观察。
- **握持方法**：[Unity XR Grab Interactable 2.3 文档](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.3/manual/xr-grab-interactable.html)将附着位置与物体运动类型分开。本项目沿用现有轻量交互层，以独立物体握点驱动同源手部。桌面不反向读取已解算腕骨来移动道具；XR 以实际 grip pose 为基准，并对追踪丢失要求松键恢复。
- **PICO**：以仓库锁定的 PICO OpenXR SDK 源码和 Unity 2022.3 / URP 14.0.12 的实际 API 为实现依据。PICO 文档站本次返回客户端网页外壳，不据此宣称任何特定帧率。Android 使用 ARM64 / IL2CPP / Vulkan / Single Pass Instanced；硬件是否满足帧预算以真机验收为准。

## 混元生 3D

用户指定入口：[混元 3D](https://3d.hunyuan.tencent.com/)。已访问入口及腾讯官方 SDK；网页生成需要登录，本机没有可用腾讯云凭据。本次未发起付费生成、未上传项目资源，也没有把原创程序网格标为 AI 生成。

可复用的 8 条中文提示词及预算在 [hunyuan-prompts.json](../ArtSource/Fish/hunyuan-prompts.json)。参数来源：[腾讯云官方 Python SDK ai3d models.py](https://github.com/TencentCloud/tencentcloud-sdk-python/blob/master/tencentcloud/ai3d/v20250513/models.py)，核对 `SubmitHunyuanTo3DProJobRequest`：

- 核对时 `Model=3.0` 支持 `GenerateType=LowPoly`，`3.1` 的 LowPoly 不可用。
- LowPoly 下 `FaceCount` 不生效；不能以“请求了 3000 面”冒充实际预算。`PolygonType=triangle`，PBR 关闭。默认返回 OBJ / GLB；未设置不受支持的 `ResultFormat=GLB`。
- 普通生成默认面数很高，不适合直接塞进 PICO 包。生成之后仍必须查看**三角化后的三角面数**，需要时在 Blender 重拓扑/Decimate，并检查嘴、尾柄、鳍边没有塌陷。

操作顺序：登录 → 选文生 3D → 粘贴单种提示词 → 可用时选低模/智能拓扑 → 下载 GLB → Blender 检查材质和轮廓 → 三角化、减面、统一原点/米制/轴向 → 导出 FBX → Unity URP 材质、真机复核。UI 名称可能改变，参数以官网当前实际显示为准。

替换门槛：单鱼最多 4,000 三角面、1 个材质、最多 1 张 1024 贴图，+Z 朝头、+Y 向上；原点在包围盒中心；长度使用 `FishingSpecies.LengthMeters`。按普通/珍珠、大小、入网/检视/装箱/放生分别检查。生成模型的授权条款应随下载归档。当前交付是独立原创的 12 种程序网格，不依赖外部登录或网络。

## 字体授权

字体基于 Google Fonts 的 Noto Sans SC，保留 SIL OFL 许可证，实例化为 500 字重并裁剪本项目所需字符；修改后名称为 **Tide Sans SC**。源提交与 OFL 位于 `ArtSource/Fonts`，可运行 `python Tools/Art/build_ui_font.py` 重建；脚本核对上游 SHA-256 并检查中文/ASCII 字形及 3 MB 上限。本次成品约 315 KB，Android 无需依赖 Windows 系统字体。
