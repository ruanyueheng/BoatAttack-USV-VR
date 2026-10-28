# 第44版计划（分支 feature/v044-polish）

用户需求清单，逐项勾选，完成后发 PR。

## 需求
1. [ ] 模型审查：找出丑的模型，用 Blender（及 AI 3D 生成站点）重做/美化。审查联系表：`Logs/V044/sheet*.png`，编号表 `Logs/V044/list.txt`，脚本 `Tools/Art/audit_model_thumbs.py`。
2. [x] 第一人称低头看到身体内部 —— 已改（FishingCharacter.FirstPerson.cs、FishingViewRig.cs LookDownOffset），四个第一人称专项通过。遗留：靠墙低头前移 16cm 可能穿墙；需确认。
3. [ ] UI 美化：学习《小王子：星辰之旅》(littleprince.dev) 绘本风——深夜蓝+奶油白衬线字+金色星芒+水彩纸纹。**只改 UI 和重做模型，3D 渲染不做全局后处理**，且不偏离本游戏海岸/渔村主题。参考图：`Logs/V044/ref/`。
4. [ ] 四岛秘境玩法重做：更复杂有趣，配更好的美术建模。现状分析见下。
5. [ ] 水壶拿取：只有跳上桌子与水壶重合才能拿起。改为正常站在桌边即可拿起。
6. [x] 研究网上用 Opus 做游戏的全流程/提示词（含 https://x.com/Saccc_c/article/2110694725640474759 小王子提示词），结合本游戏自问自答，产出 `Docs/PROMPTING_V044.md`，并把结论反哺到本计划。
7. [ ] 试用 AI 3D 站点：tripo3d、3d.hunyuan.tencent.com、meowa.ai（用户 Edge 已登录）。
8. [ ] 全面检查：建模/UI 美观；角色交互符合现实，无肢体扭曲、无穿模；行走不会莫名卡住。
9. [ ] 性能：最终运行在 PICO 4 Ultra（Android/Vulkan，72–90Hz）。新模型控制面数、贴图尺寸，材质用 URP Lit/Simple Lit，避免昂贵透明与后处理。
10. [ ] 遗留：LivingCoast 专项海龟尺寸检查失败（Metre-scaled marine model SeaTurtle），工作区里 SeaTurtleThumbnail.png/.meta 被改过。
11. [ ] 清理工作区约 750 个 Unity 重新序列化噪音改动（meta 行尾空格等），只提交有意义的改动；确认 URP 管线资产剔除设置的改动是否需要。
12. [ ] 特效：多加好看的特效（粒子等）；传送门光幕重做（FishingPortalVisual、FishingPortal.shader）；排查其他难看特效（FishingEffects.cs、Spray/Smoke/TideFilm/LighthouseBeam shader 等）。PICO 性能：粒子数、过绘制受控。
13. [ ] 剧情重写 + 全部开场/过场/结局用游戏内实时演出重做（代理 story，文档 Docs/STORY_V044.md）。VR 下不强制移动视角。
14. [ ] 交互音效设计与补全（代理 audio），可用 meowa.ai 生成。
15. [ ] 自我试玩循环：功能交付后，试玩代理用自动驾驶真正玩全流程，截图/记录卡住、穿模、扭曲、难看、无聊之处，自己修或通知对应代理，循环到挑不出问题。
16. [ ] （补充）VR 舒适性：平滑/瞬移移动、转向方式、移动时视野收窄选项、过场不动头；PICO 4 Ultra 实机打包与帧率检测。
17. [ ] （补充）新手引导与前 10 分钟体验：目标清晰、不迷路；设置项（音量/字幕大小/舒适选项）完整。
18. [ ] 四岛 NPC：更好的人物模型；不再整体常时朝向玩家（头部 look-at+交谈才转身）；驻足活动与 idle 变体；对话改进；每个角色中文配音（代理 npc，文档 Docs/NPC_V044.md，试听 Logs/V044/npc/voices.html）。
21. [ ] 海上随机事件第一批：鱼群沸腾、漂流瓶、雾中灯语、鲸与海豚、夜光海、海上集市船（光遇季节先祖式限时商船）（代理 sea-events，文档 Docs/SEA_EVENTS_V044.md）；下一版候选：风暴救援、潜水捞宝、帆船竞速、海雾海图探索。
22. [ ] 航行途中空中悬浮贴图：复现、定因、去除，并做成自动验收（代理 voyage-audit）。
23. [ ] 模型不互相穿插：常去区域（渔屋内外、码头、菜园水井、登岛点营地、秘境入口、商店、居民处）自动检测穿插并修复（代理 overlap-audit，报告 Logs/V044/overlap/report.md）；所有美术代理交付前自查。
24. [ ] 用户选定创新玩法：守灯值夜 + 潮汐日历（lighthouse-tide）、回信系统（sea-events）、相机与图鉴 + 掌心地图 + 季节节日（life-systems）、居民的一天（npc）、天气改变玩法（sky-weather）。
25. [ ] 水井打水动作更真实（can-fix）。
26. [ ] 天空：昼夜更美、日月按时间运动、月相、星空旋转与闪烁、流星、云层（sky-weather）。
27. [ ] 下雨时室内不应有雨滴（sky-weather）。
28. [ ] 性能：桌面流畅 + PICO 4 Ultra 稳定 72/90Hz（代理 perf）：基线、移动端质量档、ASTC、单通道立体兼容、Android 构建、性能验收。
29. [ ] VR 不晕：移动/转向/视野收窄/船体摇晃/阵风/过场全部舒适化，设置里有舒适选项（代理 vr-xr）。
30. [ ] PICO 官方手柄完整映射：所有桌面功能在 VR 均可操作，映射表 Docs/VR_CONTROLS_V044.md（vr-xr）。
31. [ ] 手柄按键指引明确：情境提示条、首次教学、手柄模型按键标签、手账"操作说明"页（vr-xr + ui-restyle）。
32. [ ] VR 第一人称 UI 美观：VR 手账/世界空间面板换新风格、平滑跟随、字高足够（ui-restyle + vr-xr）。
33. [ ] 混元 3D 每日 20 积分充分用于显眼环境资产（scene-art，必要时 model-polish）。
34. [ ] 自我试玩改进循环（功能交付后由试玩代理执行，多轮）。
35. [ ] 收尾：整理 虚拟现实作业 文件夹；写 Docs/CHANGELOG_V044.md 记录全部改动；给用户文件清单；推送并开 PR（已授权）。
36. [ ] 用户 PICO 实机反馈（旧版 v0.43，必须在新版中逐条消除）：①陆地上转头视角不跟随（头显旋转未作用到相机）；②VR 仍显示键盘键（如 Q），抛竿不能甩手柄；③整体是"键盘映射到手柄"而非为 VR 设计，高频交互改手势；④坐船上下起伏大。负责：vr-xr（主）、can-fix（钓鱼甩竿等手势）。修后打 Android 包并写实机验收清单。
20. [ ] 提交并发 GitHub PR。

## 约束
- Unity 工程同一时刻只能被一个 Unity 实例打开：跑 `Tools/Run-UnityValidation.ps1` 前检查 `Temp/UnityLockfile`，被占用就等待重试，不要强杀别人的进程。
- 浏览器自动化：Edge 调试端口 127.0.0.1:9222（用户资料副本 E:dgeCDP，收尾时迁到 虚拟现实作业/Tools/EdgeProfile 并删除原处）；Playwright 工具在 虚拟现实作业/Tools/BrowserAutomation（旧 E:ptools 收尾时删除）。
- Blender：`C:/Program Files/Blender Foundation/Blender 5.2/blender.exe --background --factory-startup --python-exit-code 1 --python <script> -- <args>`；输出路径用绝对 Windows 路径。
- 现有美术脚本在 `Tools/Art/`，按其惯例新增 `build_v044_*.py`。

## 来自 PROMPTING_V044 的落地要求
- 风格统一以 `Docs/UI_STYLE_V044.md`（UI 代理产出）为准，模型/特效同样引用；小王子元素海岸化：星芒→灯塔光/渔火，纸纹→旧航海图，不直接使用小王子角色、玫瑰、星球等元素。
- 第 8 项行走卡顿：用脚本让角色自动走遍各岛主要路线，统计卡住点（速度≈0 且有移动输入超过 1 秒）并逐个修。
- 每项用固定机位截前后对比图存 `Logs/V044/`；PICO 参考预算：帧时 ≤13.9ms、可见三角面 ≤30 万、DC ≤150、ASTC。
19. [ ] 收尾：删除 E:\cdptools，迁移 E:\EdgeCDP；整理 虚拟现实作业 根目录（按 目录说明.md 归档）。 另需清理 E:/tmp（1.1G 代理临时文件）、E:/tmp_npc、E:/tmp_v044_font（E:ptools 已删）。

## 协作规则（team-lead）
- 改完任何 C# 后、离开文件前，必须运行 `python -I Tools/Check-FastCompile.py`（约 4 秒，用 Unity 自带 Roslyn 编译 Assembly-CSharp 与 Editor，不占 Unity 锁），PASS 才算完成；FAIL 立即修。
- 别人负责的文件不要整文件读写覆盖；需要改时先 SendMessage 给所有者。FishingLore.cs / FishingModel.cs 所有者：realm-redesign。
- 运行时不要修改不可读（Read/Write 关闭）的导入网格。

## 用户授权（2026-10-28）
- 全部完成并自测通过后：直接推送 feature/v044-polish 并开 PR（不合并 main），PR 说明含改动清单、截图、测试结果、已知风险。
- 用户离开期间音效由 team-lead/audio 按调研与用户既往偏好（贴合情景、清脆、绘本质感、不闷）自行选定；保留全部旧版与候选，审查表留给用户回来复核。NPC 配音同理。
- PICO 实机测试留到最后，用户回来再连。
- E:\EdgeCDP 收尾时移到 虚拟现实作业/Tools/EdgeProfile 保留（含登录态，需 .gitignore 排除，绝不提交）。
- 跑 Unity 验收改用 `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-Validation-Lanes.ps1 -Suite <套件名>`（参数同 Run-UnityValidation.ps1）：自动挑空闲通道（主工程 + ../.lanes/lane1、lane2 镜像），跑前同步资源、跑完把 Logs 拷回主工程；写资源的套件（Install/Generate/Build/SceneArt/Setup）自动走主工程。最多 3 个并行。
- 禁止向 E:\ 根目录写任何文件（用户要求）；临时文件用系统临时目录或 BoatAttack/Logs/V044/<模块>/，备份放 ArtSource/V044/Backup/。

## 遗留到下一版（用户 2026-10-28 决定）
- 穿模检测报告 Logs/V044/overlap/before/report.md：205 处（商店 166 多为建筑咬合应白名单）；需修：港口浮动鱼市码头板插入灯塔礁石 0.79m、商店橱窗箱子/望远镜互穿 0.25m、星岛天文台吞掉隐藏宝藏 0.53m；灯塔回廊/谢礼包/居民附近检测覆盖不全。
