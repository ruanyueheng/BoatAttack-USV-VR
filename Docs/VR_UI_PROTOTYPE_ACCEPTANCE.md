# 潮汐手册首页 · 样板交付与验收

2026-10-14，Unity 2022.3.62f1，当前游戏版本 0.26.0。本轮完成设计审查和一个真实 World Space UI 样板，尚未开始全页推广。

## 看实际结果

![Unity 实际渲染的潮汐手册首页](Images/UIPrototype/notebook-home.png)

![使用正式游戏相机渲染的海湾摆放图](Images/UIPrototype/home-in-bay.png)

对照：[改版前](Images/UIPrototype/before.png)。状态：[Normal](Images/UIPrototype/home-normal.png)、[Hover](Images/UIPrototype/home-hover.png)、[Pressed](Images/UIPrototype/home-pressed.png)、[Selected](Images/UIPrototype/home-selected.png)、[Disabled](Images/UIPrototype/home-disabled.png)。这些状态图通过 Unity 设置样板状态并渲染，不是图片合成或真实手柄录屏。

## 已实现

- 奶油纸张、深海绿文字、沙金墨线、鼠尾草装订与状态；原创海岛明信片、九个同风格导航图标，中文手写标题与清晰正文。
- 左侧明信片与金币/容量；右侧六个常用功能卡片；底部群岛、设置、返回；日期/天气、当前装备与射线操作提示独立排版。
- 1100×780 设计坐标，0.00105 世界缩放，宽高约 1.155×0.819 m；打开位置沿用距眼前方 1.3 m、眼下 0.12 m，打开后固定世界朝向。
- 五种完整视觉状态、约 80 ms 的悬停色彩反馈、局部压纸边框与选中标记；首页记住最近访问的入口，不写入存档。
- 保留原九条 Action、右手物理射线、扳机边沿、B 关闭、追踪与失焦门禁、摇杆回中逻辑。其他页面保持原布局、图片与数据连接。
- 两种字体合计约 0.91 MB；九个图标共用 768×768 图集；纸面与边框为切片纹理；插画导入限制 1024。复用 UI/Default Shader 的单个材质副本控制绘制顺序，不新增 Shader、后处理、渲染管线特性或插件依赖。

## 实际验证

最终运行日志与结果以 `Docs/Images/UIPrototype/evidence.json` 的 `verifiedLog` 为准。样板使用原 V025 验证驱动；源文件与资源的摘要同时保存在该证据文件中。

| 项目 | 结果与范围 |
| --- | --- |
| Unity 导入/编译/运行 | 通过，使用原钓鱼场景与临时隔离存档 |
| 中文与符号 | 样板可见文案逐字检查；中文、箭头与选中符号均存在；标题和正文使用项目内字体 |
| 文本尺寸 | 样板全部可见文本的真实首选行高在框内；五种状态标记检查宽高；已修正默认行高造成的裁切 |
| 图集导入 | 禁用按 2 的幂自动缩放，保留 768×768；已修正首轮取到错图标区域的问题 |
| 射线 | 九个入口中心均由 Physics.Raycast 命中实际按钮 BoxCollider，按钮几何与可见卡片一致 |
| 状态与动作 | 五种状态均渲染；Disabled 优先于 Hover/Pressed；状态预览不替换 Action；射线移开后同帧清空悬停目标 |
| 旧页面 | 0、1、2、3、7、12、13、14、15、16 页内容与返回入口检查通过；地图继续使用共享地形图 |
| 追踪/运动门禁 | V025 已有舒适度、丢失追踪、菜单漂移、关闭后回中与教学练习检查通过；这些不是佩戴头显的实测 |
| 配色对比 | 普通标题约 8.41:1，普通副说明约 5.63:1，选中副说明约 4.92:1，主按钮约 8.41:1；Disabled 使用深色墨字，避免可读性下降 |
| 视觉审核 | 已人工检查实际首页、选中/禁用状态和场景图；最终图片不以编译通过替代 |

## 红水面截图的纠正

首次场景图使用单独创建并复制 Camera 设置的临时预览相机，出现粉红色水面。此图不能作为正确的海湾效果。该轮没有修改水面材质、Shader 或渲染管线资产。

将场景截图改为使用正式游戏相机后，分别渲染 [关闭手册的海湾](Images/UIPrototype/bay-without-notebook.png) 与 [打开手册的海湾](Images/UIPrototype/home-in-bay.png)，两张图的水面均为正常青绿色。确定问题发生于临时相机截图路径，未进一步把它归因到某个未经单独验证的水面参数。旧异常图只留在临时日志证据中，最终交付目录使用修正后的截图。

## 水面航线遮挡的纠正

水面导航航线使用 `FishingWaterGuidance` 的透明绘制队列 3100，原 UGUI 默认队列为 3000，且不写入深度。航线晚于纸面绘制，曾覆盖“自然手记”和“钓具百货”等入口。

首页现在使用 UI/Default Shader 的单个材质副本，队列设为 3200，保证先画航线再画手册。没有关闭水面航线、没有改航线 Shader，也没有把手册改成屏幕叠加；沿用默认 World Space UI 的深度测试。切换其他页面时恢复其原默认材质。

实际场景图已经人工检查：水面导航仍显示在界面周围，手册内容不再被覆盖。最终验收另对同一正式相机进行航线可见/隐藏的成对渲染，对比纸面内部像素，验证航线不会改变纸面内容；最终交付图保留航线可见状态。

## 资源与改动范围

- 样板呈现：`Assets/Scripts/Fishing/FishingXRNotebook.Home.cs`，从原 notebook 构建、Refresh 和首页反馈处接入。
- 美术资源：`Assets/Resources/FishingUIPrototype/`。插画使用内置 ImageGen 生成，完整最终提示保存在 `ArtSource/Fonts/VRUI/postcard-generation.json`；原图已复制到项目。图标与切片纸面可由 `Tools/Art/build_notebook_ui.py` 重建。
- 标题字：霞鹜文楷 Medium v1.522 派生的 Tide Hand SC，固定下载源/摘要与 OFL 位于 `ArtSource/Fonts/VRUI/`；正文仍用现有 Tide Sans SC，新增字形已重建。
- 验收：`FishingNotebookUIValidation.cs` 复用原 V025 驱动与 Canvas 截图工具；`FishingXRPresentationValidation.cs` 增加样板检查。
- 原游戏场景、预制体、交易事务、存档格式、XR SDK、版本号和已有 Windows/Android 发布包均未因样板重建或替换；没有回滚或提交工作区的既有改动。

## 尚未验收与后续

当前 ADB 无连接设备，因此没有 PICO 4 Ultra 双眼截图、真实手柄点击、侧视阅读、持续帧时或佩戴舒适度测量。上述场景图是 Unity 桌面单眼预览，不能当作真机验收。

按用户的阶段要求，待样板确认后再建立通用组件、将设计语言推广至背包/图鉴/码头/商店等页面，落实其他页面的切换/奖励动效，并进行真机与整体一致性验收。当前样板不代表全部 UI 已完成。
