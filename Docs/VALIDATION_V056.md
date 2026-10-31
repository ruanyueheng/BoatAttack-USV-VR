# 0.56.0：夹鱼检视、竿弯与提灯整合

2026-11-01，在 main `e5207415`（已合并 PR #61 / 0.55）的基础上，合入同学 [PR #59](https://github.com/ruanyueheng/BoatAttack-USV-VR/pull/59) 新增的 `0b000497`、`984a6cf3`，保留原提交及作者历史。本版范围冻结至完整 SHA `984a6cf31182fadc766d434dd79d4ad793882570`，不包含此后追加的提交。之前的家园、菜园、动物活动、墙面、商船碰撞与购买修复继续保留；未把本机既有未提交素材及自动生成设置加入提交。

## 新增功能与整合修复

- 左手靠近待处理鱼获按住握把，用实体夹鱼器检视。夹柄贴合实际渲染手掌，夹口连接鱼嘴；翻腕时鱼头仍朝上，鱼身按重力悬挂并轻微挣扎。松手回到抄网或安全托盘，再次拿取保留同一收获凭据，不提前入箱或重复奖励。持鱼时 A 回网，再按 A 入箱。
- VR 竿身随实际鱼线世界方向弯曲，低张力收线带预载、增载加深、松轮平滑减载。握柄和鱼轮保持刚性，鱼线起点使用最终变形竿尖；桌面受力路径保留。
- 统一手柄震动强度乘 1.7、时长乘 1.5，强度限制 0.95、时长限制 0.3 秒；拒绝非法值、无焦点和不支持的设备。这里只验证参数与调用条件，未验证实体震感。
- 提灯有独立佩戴状态。右扳机开关当前提灯；左扳机在岸上疾跑、船上刹车。Y 收回提灯并换装备。实际背包网格原来没有通向新增收灯动作的入口，本版将现有装备按钮改为按佩戴状态显示“熄灯并收回”或“装备到腰侧”，保留物品和原检视操作；短文案避免挤出按钮边缘。
- 解决手柄提示、提灯物品页及菜单关闭处理的合并冲突，保留相机/望远镜互斥说明、日期关闭逻辑及页号 34–37。同步店员说明的测试断言和 0.56.0 / Android 56 版本。

## 验证证据

可复现入口：`Tools/Run-UnityValidation.ps1 -Suite FishingV056`。组合 V055（含 V025、菜单/创造模式、提灯、V054 家园集市与船体物理）、FishingAngling、FishingMouseFishing；全部使用隔离存档。

| 检查 | 结果与本地证据 |
| --- | --- |
| 规则与输入 | 4844 / 74 项通过；`Logs/V056/rules-final.log`、`input-final.log` |
| 最终 Unity 综合回归 | PASS；`Logs/usv-validation-a857bda5574842b5bd608a1ca13052dc.log`。新增实际背包按钮的收回、保留物品、重装备及文案断言 |
| 最终按钮排版专项 | FishingLantern PASS；`Logs/usv-validation-0ce97e0031e94113a418c7778379d51f.log`，最终短文案的两种状态均单行放入真实按钮，收回和重装备行为再次通过 |
| 夹鱼专项 | 实际可见手掌蒙皮、夹柄接触、夹口网格、鱼嘴连接、翻腕、回网/托盘、重抓、去重入箱及存档回归通过；`Logs/MouseFishing/` |
| 竿弯专项 | 低张力收线、增载、松轮、旋转手柄后世界拉向、暂停/非法输入、卸载、鱼轮固定及鱼线端点通过；`Logs/Angling/` |
| 原生 XR 夹鱼 | 原生左握把拿起，180° 翻腕，安全托盘，重抓，A 回网、A 入箱；同一凭据，鱼箱由 0 变 1。鱼嘴与夹口、夹柄与校准手掌误差均小于 2mm，鱼头向上分量大于 .99；`Logs/V056/NativeXR/gripper-pass.json` |
| 原生 XR 竿弯 | 手柄 0°/90° 时竿尖位移与世界拉向余弦 .843/.833，实际鱼线与变形竿尖误差 0；`NativeXR/rod-pass.json`。卸载断言由上面的 Unity 专项覆盖 |
| 原生 XR 提灯/移动 | 右扳机开灯、左扳机疾跑不切灯、Y 收回、重装备、手册按住旧输入后需松手恢复；同一 .65 摇杆输入下步速 1.43→2.43m/s，船上刹车值 1；`NativeXR/lantern-input-pass.json` |
| 原生 XR 背包入口 | 射线实际点选提灯格与右下收回按钮，灯体/灯光隐藏；同一物品格重新装备恢复腰挂；`NativeXR/backpack-input-pass.json` |
| 原生 XR 工具回归 | `Tools/Test-XRTools.py fixed` PASS：猎枪、相机/望远镜互斥、观察说明与快门恢复；`NativeXR/tools-regression.json` |
| 原生 XR 菜单与退出 | 真实射线可达日期 34 并载入隔离航程、创造模式 37 开关、手部 35 左右独立微调与恢复、退出 36 取消。再次确认退出成功保存并停止 XR；`NativeXR/menus-final-pass.json`、`quit-final-pass.json` |

原生模拟日志为 `Logs/V056/native.log`、`native-backpack.log`、`native-layout.log`。模拟使用 Windows 原生 OpenXR Mock Runtime 的头部/手柄姿态和按钮输入，走生产交互逻辑并启用 PICO 画质。自动操作保存在独立测试航程，不覆盖正常玩家档。

## 实际画面

![原生 XR 翻腕后夹鱼器与垂直鱼身](Images/V056/gripper-rolled-native.png)
![原生 XR 手柄旋转后鱼竿跟随真实鱼线拉向](Images/V056/rod-world-line-native.png)
![实际背包网格的收灯入口](Images/V056/backpack-stow-native.png)

这些图片是单目诊断镜像，已人工检查夹口、鱼身悬挂、竿弯及鱼线连接。用户没有头显，本轮未进行实体 PICO 的双目、握感、震感、追踪、帧率和佩戴舒适度验收；+30° 手部默认校准仍可独立调整。

## 最终构建与本地同步

两端均由最终短文案源码构建。Windows 210 个发布文件与 ZIP 逐项哈希一致；APK 的 ZIP 内容全部可读取，确认 ARM64、LZ4 数据容器、包名 `com.ruanyueheng.tidefin.vr`、版本 `0.56.0 / 56` 和 v2 签名。两端包体都检查到 `FishMouthLocal`、`GripperJaw`、`SetLineLoad`、`StowLantern`、`LanternEquipped` 标记，499 个 C# 文件在两端构建前后哈希完全一致。

| 本地发布包 | SHA-256 |
| --- | --- |
| `Builds/FishingPrototype-v0.56.0-Windows.zip` | `2513DAA9264FA9A66BB922E6F934836219CEDB68AD954448A5D1E4608458905C` |
| `Builds/FishingPrototype-v0.56.0-Android/FishingPrototype.apk` | `89A3AD31005C428D8E67C78E88D22DD87B337EDA45EE0E71B02B67FA6CD13D36` |

Windows 构建日志 `Logs/usv-validation-b7f731d9da9e4b018fed2dd2292a4b9f.log`，Android `Logs/usv-validation-ee3b415ba45a43c887cab6b817629e69.log`。Windows 最终包的 88 场景冒烟、隔离档与生产规则检查通过：`Logs/desktop-acceptance-0d15132b93014453a265f2bb5760f660/player.log`。早期长文案产物未作为最终包。

构建使用的临时 R: 别名已移除，源码仍在原 E: 工作区；随后从 E: 运行结构检查和最终快速编译均通过。记录 `Logs/usv-validation-599e027aa3154513a27b5ea41027602d.log`、`Logs/V056/fast-compile-final.log`。本机安装包、测试日志、隔离存档、构建缓存与原有未提交素材均不加入 PR。
