# BoatAttack USV VR — v0.13.0 第一人称握持与海岸店员

基于 Unity Boat Attack，使用 **Unity 2022.3.62f1 / URP 14.0.12**。v0.13.0 在已合并的 v0.12 上继续改进：按实际蒙皮标定掌心、逐物品握点与手指姿态、低头时的本地身体遮罩与腰口封闭、望远镜的举镜/光学观察过渡，并补齐原商店站位上的成年女性店员。

女店员按本次要求接入同学 `feature/v011-cashier-integration` 分支中的 **FantasyGirl 原模型**，复用 v13 柜台、交易与去重逻辑，并修正旧 prefab 对店员脚本的 GUID 引用。原素材 README 未提供作者/许可证，本地开发接入不代表取得公开再分发许可。原 PR #20 的玩家修复、#21 的实体商店/商品说明/余额与经济测试继续保留。女店员是可见的静态角色，不是 NPC AI；其他顾客站位仍为空。

航行捕鱼、抄网收鱼、真实地形地图、背包采集制作、港口委托、日志、第一/第三人称、v8 存档与第十航点继续保留。验收扩展为 **23 个场景、三种桌面画幅**；具体通过/失败、构建、Player 图像和审查状态见本版记录，不以历史日志冒充本次通过。**PICO 真机、人工键鼠完整流程与持续性能仍需单独验收。**

- [v0.13.0 改进、PR 核对与验收记录](Docs/FISHING_V013_FIRST_PERSON.md)
- [第一人称技术调研与实现取舍](Docs/FIRST_PERSON_RESEARCH_V013.md)
- [原店员来源与导入说明](ArtSource/FantasyGirl/README.md)
- [v0.12.0 PR 审查、玩家修复与验收](Docs/FISHING_V012_PLAYER_REPAIRS.md)
- [v0.11.0 地形地图、同源人物与接触事务](Docs/FISHING_V011_TERRAIN_AVATAR.md)
- [人物来源与 CC0 许可](ArtSource/Characters/ATTRIBUTION.md)
- [v0.10.0 海岸工坊、人物与上下船改进](Docs/FISHING_V010_WORKSHOP.md)
- [v0.9.0 net handling, catch transactions and XR candidate / 实体抄鱼与 XR 闭环](Docs/FISHING_V09_CATCH_LOOP.md)
- [v0.8.0 backpack, land exploration and reverse / 背包与陆地探索](Docs/FISHING_V08_FIELD.md)
- [v0.7.1 shore mouse-look fix and regression review](Docs/FISHING_V071_LOOK_FIX.md)
- [v0.7.0 first-person hands, spinning rod and shore mouse-look](Docs/FISHING_V07_HANDS.md)
- [v0.6.1 harbor prop art integration and validation](Docs/FISHING_PROP_ART.md)
- [v0.6.0 review and acceptance / 重燃旧港信标](Docs/FISHING_V06_REVIEW.md)
- [v0.6 design and implementation history](Docs/FISHING_V06_DESIGN.md)
- [v0.5.0 embodied shore exploration / 海陆探索](Docs/FISHING_V05_SHORE.md)
- [Play the fishing prototype / 捕鱼试玩说明](Docs/FISHING_PROTOTYPE.md)
- [v0.4.2 focused HUD: review, regression and acceptance limits](Docs/FISHING_V042_REVIEW.md)
- [v0.4.1 review: progression, performance and order HUD fixes](Docs/FISHING_V04_REVIEW.md)
- [v0.3.1 living bay: weather, wildlife and modeling](Docs/FISHING_LIVING_BAY.md)
- [v0.3 bay exploration, UI and XR preparation](Docs/FISHING_V03.md)
- [v0.2 modeling, presentation and first-person desktop view](Docs/FISHING_V02.md)
- [Current integration status](Docs/INTEGRATION_STATUS.md)
- [Reproduced fixes and remaining risks](Docs/PROJECT_HEALTH_CHECK.md)
- [Team collaboration and synchronization](Docs/TEAM_COLLABORATION.md)
- [Validation commands and limits](Docs/TESTING.md)
- [Gameplay candidates — not implemented](Docs/GAMEPLAY_OPTIONS.md)
- [How to Fish research and design rationale](Docs/HOW_TO_FISH_RESEARCH.md)

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
