# v0.7.1 — 登岸鼠标转向修复与审阅说明

## 已复现的根因

`FishingShore.TryDisembark()` 会调用 `CloseBoatPanels()`，后者把 `ChartExpanded` 设为 true，显示岸上导航。v0.7.0 的 `ApplyShoreView()` 又把 `ChartExpanded` 当成禁止捕获鼠标的模态条件，因此默认登岸状态下鼠标位移始终被忽略。

上一轮自动检查直接调用 `ApplyShoreLookDelta()`，只证明角度合成正确，绕过了上述 UI/输入条件。这是上一轮测试的真实遗漏，不能用之前的绿色结果证明实际转头可用。

## 修复

- 航海图是常驻导航 HUD，展开/收起都不阻止岸上鼠标转头，不移除地图作为规避。
- 按住左 Alt 或右 Alt 才把鼠标交给地图与按钮；详情、日志、暂停、失焦与信标拨盘仍阻止转头。
- 游标已经锁定时不再每帧重新设置锁定；失焦/解锁后的第一帧丢弃旧 delta，下一帧恢复转向。
- 上船释放岸上鼠标，保留船上原有右键环顾；再次下船也能直接转头。
- 版本升为 0.7.1，使用单独构建目录，不覆盖 0.7.0。

## 回归方法与证据边界

新增 `FishingLookRegression.CheckLanding`，在真实 `TryDisembark()` 之后，保留自动展开的地图，创建 Unity Input System 的虚拟 Mouse/Keyboard，通过 `QueueStateEvent → InputSystem.Update → FishingViewRig.ApplyView` 驱动相机。测试不调用角度 helper 来伪造输入通过。

Unity 批处理不能锁定实际 OS 游标：请求 Locked 后返回 None。为避免把批处理限制误判成游戏故障，系统游标通过 `IFishingCursor` 做边界隔离：正式游戏使用 `FishingSystemCursor` 转发到 Unity Cursor API，批处理测试使用记录请求的模拟实现；应用焦点/自动化状态也在明确的输入边界传入。场景、地图状态、Input System 鼠标/键盘事件、相机姿态和上下船逻辑使用生产代码。

因此这些检查验证的是实际游戏输入处理链路及其游标请求，不等同于真实 Windows 鼠标捕获或 PICO 真机验收。

### 先失败，再通过

- 恢复旧 ChartExpanded 阻断条件后，新测试失败：地图已展开、鼠标 delta=(150,-60)、右键未按，相机旋转为 0。日志：`Logs/usv-validation-1a457c3a6bd94fb08414fc6dddf52eb9.log`。
- 移除错误阻断后，新测试与全部旧信标任务检查通过。日志：`Logs/usv-validation-ac967ade9b2748f6b4daf0a02865bdc9.log`。
- 额外覆盖：图展开/收起、零输入不漂移、无需每帧重新锁定、OS 锁定丢失、左右 Alt、暂停、焦点、详情/日志、恢复首帧防跳、自动化不捕获用户游标、上下俯仰限制、第三人称、上船与二次下船。
- 本轮纯捕鱼/手部规则：833 项通过；独立输入/渲染策略：62 项通过。

本地日志不提交 GitHub；上面的文件名仅供同一工作区复查。

## PR 范围

此前 v0.7.0 的鱼竿/双手改动还没有提交 PR，主分支仍是 v0.6.1。本 PR 会一并包含其源模型、运行时代码、测试和资料；本次鼠标修复的重点文件是：

- Assets/Scripts/Fishing/FishingViewRig.cs
- Assets/Scripts/Fishing/FishingSystemCursor.cs
- Assets/Scripts/Editor/FishingLookRegression.cs
- Assets/Scripts/Editor/FishingExpeditionValidation.cs

参考 [v0.7 模型与双手说明](FISHING_V07_HANDS.md)。没有混入 PR #12 的海岸/地形改动，没有上传 Library、Logs、账号信息、Blender 自动备份或 Windows 构建产物。

## 请审阅者实际复测

1. 运行 `Builds/FishingPrototype-v0.7.1/FishingPrototype.exe`，不要误开旧 v0.7.0。
2. 驶近任一橙旗，低速 G 登岸，保留默认展开的航海图。
3. 不按右键，移动鼠标，应连续左右/上下转头；鼠标不应撞到窗口边缘停止。
4. 分别按住左右 Alt：相机不转，鼠标可操作地图；松开后应恢复且不突然甩视角。
5. 依次暂停/恢复、打开关闭 J/H、Alt-Tab 返回，确认游标正常释放和恢复。
6. C 切换第三人称转动；返回登岸点 G 上船，再 G 下船复测。

完整路线、真实 OS 游标行为、持续性能和 PICO 控制器仍需人工验收，PR 不自动合并。

```powershell
./Tools/Run-UnityValidation.ps1 -Suite FishingExpeditionCapture
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingShore
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
./Tools/Run-FishingAcceptance.ps1 -Mode Smoke -Player Builds/FishingPrototype-v0.7.1/FishingPrototype.exe -Session v071-look
```

## 本轮其他回归

- Unity FishingSmoke：通过。日志 `Logs/usv-validation-ce8f1cd2cd604b3999bb214898a28e1e.log`。
- Unity FishingShore：四处登岸点的步行、调查、上船和救援通过。日志 `Logs/usv-validation-6df48eeaaafb4ed1a1bba847a8fc7b09.log`。
- 新增鼠标输入链路回归：39 项检查通过（独立于 833 项规则测试）。
- v0.7.1 非 Development Windows 构建通过。日志 `Logs/usv-validation-a959b5df3dfb46458a8f013ac34a11a7.log`。
- 新 v0.7.1 独立 Player 的验收 Smoke 通过，使用隔离存档。日志 `Logs/desktop-acceptance-bca3d53bcd2646a7915d0e5faf284eb3/player.log`。该 Smoke 不等于物理鼠标认证。
