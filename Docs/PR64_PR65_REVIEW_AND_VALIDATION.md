# PR #64 / #65 审阅、同步与 PICO VR 模拟验证

日期：2026-11-02。Unity **2022.3.62f1** / URP **14.0.12**；游戏版本仍为 **0.58.0 / 58**。

## 整合结论

- 主线基准：`a6866f9ad6babb15ba34f3280340f03c69110f04`。
- [PR #64](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/64)：`11ec17e07cfbcaae647b872bdef6b6cc39e6bea4`。
- [PR #65](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/65)：`21d5fb4ffd8142ed0784b22a7741b3495217ff39`。
- `git merge-base --is-ancestor origin/pr/64 origin/pr/65` 返回 0，#65 完整包含 #64。无需重复挑选或合并 #64。
- `git merge-tree --write-tree` 对主线与 #65、本地原分支与 #65 均返回成功，没有内容冲突。提交前再次抓取，两份 PR 和主线仍是上述提交。
- 两份 PR 的讨论和审阅记录均为空。审阅覆盖运行时代码、调用方、界面入口、验证脚本及配置；新增资源的序列化空格变化来自原 PR。

本地从原来的 v0.57 分支切换到 `fix/pico-v058-pr64-pr65-review-20261102`，包含两份 PR 的全部功能和本次修复。原有未提交修改先以 Git stash 和外部补丁备份，再恢复；未提交素材保留，原有材质及规划文件不纳入本次新提交。没有修改场景、预制体、包版本或普通玩家存档，也没有合并远端主线。

## 发现与修复

### 1. [P2] 卖鱼入口覆盖鱼箱的“上一页”按钮

来源为 #64，#65 继续继承。`FishingXRNotebook.Refresh()` 的第 13 页调用 `Pager()` 后，用 `Set(6, ...)` 放置卖鱼入口；`Pager()` 的上一页也占用第 6 号按钮。升级鱼箱并存入七条鱼，点击下一页后，“上一页”被卖鱼按钮替换，玩家无法通过按钮返回前六条鱼；横扫翻页手势仍可用。

将卖鱼入口移到原“返回首页”所在的第 7 号按钮，保留第 6、8 号分页按钮；卖鱼页本身提供首页入口。补充实际界面的七鱼用例：翻到第二页、点击上一页、验证卖鱼入口仍可操作。

验证采用先失败再通过：在旧代码上，V025 明确失败于 `The sale shortcut preserves the previous-page button`；应用这一行修复后，完整 V025 通过。

### 2. 原生输入脚本缺少命令确认及部分交互覆盖

首次原生检查出现一次松握退出断言失败，随后独立复测退出正常。旧脚本只比较发送前后的游戏帧号，未确认新命令已被模拟器读取。现在状态返回命令序号及应用帧号，脚本等待对应命令生效至少九帧后再检查，避免把旧输入对应的状态作为新命令结果。

原脚本只测试快购、设施入口和望远镜。本次增加锯子、锤子近距直接握持、移开工件后仍持握、真实加工动作计数、松手及取消不扣未完成材料，以及左右手分别拾取一枚落果、持续握住不重复收成。落果由隔离场景调用生产摇树路径准备，**拾取本身经过原生 OpenXR 握把输入**；不把此用例描述为原生摇树动作验收。

TypeSafe 辅助核验使用 `jev-latest`，实际返回 **`jev-1.14.0`**。其指出原生脚本没有落果拾取用例；对分页结论仅给出低置信度 0.22，因此分页缺陷以原文检查和上述失败/通过的 Unity 用例为依据。没有将模型判断当作执行授权或测试结果。

## 本轮验证

| 检查 | 结果 | 范围 |
| --- | --- | --- |
| Runtime / Editor 快速编译 | 通过 | 当前源码，无编译错误 |
| 捕鱼规则 | 4,852 项通过 | 交易、背包、制作、存档等纯规则 |
| 输入规则 | 74 项通过 | 纯输入及渲染策略 |
| Unity V025 | 通过 | 创造交易、卖鱼确认、鱼箱分页、VR 界面、舒适区几何 |
| Unity V058 | 通过 | 追踪工具接触、光学望远镜、真实快购、六设施部署、拾果、店员网格动作 |
| Unity V056 | 通过 | 综合回归，含钓鱼与夹鱼交互 |
| Unity V057 | 通过 | 手作、沉船航线净空、旧设施迁移 |
| 原生 Mock OpenXR | **20 项观察通过** | 扳机与握把输入经过原生运行时和生产逻辑，未启用自动游玩 |
| Git LFS 指针、改动空白检查 | 通过 | 已继承资源指针有效；本次修改没有空白错误 |

原生检查的精简记录见 [native-summary.json](Validation/PR64-65/native-summary.json)。完整状态、原始日志和截图在本地忽略目录 `Logs/PR64-65`、`Logs/XRSimulator` 及 `Logs/review-pr64-65-*` 中；精简记录不包含本机用户目录或存档路径。

原生模拟画面：

![六类设施入口](Validation/PR64-65/six-facilities.png)

![双握近眼望远镜](Validation/PR64-65/binocular-aligned.png)

可重复运行的入口：

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release
python Tools/Check-FastCompile.py
./Tools/Run-UnityValidation.ps1 -Suite FishingV025
./Tools/Run-UnityValidation.ps1 -Suite FishingV058
./Tools/Run-UnityValidation.ps1 -Suite FishingV056
./Tools/Run-UnityValidation.ps1 -Suite FishingV057
```

随后按 `Tools/Test-XRV058.py` 文件头的显式隔离配置启动原生模拟器，运行：

```powershell
python Tools/Test-XRV058.py --output Logs/PR64-65/native-final.json
```

## 验证边界及后续改进

本轮 `adb devices -l` 未发现设备。完成的是 Windows Editor 中的原生 Mock OpenXR 模拟，启用项目的 PICO 画质设置；截图为单眼镜像，不能证明 PICO 双眼显示、设备帧率或佩戴舒适度。结束时已正常退出本轮启动的模拟器。

本轮未重新构建、安装或上传 APK；原 PR 的 APK/设备记录属于作者此前验证，不作为本轮实机结果。游戏和 Android 版本号未变。

实机验收应优先检查望远镜瞳距、近裁剪与长时间双眼舒适度，锯锤手腕角度及桌面高度，弯腰拾果时的追踪稳定性，以及开启额外光学相机、店员动作后的持续帧率。当前没有据此确认新的真机缺陷。保留草稿，等待佩戴验收后再合并。
