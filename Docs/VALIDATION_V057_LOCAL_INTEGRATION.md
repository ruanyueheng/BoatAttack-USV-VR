# v0.57 本地 PICO 分支整合验证

日期：2026-11-02，Asia/Shanghai。Unity 2022.3.62f1。

将 `origin/main` 的 `a6866f9`（PR #63、v0.57）合入 `feature/pico-hand-alignment-travel-20261031`，保留本地 `a8d4d41` 与 `10306f3` 的卖鱼入口、创造模式免费升级、双手实体望远镜和追踪镐握持修复。更早的五个本地 PICO 提交已在主线历史中。

合并处理：

- 航程设置、手部对齐、退出、创造模式和卖鱼分别使用 34、35、36、37、38，避免卖鱼页覆盖创造页。
- 望远镜帮助、装备提示和 A 记录链路采用双手实体光学状态；相机同时排除望远镜、猎枪和物品检视。
- 保留创造模式本机偏好及免费升级规则，同时保留主线日期/物资、家园、市场、手作和沉船航线内容。
- 合并前的本地 Unity 序列化改动已单独保存；按用户要求另作一次提交，不与代码冲突处理混在一起。

验证结果：

| 检查 | 本次结果 |
| --- | --- |
| Unity Roslyn 快速编译 | Runtime、Editor 均通过 |
| 纯捕鱼规则 | 4,847 项通过 |
| 纯输入规则 | 74 项通过；`Logs/v057-merge-input.log` |
| Unity V025 | PASS；`Logs/usv-validation-f2c096d57e0348e4be2b9bfc3a6042b5.log` |
| Unity V056 综合回归 | PASS；`Logs/usv-validation-76d41403423d4b09be999c7eef5518bb.log`；包含五个独立页面、望远镜、家园/市场与捕鱼回归 |
| Unity V057 | PASS；`Logs/v057-merge-handcraft-route.log`；包含手作接触、航线净空与 32 台设施迁移 |
| Git LFS | 主线所需对象已下载，`git lfs fsck --objects --pointers HEAD origin/main` 通过 |

Unity 检查使用显式批处理入口和隔离测试存档。本次未构建新的 Windows/PICO 安装包，未验收实体头显舒适度、设备帧率或真实手柄握感。
