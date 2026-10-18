# 第31版原创资源

- `field-guide-v031.blend`：可开合自然手册的 Blender 编辑源。
- `guide-models.json`：主体、封面和翻动页的三角面与摘要。
- `guide-open.png`：书体参考视图；运行中的开合由 Unity 手部系统控制。
- `lantern-grip.json`：新提手的固定握点、手指弯曲量与实际网格验证依据。
- `image-prompts.json`：内置 imagegen 的完整提示词与项目内资源路径。

`Tools/Art/build_v031_guide.py` 可在新建的后台 Blender 进程重现手册、海豚与陆龟肖像。肖像比例沿用游戏现有海豚，不使用第三方站点资产。运行资源在 Assets/Resources/FishingV031；提灯源GLB和9S原始ZIP继续保留在ArtSource/V030。

提灯修改仅涉及运行材质和固定手部校准：灯窗为暖琥珀色，外框关闭把绿色海水投到灯芯上的环境反射。未改写用户原始模型。

手册FBX导入需开启 bakeAxisConversion，保留薄Y轴和封面从铰链向+X伸展的约定；专项检查验证这两项，避免封面翻转方向出错。
