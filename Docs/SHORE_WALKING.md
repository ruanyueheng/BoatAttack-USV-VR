# 靠岸下船与陆地资源修复

## 试玩

Unity 2022.3.62f1 打开 `Assets/scenes/Fishing/fishing_Island.unity`，进入 Play Mode。

1. 驶近有实体碰撞的海岸或青色市场码头，按 S / Shift 制动到 0.5 m/s 以下。
2. 按 G 下船。船周围 5.5 m 内需要有露出水面、坡度不超过 45°、容得下角色且路径无遮挡的落脚点。渔区停泊先按 Q 收起渔具。
3. WASD / 方向键步行；右键环顾，V 回正。岸上保持第一人称，船固定停泊，禁止离船驾驶/捕鱼。码头交易仍可用。
4. 航海图以角色为中心，“船”表示登船点。返回下船位置 2 m 内按 G 上船，恢复原来的船上视角；R 救援回港。

本步只实现桌面基础步行，不含跳跃、游泳和岸上建筑交互，也没有更改 XR 相机。没有在 PICO 或实体键鼠上完成验收。

## 陆地消失的根因

原 `.gitattributes` 的 `*.asset text` 强制将 Terrain、NavMesh、LightingData 二进制文件当作文本。Windows `core.autocrlf=true` 检出时插入 CR 字节，破坏 Unity 序列化文件长度/偏移，导致 `Invalid serialized file header`，地形无法加载。

改成 `*.asset text=auto`：YAML 仍是文本，二进制不再转换。已确认本机 11 个二进制资源仅受 CRLF 转换影响，将它们从 Git 原件恢复并核对原始 Git blob 哈希。没有重新生成/覆盖美术地形或修改地形 GUID。

已经损坏的 Windows 检出需要重新提取原始二进制；只修改规则不会自动修好文件。恢复前备份任何真正的地形编辑。不要对二进制文件使用文本替换工具。

## 验证

- .NET 原捕鱼逻辑、输入/渲染策略回归通过。
- Unity FishingSmoke 完整 Play Mode 回归通过，包括三种鱼、传说鱼、交易、进度及备份。
- 新 `[FISHING-SHORE]` 回归通过：移动/暂停禁止下船、真实市场码头落脚、相机转移、WASD 共用的步行路径、墙/边缘阻挡、停泊、远处禁止上船、重复上下船、救援和水下/触发器/无陆地拒绝。
- `BoatAttack.EditorTools.FishingTerrainValidation.Run` 验证两块 513 分辨率地形，分别检测到 69、103 个水面以上采样点，均与 TerrainCollider 匹配。
- 离屏地形截图曝光异常，未作为视觉验收证据；最终画面与实体键鼠操作需要在编辑器中试玩。

批处理时关闭交互编辑器，运行 `Tools/Run-UnityValidation.ps1 -Suite FishingSmoke`。地形单独验证可用 Unity `-batchmode -nographics -projectPath <工程> -executeMethod BoatAttack.EditorTools.FishingTerrainValidation.Run`。自动测试使用独立进度。
