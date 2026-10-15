# 汐禾店员对话（V028 选择性接入 PR #38）

## 体验入口

进入钓具店，走到柜台前：电脑按 **E** 交谈，点击选项或按 **1–4** 选择话题，E 显示完整文字，Esc/Q 结束。Tab 仍可直接购物。

PICO 靠近按右 **A** 交谈，右手射线配合右扳机选择，B 关闭。使用现有世界空间手册，不另外增加一套输入系统。

## 对话内容

- 汐禾与小店：小店来历、她喜欢的海岛生活。
- 商品与装备：当前金币、升级建议、猎枪与装备；可进入原有购物界面。
- 制作指南：熔炉炼料、锻造台塑形、工作台组装；石镐、铁镐、煤与毛皮提灯配方。
- 海岛生活：漂流木做木棍、采集和狩猎、回潮渔屋与靠岸建议；可标记真实家的泊位。

共 15 个可到达话题，有返回上级选项及固定“查看商品”“结束交谈”入口。首次、再次见面和夜间有不同招呼；不添加赠币、自动购买或强制任务。配方数量读取当前生产配方，猎枪售价读取现有经济规则。

交谈阻止移动、采集、射击及购买；查看商品仅切换界面，实际付款继续走原有交易。离开柜台、暂停或失去焦点会关闭交谈。家的航点在关闭对话的消耗帧之后设置，避免关闭帧阻挡导航。

## 整合基线与保留内容

以当前 V027 为基线，只逐项移植 PR #38 的新增对话、用户工具模型、握持/灯光以及制作设施分工。保留 V027 奶油色界面、房屋和海岛，不整体检出旧版本文件。

未升级 Unity，项目继续使用 2022.3.62f1。

## 验证

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release
powershell -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite FishingCashierDialogue
powershell -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite FishingV025
powershell -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite FishingForge
```

对话场景测试使用隔离临时存档：实际柜台 E 路径、四个入口、全部话题的电脑和世界空间文字布局、输入阻挡、原价购物、家的导航、见面标记落盘和远距离失效。

世界空间排版通过桌面批处理检查，不等同于佩戴 PICO 头显的舒适度和实体输入验收。证据保存在未纳入 Git 的 `Logs/CashierDialogue`。
