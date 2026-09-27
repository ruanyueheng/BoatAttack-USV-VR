using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop HUD. Active XR uses world instruments, never this screen-projected overlay.</summary>
    public sealed partial class FishingHUD : MonoBehaviour
    {
        public FishingPrototype Controller;
        private const float CardSeconds = 5f;
        private Font _font;
        private GUIStyle _label;
        private int _journalPage;
        private float _height, _scale, _offset;
        private readonly List<int> _picked = new List<int>();
        private readonly int[] _perLine = new int[3];

        public static float LayoutScale(int width, int height) => Mathf.Max(0.01f, Mathf.Min(width / 1600f, height / 900f));

        /// <summary>Journal tab: 0 catches, 1 legend, 2 wildlife. Public so automated captures can show each page.</summary>
        public int JournalPage
        {
            get => _journalPage;
            set => _journalPage = Mathf.Clamp(value, 0, JournalTabs.Length - 1);
        }

        /// <summary>The clickable left column (toolbar, weather card, observe button) must not also cast.</summary>
        public static bool BlocksToolPointer(Vector2 screen, int width, int height)
        {
            float scale = LayoutScale(width, height);
            float offset = (width - 1600 * scale) * .5f;
            var p = new Vector2((screen.x - offset) / scale, (height - screen.y) / scale);
            return new Rect(28, 210, 330, 262).Contains(p);
        }

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        }

        private void OnGUI()
        {
            var c = Controller;
            if (c == null || c.Career == null || c.Player == null || XRSettings.isDeviceActive) return;
            _scale = LayoutScale(Screen.width, Screen.height);
            _height = Screen.height / _scale;
            _offset = (Screen.width - 1600 * _scale) * 0.5f;
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(_offset, 0, 0), Quaternion.identity, new Vector3(_scale, _scale, 1));
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { font = _font, wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            _fade = 1f;
            bool desk = c.AtHarbor && !c.Paused;
            DrawBrand();
            DrawNavigation();
            DrawWallet();
            DrawStatus();
            DrawToolbar();
            if (c.ShowDetails) DrawClimate();
            DrawObserve();
            if (desk) DrawHarborDesk();
            else if (c.ShowDetails) DrawLegendCard(DrawOrderCard(130) + 14);
            if (c.ShowDetails && !c.Anchored)
            {
                DrawMap();
                DrawWorldLabel(c.NavigationPosition + Vector3.up * 2.2f, c.NavigationName);
            }
            DrawMessage(desk);
            if (!desk) DrawAction();
            DrawLegendBanner();
            DrawCatchCard();
            if (c.ShowDetails) DrawKeyBar(desk);
            if (c.JournalOpen) DrawJournal();
            else if (c.Paused) DrawPause();
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        private void DrawBrand()
        {
            Card(new Rect(28, 24, 330, 92), Mint);
            Label(50, 30, 290, 42, "怪鱼航线", 30, Color.white, true);
            Label(51, 78, 290, 22, "TIDE & FIN  ·  金帆传说  v" + FishingPrototype.Version, 14, Mint);
        }

        private void DrawNavigation()
        {
            var c = Controller;
            var r = new Rect(376, 24, 700, 92);
            Card(r, Sky);
            Vector3 delta = c.NavigationPosition - c.Player.transform.position;
            float bearing = Mathf.DeltaAngle(c.Player.transform.eulerAngles.y, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
            var centre = new Vector2(r.x + 52, r.y + 47);
            Disc(centre, 29, InkRaised);
            Outline(new Rect(centre.x - 29, centre.y - 29, 58, 58), Sky * new Color(1, 1, 1, 0.45f), 29);
            var matrix = GUI.matrix;
            Rotate(bearing, centre);
            Rounded(new Rect(centre.x - 3, centre.y - 22, 6, 24), Mathf.Abs(bearing) < 14 ? Mint : Sky, 3);
            Disc(new Vector2(centre.x, centre.y - 22), 6, Mathf.Abs(bearing) < 14 ? Mint : Sky);
            GUI.matrix = matrix;
            Disc(centre, 4, Color.white);
            string direction = Mathf.Abs(bearing) < 14 ? "正前方" : bearing < 0 ? "左转 " + Mathf.Abs(bearing).ToString("0") + "°" : "右转 " + bearing.ToString("0") + "°";
            Label(r.x + 100, r.y + 16, 580, 32, c.NavigationName + "   " + FishingPrototype.FlatDistance(c.Player.transform.position, c.NavigationPosition).ToString("0") + " m", 24, Color.white, true);
            Label(r.x + 101, r.y + 56, 580, 22, direction + "   ·   N 切换航点   ·   提示仅为方位，请沿可通行水道航行", 14, Mint);
        }

        private void DrawWallet()
        {
            var c = Controller;
            var career = c.Career;
            var r = new Rect(1094, 24, 478, 92);
            Card(r, Gold);
            Label(r.x + 22, r.y + 18, 170, 42, career.Coins.ToString("N0", CultureInfo.InvariantCulture), 32, Gold, true);
            Label(r.x + 23, r.y + 62, 170, 20, "金币", 13, Muted);
            bool full = career.Count >= career.Capacity;
            Label(r.x + 196, r.y + 18, 160, 26, "鱼箱 " + career.Count + " / " + career.Capacity, 18, full ? Danger : Color.white, true);
            Bar(new Rect(r.x + 196, r.y + 50, 150, 8), career.Count / (float)career.Capacity, full ? Danger : Mint);
            Label(r.x + 196, r.y + 64, 160, 20, "货值 " + career.HoldValue, 13, Muted);
            Label(r.x + 368, r.y + 18, 100, 26, "委托 " + career.OrdersCompleted, 16, Color.white, true);
            if (c.LegendTrophy) Chip(r.x + 366, r.y + 54, "★ 传说", Gold, true);
            else Label(r.x + 368, r.y + 58, 100, 20, "传说 ？", 13, Faint);
        }

        private void DrawStatus()
        {
            var c = Controller;
            var r = new Rect(28, 130, 330, 70);
            Card(r, c.Anchored ? Gold : Mint);
            string main = c.Anchored ? "捕捞工作站 · " + FishingSpecies.AreaName(c.Spot) : (c.Speed * 3.6f).ToString("0.0") + " km/h";
            Label(r.x + 20, r.y + 15, 300, 30, main, 21, Color.white, true);
            Label(r.x + 20, r.y + 44, 300, 20, (c.FirstPerson ? "第一人称" : "第三人称") + " · " + (c.ComfortMode ? "舒适航行" : "标准动力") + " · 右键环顾", 13, Muted);
        }

        private void DrawToolbar()
        {
            var c = Controller;
            if (c.Paused) return;
            if (Button(new Rect(28, 212, 102, 40), "图鉴 J", true, false, 16)) c.ToggleJournal();
            if (Button(new Rect(142, 212, 102, 40), "设置 Esc", true, false, 16)) c.TogglePause();
            if (Button(new Rect(256, 212, 102, 40), "视角 C", true, false, 16)) c.ToggleView();
        }

        private void DrawClimate()
        {
            var c = Controller;
            var w = c.Weather.Climate;
            var r = new Rect(28, 264, 330, 150);
            Card(r, Sky);
            WeatherIcon(new Rect(r.x + 18, r.y + 22, 48, 48), w.Current);
            Label(r.x + 80, r.y + 20, 240, 30, BayClimate.Name(w.Current), 22, Color.white, true);
            Label(r.x + 80, r.y + 52, 240, 20, FishingSpecies.Name(BayClimate.Favored(w.Current)) + "活跃 · 咬钩更快 · 稀有 +4%", 13, Mint);
            float progress;
            string text;
            if (w.Blend < 1f) { progress = w.Blend; text = "天气过渡中 · " + (w.Blend * 100).ToString("0") + "%"; }
            else if (w.Automatic) { progress = 1f - w.Remaining / BayClimate.Duration(w.Current); text = "下一个：" + BayClimate.Name(w.Forecast) + " · " + w.Remaining.ToString("0") + " 秒"; }
            else { progress = 1f; text = "天气已固定 · 设置中可恢复循环"; }
            Label(r.x + 20, r.y + 88, 290, 20, text, 13, Muted);
            Bar(new Rect(r.x + 20, r.y + 116, 290, 8), progress, Sky);
        }

        private void DrawObserve()
        {
            var c = Controller;
            if (!c.Ecology.Candidate.HasValue || c.Paused) return;
            float y = c.ShowDetails ? 426 : 264;
            if (Button(new Rect(28, y, 330, 44), "T 观察 · " + BayClimate.MarineName(c.Ecology.Candidate.Value), true, true)) c.Ecology.Observe();
        }

        /// <summary>Order card on the right column; returns its bottom edge for the legend card below.</summary>
        private float DrawOrderCard(float y)
        {
            var career = Controller.Career;
            var order = career.CurrentOrder;
            bool ready = FishingOrders.Select(order, career.Inventory, _picked, _perLine);
            var r = new Rect(1242, y, 330, 110 + order.LineCount * 32);
            Card(r, ready ? Mint : Gold);
            Label(r.x + 20, r.y + 18, 290, 18, order.Client, 13, Muted);
            Label(r.x + 20, r.y + 36, 290, 28, "委托 · " + order.Name, 20, Color.white, true);
            DrawOrderLines(order, r.x + 18, r.y + 72, 294);
            Label(r.x + 20, r.y + 78 + order.LineCount * 32, 290, 22, ready ? "已集齐 · 返港按 E 交付" : "奖金 +" + order.Bonus + " · 鱼价另计", 14, ready ? Mint : Gold, true);
            return r.yMax;
        }

        private void DrawOrderLines(FishingOrder order, float x, float y, float width)
        {
            for (int i = 0; i < order.LineCount; i++)
            {
                var line = order.Line(i);
                float ly = y + i * 32;
                bool done = _perLine[i] >= line.Count;
                FishGlyph(new Rect(x, ly, 38, 26), line.Kind, line.Rare);
                Label(x + 46, ly + 3, width - 120, 22, line.Describe(), 15, done ? Mint : Color.white);
                Label(new Rect(x + width - 76, ly + 3, 76, 22), _perLine[i] + " / " + line.Count, 15, done ? Mint : Muted, true, TextAnchor.UpperRight);
            }
        }

        private void DrawLegendCard(float y)
        {
            var c = Controller;
            var career = c.Career;
            var r = new Rect(1242, y, 330, 134);
            Card(r, Gold);
            int clues = 0;
            for (int i = 0; i < LegendRules.ClueCount; i++) if (LegendRules.ClueUnlocked(i, career)) clues++;
            Label(r.x + 20, r.y + 18, 200, 26, career.LegendCatches > 0 ? "传说 · 金帆旗鱼" : "传说 · ？？？", 18, Gold, true);
            Label(new Rect(r.x + 170, r.y + 20, 140, 22), career.LegendCatches > 0 ? "已放生 " + career.LegendCatches + " 次" : "线索 " + clues + " / " + LegendRules.ClueCount, 13, Muted, false, TextAnchor.UpperRight);
            bool inArea = c.Spot == FishingSpecies.LegendArea && (c.Anchored || c.SpotDistance <= 22f);
            float x = r.x + 20;
            x += ConditionChip(x, r.y + 52, 0, "航标水道", inArea) + 6;
            ConditionChip(x, r.y + 52, 1, "金色夕照", c.Weather.Climate.Current == BayWeather.Sunset);
            x = r.x + 20;
            x += ConditionChip(x, r.y + 82, 2, "珍珠饵", c.Bait == BaitKind.Pearl) + 6;
            ConditionChip(x, r.y + 82, 2, "强化鱼线", career.LineLevel >= 1);
            bool ready = inArea && LegendRules.CanStrike(FishingSpecies.LegendArea, c.Weather.Climate.Current, c.Bait, career.LineLevel);
            Label(r.x + 20, r.y + 110, 290, 18, ready ? "条件齐备！在水道抛竿试试运气" : "线索在日志“传说”页查看", 12, ready ? Gold : Faint);
        }

        private float ConditionChip(float x, float y, int clue, string text, bool met)
        {
            if (!LegendRules.ClueUnlocked(clue, Controller.Career)) return Chip(x, y, "？ 未知条件", Faint);
            return Chip(x, y, (met ? "√ " : "") + text, met ? Mint : Muted, met);
        }

        private void DrawHarborDesk()
        {
            var c = Controller;
            var career = c.Career;
            var order = career.CurrentOrder;
            bool ready = FishingOrders.Select(order, career.Inventory, _picked, _perLine);
            var r = new Rect(1122, 130, 450, 600);
            Card(r, Mint);
            float x = r.x + 22, w = r.width - 44;
            Label(x, r.y + 18, w, 30, "潮汐渔港 · 交易台", 22, Color.white, true);
            Label(x, r.y + 50, w, 20, "E 优先交付委托，其次出售并保留委托鱼", 13, Muted);
            float y = r.y + 82;
            Section(x, y, w, "委托 · " + order.Name, order.Client);
            y += 34;
            DrawOrderLines(order, x, y, w);
            y += order.LineCount * 32 + 4;
            if (Button(new Rect(x, y, w, 40), ready ? "E  交付委托 · 奖金 +" + order.Bonus : "委托未集齐 · 奖金 +" + order.Bonus, ready, ready)) c.DeliverOrder();
            y += 56;
            Section(x, y, w, "出售鱼货", "鱼箱 " + career.Count + "/" + career.Capacity + " · 货值 " + career.HoldValue);
            y += 34;
            int sale = career.SaleValue;
            int reserved = career.ReservedCount;
            if (Button(new Rect(x, y, w * 0.5f - 5, 40), "E  出售 +" + sale, sale > 0)) c.Sell();
            if (Button(new Rect(x + w * 0.5f + 5, y, w * 0.5f - 5, 40), "X  全部出售 +" + career.HoldValue, career.Count > 0)) c.SellAll();
            y += 46;
            Label(x, y, w, 20, reserved > 0 ? "已为委托保留 " + reserved + " 条，普通出售不会卖掉它们。" : "没有需要保留的委托鱼。", 13, reserved > 0 ? Mint : Faint);
            y += 32;
            Section(x, y, w, "装备升级", "数字键 1 / 2 / 3");
            y += 36;
            for (int t = 0; t < 3; t++)
            {
                var track = (UpgradeTrack)t;
                int level = career.Level(track);
                int cost = career.NextUpgradeCost(track);
                Label(x, y, 110, 24, FishingText.UpgradeName(track), 17, Color.white, true);
                Pips(x + 76, y + 7, level, FishingCareer.MaxLevel, Gold);
                string effect = FishingText.UpgradeEffect(track, level) + (cost > 0 ? "  →  " + FishingText.UpgradeEffect(track, level + 1) : "");
                Label(x, y + 26, 260, 20, effect, 13, Muted);
                string label = cost > 0 ? "[" + (t + 1) + "]  " + cost + " 金币" : "已满级";
                if (Button(new Rect(x + w - 136, y + 4, 136, 38), label, cost > 0 && career.Coins >= cost, false, 15)) c.BuyUpgrade(track);
                y += 56;
            }
            Label(x, r.yMax - 34, w, 20, "Q / W 起航   ·   N 航点   ·   J 图鉴与传说线索", 13, Faint);
        }

        private void DrawAction()
        {
            var c = Controller;
            var s = c.Session;
            var r = new Rect(386, _height - 214, 828, 164);
            bool legend = c.Anchored && s.Species.Legendary && (s.Phase == CatchPhase.Bite || s.Phase == CatchPhase.Fighting || s.Phase == CatchPhase.Landing);
            Card(r, legend ? Gold : c.Anchored ? Mint : Sky);
            string title, help;
            Color titleColor = Color.white;
            if (!c.Anchored)
            {
                string area = FishingSpecies.AreaName(c.Spot);
                bool near = c.SpotDistance <= 22f;
                title = near ? (c.Speed > 3f ? "进入" + area + " · 先按住 Shift 减速" : "进入 " + area + " · 按 E 停船") : "探索海湾 · 驶向彩色航标";
                help = near ? FishingSpecies.Of(FishingSpecies.AreaSpecies(c.Spot)).Habitat : "一 / 二 / 三条横杠对应三处渔区；不必只靠颜色找路。";
            }
            else
            {
                switch (s.Phase)
                {
                    case CatchPhase.Waiting:
                        title = "等待咬钩" + new string('·', 1 + (int)(Time.unscaledTime * 2f) % 3);
                        help = "留意浮标与声音；出现咬钩提示再点击左键。";
                        break;
                    case CatchPhase.Bite:
                        title = legend ? "金帆旗鱼咬钩！ · 点击左键提竿" : "咬钩了！ · 点击左键提竿";
                        titleColor = Gold;
                        help = "剩余 " + s.Remaining.ToString("0.0") + " 秒 · 及时提竿进入拉锯";
                        break;
                    case CatchPhase.Fighting:
                        title = FightTitle(s);
                        titleColor = s.Beat == FishBeat.Leap || s.Tension > 0.78f ? Danger : Color.white;
                        help = FishingSpecies.Name(s.Kind) + " · 距离 " + s.Distance.ToString("0.0") + " m · 收线与放线交替，避免持续绷紧或松弛";
                        break;
                    case CatchPhase.Landing:
                        title = "抄鱼 · 指针进入绿色区时按 E";
                        help = (legend ? "旗鱼太大，绿色区更窄 · " : "绿色中段是有效区 · ") + "剩余 " + s.Remaining.ToString("0.0") + " 秒";
                        break;
                    case CatchPhase.Caught:
                        title = "捕获成功 · 点击左键再抛一竿";
                        help = "B 换饵 · Q 起航 · 返港在交易台出售或交付委托";
                        break;
                    case CatchPhase.Escaped:
                        title = "鱼逃走了 · 点击左键再试一次";
                        help = FishingText.Failure(s.Failure);
                        break;
                    default:
                        title = FishingSpecies.AreaName(c.Spot) + " · 点击左键抛竿";
                        help = "B 切换鱼饵 · 咬钩后点击提竿 · Q 收竿起航";
                        break;
                }
            }
            Label(r.x + 24, r.y + 18, 780, 34, title, 25, titleColor, true);
            Label(r.x + 25, r.y + 56, 780, 24, help, 16, Muted);
            if (c.Anchored && s.Phase == CatchPhase.Fighting) DrawFightGauges(r, s);
            else if (c.Anchored && s.Phase == CatchPhase.Landing) DrawLandingGauge(r, s);
            else if (c.Anchored && s.Phase == CatchPhase.Bite) Bar(new Rect(r.x + 24, r.y + 118, 780, 10), s.Remaining / 2.5f, Gold);
            else
            {
                float x = r.x + 24;
                x += Chip(x, r.y + 104, "B  " + FishingVoyage.BaitName(c.Bait), Gold, true, 14) + 10;
                Label(x, r.y + 107, 600, 22, FishingVoyage.BaitHelp(c.Bait), 15, Gold);
            }
        }

        private static string FightTitle(CatchSession s)
        {
            if (s.Beat == FishBeat.Leap) return "跃出水面！ · 立刻松开左键";
            if (s.Tension > 0.78f) return "张力过高！ · 松开左键";
            if (s.Beat == FishBeat.Rest) return "鱼在回气 · 按住左键收线";
            if (s.Beat == FishBeat.Windup) return "鱼在蓄力 · 准备松开左键";
            return "鱼在冲刺 · 放线避开拉力";
        }

        private void DrawFightGauges(Rect r, CatchSession s)
        {
            float x = r.x + 24;
            x += BeatChip(x, r.y + 88, "回气 · 收线", s.Beat == FishBeat.Rest, Mint) + 8;
            x += BeatChip(x, r.y + 88, "蓄力 · 准备", s.Beat == FishBeat.Windup, Gold) + 8;
            if (s.Species.Legendary) x += BeatChip(x, r.y + 88, "跃水 · 松开", s.Beat == FishBeat.Leap, Danger) + 8;
            BeatChip(x, r.y + 88, "冲刺 · 放线", s.Beat == FishBeat.Surge, Danger);
            bool high = s.Tension > 0.78f;
            Label(r.x + 24, r.y + 122, 110, 24, "张力 " + (s.Tension * 100).ToString("0") + "%", 16, high ? Danger : Mint, true);
            var bar = new Rect(r.x + 136, r.y + 128, 440, 12);
            Bar(bar, s.Tension, high ? Danger : Mint);
            Fill(bar.x + bar.width * 0.78f - 1, bar.y - 5, 2, 22, Color.white);
            Label(r.x + 596, r.y + 122, 90, 24, "距离", 14, Muted);
            Bar(new Rect(r.x + 640, r.y + 128, 164, 12), 1f - s.Distance / Mathf.Max(0.01f, s.MaxDistance), Sky);
        }

        private float BeatChip(float x, float y, string text, bool active, Color color)
            => Chip(x, y, text, active ? color : Faint, active, 14);

        private void DrawLandingGauge(Rect r, CatchSession s)
        {
            var track = new Rect(r.x + 24, r.y + 112, 780, 14);
            Rounded(track, Track, 7);
            float zone = s.LandZone;
            Rounded(new Rect(track.x + track.width * (0.5f - zone), track.y, track.width * zone * 2f, track.height), s.CanLand ? Mint : new Color(0.37f, 0.93f, 0.77f, 0.55f), 7);
            float px = track.x + track.width * s.LandPointer;
            Rounded(new Rect(px - 4, track.y - 9, 8, 32), Color.white, 4);
        }

        private void DrawLegendBanner()
        {
            var c = Controller;
            var s = c.Session;
            if (!c.Anchored || !s.Species.Legendary || c.Paused) return;
            if (s.Phase != CatchPhase.Bite && s.Phase != CatchPhase.Fighting && s.Phase != CatchPhase.Landing) return;
            bool leap = s.Phase == CatchPhase.Fighting && s.Beat == FishBeat.Leap;
            float pulse = leap ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 18f) : 1f;
            var r = new Rect(520, 130, 560, 52);
            Rounded(new Rect(r.x, r.y + 4, r.width, r.height), CardShadow, 26);
            Rounded(r, leap ? Color.Lerp(Danger, Gold, 0.35f) * new Color(1, 1, 1, pulse) : Gold, 26);
            string sub = s.Phase == CatchPhase.Bite ? "提竿！" : s.Phase == CatchPhase.Landing ? "准备抄网" : leap ? "跃出水面 · 松开！" : "稳住节奏";
            Label(r, "传说怪鱼 · 金帆旗鱼   —   " + sub, 20, InkText, true, TextAnchor.MiddleCenter);
        }

        private void DrawCatchCard()
        {
            var card = Controller.LastCatch;
            if (card.Fish == null) return;
            float age = Time.unscaledTime - card.ShownAt;
            if (age < 0f || age > CardSeconds) return;
            float appear = Mathf.SmoothStep(0f, 1f, age / 0.3f);
            _fade = Mathf.Min(appear, Mathf.Clamp01((CardSeconds - age) / 0.6f));
            var fish = card.Fish;
            var species = FishingSpecies.Of(fish.kind);
            var r = new Rect(470, 128 - (1f - appear) * 18f, 660, 152);
            Card(r, species.Legendary || card.PersonalBest ? Gold : Mint);
            Rounded(new Rect(r.x + 18, r.y + 22, 148, 108), InkRaised, 12);
            FishGlyph(new Rect(r.x + 26, r.y + 36, 132, 80), fish.kind, fish.rare);
            Label(r.x + 186, r.y + 20, 300, 38, species.Name, 28, species.Legendary ? Gold : Color.white, true);
            Label(r.x + 186, r.y + 60, 200, 30, FishingText.Weight(fish.grams), 22, card.PersonalBest ? Gold : Color.white, true);
            // Where this fish sits in its species' size range.
            var scale = new Rect(r.x + 320, r.y + 74, 170, 6);
            Rounded(scale, Track, 3);
            float t = FishingSpecies.SizePermille(fish.kind, fish.grams) / 1000f;
            Disc(new Vector2(scale.x + scale.width * t, scale.center.y), 7, card.PersonalBest ? Gold : Mint);
            Label(scale.x - 2, scale.y + 10, 40, 18, "小", 12, Faint);
            Label(new Rect(scale.xMax - 38, scale.y + 10, 40, 18), "大", 12, Faint, false, TextAnchor.UpperRight);
            Label(r.x + 186, r.y + 102, 320, 24, species.Legendary ? "海洋站奖励 +" + card.Bounty + " 金币 · 已测量放生" : "价值 " + fish.value + " 金币 · 已放入鱼箱", 16, Gold);
            float chipY = r.y + 24;
            if (species.Legendary) { Chip(r.xMax - 160, chipY, "★ 传说放生", Gold, true); chipY += 32; }
            if (card.Discovery) { Chip(r.xMax - 160, chipY, "图鉴新发现", Mint, true); chipY += 32; }
            if (card.PersonalBest) { Chip(r.xMax - 160, chipY, "个人纪录", Gold); chipY += 32; }
            if (fish.rare) Chip(r.xMax - 160, chipY, "珍珠变体", Sky);
            _fade = 1f;
        }

        private void DrawMessage(bool desk)
        {
            var c = Controller;
            string text = c.SaveWarning.Length > 0 ? c.SaveWarning : c.VisibleMessage;
            if (text.Length == 0) return;
            var r = new Rect(386, _height - 272, desk ? 720 : 828, 48);
            Rounded(new Rect(r.x, r.y + 3, r.width, r.height), CardShadow, 12);
            Rounded(r, Ink, 12);
            Rounded(new Rect(r.x + 12, r.y + 12, 4, r.height - 24), c.SaveWarning.Length > 0 ? Danger : Mint, 2);
            Label(new Rect(r.x + 28, r.y, r.width - 40, r.height), text, 16, c.SaveWarning.Length > 0 ? Danger : Color.white, false, TextAnchor.MiddleLeft);
        }

        private void DrawMap()
        {
            var c = Controller;
            float y = _height - 264;
            var r = new Rect(28, y, 330, 214);
            Card(r, Sky);
            Label(r.x + 20, y + 18, 290, 26, "航海图 · N 切换目标", 16, Color.white, true);
            Vector3 min = c.harbor, max = c.harbor;
            foreach (var p in c.fishingSpots) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            min -= Vector3.one * 30;
            max += Vector3.one * 30;
            for (int i = 0; i < 4; i++) Fill(r.x + 24 + i * 94, y + 54, 1, 140, new Color(1, 1, 1, 0.05f));
            for (int i = 0; i < 3; i++) Fill(r.x + 24, y + 58 + i * 64, 282, 1, new Color(1, 1, 1, 0.05f));
            MapMarker(MapPoint(c.harbor, min, max, y), "港", Mint, c.NavigationTarget == 0);
            for (int i = 0; i < c.fishingSpots.Length; i++)
                MapMarker(MapPoint(c.fishingSpots[i], min, max, y), (i + 1).ToString(), i == FishingSpecies.LegendArea && c.LegendTrophy ? Sky : Gold, c.NavigationTarget == i + 1);
            var player = MapPoint(c.Player.transform.position, min, max, y);
            var matrix = GUI.matrix;
            Rotate(c.Player.transform.eulerAngles.y, player);
            Rounded(new Rect(player.x - 3, player.y - 11, 6, 18), Color.white, 3);
            Disc(new Vector2(player.x, player.y - 10), 4.5f, Color.white);
            GUI.matrix = matrix;
        }

        private static Vector2 MapPoint(Vector3 world, Vector3 min, Vector3 max, float y)
            => new Vector2(57 + Mathf.Clamp01((world.x - min.x) / (max.x - min.x)) * 264, y + 190 - Mathf.Clamp01((world.z - min.z) / (max.z - min.z)) * 124);

        private void MapMarker(Vector2 p, string text, Color color, bool target)
        {
            if (target) Outline(new Rect(p.x - 15, p.y - 15, 30, 30), Mint, 15, 2);
            Disc(p, 11, color);
            Label(new Rect(p.x - 11, p.y - 11, 22, 22), text, 13, InkText, true, TextAnchor.MiddleCenter);
        }

        private void DrawWorldLabel(Vector3 world, string text)
        {
            var s = Controller.viewCamera.WorldToScreenPoint(world);
            if (s.z <= 0) return;
            float x = (s.x - _offset) / _scale, y = (Screen.height - s.y) / _scale;
            if (x < 460 || x > 1140 || y < 215 || y > _height - 280) return;
            Chip(x - 60, y, "◆ " + text, Mint, true, 15);
        }

        private void DrawKeyBar(bool desk)
        {
            var c = Controller;
            string keys = desk ? "E 交付/出售    X 全部出售    1 / 2 / 3 升级    Q / W 起航    N 航点    J 图鉴    Esc 设置"
                : c.Anchored ? "左键 抛竿 / 提竿 / 按住收线    E 抄鱼    B 换饵    Q 起航    C 视角    V 视线回正    J 图鉴    Esc 设置"
                : "W 前进    A/D 转向    S/Shift 刹车    E 停船/交易    N 航点    J 图鉴    T 观察    R 救援    H 简洁    Esc 设置";
            var r = new Rect(28, _height - 40, 1544, 30);
            Rounded(r, Ink, 15);
            Label(r, keys, 14, Muted, false, TextAnchor.MiddleCenter);
        }

        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
