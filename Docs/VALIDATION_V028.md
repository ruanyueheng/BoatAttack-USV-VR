# 第28版验证记录

2026-10-15。Unity 2022.3.62f1 / v0.28.0。以已经合并的第27版为基础，选择性接入 PR #38 的对话、用户工具及制作分工，保留第27版 UI 布局和家园。

## 规则与场景检查

| 检查 | 结果 | 本机证据 |
| --- | --- | --- |
| 游戏规则 | 3820 通过 | `Logs/v028-rules-final.log` |
| 输入与渲染策略 | 70 通过 | `Logs/v028-input-final.log` |
| 海岸模型 | 六模型 / 9249 三角面；最终 FBX 摘要与生成清单一致 | `ArtSource/V028/coast-manifest.json` |
| 昼夜 UI 与海图 | 字色对比度、缓动帧率一致性、隐藏 Canvas 初始化、实际水域像素、中文标题字形通过 | `Logs/usv-validation-ac004b30b707401099888d1c043d0cfd.log` |
| 睡眠与腰挂 | 床沿髋部、膝足、手部、相机净空、单次晨间提交、跨帧画面通过 | `Logs/usv-validation-57cf2998e6b743e18312d8634d6e07b9.log` |
| 钓获、烧烤与生态 | 真实抄网结算三种海漂物、重复领取、满背包、磁盘保存恢复、八角落脚、海鸥叼鱼尺度通过 | `Logs/usv-validation-675c54371e724526b2582dbb086149cb.log` |
| 汐禾对话 | 15 话题、真实柜台入口、电脑/世界空间排版、输入阻挡、正常购物与家园导航通过 | `Logs/usv-validation-3bb12a915beb4012b3e4570cfad6bf2d.log` |
| 提灯 | 原模型及材质、暖光、灯罩发光、握持与腰挂检查通过 | `Logs/usv-validation-91a48ed5fed1496fa29ad4479fdbd57c.log` |
| 第一人称最终握持 | 原骨架、真实木柄五指接触、望远镜光路、图鉴大角度俯视及持灯可见区通过 | `Logs/usv-validation-21fdec99e96540958709cb60295558c3.log` |
| 第27版家园 | 原岛屿、住宅、设施迁移及更新后的熔炼→锻造材料链通过 | `Logs/usv-validation-50e0c19ccf0a474e8c3a0b1810e5be76.log` |
| 捕鱼与工坊 | 实际鱼获、放生、材料加工、放置/恢复、存档回归通过 | `Logs/v028-cont-final-regressions.log` 中的 Catch / Workshop |
| 第24版兼容 | 人物、上岸、店员与交谈入口通过 | `Logs/usv-validation-ff45dccd022842edbb7731dac4d24e45.log` |
| 完整游玩 Smoke | 航行、捕鱼、交易、传说鱼、天气、动物和存档恢复通过 | `Logs/usv-validation-ea70fa8a63714345a3e596a991457ff1.log` |

日志记录的 PASS 仅说明相应自动检查完成。早期失败日志仍保留，不能用作最终通过证据。正常玩家的 v12/v13 存档不参与自动验收。

## 修正范围

- 海图的源地形与水域使用不同颜色编码，修正重复颜色转换后，直接检查渲染图片中的昼夜水域颜色。黄昏最终停留在可读的浅色或深色主题，保留平滑过渡。
- 修正隐藏反馈 Canvas 的主题初始化；补齐新配方和海岸标题所需汉字。金币、鱼箱图标随资源保留 CC BY 3.0 署名，正文及手写字体保留原许可。
- 烤架与鱼箱根据实际地形落位，检查完整支撑角点及站立空间。烤鱼从真实鱼箱预留，成品满包不丢失，已领成品和宝匣不能再次结算。
- 使用独立 v13 存档，仅在 v13 主文件和备份均不存在时迁移 v12；保留原 v12 文件。烧烤鱼的凭证不会因后续钓获历史截断而丢失。
- 睡眠保持独立碰撞胶囊，通过角色骨架显示坐下、躺卧和起身；坐姿手部不再被默认持物姿态覆盖。跨两个真实帧采集蒙皮，避免同帧切换视角复用旧姿态。
- 海鸥口中小鱼按源网格归一为约 15 厘米；真实世界尺度和喙部位置都有检查。
- 握持验收使用包含角色缩放的实际蒙皮，与骨骼及权重重建的坐标核对，修正重复缩放造成的误报。镐子食指按真实木柄贴合；望远镜双手握点各向外调整约 2.86 厘米，保留原有穿模和物镜净空阈值。

## 实际画面

XR 页面来自实际世界空间 Canvas 的编辑器采样，不代表头显双眼画面。睡眠图跨帧采集；海岸道具图使用外置取景相机，第一人称角色的隐藏部分不能据此视为完整第三人称展示。

- [白天手册](Images/V028/xr-home-day.png)、[夜间手册](Images/V028/xr-home-night.png)、[浅色海图](Images/V028/xr-map-day.png)、[夜间海图](Images/V028/xr-map-night.png)。
- [睡眠动作联系表](Images/V028/sleep-contact.jpg)、[坐床沿](Images/V028/sleep-sit-body.png)、[躺卧](Images/V028/sleep-recline-body.png)、[坐起](Images/V028/sleep-get-up-body.png)。
- [海岸物件与生态联系表](Images/V028/coast-contact.jpg)、[熟鱼与烤架](Images/V028/barbecue-cooked-fish.png)、[海鱼跃水](Images/V028/fish-jump.png)、[海鸥叼鱼](Images/V028/gull-with-fish.png)。
- [最终镐子握持](Images/V028/first-person-pickaxe.png)、[望远镜携带](Images/V028/first-person-binoculars.png)、[持灯视野](Images/V028/first-person-lantern.png)。

## 尚未实测

本机没有连接 PICO。安装启动、双眼清晰度、真实控制器追踪、手部舒适度和持续帧时仍待真机验证；自动几何检查及桌面画面不能替代这些检查。第28版新增进度写入 v13，旧版仍读取原 v12，不能带回第28版的新物品和烧烤进度。

