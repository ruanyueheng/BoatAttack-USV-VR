# 第19版资产来源与制作记录

2026-10-11开始制作，2026-10-12完成桌面集成。运行时使用45个经过导入检查的FBX，材质统一为工程的顶点色调色板；只将选中的资源导入Unity。

## 外部CC0资源

- Kenney：[Pirate Kit 2.1](https://kenney.nl/assets/pirate-kit)、[Food Kit 2.0](https://kenney.nl/assets/food-kit)、[Nature Kit](https://kenney.nl/assets/nature-kit)。提交所选33个原始GLB和各包原始`License.txt`，下载ZIP与未使用模型保留在本地，不纳入PR。
- Quaternius：[Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html)，工程已核验的Standard FREE原始包。只使用免费包中的成年Superhero Female/Male基础网格和授权发型，不声称使用付费Regular或Teen角色。原始许可在`Characters/License_Standard.txt`；上游获取记录及所选原件散列保留在`Characters/SOURCE.md`。
- 原有FantasyGirl店员资源与原有玩家角色继续使用原工程的授权及导入设置。

## 工程内改造与原创内容

- Blender 5.2.2生成罗盘、绳卷、提灯、双筒望远镜、支架望远镜、风铃和星象棱镜七种原创道具。
- 将Kenney模型的材质颜色转入顶点色，修正输出的颜色空间，潮林树叶/树皮调整为海湾配色。
- 五个居民外观由两种真实成人基础网格派生：低面数、自然垂臂、实际的布衣曲面外层、完整不透明的区域衣裤配色、不同发型和工作道具。没有用方块或胶囊冒充人形网格；碰撞胶囊只负责阻挡。
- 居民是固定地点的对话角色，运行时会朝近处玩家转身；没有制作个体日程、完整待机/口型动画或配音。
- 秘境、石拱、黄铜门栅、铺装、符文和绳梯由Unity构建器制作；机关与视觉状态使用同一套生产规则。

`asset-manifest.json`记录每个FBX的来源、面数、尺寸和SHA-256。`tidal-assets-v019.blend`为可编辑的工程源；`Tools/Art/build_v019_assets.py`可重建FBX。Unity导入/场景安装入口为`FishingV019WorldBuilder.Install`，截图与实际玩法检查为`FishingV019Validation.Run`。

Blender插件在本次任务中未连接，实际制作使用本机Blender后台进程，没有调用收费模型生成服务。
