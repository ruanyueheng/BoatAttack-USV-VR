# Tide Hand SC

仅用于潮汐手册样板的手写标题。由霞鹜文楷 Medium v1.522 裁剪并重命名，原字体与派生字体适用同目录的 SIL OFL。下载 URL、固定版本和 SHA-256 保存在 `source.json`；派生字体保留原字体版权与授权名表。

在项目根目录运行 `python Tools/Art/build_notebook_ui.py` 重建标题字库、九个原创导航图标与切片纸面。使用已有 Python / Pillow / fontTools，不新增项目依赖。源字体缓存位于 `Logs/font-cache/LXGWWenKai-Medium-v1.522.ttf`，重建时验证固定摘要。

正文继续由 `Tools/Art/build_ui_font.py` 重建 Noto Sans SC 派生的 Tide Sans SC。新增中文文案后应重建字集并运行 Unity 的缺字和文本尺寸检查。
