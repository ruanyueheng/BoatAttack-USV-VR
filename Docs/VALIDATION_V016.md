# v0.16.0 桌面验收记录

日期：2026-10-11（北京时间）。使用临时隔离存档，未改动普通玩家存档。

| 检查 | 结果与范围 |
|---|---|
| .NET FishingTests | **1,851通过**，含缩小网口后的越界捕获拒绝、鱼获事务及既有规则 |
| .NET InputTests | **62通过**，输入与渲染策略 |
| Unity 第16版专项 | 航行/到达未选择钓鱼时隐藏竿；选择钓鱼后装备；12个摇柄角度下真实掌心到达、握点位于可见旋钮、手腕不翻转；右手持竿左手抄网；实际网捕获/入箱；船体浮航稳定；舷外机朝向与推进点一致；真实船壳扫掠被导航浮标阻挡 |
| Unity 回归 | Character、FirstPerson、Interaction、Catch、FishingSmoke、Structure通过；包括实际箱沿接触、取消与重复提交、上下船与源骨架表现 |
| Windows Build | **成功**：`Builds/FishingPrototype-v0.16.0/FishingPrototype.exe` |
| Windows Player | 隔离存档的30场景生产规则验收通过 |
| 完整HUD截图 | 1600×900与1280×1024各30张，核验版本、分辨率、完整HUD标记，逐张记录SHA-256；视觉检查联系表与航行、收线、抄网大图 |

复现：`Tools/Run-V016Validation.ps1 -Capture`。该入口沿用既有验证与Player工具，提交的船预制体可直接使用。截图与构建散列见 [capture-evidence.json](Images/V016/capture-evidence.json) 和 [build-artifacts.json](Images/V016/build-artifacts.json)。

![原创船模型，3863三角面](Images/V016/SoloSkiff.png)

![航行时不显示鱼竿](Images/V016/Navigation.jpg)

![收线：左手握旋钮，右手持竿](Images/V016/Reeling.jpg)

![抄鱼：左手握网，右手继续持竿](Images/V016/Landing.jpg)

[宽屏全部场景](Images/V016/all-scenes-wide.jpg) · [方形画幅全部场景](Images/V016/all-scenes-square.jpg) · [第三人称船体与鱼箱尺寸](Images/V016/SkiffInGame.jpg) · [摇柄一圈的接触检视](Images/V016/crank-contact.jpg)

## 验证边界与取舍

- Blender船模、Unity导入后的米制尺寸/舷外机方向、运行时碰撞和可见网格分别核验。摇柄的导入镜像修正在专用导入器里，导入版本递增保证旧缓存重新导入。
- 单人坐姿采用稳定船体：保留升沉和航向，原生刚体约束锁定横倾/纵倾。没有将波浪倾斜做成玩法。
- 自动姿态/接触测试和截图不等同于长期键鼠手感验证。PICO控制器检查使用模拟输入，未连接头显，未认证真机握点、摇轮阈值、立体显示或帧率。
- 在本地既有工程执行，原有地图、店员和URP/XR配置改动保留，未混入第16版提交。未在全新Unity缓存的检出中重复整套导入和构建。
- 本版提交PR审查，不自动合并。
