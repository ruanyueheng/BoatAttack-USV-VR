# AUDIO v0.44 音效设计调研

适用范围：海岸钓鱼 / 种田 / 秘境 VR 小游戏，美术风格是绘本手账，主题是海岸和星光，平台为 PICO 4 VR 加桌面版。
调研日期：2026-10-28。说明：标"未找到出处"的条目是经验性建议，不是引用；没有编造任何链接或引语。

## 0. 参考来源速览

| 游戏 | 可用来源 | 对本项目的价值 |
|---|---|---|
| Stardew Valley | 作者 Eric Barone 一人用 Reason 自带音源合成全部音效，部分取自自然声（[Wikipedia 原声条目](https://en.wikipedia.org/wiki/Stardew_Valley_(soundtrack))）；访谈 [THE SOUND TEST #01](https://www.youtube.com/watch?v=ipxnN2HKcNs) | 用合成音加少量自然声，小团队也能做出风格统一的音效 |
| 动物森友会 NH | [Audiokinetic 博客：全盲玩家如何靠声音钓鱼](https://www.audiokinetic.com/en/blog/animal-crossing-is-playable-by-the-totally-blind/)；分析视频 [Scruffy: Wholesome Sound Design](https://www.youtube.com/watch?v=A4mN7CsAys8)、[Escapist: How ACNH Sound Design Relaxes You](https://www.youtube.com/watch?v=7hXuZN5H3yQ) | 钓鱼过程分成"试探 / 咬钩 / 收线"三段声音，水花大小随鱼的体型变化 |
| A Short Hike | 音效由开发者 Adam Robinson-Yu 本人制作（[Game Developer: Road to the IGF](https://www.gamedeveloper.com/business/road-to-the-igf-adam-robinson-yu-s-i-a-short-hike-i-)）；Mark Sparling 的配乐按所在区域淡入淡出分层（[Nintendo Everything 访谈](https://nintendoeverything.com/interview-a-short-hike-dev-on-bringing-the-game-to-switch-inspiration-art-style-possibility-of-a-sequel-more/)） | 配乐按区域分层叠加，可以直接借鉴到秘境主题 |
| Dredge | [High Score 2023: THE SOUND OF Dredge（David Mason、Mika Cornelius）](https://www.youtube.com/watch?v=t2LenpP031k) | 钓鱼加不安感的氛围做法（未找到文字版出处） |
| Sky 光遇 | Vincent Diamante 作曲，管弦乐团和木管演奏者 Kristin Naigus 同时录制了部分音效（[Gaming Audio News](https://gamingaudionews.com/2020/08/03/soundtrack-to-sky-children-of-the-light-available-now/)） | 音效和配乐用同一批乐器，音色天然统一，对应"星光"主题 |
| 旷野之息 | 官方 Making of 第二集：脚步声用拟音录制，背后武器的晃动声随装备和潜行状态变化（[Zelda Universe 整理](https://zeldauniverse.net/2017/03/15/part-two-of-the-making-of-breath-of-the-wild-discusses-open-air-game-scale-and-sound-design/)）；以钢琴为主、给环境声留空间（[iMore](https://www.imore.com/why-legend-zelda-breath-wilds-sound-design-perfect)） | 配乐少，环境声多；高频动作的声音必须有变体 |
| Unpacking | [A Sound Effect：14,000 个音效](https://www.asoundeffect.com/unpacking-game-audio/)（内嵌 GDC 演讲 "Unpacking: The Fun Behind the Foley"）；[NME](https://www.nme.com/news/gaming-news/unpacking-features-over-14000-sound-effect-files-3087521)：同一个交互按速度区分不同的声音 | 拿起/放下的声音按材质和速度区分，适合背包和宝箱 |

没有找到光遇和 BotW 专门讲音频的 GDC 演讲（光遇的 GDC 演讲只讲社交设计）。

## 1. UI 音效通用原则（第 4 条情景的基础）

- **整套 UI 用同一种音色，靠音高方向表达含义**：确认音上行（例如两音上行纯五度），取消音用同样的音色下行，第二个音略微下垂。玩家听方向就知道"是 / 否"（[itch.io 合成 UI 音效 devlog](https://yetiancai.itch.io/cozy-ui-sfx-190/devlog/1730801/synthesizing-game-ui-sound-effects-with-code)）。
- **按重要程度分级**：导航音最小最轻，选中音稍大，确认音最完整（Total War: Warhammer 3 演讲摘要，[daily.dev](https://daily.dev/posts/what-was-that-noise-mastering-creative-ui-sound-design-with-intention-zx8sroype)）。演讲中还提到：控制认知负荷、用同一套声音调色板、用奥卡姆剃刀避免过度设计。
- UI 是一套语义系统，可以调的参数有音高、响度、音色、方向、协和/不协和、短促还是绵长（[A Sound Effect: Sci-Fi UI](https://www.asoundeffect.com/sci-fi-ui-sound-effects/)）。
- 本项目的落地建议：UI 统一使用"八音盒 / 钢片琴 + 纸张轻摩擦"这一家族，全部落在同一个调式里（建议 C 大五声音阶，所有音天然和谐）。单次 UI 音控制在 80–250 ms，峰值比钓鱼主事件低 6–10 dB，避开 2–4 kHz 的刺耳区，并把 200 Hz 以下全部高通切掉。

## 2. VR 空间音频注意点

- Meta 官方建议：尽量对声源做空间化。没有空间化的立体声会"head-locked"（固定在头部、不跟随转头），会破坏沉浸感（[Meta: Immersive sound](https://developer.oculus.com/design/audio-intro-mixing)）。送进空间化器的素材必须是**单声道**（[Meta Spatializer 文档](https://developers.meta.com/vr/documentation/unreal/audio-spatializer-features/)）。
- 声源相互重叠时容易彼此掩蔽，要把它们分到不同频段，再加上空间分离（[Meta Audio design](https://developers.meta.com/vr/design/audio/)）。
- 在 Unity 中：世界内的声音设 `spatialBlend = 1`，用单声道 wav。浮漂、鱼、宝箱、传送门的声源挂在对应物体上；手柄上的声音（绳鸣、收线）挂在手柄上，属于 1 米以内的近场声。
- 例外：菜单 UI 和旁白可以保持 2D、固定在头部，这是第三方的做法（[Konga Studios](https://www.kongastudios.com/post/how-audio-works-on-meta-quest-2-3-for-vr-developers)），Meta 官方没有明说。折中办法：腕表或手上的面板音效用 3D 挂在面板上，全屏系统提示用 2D。
- 振动和声音应该同时触发；咬钩、绳鸣这类事件在 VR 中"声音 + 触觉"缺一不可。（未找到出处，属于经验建议）

## 3. 情景逐条

### 3.1 咬钩
- **这一刻发生了什么**：浮漂先轻轻试探几下，然后被猛地拽进水里，玩家必须在很短的时间窗口内抬竿。
- **同类做法**：动森 NH 有三种不同的声音，分别是轻碰浮漂、明显更大的咬钩声、收线声，水花大小随鱼的体型变化。全盲玩家只靠声音就能钓鱼（[Audiokinetic](https://www.audiokinetic.com/en/blog/animal-crossing-is-playable-by-the-totally-blind/)）。Stardew 咬钩时会响一声提示音并显示感叹号（具体音色设计未找到出处）。
- **原则**：试探声是轻的"嘀 / 啵"小水声，-18 dB 左右，随机 2–3 次，这是给玩家的预备信号。咬钩声分三层：瞬态层是 0–30 ms 的低沉"咚"加入水破裂声；主体层是 150–300 ms 的水花，大鱼用更低的频段和更大的水花；尾音层是 300–600 ms 的气泡加一个短促的上行提示音（音色和 UI 同一家族，只在咬钩时出现，作为"现在抬竿"的语义标志）。响度设为全场最高的事件之一，比环境声高 10–12 dB。音高随机 ±5%，准备 3–4 个变体。声源挂在浮漂上，用 3D 播放，同时触发手柄强振动。

### 3.2 遛鱼绳鸣 / 鱼线张力
- **这一刻发生了什么**：玩家和鱼拉锯，张力持续变化，过紧会断线。
- **同类做法**：未找到出处，没有找到 Dredge 或 Stardew 这一部分的文字资料。
- **原则**：做成连续循环，不要用重复触发的单次音效。由两条循环组成：①绕线轮的"咔嗒"齿轮循环，播放速率跟收线速度挂钩；②钢丝或弓弦摩擦的绳鸣循环，音高和音量都由张力 0–1 驱动（例如 pitch 0.8→1.4，音量 -24→-10 dB）。张力超过 0.85 时叠加一层高频颤音作为危险警告，同时加强振动。断线时播放"啪"的瞬态加松弦下滑音。循环长度 2–4 秒，首尾做零交叉对齐。参数变化要平滑（约 50–100 ms 的线性插值），避免听到台阶感。声源挂在手柄上。

### 3.3 传送门进入与穿越
- **这一刻发生了什么**：靠近传送门时它发光、低鸣；穿过时视野切换到秘境。
- **同类做法**：光遇用木管乐器和管弦乐团录制音效，和配乐风格统一（[Gaming Audio News](https://gamingaudionews.com/2020/08/03/soundtrack-to-sky-children-of-the-light-available-now/)）。传送门本身的做法未找到出处。
- **原则**：分三段。①待机：门上挂一个 3D 的低鸣循环（100–300 Hz 的柔和 pad，叠加稀疏的风铃 / 星星闪烁粒子声，每 1–3 秒随机触发一次），按距离衰减，起到声音引导玩家的作用；②进入：0.5–1 秒的上行扫频，用竖琴或钢片琴的琶音加上一个反向的浪声，用浪声扣住海岸主题；③穿越的那一刻：所有声音做 200–400 ms 的低通加混响甩尾（"潜入水下"的感觉），秘境环境声随后淡入。整个过程不超过 1.5 秒；穿越瞬间在头部做 2D 处理是可以接受的。

### 3.4 UI：确认 / 返回 / 拒绝 / 翻页 / 通知
- **这一刻发生了什么**：菜单和手账面板中的基础操作。
- **同类做法**：见第 1 节的来源；动森 NH 的 UI 音色统一偏柔软（分析视频，见第 0 节）。
- **原则**：
  - 确认：两音上行，约 120 ms
  - 返回：同一音色两音下行，约 120 ms，比确认音低 2 dB
  - 拒绝：同一音高重复两次，或叠加一个闷音的不协和音，150 ms，不要用刺耳的蜂鸣
  - 翻页：真实纸张翻动的拟音，呼应手账风格，150–300 ms，准备 4–6 个变体，音高 ±8%
  - 通知：三音上行琶音加一点亮色尾音，约 400 ms，是 UI 中最响的一种，但仍低于钓鱼事件

  所有 UI 音都限制在 300 Hz–6 kHz，开头 5 ms 内到达峰值，避免发软。

### 3.5 起跳
- **这一刻发生了什么**：桌面版的跳跃；在 VR 中高频触发、而且是玩家自己发起的动作。
- **同类做法**：BotW 的脚步声用拟音录制，背后装备的晃动声随状态变化（[Zelda Universe](https://zeldauniverse.net/2017/03/15/part-two-of-the-making-of-breath-of-the-wild-discusses-open-air-game-scale-and-sound-design/)）。A Short Hike 起跳的具体做法未找到出处。
- **原则**：只要短的起跳声（沙地或木板的蹬地 + 一点衣料声，80–150 ms），必须做落地声，落地声按地表材质区分（沙、木、草、石）。不要用卡通的"boing"，除非整体风格非常 Q 版。准备 4 个以上变体，音高 ±6%，音量 ±2 dB，比脚步声高 2–3 dB。VR 中声源放在脚下，用 3D 播放。

### 3.6 打开背包
- **这一刻发生了什么**：打开手账或布包面板。
- **同类做法**：Unpacking 的拿起/放下按物件和速度区分不同的声音，大量使用真实物件拟音（[A Sound Effect](https://www.asoundeffect.com/unpacking-game-audio/)、[NME](https://www.nme.com/news/gaming-news/unpacking-features-over-14000-sound-effect-files-3087521)）。
- **原则**：打开时播放帆布 / 皮扣 / 本子翻开的拟音，200–350 ms，接一个很轻的 UI 家族单音（-6 dB）把它标记成"系统界面"。关闭时把同样的素材缩短反向处理，并降低半音。放入物品按类别区分：鱼是湿的"啪嗒"，种子是沙沙声，金币是叮当声。这是 Unpacking 式的、性价比最高的细节。

### 3.7 开宝箱
- **这一刻发生了什么**：一个小高潮，带有期待感，然后揭晓结果。
- **同类做法**：Zelda 系列的开箱有铺垫和揭晓两段，属于玩家共识；具体文字出处未找到。
- **原则**：两段式。①铺垫：木头吱呀加锁扣，300–600 ms，可以配合开盖动画；②揭晓：按稀有度分级的亮色和弦或琶音加星光闪烁，普通品 0.5 秒，稀有品 1.5 秒，叠加多一层低频 pad。揭晓段的音量比铺垫高 3–4 dB。稀有度要靠音高和层数来区分，不能只靠音量。

### 3.8 已获认可的项目（稀有鱼出水 / 卖鱼得金币 / 剧情短句 / 秘境主题音色）
- 目前的方向没问题，只有几点可以小幅改进，不需要推翻：
  - **稀有鱼出水**：参照动森，水花大小跟鱼的体型挂钩，再加一条 1–2 秒的短乐句，让配乐临时压低（ducking -6 dB）。
  - **卖鱼得金币**：数量多时不要每枚金币都单独响，改成"连串叮当 + 结尾一声大的"，音高随金额逐级上升。
  - **剧情短句**：如果用"咿呀"式的伪语音（动森风格），把音节音高随机 ±1 个全音，并按角色区分基准音高。
  - **秘境主题音色**：参考 A Short Hike，按区域分层淡入淡出；参考光遇，让音效和配乐共用乐器，统一音色。

## 4. 免费专业音效库与许可

| 库 | 官网 / 许可页 | 要点 |
|---|---|---|
| Sonniss GDC Game Audio Bundle | [gdc.sonniss.com](https://gdc.sonniss.com/)；[许可](https://sonniss.com/gdc-bundle-license/) | 免版税，可商用，**无需署名**，项目数量不限；禁止原样转售音效、禁止声称自己是原录音作者；新版条款禁止用于训练 AI。每年的包体积很大（2026 年版 7.47 GB）。官方以整包 / 分卷压缩包的形式发放，能否按分类单独下载以官网当年的页面为准；第三方镜像站往往按厂商分文件夹，但并非官方渠道 |
| Freesound | [freesound.org](https://freesound.org/)；[FAQ / 许可](https://freesound.org/help/faq/) | 每个文件单独标注许可：CC0 无需署名；CC-BY 需要署名作者、作品名和链接；CC-BY-NC 不能商用。下载时逐个记录 |
| BBC Sound Effects | [sound-effects.bbcrewind.co.uk](https://sound-effects.bbcrewind.co.uk/)；[许可](https://sound-effects.bbcrewind.co.uk/licensing) | RemArc 许可：只限个人、教育、研究等**非商业**用途，需要署名；课程作业可以用，以后商业发布必须换掉或另购授权 |
| Zapsplat | [zapsplat.com](https://www.zapsplat.com/)；[Standard License](https://www.zapsplat.com/license-type/standard-license/) | 免费账户可以商用，但**必须署名**（例如在致谢中写 "Sound effects obtained from zapsplat.com"），只能下 MP3，有下载频率限制；Gold 会员免署名 |
| Mixkit | [mixkit.co](https://mixkit.co/free-sound-effects/)；[许可](https://mixkit.co/license/) | 音效适用 Sound Effects Free License，可商用，包括游戏，无需署名；不得原样再分发。注意：Mixkit 的**音乐**许可不包括游戏 |
| Pixabay | [pixabay.com/sound-effects](https://pixabay.com/sound-effects/)；[许可摘要](https://pixabay.com/service/license-summary/) | Pixabay Content License：免费、可商用、可修改、无需署名；不得原样转售；用户上传的内容不保证第三方授权，需要自己留意 |

建议：优先用 Sonniss 和 CC0 素材做底层，所有 CC-BY 和 Zapsplat 的素材统一记在 `Credits` 里；BBC 素材只在课程版中使用，并单独标记。

注：BBC 和 Pixabay 的许可页链接根据官网结构给出，本次检索只通过第三方摘要确认了条款内容，提交前请打开核对一次。
