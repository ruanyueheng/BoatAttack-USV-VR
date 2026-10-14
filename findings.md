# 第23版发现

- 第22版PR #32已合并到origin/main，当前基线0f4c17ed；创建feature/v023-sea-life-home-and-prologue。
- 四条用户原件位于ArtSource/Hunyuan/Fish/Originals：Coral、Dart、Puffer、Sailfish。
- import_hunyuan_fish.py已有12物种导入及缩略图路径，旧版使用Blender EEVEE名称，需要适配5.2且仅导入新原件。
- 现有物种12个、发现位23个，追加物种需要审查发现位与存档范围。
- 开发前存在52个受追踪文件修改及旧资源未追踪文件，不能整仓stage或reset。
- Unity 2022.3.62f1、6000.0.73f1、6000.3.24f1和Blender 5.2已安装；Unity及Blender没有运行。

## 资源检索与选择
- Kenney家具包 https://kenney.nl/assets/furniture-kit ：140个CC0模型，下载后选择23个，保留所选GLB及原License.txt。
- Kenney生存包 https://kenney.nl/assets/survival-kit ：80个CC0模型，木板/木材/石料被采用；原工作台过于粗略，改为原创圆角木工台、抽屉、虎钳、金属紧固件与锤。
- Quaternius Ultimate Animated Animals为CC0，但12种主要是陆地动物，不能补齐目标海洋物种。
- Poly Pizza两种Octopus为CC-BY；免费下载端遇到访问验证，且混用风格不如统一制作。只作为检索候选，未复制模型或付费资产。
- Poly Pizza列出收费Low Poly Animated Animals和Animated Sea Animals Vol.2：未购买；8种目标物种使用原创Blender几何。
- 玩家鱼原件约5万三角面；本版四条降至3500，纹理1024，鼓鼓鱼头向修正，高帆缩略图增加纵向留白。
- 新增8种：SeaTurtle、Octopus、Crab、Lobster、Seahorse、Stingray、Jellyfish、Cuttlefish。新资源5000三角面以内，独立轮廓、顶点配色和实物照片。
- 原发现位已占23位，增加8种后为39位：改用long，v12独立存档读取v11，旧12项纪录自动补为20项。
