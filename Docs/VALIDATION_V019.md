# v0.19.0 验收记录

2026-10-11开始开发，2026-10-12完成桌面验收（北京时间）。Unity2022.3.62f1，测试全部使用隔离存档；普通存档和偏好不作为测试夹具。

| 检查 | 结果与实际范围 |
|---|---|
| FishingTests | **2,146通过**；PR提交前从暂存索引隔离导出的源码再次通过；新增v10继承v11、NPC认识、四种秘境规则、错误音序、方向循环、重复奖励拒绝、剧情终章、无效/损坏进度、金币上限与体力边界 |
| InputTests | **62通过**；同样从暂存索引隔离导出的源码再次通过；原有输入、失焦恢复和渲染策略 |
| V019专项 | **通过**；四岛真实登陆与连续角色移动、NPC对话及模态隔离、四种实际机关、实体门、首通/重复领取、进出秘境安全支撑、终章、店员介绍、用品购买、口粮消耗与恢复、绳梯登顶、游泳入海/休息/上岸、救援、Unity JSON重载 |
| 船体转向 | **通过**；基础目标从45–26提高到60–38°/s，响应系数4→4.8，钓艇最大角速度1→1.8rad/s。专项采样实际最大约0.714rad/s，船体倾斜低于1°，动力有效采样全部保持；这些采样值不是所有航速和升级下的固定转向率 |
| V018/V017/V016 | **通过**；来信、旧宝箱、航标、旧终章、船只升级、推进/倒航、波浪动力、船壳碰撞、捕捞/抄网/入箱与船体稳定 |
| FishingShop/FishingShore | **通过**；柜台距离与视线、旧商店购买/输入隔离、原四岸区、真实行走通路、调查奖励、登船及救援 |
| Regression/FishingSmoke/Structure | **通过**；共享组件、主要捕鱼玩法、旧存档/备份恢复、天气/观察、三个源场景结构 |
| Windows构建 | **成功**；`Builds/FishingPrototype-v0.19.0/FishingPrototype.exe` |
| Windows Player | **45场景通过**；在第18版35场景上增加NPC剧情、四秘境、世界航记、远航物资、地区渐显、游泳与攀登 |
| 完整HUD截图 | **1600×900和1280×1024各45张**；全部核对尺寸、版本和HUD探针，并保存SHA-256；重点画面的文字、标签、按钮、人物与机关布局经过目视检查 |
| 新资产 | **45个FBX通过导入、顶点色、尺寸/面数约束与缩略图检查**；七种原创Blender道具、五个由授权成人网格派生的居民外观及精选Kenney模型。来源、尺寸、面数与散列见资产清单 |

复现入口：`Tools/Run-V019Validation.ps1 -Capture`。只重建场景时使用`Tools/Run-UnityValidation.ps1 -Suite FishingV019Install`；模型由本机Blender执行`Tools/Art/build_v019_assets.py`重建。

证据：[capture-evidence.json](Images/V019/capture-evidence.json)记录最终两轮截图的真实文件位置与散列；[build-artifacts.json](Images/V019/build-artifacts.json)记录最终Windows程序及程序集散列。完整原始PNG与日志保留在这些记录指向的本地Logs目录。

## 重点画面

![分支剧情对话](Images/V019/ResidentDialogue-wide.png)

![商店分区与用品插图](Images/V019/ShopRations-wide.png)

![星象秘境](Images/V019/StarSanctuary-wide.png)

[森光庭院](Images/V019/ForestSanctuary-wide.png) · [沉潮机库](Images/V019/WreckSanctuary-wide.png) · [世界航记](Images/V019/LoreJournal-square.png) · [地区渐显](Images/V019/RegionArrival-wide.png) · [游泳](Images/V019/SwimmingView-wide.png) · [攀登](Images/V019/ClimbingView-square.png)

![部分资产导入预览](Images/V019/asset-gallery.png)

## 结果边界与修复记录

- 自动测试调用真实角色、引擎、碰撞、交易和保存规则，仍不能替代长时间人工键鼠手感或实体PICO操作；没有头显真机性能/立体显示结论，没有新增Android安装包。
- NPC为固定地点的对话角色，包含转身与分支文本；没有完整AI日程、口型或配音。游泳/攀登使用现有角色的轻量骨骼叠加，攀登限于两条制作好的绳梯。
- 秘境是四个独立房间和四类机关；中途退出重置未完成机关，已完成回响与奖金永久保存。没有扩展战斗、滑翔、物理建造或大型开放世界系统。
- 商店有18种实际可购用品及六条升级线；部分陈列水果/食品作为环境物件，不单独出售。
- 目视检查重点画面不等于逐像素审查全部90张PNG。截图顶部的SANDBOX栏只属于显式验收模式，普通启动不会显示。
- 开发期间定位并修正了缩略图层被URP过滤、旧角色顶点色通道覆盖衣服颜色、居民误取树冠高度、测试绕出商店栈桥、口粮整组丢弃接口误用、游泳上岸抓取距离不足等问题；失败日志保留，没有用放宽奖励/碰撞守卫替代修复。
- 本版使用独立`boatattack-fishing-v11.json`；首次读取可以继承v10。旧文件保留，v11检查NPC、回响、终章及金币的组合有效性。资源导入与验收基于当前本地缓存，未在全新Unity缓存中重建全部依赖。
- GitHub审查提交范围为`feature/v019-tidal-realms`中的第19版代码、运行时资产、所选可重建源文件和验收证据。开始工作时已有的船模、资源导入与XR设置修改保留在本地，不纳入本版PR；项目设置仅提交版本号更新。Windows成品与完整原始日志不上传Git，构建入口和散列随PR保留。
