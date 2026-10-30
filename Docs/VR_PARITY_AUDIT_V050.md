# Windows → PICO 内容迁移审计（0.50，进行中）

基准为当前工程Windows路径，而不是旧版本截图。审计分开记录源代码映射、模拟输入与画面、设备安装、硬件体验；“有入口”不等于“已经实玩通过”。不以系统72FPS冒充游戏性能。所有测试使用独立存档，保留已有工作区。

## 已确认并处理的缺口

1. 背包缺少整组移动、合并/交换、拆分、快速转移和二次确认销毁。增加物品操作与目标格页面，复用共享事务和物品/数量校验，仍保护任务物品与自然手册。
2. 鱼图鉴与自然观察选中详情被外层Refresh即时覆盖。将完整描述纳入页面内容并保存会话选择；增加已发现筛选、普通/珍珠发现、体重区间与最大纪录。
3. VR缺少完整传说线索及调查航点入口。增加航海手札目录、线索页、世界与秘境阅读/蓝图/导航。
4. 沿岸调查缺少8项任务明细及精确信标指引开关。增加分项清单、既有导航与ToggleGuide入口。
5. VR战斗提示没有持续生命信息，现接入同源Combat.Status。
6. 常驻指引仅为纯文字纸片，现加入Windows共用海岛插画、标题/正文/操作页脚；教学卡加入实际按键图标，进度保留在页头。
7. 发现隔离XR演示仍写入“已教学”偏好，改为只在正式档持久化。

## 功能覆盖矩阵

“源映射”表示已核对实际调用或共享模型，尚不能代表佩戴或完整流程通过。本轮新增测试仍在运行，最终结果另记。

| 内容 | Windows入口/模型 | PICO入口/模型 | 本轮判断与验证状态 |
|---|---|---|---|
| 继续/新建/读写航程 | HUD.Menu/Saves、SlotStore | Notebook.Saves、同一SlotStore | 源映射；VR入口在设置页，首屏布局不同 |
| 三槽手动/自动档与覆盖确认 | Prototype.Saves | Notebook.Saves调用相同事务 | 既有XRInteraction有专项；本轮回归中 |
| 暂停、救援、校准 | HUD.Pages/Menu | Notebook14/16/22 | 源映射；原生模拟已进入设置 |
| 头部转向、船体舒适 | ViewRig/物理船 | XRSeat/Comfort | Mock原生XR已启动；既有头部专项继续回归 |
| 驾驶/倒车/刹车/加速 | DesktopInputSource | XRInputSource/CommonUsages | 源映射；本轮原生驾驶待玩 |
| 航海图/航点/路线 | HUD.Chart/TerrainMap、WaterRoute | Notebook12、PalmMap、相同导航 | 源映射，手册原生点击已验证 |
| 浅湾/水道/外海/北岛解锁 | FishingGrounds/Career | 同一Cast/Interact | 共享领域；本轮原生捕鱼待玩 |
| 抛竿/咬钩/搏鱼 | MouseAngling/CatchSession | Hands/Notebook A/Trigger、同一Session | 源映射；FishingCatch回归待运行 |
| 实物抄网/检视/收纳 | CatchRig | 同一CatchRig，XR手部几何 | 既有74项回归，本轮重跑待定 |
| 鱼获存放/测量/放生/防重复 | Career.PendingCatch | DockConsole/实体动作、同一Career | 源映射；保留确认与所有权 |
| 鱼箱与订单所需鱼 | HUD订单/交易页 | Notebook13/7、DockConsole | 源映射；共享选择与结算 |
| 船舶和钓具升级/专精 | HUD.Shop/Fleet | Notebook4、同一Fleet/Career | 源映射 |
| 商店/探索补给/NPC售货 | HUD.Shop、居民柜台 | Notebook4/5/8、同一交易 | 源映射，实体柜台体验待原生模拟 |
| 背包装备与检视 | HUD.Inventory/Inspection | Notebook1/27、EquipSlot | 已补完整物品操作；新增XR断言中 |
| 移动/合并/交换/拆分/快速转移 | Backpack.Move/QuickMove | Notebook27/28、共享MoveFieldItem | 原遗漏；已补，守恒与边界测试中 |
| 确认销毁/保护任务物品 | Backpack.Discard | Notebook27、共享DiscardFieldItem | 原遗漏；新旧数量报价/关闭UI守卫测试中 |
| 住宅储物箱与实物掀盖 | HUD.Storage、Home.Storage | Notebook18/19、实物握盖 | 同一TransferStorage；两端均整组转移，不误报“缺少拆分” |
| 设施放置/制作/熔炼/领取/拆回 | HUD.Crafting、WorkshopWorld | Notebook2/7、XR工具、相同事务 | 源映射 |
| 筛砂/绕绳/锯木/锻造/枪械件 | HUD.Handcraft、HandGames | XR实体工具、A切换锻造产物 | 源映射；SwitchForgeOutput确有XR调用 |
| 挖矿/斧铲耙/手工接触 | FieldWorld/WorldAction | XRNotebook.UseTrackedTool/SampleTool | 源映射，物理专项仍需真实输入复测 |
| 水井/转柄/提壶/浇灌 | FarmWorld | XRGrab/实体接触、相同Farm | 源映射 |
| 菜畦/种收/果园 | Farm/Home.Orchard | 同一实体世界与RequestWorldInteraction | 源映射 |
| 睡眠/沙发/电台/住宅设备 | Home.Leisure/睡眠事务 | A/B/Grip等XR分支 | 源映射；Home.Leisure有明确XR输入 |
| 水族箱/活捉/饲养/繁殖/诱捕 | HUD.HomeEcology | Notebook21 | 两端均HomeEcology.Choices(mode)，Jev截断样本partial不作为遗漏证据 |
| 海洋/陆地动物观察 | HUD.Journal/Nature | Notebook15、实体观察 | 已补完整提示/照片与持久选择 |
| 鱼获图鉴/筛选/记录 | HUD.Journal.Catalog | Notebook10/31 | 原部分迁移；已补，同源数据 |
| 传说线索/回放记录/调查坐标 | HUD.Journal.Legend | Notebook29 | 原无完整入口；已补 |
| 沿岸8项调查/奖金/指引开关 | HUD.Islands.Shore | Notebook33 | 原部分迁移；已补 |
| 日历/昼夜/自动时钟/天气 | HUD.Calendar/Menu | Notebook3/14 | 同一Calendar/Weather |
| 主线村长/海图/四境/宝藏 | VoyageWorld/Lore | Notebook9/世界实体 | 共享领域与交互入口 |
| 来信/漂流瓶/故事阅读 | HUD.Letter/Letters | Notebook24、Letter世界画布 | 源映射；新增世界故事入口复用Show |
| 风/林/潮/星机关与观察 | RealmWorld/Trial | 真实曲柄/镜/扳机等XR分支 | 源映射，不能仅凭代码称完整实玩 |
| 守望者/店员/NPC对话 | Residents、HUD.Story | Notebook8、相同Residents | 源映射；插画/正文排版需继续视觉检查 |
| 猎矛/猎枪/散布/战利品 | RifleHunting/动物事务 | XR枪口/手部、同一命中与收据 | 源映射 |
| 礁蟹守卫/夜怪/生命/危险圈 | CombatWorld | 同一世界血条/危险圈、Guidance | 已补持续生命信息 |
| 游泳/攀爬/体力 | Shore/ClimbRoute | XR双手横档/摇杆/同一碰撞体 | 源映射，已有XRPhysical专项 |
| 相机/相册/照片图鉴 | Life.Camera、HUD.Album | XR实体相机、Notebook25/26 | 共享Album与PhotoCodex全部条目；Jev部分样本不足，未确认功能缺失 |
| 节日/海上事件/生态/值夜 | Life.Festival/SeaEvents/Keeper | 相同世界实体与交互入口 | 源映射，逐场景模拟待做 |
| 五路声音混音/音乐/环境/语音 | HUD.Menu、WorldAudio | Notebook20、相同混音与事件 | 源映射 |
| 地名/区域声光反馈 | Regions、桌面HUD | Region inscription XR世界画布 | 确有XR替代，非缺失 |
| 过场/序章/重看/跳过 | Prologue/TitleVoyage | 同一Prologue的XR机位/Canvas与A/B | 源映射；启动首屏并非桌面标题屏原样 |
| 场景模型/LOD/材质 | 同一Assets与世界 | MobileQuality/相同资源，保留低模LOD | 视觉预算不同；未把低模策略当缺少内容 |
| 指引/教学/可读性 | 桌面图标/插画/分区 | XRHints/Notebook反馈与教学 | 新布局与按键图标；严格文字溢出回归中 |
| 分辨率/全屏/鼠标/FOV | 桌面设置 | HMD追踪/眼高校准/VR舒适设置 | 平台特有，不要求复制鼠标或窗口控制 |
| 真机帧率/镜片清晰度/佩戴舒适 | PC测量不能代替 | PICO硬件 | 尚受系统场地确认阻塞；不宣称通过 |

## 证据与独立复核

- 原生输入：`Logs/XRSimulator/state.json`记录xr=true、focused=true、automated=false、isolated=true；B键读取为true打开主页，真实控制器射线与扳机进入设置和手柄教学。
- 初始画面：`Logs/XRSimulator/before-notebook.png`、`before-settings.png`、`before-guide.png`。
- Jev使用jev-latest，实际model=jev-1.14.0，输入16160/output470 tokens。10个狭义“操作覆盖”问题合批。结果与原文摘录在`parity-evidence.json`及`jev-parity.json`。confidence是分布集中程度，不是准确率。存档/生态/相册等低置信度或摘录不完整结论已回原文核查。
- 第一轮新布局溢出失败已记录；第二轮新增库存断言把分半方向错误假定为向下取整，现改为守恒且两组数量差1，保留正式领域既有分半规则。
- 新增XR-PARITY断言与V025全套回归进行中，最终安装与实玩结果待补。

当前自动验证：V025最新730 CHECK PASS（c0a63697cbe34c7395efa3a753d403b5，含36条XR-PARITY）；FishingCatch74 CHECK PASS（7031889e11944bd5b3b5c61fd1432e9c）；纯领域4728、输入70通过。Field旧场景验收的8拾取预期已按完整16稳定ID与实际地面加强，重跑中。

Field最终通过：b1d38fd71c8d4c66940f640a3720b0eb，实际16个拾取/宝箱稳定ID、模型及地面全部检查通过；其余键盘移动、背包、动物、JSON迁移和真实倒车也通过。
