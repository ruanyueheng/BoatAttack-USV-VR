# UI 风格圣经 V044 ——「潮汐夜航手账」

参考《小王子：星辰之旅》（littleprince.dev）：深夜蓝水彩星空、奶油白衬线字、柔和投影、金色细线与四芒星。
本游戏保持海岸渔村/航海手账主题：**世界里的 HUD 是"夜航玻璃"，阅读界面是"水彩手账纸"**。

## 1. 色板（`FishingUITheme` Day/Night，按 `NightAmount` 0.38s 指数过渡）

| 角色 | 日 | 夜 | 用途 |
|---|---|---|---|
| Paper | #F6ECD5 | #162236 | 阅读面板底色（夜为深潮蓝） |
| Light | #FFFAEE | #202F46 | 选中/抬起表面 |
| Ink | #263A48 | #F7EED6 | 正文 |
| Muted | #546468 | #B8C6D6 | 次级文字 |
| Gold | #C49652 | #ECC882 | 金线、星芒、强调 |
| Sage / Coral | #6D9F89 / #EF916B | #89C3B1 / #F7A684 | 成功 / 警示 |
| HUD 夜航玻璃 | #13203A @86% | 同 | 世界内 HUD 卡片（日夜一致） |
| HUD 文字 | CreamInk #F7EBD0、AmberInk #E0B977、薄荷 #B3F0D4、危险 #FFA385 | 同 | 夜航玻璃上的文字 |

规则：一屏最多一种强调色；金色只用于线、星、数字，不做大面积填充。

## 2. 字体
- **标题/地名/章节**：Noto Serif SC SemiBold（思源宋体，SIL OFL 1.1），`Resources/FishingV044/Fonts/NotoSerifSC-SemiBold-Subset.otf`，许可证 `NotoSerifSC-OFL.txt`。
  子集化：`python -I Tools/Art/build_v044_ui_font.py ArtSource/V044/Fonts/build/NotoSerifSC-SemiBold.otf Assets/Resources/FishingV044/Fonts/NotoSerifSC-SemiBold-Subset.otf`（完整源字体只放 ArtSource，不进包），收录 ASCII + 常用标点 + GB2312 一级 3755 字 + 所有脚本中出现的汉字（约 3900 字，1.3 MB，原 11.7 MB）。新增文案后重跑脚本。缺字回退 TideSansSC（importer fallback）。
- **正文/数字/按键提示**：TideSansSC（无衬线，小字号更清晰）。
- 毛笔字 MaShanZheng 退役（仍保留在 Resources 供旧存档/手工游戏引用，后续可删）。
- 字号阶梯（1600×900 逻辑像素）：地名 40 / 卡片标题 27–34 / 行动标题 20 / 正文 15–17 / 注释 13，**不低于 12**。

## 3. 面板材质（`Resources/FishingV044/UI`，`Tools/Art/build_v044_ui_textures.py` 程序化生成）
- `PanelPaper` 256²：奶油水彩纸，湿画法色块、毛边羽化、内缩 16px 双端金细线、四角四芒星。9-slice 边 40px。夜间乘深潮蓝色调。
- `PanelVeil` 256²：深夜蓝水彩玻璃 86% 不透明，同样毛边/金线/星角。世界内 HUD 专用。
- `Star` 64²：白色四芒星+光晕，绘制时着色。`GiltRule` 512×16：两端渐隐金线。`Slot` 128²：快捷栏格。`TitleGlow` 256×128：地名暗晕。
- 全部 ≤1024、压缩、无 mip，IMGUI 每个面板 1 次 9-slice 绘制（旧实现 Card 约 20 次绘制）。

## 4. 图标
线描 + 平涂，1.5px 墨线，圆角端点；选中态用金色着色 + 下方小星，而非加粗边框。物品图沿用模型缩略图。

## 5. 动效
- 出现：淡入 0.2s + 上浮 6–8px（SmoothStep），消失淡出 0.5s。地名卡 1.1s 上浮。
- 禁止：抖动、缩放弹跳、持续闪烁；VR 中禁止任何头锁定动画。

## 6. 布局网格与安全区（逻辑 1600×900，按短边等比缩放，左右居中）
- 外边距 24；8px 基础网格。
- 区域分工：左上=地点/时间；右上=货币/工具栏/航海目标；上中=通知(y24) → 地名(y150–240)；下中=行动卡 + 快捷栏；左下=小地图。
- 中心 560×360 区域（准星周围）不放常驻面板。

## 7. 信息层级 / 情境显示
1. 当前行动（下中，唯一常驻卡片）
2. 瞬时反馈：通知、钓获卡（7s 自动淡出）
3. 地名（到达时，期间**隐藏航海目标卡**避免同屏三卡）
4. 常驻弱信息：地点、货币、快捷栏、小地图——无底卡，仅文字+1px 暗影

## 8. VR 可读性（世界空间 uGUI 手账）
- 手账距眼 1.1 m（遇墙拉近，最近 0.8 m），40° 外懒跟随；Canvas 0.00105 m/px 下正文 21–22 px、教学卡 ≥18 px 约 ≥1° 视角；正文对比度 ≥ 7:1，次级 ≥ 4.5:1（`FishingV028UIValidation` 检查）。
- 世界 UI 不用模糊、实时阴影、额外透明叠层；装饰用烘进贴图的 9-slice。
