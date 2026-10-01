# v0.6.0 审查候选：重燃旧港信标

更新：2026-09-29。分支：`feature/fishing-expedition-rules`。

## 状态与范围

核心工程实现、全部本轮自动化回归、Windows 构建及独立程序冒烟已完成。**不是已经完成真人首次体验或 PICO 真机验收的发布版。**
对照设计，最后的真人键鼠全路线试玩仍未完成；PR 应保持 Draft，不自动合并。

- 主场景仍是 `Assets/scenes/Fishing/fishing_Island.unity`，不重新序列化旧场景或预制体，不改 Unity/URP/包版本。
- 西湾记录、南岸三种标记箱、旧港灯塔校准、北岛珊瑚鱼捕获、港口独立提交构成任务闭环。
- 新增原创程序化八面收分塔、加强环、检修舱、梯子、灯头护框、透镜与铜色灯帽；没有导入来源不明的外部资产。
- 新第四渔区中心 `(42, 0, 102)`，半径 22m；复用珊瑚鱼，修复前锁定。原三个渔区和传说鱼条件保持不变。
- 组件和维修记录可先取得再接任务；修复必须具备全部前置事实；一次 E 只触发一个动作，随后仍可完成旧调查。
- 旋转采用 A/D 或左右箭头，容差 8°，E 主动确认；Esc、暂停、日志、失焦、救援或上船会释放输入占用。
- 新捕获成功入箱后才记证据，旧库存、错误鱼种/渔区、未修复和入箱失败都不计；证据不会因出售/放生丢失。
- “提交捕获记录”是交易台独立按钮，不消耗鱼、无额外金币、不可重复领奖；永久点亮信标及日志纪念为奖励。

## 本轮审查修复

1. 更新运行版本、构建目录及桌面验收脚本默认值为 0.6.0，避免把旧构建误当作新版本验证。
2. 北岛任务达成使用独立任务通知，不再被鱼获卡片的重复提示抑制逻辑吞掉。
3. 精确指引星标改为地图最上层偏移标记，避免被渔区/岸点盖住；日志任务名与当前目标分行显示。
4. 新增 v5 专用存档入口，旧档只读迁移；增加磁盘 JSON 优先级、备份恢复及旧文件字节不变检查。
5. 验收工具切换预设/自由存档后刷新组件箱外观与临时旋转状态，避免沿用另一份进度的显示。
6. 在隔离任务验收中加入北岛海侧停船点到新渔场的碰撞通道检查及真实发动机推进，补齐新渔点不是只能传送抵达的证据。

## 模型截图

以下为本轮 `FishingExpeditionCapture` 输出的定机位渲染截图（不是完整 HUD 或真人视点验收）。同一任务验收同时检查修复前/后透镜材质状态。

![修复后的旧港信标](Images/v06-beacon-restored.jpg)

![带双环符号的维修组件箱](Images/v06-component-crate.jpg)

## 存档升级与回退

- 正常游戏写入 `Application.persistentDataPath/boatattack-fishing-v5.json`。
- 当新文件及其 `.bak` 都不存在时，读取旧 `boatattack-fishing-v1.json`（历史 schema v1–v4）及其备份，后续只保存到新路径。
- 旧文件不修改；旧版可继续使用旧档，但两套进度不会双向同步。不要把 v5 内容手动拷入旧文件。
- 若新文件/备份已经存在但损坏，不静默回滚到旧档覆盖新任务。显示存档警告，沿用原备份恢复策略。
- 自动化与手动验收工具始终使用临时目录下的隔离档，不读取或改写正式玩家档。

## 验证记录

以下只计本轮实际执行，失败日志不作为通过证据；所有 Unity 验证串行执行。

| 项目 | 结果 | 证据 / 边界 |
|---|---|---|
| 纯捕鱼规则 | 814 通过 | `Logs/v06-audit-rules-3b9c5548efc14abdaf18938a34a430af.log` |
| 纯输入/渲染策略 | 62 通过 | `Logs/v06-audit-input-1b9d63fcd7ae42749a26f68b060ae26f.log` |
| v0.6 任务场景 + 模型截图 | 通过 | `Logs/v06-review-expedition.txt`；真实步行、校准、短程北岛航行、真实入箱事件与磁盘 JSON；不等于真人输入 |
| 原海岸 / 原捕鱼与 HUD / 项目结构 | 通过 | `Logs/v06-review-FishingShore.txt`、`v06-review-FishingSmoke.txt`、`v06-review-Structure.txt` |
| 组件回归 | 14 通过，0 失败 | `Logs/v06-review-Regression.txt` |
| 原竞速 Play Mode | 通过 | `Logs/v06-review-PlayMode.txt`；菜单→比赛→菜单→第二场、四船与引擎重启 |
| Windows 非 Development 构建 | 通过 | `Logs/v06-review-FishingBuild.txt`；`Builds/FishingPrototype-v0.6.0/FishingPrototype.exe` |
| 独立 Windows 程序冒烟 | 通过 | `Logs/v06-review-WindowsSmoke.txt`；使用新构建和隔离验收档，不证明物理键鼠输入 |
| 真人键鼠全航线 | 未执行 | 转为正式发布前的人工门槛，不能用自动化代替 |
| PICO 真机 | 未执行 | 单独的 VR 验收范围，本 PR 不宣称完成 |
| `.meta` / diff / 资产 | 通过 | 六个新脚本的 GUID 唯一；`git diff --check` 无错误；原 LFS 场景和构建资产可用；两张审查图走 LFS |

复现命令（从仓库根目录，先关闭交互 Unity）：

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingExpedition
./Tools/Run-UnityValidation.ps1 -Suite FishingExpeditionCapture
./Tools/Run-UnityValidation.ps1 -Suite FishingShore
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite Structure
./Tools/Run-UnityValidation.ps1 -Suite Regression
./Tools/Run-UnityValidation.ps1 -Suite PlayMode
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke -Session v060-review
```

日志汇总中的首行指向该次唯一 Unity 日志；原始日志、自动化存档与构建不提交 Git。GitHub CI 结果以 PR 检查为准，不由本地结果推断。

环境启动曾因沙箱许可证客户端连接失败中止；正常用户环境恢复后重新执行。北岛几何测试初版从栈桥中心起算，正确检测出碰撞；已改为与实际登岸一致的海侧停船位置，未删除或忽略栈桥碰撞。

## 首次人工试玩清单（未完成）

```powershell
./Tools/Run-FishingAcceptance.ps1 -Mode Manual -Session v060-first-play
```

默认自由试玩使用独立档；请保留完整 `Builds/FishingPrototype-v0.6.0` 目录，不只复制 exe。

1. 港口 **Tab** 打开交易台，按独立任务按钮接受委托。
2. **N** 或地图选 A 西湾，低速海侧 **G** 下船；到水样台附近 **E** 读维修记录。
3. 前往 D 南岸，辨认鱼纹、双环、条纹箱；**E** 回收双环维修箱组件。
4. 前往 C 旧港，沿屋外栈道绕墙角找到信标，**E** 进入校准；**A/D** 调整，**E** 确认。
5. 驶向 B 北岛以北的 **渔区 4**，减速 **E** 停船，完成一条珊瑚鱼入箱。
6. 正常驾船返港，交易台按 **提交捕获记录**；确认没有重复金币、没有扣鱼，重启仍有亮灯和日志。
7. **J → 海岸调查** 查看区域/地标线索；确实迷路再开启精确指引。记录不开精确指引是否可推进、总用时、各卡点及反复操作。
8. 检查全部航段礁石绕行、标记可见性、键鼠模态互斥、首/第三人称画面、最差帧率。目标 10–15 分钟尚未实测，不作为达标结论。

合并前：人工清单有记录、审查问题处理、CI 通过后再转为 Ready for review / 合并。PICO 的移动、手柄交互、真实头部碰撞和性能是单独验收范围。
