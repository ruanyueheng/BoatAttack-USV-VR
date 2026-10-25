# V039 验证记录

日期：2026-10-25；Unity 2022.3.62f1；Blender 5.2.2 LTS实时BlenderMCP。运行检查使用临时隔离存档。

## 复现与边界

第38版中心及±0.85m路线在本机仍通过，无法据此认定用户卡住的唯一原因。新版本检查两端接缝、±1.12m区域、斜向往返、16/50ms移动步长、侧栏和完整机关流程。

实际抓到并修复的漏点包括：绕线取消时恢复-55度、绳索被排除于结构碰撞、岸面固定0.6m最低高度、家园固定高度的边石/栅栏。浅岸检查还覆盖水线碎石和继续走到干岸，避免只验证泳姿状态。

收尾新增的未承重桥面检查在旧逻辑上真实失败：桥板外观已升起，碰撞却未开放。现让未释放的浮桥保持在水下，靠近时提示机关步骤；推箱进度未完成时不会提前四舍五入成100%。已解锁旧档和货箱完全到位后的注水路线保持通行。

## 已完成的专项

- V039整合：30段桥面连续步行、侧栏阻挡、菜园柱间阻挡与门洞通行；实际网格的边石/栅栏接地及57个植物根部接地；海水→浅水步行→干岸，全程无跳跃输入。
- 手作：正面绕线完整转圈、取消重开、两掌实际可达、视野与材料不误扣。
- 烧烤：混元原始模型50000面→运行6000面；GLB再导入仍为单网格、6000面、UV及贴图完整，原始GLB哈希不变。Meowa音效实际播放、烟雾实际产生、暂停停止音效与发射；手持和图标与架上资源一致。
- 烟雾64粒、炭火20粒的固定上限；近距离按状态运行，减少特效时降低发射率。

## 最终验收

| 项目 | 结果 |
|---|---|
| 4356条规则、70条输入策略 | [规则通过](Validation/V039/rules.txt)、[输入通过](Validation/V039/input.txt) |
| 13组基础Unity回归 | [本版整合](Validation/V039/FishingV039.txt)、[四秘境](Validation/V039/FishingV036Portal.txt)、[手部](Validation/V039/FishingV037Hands.txt)、[农场](Validation/V039/FishingV035Farm.txt)、[外海游泳](Validation/V039/FishingV038Swimming.txt)及钓鱼、狩猎、提灯、交互、性能和结构检查通过 |
| 未承重桥面与烧烤收尾专项 | [沉船完整流程](Validation/V039/FishingV039Realm.txt)、[烧烤及模型绑定](Validation/V039/FishingV028Coast.txt)通过 |
| 最终Windows / ARM64 Android | [Windows构建](Validation/V039/FishingBuild.txt)、[Android构建](Validation/V039/FishingAndroidBuild.txt)通过 |
| 包内容与版本 | [210文件逐项哈希、APK版本39、ARM64及v2签名](Validation/V039/packages.json)通过；更新证书与V038一致 |
| 最终独立Windows程序 | [88个不同场景](Validation/V039/FishingPlayer.txt)通过；重复应用夹具不重复计数 |
| 资源完整性 | [13模型哈希、单网格GLB、贴图、音频循环](Validation/V039/assets.json)通过；[本地自审](Validation/V039/review.md)完成 |

正常游玩存档、旧构建和预存XR配置保留。PICO真机控制器、佩戴体验与长时间设备帧率未实测。独立Player冒烟没有替代源场景画面复看或真实输入验证。

生成凭证：[混元页面](Images/V039/hunyuan-generated.png)、[Meowa页面](Images/V039/meowa-sizzle-generated.png)。

![实际游戏中的混元烤鱼与轻烟](Images/V039/hunyuan-grill-close.png)

![家园边石和栅栏沿坡地落地](Images/V039/garden-feet-close.png)

![朝向玩家的绕线平面](Images/V039/winder-front.png)
