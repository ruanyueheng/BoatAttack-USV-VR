# 第17版船舶素材检索与选型

检索日期：2026-10-11。优先考察可下载、许可明确、能适配单人第一人称钓鱼的模型。下面的网页面数是作者/平台标注；只有 Kenney 样本另外下载并做了本地检查。

| 素材与来源 | 许可 / 规模 | 适配判断 |
|---|---|---|
| [Kenney Watercraft Kit 2.1](https://kenney.nl/assets/watercraft-kit) | CC0；45个文件；本地 `boat-fishing-small` 实测237三角面 | 包内有小渔船、划艇、快艇、拖船。样本轻量、配色统一，但近距离细节少，驾驶台布局与现有身体/鱼箱不一致。已保存一个GLB候选及原许可证，方便后续比较。 |
| [Muyaya Concept: Low Poly Fishing Boat](https://sketchfab.com/3d-models/low-poly-fishing-boat-8554595b733c46ccabc6dc6a90adaa6f) | CC BY；1,277三角面；FBX/GLB/Blend；单材质、调色板纹理 | 较适合低模海湾场景；替换玩家船前仍需检查甲板空间、缩放和视角。尚未下载导入。 |
| [Muyaya Concept: Low poly Greek Fishing Boat](https://sketchfab.com/3d-models/low-poly-greek-fishing-boat-315a83fe28014d2f890acbe16f04a26f) | CC BY；约4.9k三角面；FBX/GLB/Blend；单材质 | 传统希腊渔船轮廓更完整，可作为后续外形方向；需要重新安排驾驶与垂钓站位。尚未下载导入。 |
| [Ottto3d: Low Poly Old Rusty Fishing Boat](https://sketchfab.com/3d-models/low-poly-old-rusty-fishing-boat-3713c37983ea4c04b87fe173e2631b76) | CC BY；10,469三角面；Blend/FBX/OBJ；2K/4K PBR纹理 | 有船舱和驾驶设备，适合更大的船；现有单人钓艇玩法不需要这一体量。移动端需要纹理压缩与材质合并。尚未下载导入。 |
| [wasabicats: Low Poly Fishing Boat](https://sketchfab.com/3d-models/low-poly-fishing-boat-3e929d6f6ee6452fa2a05a8e8faae7ab) | CC BY；615三角面；PS1风格 | 更适合背景船；不适合当前近距离握持与船上物件的视觉密度。尚未下载导入。 |
| [Innerscene Fishing boat](https://www.innerscene.com/tools/library/3d-parts/fishing-boat-3c5a6bfd) | CC0；GLB 7.8MB；网页标注约8.7×3.5×4.1m；面数未核实 | 可作为较大渔船候选，但尺寸与当前约4m敞口艇差异明显。尚未下载导入。 |
| [artfletch: Small Fishing Boat](https://sketchfab.com/3d-models/small-fishing-boat-7e2f6cc030b6405c97606b1668d76beb) | CC BY；平台标注约200万三角面；实景扫描 | 可参考真实轮廓与磨损；直接用于本项目成本过高，需要重新拓扑。尚未下载导入。 |
| [JasperTobias Fishing Boat](https://sketchfab.com/3d-models/fishing-boat-e07c8b9cc38543879a4e2fe145e62df6) | CC BY-NC；约3.8k三角面 | 非商业限制，不作为需要自由再分发的默认素材。尚未下载导入。 |

第17版采用修复并细化原敞口艇的方案：连续厚壁船壳、正确内外法线、奶油色内舱、船内肋条、船首储物板、舷外机支架/螺旋桨与带护架甲板灯。保留原有座位、鱼箱托架、握持坐标、浮力和海岛玩法；可见模型4,906三角面、一个合并网格，碰撞船壳62三角面。`ArtSource/BoatReferences/Kenney`中的GLB是候选源文件，未接入运行时。

Kenney下载地址：[官方素材包](https://kenney.nl/media/pages/assets/watercraft-kit/a335cfed49-1713519620/kenney_watercraft-pack.zip)。原版许可证随样本保留。CC BY模型若以后采用，应保留作者署名和许可链接，并记录修改；本次没有把未下载核查的模型标为已导入。

![本版实测船模](Images/V017/SkiffStudio.png)
![闭合内壁与船底](Images/V017/SkiffInterior.png)
