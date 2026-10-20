# BoatAttack USV VR — v0.35.0 四境与回潮菜园

第35版补齐两种手作的双臂、握持、摇贝和落砂表现，并按设备显示启动键；四座秘境岛及内部按不同路线与玩法重建。家屋和扩大的家岛加入精细木构、家具、花园与12块可玩的菜畦，开垦、播种、浇水、跨日生长、收获、售卖和存档组成完整循环，地图同步新岸线。常驻信息减少底框，必要阅读区白天浅色、夜晚深色；书法标题采用沿笔画的镂金轮廓与五种字芯填色，正文保留易读字体。保留鼠标钓鱼、左手检视和三槽手动/自动存档。详见[第35版说明](Docs/PLAY_V035.md)、[验证记录](Docs/VALIDATION_V035.md)和[建模来源](ArtSource/V035/BUILD_NOTES.md)。

基于 Unity Boat Attack，使用 **Unity 2022.3.62f1 / URP 14.0.12**。第21版加入每次不同的遛鱼节奏、传说鱼反扑、10秒抄网窗口、图鉴调查解锁、天气与潮汐鱼种条件、加速过热和二级船舶专精取舍。

**Ctrl + W 加速，Shift 刹车，S 倒航。** 注意热量，过热后巡航冷却。J 查看调查清单与出没条件，低速 G 下船；E 交谈、寻宝或抄鱼。四岛、秘境、游泳、攀登继续可用，使用 v14 种田进度格式，兼容既有 v13 三槽存档，保留旧档备份。Windows 试玩入口：`Builds/FishingPrototype-v0.35.0/FishingPrototype.exe`；PICO 候选安装包：`Builds/FishingPrototype-v0.35.0-Android/FishingPrototype.apk`。两端构建已生成并校验，电脑便携包为 `Builds/FishingPrototype-v0.35.0-Windows.zip`。实体头显体验仍需人工验收。

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
