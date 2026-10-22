# V037 绘本界面素材

使用内置 imagegen 生成原创绘本底纹，素材保存在 `Assets/Resources/FishingV037/`。运行导入上限1024，压缩、关闭mipmap，保留原始生成PNG作为可追溯源。

- `DreamSettings.png`：设置页的完整暖色水彩背景。杏色象牙纸、细金藤蔓、星月、环形行星和玫瑰沿边缘展开，控件区域保持留白；原图1672×941，整体显示，随昼夜调整色调。见[运行素材](../../Assets/Resources/FishingV037/DreamSettings.png)与[完整提示词](settings-prompt.md)。
- `Celestial.png`：航图与航行手册。星海航图、细罗盘、远帆，大片留白。
- `Herbarium.png`：鱼获图鉴、自然观察。淡鼠尾草纸、海草标本、贝壳与小蝶，非镜像边饰。
- `Workshop.png`：工坊、背包和家园储物记录。灰蓝亚麻纸、陶土铅笔细线、齿轮与月牙。

主菜单保留实时3D港湾。标题与选项区域使用连续羽化的阅读薄幕，渐隐到海景中；不再铺设独立信纸面板或拼接角饰。设置页使用一幅连续水彩画作为全屏底图，花纹沿边缘分布。

前三张纸面的完整提示词见 [prompts.md](prompts.md)，新增设置背景见 [settings-prompt.md](settings-prompt.md)。标题保留真实中文字体，图片不含文字；只有开场主标题保留细金描边，图鉴使用墨色书写标题，设置与储物使用清晰正文体。信件保留原来的私人信纸与封蜡。

## 水壶握持净空修复

沿用 V036 的原创白釉星月水壶，仅将提手顶部从局部 y=.038m 提升到 .120m，并让两侧曲线连续衔接壶身。壶口、壶嘴和装饰锚点保留，给真实角色手套留出握持空间；运行时握点与提手中心同步。

- [便携 GLB](starlight-watering-can.glb)、[Blender 源文件](starlight-watering-can-v037.blend)、[预览](starlight-watering-can-preview.png)。
- 作者脚本：`Tools/Art/build_v036_watering_can.py`，使用 Blender 5.2 后台执行。仅选中最终水壶导出，7956 三角面、4 材质；游戏继续通过原预制体读取同一 FBX GUID。

这次 Blender MCP 未连接，使用本机 Blender 后台执行原作者脚本；未修改交互式场景。
