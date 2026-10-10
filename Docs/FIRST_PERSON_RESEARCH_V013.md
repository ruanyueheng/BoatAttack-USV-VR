# v0.13 第一人称交互调研与实现取舍

## 调研结论

本轮实际读取了下列官方文档及开发者文章。搜索/摘要接口在本环境报错，改用只读 HTTP 获取正文；没有把未访问的搜索摘要作为依据。

第一人称问题需要拆成三件事：**手与物品的真实接触、自己的身体如何在本地视图中绘制、手物在投影画面中的比例**。调整一个偏移值或近裁剪面不能同时解决三者。

### 1. 握点和手势

- 每件物品分别定义左右掌心的位置、朝向和手指姿态。物品原点不是握点；腕骨也不是可见手掌中心。[3][7]
- 双骨 IK 只负责上臂、前臂和腕部，不能自动生成手指包裹物品的姿态。需另外处理掌面、拇指对握及其余四指的弯曲。[3]
- 单位与坐标空间必须一致。`TransformPoint` 已包含缩放，不能再次乘缩放；不能同时让工具跟随解算后的腕骨、又让该腕骨跟随工具，形成反馈循环。[7]

**项目中的落实：**

1. 用源人物掌骨所绑定的真实蒙皮顶点标定掌心，而非仅取腕骨与指节骨的中点。
2. 镐柄轴为 `+Y`，镜筒轴为 `+Z`，因此两者使用不同的手掌朝向，不能套同一握拳姿态。
3. 分别配置食指、中指、无名指、小指和拇指；大直径镜筒不使用细柄的紧握姿态。
4. 保持「基础动画/站姿 → 相机 → 独立工具和目标 → 最终手臂 IK → 钓线」的顺序，不伸长手臂骨骼，不放宽接触事务的可达性与视线守卫。
5. 用实际 `BakeMesh` 顶点检查镐柄深入穿透与前物镜遮挡，而不只验证“掌心目标到了指定坐标”。

### 2. 低头与身体呈现

只把 Head 骨骼换层不能隐藏同一 `SkinnedMeshRenderer` 中的颈部。完整身体方案也必须处理近眼部位、求值顺序和阴影；Epic 的本地表现/世界表现分离思路值得借鉴，但并不等于 Unity 自动具备同名功能。[6][8]

**本版选择：**

- 保留原身体、骨架、手部顶点、腿脚和第三人称造型；不把头骨缩放为零。
- 桌面第一人称使用同源网格的本地三角形子集，隐藏头颈及过近的上身。完整身体继续负责阴影，避免双重投影。
- 原下肢是独立网格，上身隐藏后会露出裤腰开口，因此按原腰口轮廓补上只在本地显示的封口；它随髋部运动，不改原模型资产。
- 第三人称恢复完整原身体；光学观察时仅保留世界与完整身体阴影，不让手、腿或镜筒盖住视线。

### 3. 望远镜与构图

望远镜应区分携带、举起、观察、放下。观察时可以使用目镜遮罩，不必把整套双手和镜筒一起放大到眼前。必须对准后端目镜，而不是前端物镜。

**本版选择：**

- 双手在镜筒外侧握持，避开前物镜。
- 举镜/放下用约 `0.28 s` 过渡；后段才收窄世界 FOV。
- 进入第一人称观察后隐藏本地手物实体，显示双筒光学遮罩；第三人称仍看得到角色举镜，不继承光学缩放。
- 放下、暂停、切换装备、进商店或离开岸上时恢复普通呈现；不修改正常玩家的存档格式。
- 普通桌面 `Camera.fieldOfView = 68°`，光学观察 `38°`。这是 Unity 的**垂直角**，普通画面在 16:9 下约为水平 `100.3°`；不能直接照抄 Unreal 的水平 FOV 数值。[1][4][6]
- 手物位置以人物眼/肩参考系计算，而不是跟随第三人称远处相机；最后仍通过真实手臂长度检查。

这些数值是本项目的构图选择，不是“第一人称游戏行业统一标准”。验收同时覆盖 16:9、16:10、21:9、5:4，以及平视、俯仰、站立/蹲下和举镜切换。单张截图不能证明整个视角范围都没有接缝。

## 为什么没有直接加第二台手部相机

URP 14 可以用 Base/Overlay camera stack 独立渲染 viewmodel，但需要同时处理：

- Overlay `Clear Depth` 丢弃世界深度后，手物可能隔墙显示。
- 手和物品必须处于一致的深度空间，不能分别强行覆盖。
- 水面深度、反射、后处理和额外绘制成本需要重新验收。
- URP 14 的 TAA 与 Camera Stacking 不兼容。[1][2][5]

本项目已有同源全身、海水与接触事务系统，因此本版选择**单相机 + 同源本地身体遮罩 + 正确物品握持 + 独立光学状态**，没有为修手部而改动全局 URP 相机栈。

## 边界

- 几何阈值检查是回归指标，不是“所有像素永远不穿模”的保证；结果必须同时结合实际 Player 图像。
- 不把桌面 FOV 或相机偏移套到 HMD 投影。PICO 真机、控制器实握、持续性能与晕动感仍需单独验收。
- 原低多边形人物并非精细手部扫描模型；保留其风格，不伪称已引入完整动作捕捉或物理手指碰撞。

## Sources

1. [Unity URP 14：Camera component reference](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/camera-component-reference.html)
2. [Unity URP 14：Camera stacking concepts](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/cameras/camera-stacking-concepts.html)
3. [Unity Animation Rigging 1.2：Two Bone IK](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/constraints/TwoBoneIKConstraint.html)
4. [Unity 2022.3：Camera.fieldOfView](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Camera-fieldOfView.html)
5. [Unity URP 14：Anti-aliasing](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/anti-aliasing.html)
6. [Epic UE 5.6：First Person Rendering](https://dev.epicgames.com/documentation/en-us/unreal-engine/first-person-rendering?application_version=5.6)
7. [Geoff Keene / Zag：The Right Way to Do Left-Hand Weapon IK](https://zaggoth.wordpress.com/2019/01/26/ue4-tutorial-the-right-way-to-do-left-hand-weapon-ik/)
8. [Froyok：True First Person Camera in Unreal Engine 4](https://www.froyok.fr/blog/2018-06-true-first-person-camera-in-unreal-engine-4/) —— 含《Syndicate》《Mirror’s Edge》等画面与完整身体方案讨论；未据截图猜测这些游戏的真实 FOV。
