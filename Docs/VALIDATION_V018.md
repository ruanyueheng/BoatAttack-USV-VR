# v0.18.0 验收记录

日期：2026-10-11（北京时间）。工程使用Unity2022.3.62f1，所有自动运行、购买和任务操作都使用隔离存档。

| 检查 | 结果与范围 |
|---|---|
| FishingTests | **2,089通过**；包括v9继承、船舶升级和扣款、用品入包/满包拒购、非线性岛屿进度、重复奖励拒绝、损坏存档和金币上限 |
| InputTests | **62通过**；输入、失焦恢复与渲染策略 |
| 船模生成与导入 | **通过**；船模4,906三角面、单网格；碰撞船壳62三角面。加深25cm的螺旋桨与实际动力采样点同步，生成后校验原有碰撞船壳文件SHA-256保持一致 |
| V018专项 | **通过**；四个真实支撑登陆点，登陆后连续步行至来信、宝箱和航向盘；通过角色手部动作接触并提交，宝箱防重复领取、正确方向点灯、四灯返港交付；金币、船舶外观与引擎立即更新，Unity JSON保存重载；新岛和商店进入真实地形底图 |
| V017航行回归 | **通过**；港口4秒起步约5.48m/s；持续转向最大倾斜0°、最高采样速度7.13m/s；螺旋桨有效采样**1,491/1,491**；倒航限速、松油门减速、完整船壳碰撞阻挡、倒船脱困、完全离水停止推进 |
| V016回归 | **通过**；单人钓艇、握持、摇轮、抄网、鱼获入箱与捕鱼后船体稳定 |
| 商店专项 | **通过**；14航点，柜台距离与视线、真实登陆/入口/柜台/上船通路、顾客与店员站位、金币和满级限制、界面开关帧隔离、暂停/离开/救援后的购买权限 |
| 岸区专项 | **通过**；原有四岸区、行走、调查奖励、上船与救援继续可用 |
| 工程与玩法回归 | **Regression、FishingSmoke、Structure通过**；包含已有鱼种、委托、天气、观察、存档备份和三个源场景结构 |
| Windows构建 | **成功**：`Builds/FishingPrototype-v0.18.0/FishingPrototype.exe` |
| Windows Player | **35场景通过**，包含豪华钓艇、新岛登陆、商店用品和群岛航记；检查真实生产规则和普通存档/偏好隔离 |
| 完整界面截图 | **1600×900与1280×1024各35张**；全部检查版号、尺寸与完整HUD探针，并记录SHA-256。目视检查重点商店、船只、岛屿与航记图片 |

复现入口：`Tools/Run-V018Validation.ps1 -Capture`。岛屿重新生成入口为`Tools/Run-UnityValidation.ps1 -Suite FishingV018Install`；船模重建使用Blender执行`Tools/Art/generate_solo_skiff.py`，再执行`FishingSkiffBuild`。

截图记录见[capture-evidence.json](Images/V018/capture-evidence.json)，构建散列见[build-artifacts.json](Images/V018/build-artifacts.json)。完整70张PNG保留在记录中的本地`Logs/desktop-acceptance-*`目录。设计选择、调研来源、商品与操作说明见[第18版设计](FISHING_V018_DESIGN.md)。

## 实景与界面

![商店内部满载陈列](Images/V018/shop-interior.png)
![商店外立面](Images/V018/shop-exterior.png)
![豪华船舶](Images/V018/LuxuryBoat-wide.png)
![探索用品柜台](Images/V018/ShopSupplies-wide.png)
![群岛航记](Images/V018/IslandJournal-wide.png)

[月牙风铃岛](Images/V018/island-0.png) · [翡翠潮林岛](Images/V018/island-1.png) · [沉船沙洲](Images/V018/island-2.png) · [星望遗迹岛](Images/V018/island-3.png)

[方形画幅：豪华船](Images/V018/LuxuryBoat-square.png) · [用品柜台](Images/V018/ShopSupplies-square.png) · [群岛航记](Images/V018/IslandJournal-square.png)

## 结果边界

- 标准满油门的专项复验中，基础直航采样约5.83–6.84m/s、满级动力约9.17–9.22m/s。实际航速受水阻、波浪和输入比例影响；舒适模式继续采用65%输入。旧版4.48m/s记录与本次测试位置、波浪相位不同，不把它们当作严格控制条件下的百分比性能比较。
- 岸上检查使用真实CharacterController与连续移动，通过手部接触动作提交，模拟输入不能替代长时间人工键鼠手感。旧岸区与商店已有实际上船回归，新岛还检查连续返回登陆点与正常上船。
- 检查重点画面不等于逐像素审查70张截图。新增岛屿是轻量模型，复用原工程棕榈素材；没有增加完整NPC日程、战斗、攀爬或大型剧情过场。
- 一轮方形截图在旧岸上人物/工具夹具断言处失败，留下12张有效截图；同一构建重跑后35场景全部通过，未放宽原断言。失败记录保留于`Logs/desktop-acceptance-21a0ba2c495a440e952d5abe96be7551/player.log`，这次重跑通过不等于长期夹具稳定性认证。
- PICO潮汐手册已接入新升级、用品与岛屿操作；**未做头显实体输入、立体显示或真机性能验收，也未生成新的Android安装包**。
- 新进度写入独立v10文件，旧档作为继承来源保留。工作基于现有本地资源与缓存，未在全新缓存中重做全部导入；没有回滚原有资源、导入或XR设置的本地改动。
- 第18版代码、场景、模型和验收资料提交至`feature/v018-island-adventures`分支供PR审查。Windows构建与完整原始日志保留在本地；原有其他资源、导入和XR设置的本地改动不纳入本次提交。
