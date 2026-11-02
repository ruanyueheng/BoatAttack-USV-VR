# BoatAttack USV VR — v0.60.0 VR 模拟审阅与修复

2026-11-03 已对当前 0.60.0 源码完成一轮原生 OpenXR 模拟试玩，修复附近互动、岸上快门冲突、近眼遮挡和照片写入失败保护；验证范围、截图及未验证项见[本轮审阅记录](Docs/VR_SIMULATION_REVIEW_20261103.md)。

海岛生活冒险：驾驶小艇航行、VR 钓鱼、四岛探索、手作生产、家园种养与商店交易。0.58 修复双筒望远镜尺寸与头显双眼对齐、握把拿起锤锯和转轮手作、左右手握把拾果、六类栖地建设入口和落地显示、菜单真实快购，并为汐禾加入呼吸、招呼和交谈动作。操作与验证见[0.58 修复记录](Docs/VALIDATION_V058_VR_INTERACTIONS.md)。

本地审阅已确认 PR #65 包含 #64、两者无合并冲突；补修鱼箱“上一页”被卖鱼入口覆盖的问题，并增加锯锤和左右手拾果的原生 OpenXR 回归。详见[两份 PR 的审阅与本轮验证](Docs/PR64_PR65_REVIEW_AND_VALIDATION.md)。

## 0.56 与此前整合

本版在已合并的 0.55 上，同步同学 [PR #59](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/59) 截至 `984a6cf3` 的新增改动：左手夹鱼器与重力悬挂检视、随实际鱼线方向弯曲的 VR 鱼竿、统一震动倍率、提灯显式收回背包和左扳机岸上疾跑。补齐实际背包网格的收灯入口，保留原手部校准、创造模式、航程日期、速度调节、确认退出及 [PR #60](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/60) 的家园集市修复。详见[0.56 整合与验证](Docs/VALIDATION_V056.md)和[0.55 验证记录](Docs/VALIDATION_V055.md)。

本版重做萝卜幼苗与种子袋、奶油漆墙面、五类动物住所及栏内活动，补齐菜园与限定商船实体碰撞，并完善商品价格、注视选择和 A 键购买。修复进入 VR 后商品牌变空、驻钓时购买被抛竿抢占及世界文字透墙。详见[0.54 验证与购买方法](Docs/VALIDATION_V054.md)。

本版整合新的界面、四境机关、居民动作与配音、海上事件、守灯/潮汐、相机图鉴及 VR 交互；补齐古树、风车、沉船、灯塔，并修复航线穿入旧地形和家园泊位净空。详见[第44版改动](Docs/CHANGELOG_V044.md)、[验证与遗留](Docs/VALIDATION_V044.md)和[VR操作/实机清单](Docs/VR_CONTROLS_V044.md)。

使用 Unity 2022.3.62f1。存档保存为 schema20，兼容已有有效旧档；升级保存后请继续使用本版，并保留原备份。PICO 真机帧率与佩戴体验仍需实际验收，岛0/岛1的性能目标尚未达成。

## 第39版及此前内容

第39版加宽沉船浮桥并补齐绳栏实体碰撞与通行开口，修复自然上岸、绕线反复恢复斜角和家园物件悬浮；烤鱼采用腾讯混元3D实际生成模型，配合Meowa滋滋声、轻烟、炭火与海岛环境粒子。详见[第39版说明](Docs/PLAY_V039.md)和[本轮验证](Docs/VALIDATION_V039.md)。此前持壶、持镐、水体与家园内容继续保留，见[第38版说明](Docs/PLAY_V038.md)。

基于 Unity Boat Attack，使用 **Unity 2022.3.62f1 / URP 14.0.12**。第21版加入每次不同的遛鱼节奏、传说鱼反扑、10秒抄网窗口、图鉴调查解锁、天气与潮汐鱼种条件、加速过热和二级船舶专精取舍。

**Ctrl + W 加速，Shift 刹车，S 倒航。** 注意热量，过热后巡航冷却。J 查看调查清单与出没条件，低速 G 下船；E 交谈、寻宝或抄鱼。四岛、秘境、游泳、攀登继续可用，使用 v15 家园储物与果树进度格式，兼容既有 v14 及更早有效存档，保留旧档备份。按[运行与验收工具](Docs/TESTING.md)构建当前版本；第39版检查范围及构建状态以[本轮验证](Docs/VALIDATION_V039.md)为准。实体头显体验仍需人工验收。

- [第21版操作与改进](Docs/PLAY_V021.md)
- [第21版验证记录](Docs/VALIDATION_V021.md)
- [第21版PR审查](Docs/PR_REVIEW_V021.md)
- [第20版操作与改进](Docs/PLAY_V020.md)
- [第20版测试、构建与实际画面](Docs/VALIDATION_V020.md)
- [第20版Blender模型与来源](ArtSource/V020/BUILD_NOTES.md)
- [第19版潮汐回响玩法](Docs/PLAY_V019.md)
- [第18版设计、调研与使用说明](Docs/FISHING_V018_DESIGN.md)
- [第18版测试、构建与截图](Docs/VALIDATION_V018.md)
- [第17版改进与使用说明](Docs/FISHING_V017_BOAT_HANDLING.md)
- [第17版测试、构建与截图](Docs/VALIDATION_V017.md)
- [8种船舶素材、许可与选型](Docs/BOAT_ASSET_RESEARCH_V017.md)
- [第16版单人钓艇与真实握持](Docs/FISHING_V016_SOLO_SKIFF.md)
- [第15版海湾体验](Docs/FISHING_V015_COASTAL_EXPERIENCE.md)
- [运行与验收工具](Docs/TESTING.md)

The original project description and credits follow below.

---

**_Note:This repository uses GitLFS, to use this repo you need to pull via Git and make sure GitLFS is installed locally_**

# Boat Attack
###### Demo Project using the Universal RP from Unity3D

[![Click for Youtube Video](https://media.githubusercontent.com/media/Verasl/BoatAttack/release/2019.3/Assets/Textures/UI/welcome-title.png)](https://drive.google.com/file/d/1PTNdhnqbhzSWUCfAinIefP5cfr6Dezuw/view)

This Unity project has been created to aid the testing and development of Universal RP. The project is a small vertical slice of a boat racing game, complete with race-able boats and island environment.

Feel free to post any issues, but know this is a 'as is' repo, it's meant more for discovery of how some of the Universal RP features work and a learning resource for some tricks.

**Project Features**:
  * Uses Universal RP from Unity
  * Mobile optimized, low poly, LODs, no compute
  * C# Jobs buoyancy system
  * Cinemachine camera setups
  * Shader Graph usage
  * Post-processing v3 with Volume blending
  * Addressables asset management package
  * Custom Universal RP rendering for planar reflections via [SRP callbacks](https://docs.unity3d.com/ScriptReference/Rendering.RenderPipelineManager.html)
  * Custom SciptableRenderPass usage for WaterFX and Caustics
  * Gerstner based water system in local package(WIP)
  * Much more..

[Demo Footage](https://drive.google.com/file/d/1PTNdhnqbhzSWUCfAinIefP5cfr6Dezuw/view)

# Usage

#### Getting the project
via Git:
  1. Make sure you have GitLFS installed, check [here](https://git-lfs.github.com) for details.
  2. Clone the repo as usual via cmd/terminal or in your favourite Git GUI software.
  3. Checkout the branch that matches the Unity verison you are using, eg `release/2019.3`

Downloadable zips:
  1. [2019.3 Project (Unity 2019.3f5)](https://drive.google.com/file/d/1vXpbVC36GHnyC-Eitl1WpLay9l_YqJGQ/view?usp=sharing)

#### Load the project:
Once you have the project files locally you can load the project, ideally in the Unity version that is noted in the `ProjectSettings/ProjectVersion.txt` for the best experience.
Upon loading the project will display a small welcome screen with some buttons to load starting scenes.

Scenes worth noting:
 - `scenes/main_menu.unity` - Starting menu scene if you want to have a full play-through of the demo.
 - `scenes/demo_island.unity` - Setup to play in the editor and go straight into an AI based race.
 - `scenes/_levels/level_Island.unity` - The scene loaded when entering from the main menu.
 - `scenes/Testing/***.unity` - Assorted test scenes, these are in need of updating and come as is.

#### Build the project:
One thing to make sure you do before building is make sure to build the addressable assets, this can be done via the addressables window, for more information please checkout the addressables [package documentation](https://docs.unity3d.com/Packages/com.unity.addressables@latest).
Once the addressable assets are built you can continue to build a player as usual.

One thing to mention is not all controls and platforms have been tested, especially for the menu work. if you want to just see the project running on a device you can add the `scenes/demo_island.unity` scene to the build list and disable/remove the others.

# Todo

As this project is on going there is a lot more left that needs to be worked on, so I repeat this is not a resource for production ready workflow ideas or systems and lots of it was put together very quickly.

Some of the things left to do:
 * Make water system more modular and improve UX
 * Improve boat AI
 * Add imposter rendering for vegetation
 * Cleanup menu system to switch between Demoing/Playing/Benchmarking
 * Implement Unity Physics
 * Optimize cross platform performance and stability
 * Continue code cleanup
 * Wiki explaining features/systems in more depth
 * Add more sizzle....

![Sunny Island](https://gdurl.com/STO1)

# Credits
[Andre McGrail](http://www.andremcgrail.com) - Design, Programming, Modeling, Textures, SFX

[Alex Best](https://big_ally.artstation.com) - Modeling, Textures

[Stintah](https://soundcloud.com/stintah) - Soundtrack

Special thanks to:

[Felipe Lira](https://github.com/phi-lira) - For Making Universal RP & LWRP

[Tim Cooper](https://github.com/stramit) - Assorted SRP code help

And thanks to many more who have helped with suggestions and feedback!

# Notes

*Make sure you clone the repo as downloading the zip will not contain the GitLFS files(all textures/meshes etc)
