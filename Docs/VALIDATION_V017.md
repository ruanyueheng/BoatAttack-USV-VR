# v0.17.0 验收记录

日期：2026-10-11（北京时间）。在现有Unity2022.3.62f1工程中执行，测试使用隔离存档，没有操作普通玩家进度。

| 检查 | 实际结果 |
|---|---|
| FishingTests | **1,861通过**；包含手动时间、保存恢复、跨日不倒退、无效时刻与日历上限、31场景验收目录及既有捕鱼规则 |
| InputTests | **62通过**；输入、失焦恢复和渲染策略 |
| Blender生成 | **通过**；船壳流形检查、内壁朝向检查；可见船模4,906三角面、1个网格；凸碰撞船壳62三角面 |
| Unity第17版专项 | **通过**；港口4秒起步速度约3.58m/s；左右各15秒满舵，最大倾斜0°；开阔水面最高采样速度约4.48m/s，最高航向角速度1rad/s；动力入水有效采样1,438/1,492（96.38%）；前进/倒航限速、松油门减速、实体障碍阻挡、接触后倒船脱困、完全离水后停动力 |
| Unity昼夜专项 | **通过**；旧JSON存档仍默认自动时间；固定时间在实际天气Update中保持不变；白天关灯、夜晚启用船载灯；同机位开灯/关灯实景对比 |
| Unity回归 | **V016、LivingCoast、Regression、FishingSmoke、Structure全部通过**；包含握柄一圈、抄网/入箱、船舶碰撞扫掠、日历/天气、旧赛艇引擎生命周期、商店/委托、存档与备份、地图/界面边界和源场景结构 |
| Windows构建 | **成功**：`Builds/FishingPrototype-v0.17.0/FishingPrototype.exe` |
| Windows Player | **31场景通过**；隔离存档、生产规则状态检查，新增第三人称船上夜灯场景 |
| 完整HUD截图 | **1600×900与1280×1024各31张**；逐张验证版本、尺寸和完整HUD标记，保存SHA-256；目视检查两张完整联系表，以及昼夜控制和夜灯场景的大图 |

复现入口：`Tools/Run-V017Validation.ps1 -Capture`。完整截图记录见[capture-evidence.json](Images/V017/capture-evidence.json)，本地构建散列见[build-artifacts.json](Images/V017/build-artifacts.json)。原始62张PNG保留在本地`Logs/desktop-acceptance-*`下；提交联系表与本版重点图片。

## 本版重点画面

![厚壁船壳与甲板灯](Images/V017/SkiffStudio.png)
![第一人称夜间局部照明](Images/V017/NightDeck.png)
![同机位临时关灯对照](Images/V017/NightWithoutLamp.png)
![日历中的手动昼夜操作](Images/V017/Calendar-wide.png)
![Player第三人称夜灯与船体](Images/V017/NightBoat-wide.png)

[宽屏31场景](Images/V017/all-scenes-wide.jpg) · [方形画幅31场景](Images/V017/all-scenes-square.jpg) · [方形画幅日历](Images/V017/Calendar-square.png)

## 验证边界

- 旧版本翻船已实际复现：持续转向达到47.43°，测试末速度约0.0064m/s。旧复现使用帧级直接调用，新专项改用实际FixedUpdate驱动；不把这两个记录当作相同输入条件下的性能对比。
- 开阔水面动力测试先确认40m无障碍区域；礁石/浅水是实体障碍，不把触礁后的动力下降误判为波浪出水。另用40m宽实体障碍检验实际接触阻挡和倒船退出，碰撞时允许船体转为侧身。
- 昼夜选择推进真实日历，可能跨日；固定时间偏好用默认false的`timeFixed`字段保存，旧Unity JSON缺少字段时继续自动流逝。
- 目视检查联系表和重点大图不等同于逐像素的全部画面审查。没有进行长时间人工键鼠手感、PICO头显实体输入、立体显示或真机性能验收。
- 使用本地现有工程与缓存，未在全新Unity缓存中重做全部导入。原有店员素材、地图和XR/导入设置的本地改动没有加入本版PR；ProjectSettings只提交版本号变化。
- 本次提交PR审查，不自动合并。
