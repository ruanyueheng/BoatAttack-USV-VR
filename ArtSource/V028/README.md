# V028 沿海物件来源与生成记录

核查日期：2026-10-15。运行时模型位于 `Assets/Resources/FishingV028/Coast/`，可编辑工程为本目录的 `Living-coast-v028.blend`。六张透明缩略图由各自最终模型渲染，背包、手持物件和场景使用同一套模型。

## 网上素材与许可

本版复用工程中已经保存、已经纳入 Git 的 Kenney 原始素材；再次核对官网和包内许可证，二者均注明 CC0。没有用网页预览图代替可分发模型。

| 物件 | 原始素材及许可 | 本版加工 |
| --- | --- | --- |
| 香烤海鱼 `GrilledFish` | [Kenney Food Kit](https://kenney.nl/assets/food-kit)，Food Kit 2.0；原件 `../V019/Kenney/food-kit/Models/GLB format/fish.glb`；[包内许可证](../V019/Kenney/food-kit/License.txt) | 转为侧卧整鱼，重新调整比例；把原材质颜色转为暖褐色顶点色，添加焦痕、表皮刻纹、柠檬切片和香草。 |
| 沉海宝匣 `SeaTreasure` | [Kenney Pirate Kit](https://kenney.nl/assets/pirate-kit)，Pirate Kit 2.1；原件 `../V019/Kenney/pirate-kit/Models/GLB format/chest.glb`；[包内许可证](../V019/Kenney/pirate-kit/License.txt) | 统一尺度和木铜配色，补上铆钉、黄铜锁扣、锁孔及海底附着物。 |
| 海风烧烤架 `Barbecue` | 本版 Blender 建模 | 倾斜铁腿、脚垫、炭盆、灰盘、炭块、余烬、金属烤网、挡风板、通风孔、侧木板和手柄。 |
| 渔屋鱼箱 `FishCooler` | 本版 Blender 建模 | 保温箱体、盖沿、箱扣、销轴、绳柄和鱼标；与家中奶油色、海绿色配色一致。 |
| 海漂旧靴 `OldBoot` | 本版 Blender 建模 | 连续靴筒、卷边、分层鞋底、鞋眼和鞋带，附有水草。 |
| 锈蚀铁罐 `RustyCan` | 本版 Blender 建模 | 不规则罐壁、卷边、罐底、金属接缝及锈蚀色块。 |

CC0 原始资产由 Kenney 创作；以上自制和修改部分为本项目新增内容，不对第三方原件重新声明所有权。原始 GLB 的 SHA-256：

```text
food-kit/fish.glb    5bb8109f83f7ec4609427c6d6de76572c4fc4d055dfc16d47f2c397290c0e29d
pirate-kit/chest.glb 26d26c4c032a12320fe05b5005a72eba96db7167db0d35873639a4b7dcc06bcb
```

## 生成与预算

在仓库根目录用 Blender 5.2 的后台进程运行：

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python Tools/Art/build_v028_living_coast.py
```

脚本保留米制坐标，合并每件物件的网格和材质，检查顶点色覆盖、尺寸、离地高度与三角面上限，再导出 FBX、256×256 透明缩略图、Blender 工程和 `coast-manifest.json`。运行时使用项目顶点色材质；原始贴图的颜色已采样到顶点色，成品不依赖外部贴图文件。

| 模型 | 三角面 |
| --- | ---: |
| Barbecue | 3,380 |
| FishCooler | 864 |
| GrilledFish | 841 |
| OldBoot | 1,924 |
| RustyCan | 1,432 |
| SeaTreasure | 808 |
| 合计 | 9,249 |

2026-10-15 已逐件核对六个 FBX 的 SHA-256，全部与清单一致；原始 GLB 与许可证均已存在于 Git。已人工查看整鱼、旧靴和烤架缩略图，轮廓、配色及主要结构清楚。Unity 导入、真实场景截图与游戏交互结果见 `Docs/VALIDATION_V028.md`；单凭缩略图不代表 PICO 头显中的最终观感或性能。
