using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Edge-anchored desktop HUD. Details and trading are explicitly opened, never permanent scenery.</summary>
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
        private FocusLayout Layout => new FocusLayout(_height, Controller.Anchored, Controller.Session.Phase);
        public static float LayoutScale(int width, int height) => Mathf.Max(0.01f, Mathf.Min(width / 1600f, height / 900f));
        public int JournalPage { get => _journalPage; set => _journalPage = Mathf.Clamp(value, 0, JournalTabs.Length - 1); }
        private float CatchAge => Controller.Acceptance != null && Controller.Acceptance.KeepCatchCardVisible ? 1f : Time.unscaledTime - Controller.LastCatch.ShownAt;
        private bool CatchVisible => Controller.LastCatch.Fish != null && CatchAge < CardSeconds;

        public bool BlocksPointer(Vector2 screen, int width, int height)
            => Controller.StoryBlocksGameplay || Controller.ShopBlocksGameplay || Controller.InventoryOpen || BlocksToolPointer(screen, width, height, Controller.ChartExpanded, Controller.ShowDetails,
                Controller.HarborDeskOpen && Controller.AtHarbor, Controller.AtHarbor,
                Controller.Ecology.Candidate.HasValue && !CatchVisible, Controller.Acceptance != null);

        public static bool BlocksToolPointer(Vector2 screen, int width, int height, bool chartExpanded = true,
            bool details = false, bool harbor = false, bool atHarbor = false, bool observe = false, bool acceptance = false)
        {
            float scale = LayoutScale(width, height), offset = (width - 1600 * scale) * .5f;
            var point = new Vector2((screen.x - offset) / scale, (height - screen.y) / scale);
            var layout = new FocusLayout(height / scale, false, CatchPhase.Idle);
            return HotbarRect(height/scale).Contains(point) || layout.Toolbar.Contains(point) || (chartExpanded ? layout.Chart : layout.ChartTab).Contains(point)
                || (observe && layout.Observe.Contains(point)) || (atHarbor && layout.HarborButton.Contains(point))
                || (details && new Rect(1242, 130, 330, 422).Contains(point))
                || (harbor && new HarborLayout(3).Panel.Contains(point))
                || (acceptance && AcceptanceBanner.Contains(point));
        }

        private void Awake() => _font = FishingUIFont.Font;

        private void OnGUI()
        {
            var c = Controller;
            if (c == null || c.Career == null || c.Player == null || XRSettings.isDeviceActive) return;
            if(c.Prologue.Open || c.Home.Sleeping)return;
            if(Event.current.rawType==EventType.MouseDown)_pressedButton=default;
            DrawOpticalView();
            _scale = LayoutScale(Screen.width, Screen.height);
            _height = Screen.height / _scale;
            _offset = (Screen.width - 1600 * _scale) * .5f;
            var matrix = GUI.matrix;
            var color = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(_offset, 0, 0), Quaternion.identity, new Vector3(_scale, _scale, 1));
                if (_label == null) _label = new GUIStyle(GUI.skin.label) { font = _font, wordWrap = true, richText = false, padding = new RectOffset() };
                _fade = 1f;
                if (c.Letter.Open) DrawLetter();
                else if (c.DialogueOpen) DrawDialogue();
                else if (c.ShopOpen) DrawTackleShop();
                else if (c.InventoryOpen) { DrawInventory(); DrawInventoryNotice(); }
                else if (c.JournalOpen) DrawJournal();
                else if (c.Paused) DrawPause();
                else
                {
                    DrawCompactHeading();
                    DrawPocket();
                    DrawCompactToolbar();
                    DrawChart();
                    if (c.HarborDeskOpen && c.AtHarbor) DrawHarborDesk();
                    else if (c.ShowDetails) DrawLegendCard(DrawOrderCard(130) + 14);
                    DrawFocusedAction();
                    DrawCatchToast();
                    DrawCompactNotice();
                    DrawHotbar();
                    DrawHuntingAim();
                    DrawExplorationStatus();
                    DrawRegionTitle();
                }
                if(c.Realms!=null && c.Realms.Blackout>0)Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.008f,.016f,.025f,c.Realms.Blackout));
                if (c.Acceptance != null) DrawAcceptanceToolbar();
            }
            finally { GUI.matrix = matrix; GUI.color = color; }
        }

        private float DrawOrderCard(float y)
        {
            var career = Controller.Career;
            var order = career.CurrentOrder;
            bool ready = FishingOrders.Select(order, career.Inventory, _picked, _perLine);
            var r = new Rect(1242, y, 330, 110 + order.LineCount * OrderLineHeight);
            Card(r, ready ? Mint : Gold);
            Label(r.x + 20, r.y + 18, 290, 18, order.Client, 13, Muted);
            Label(r.x + 20, r.y + 36, 290, 28, "委托 · " + order.Name, 20, Color.white, true);
            DrawOrderLines(order, r.x + 18, r.y + 72, 294);
            Label(r.x + 20, r.y + 78 + order.LineCount * OrderLineHeight, 290, 22, ready ? "已集齐 · 返港按 E 交付" : "奖金 +" + order.Bonus + " · 鱼价另计", 14, ready ? Mint : Gold, true);
            return r.yMax;
        }

        private void DrawOrderLines(FishingOrder order, float x, float y, float width)
        {
            for (int i = 0; i < order.LineCount; i++)
            {
                var line = order.Line(i);
                float ly = y + i * OrderLineHeight;
                bool done = _perLine[i] >= line.Count;
                FishGlyph(new Rect(x, ly, 38, 26), line.Kind, line.Rare);
                Label(x + 46, ly + 1, width - 120, 22, FishingSpecies.Name(line.Kind) + " ×" + line.Count, 15, done ? Mint : Color.white);
                string requirement = line.MinGrams > 0 ? "至少 " + FishingText.Weight(line.MinGrams) : "普通 / 珍珠均可";
                if (line.Rare) requirement = line.MinGrams > 0 ? requirement + " · 珍珠变体" : "仅珍珠变体";
                Label(x + 46, ly + 22, width - 52, 20, requirement, 14, done ? Mint : Muted);
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
            Label(r.x + 20, r.y + 110, 290, 18, ready ? (FishingSurvey.LegendCoordinates(career)?"调查坐标已定位 · 此处抛竿必遇传说":"条件齐备！在水道抛竿试试运气") : "J 图鉴调查可解锁传说精确坐标", 12, ready ? Gold : Faint);
        }

        private float ConditionChip(float x, float y, int clue, string text, bool met)
        {
            if (!LegendRules.ClueUnlocked(clue, Controller.Career)) return Chip(x, y, "？ 未知条件", Faint);
            return Chip(x, y, (met ? "√ " : "") + text, met ? Mint : Muted, met);
        }

        private bool _harborBoatUpgrades;
        private void DrawHarborDesk()
        {
            var c = Controller;
            var career = c.Career;
            var order = career.CurrentOrder;
            bool ready = FishingOrders.Select(order, career.Inventory, _picked, _perLine);
            var layout = new HarborLayout(order.LineCount);
            var r = layout.Panel;
            Card(r, Mint);
            float x = r.x + 22, w = r.width - 44;
            Label(x, r.y + 18, w, 30, "潮汐渔港 · 交易台", 22, Color.white, true);
            Label(x, r.y + 50, w, 20, "E 优先交付委托，其次出售并保留委托鱼", 13, Muted);
            float y = r.y + 82;
            Section(x, y, w, "委托 · " + order.Name, order.Client);
            DrawOrderLines(order, x, layout.OrderY, w);
            y = layout.Delivery.y;
            if (Button(new Rect(x, y, w, 40), ready ? "E  交付委托 · 奖金 +" + order.Bonus : "委托未集齐 · 奖金 +" + order.Bonus, ready, ready)) c.DeliverOrder();
            y = layout.SaleHeadingY;
            Section(x, y, w, "出售鱼货", "鱼箱 " + career.Count + "/" + career.Capacity + " · 货值 " + career.HoldValue);
            y = layout.Sale.y;
            int sale = career.SaleValue;
            int reserved = career.ReservedCount;
            if (Button(new Rect(x, y, w * 0.5f - 5, 40), "E  出售 +" + sale, sale > 0)) c.Sell();
            if (Button(new Rect(x + w * 0.5f + 5, y, w * 0.5f - 5, 40), "X  全部出售 +" + career.HoldValue, career.Count > 0)) c.SellAll();
            y = layout.ReservedY;
            Label(x, y, w, 20, reserved > 0 ? "已为委托保留 " + reserved + " 条，普通出售不会卖掉它们。" : "没有需要保留的委托鱼。", 13, reserved > 0 ? Mint : Faint);
            y = layout.UpgradeHeadingY;
            Section(x, y, w, _harborBoatUpgrades?"船舶升级":"钓具升级", "点击下方按钮升级");
            if(Button(new Rect(x+w-170,y,170,28),_harborBoatUpgrades?"切换钓具":"切换船舶",true,false,13))_harborBoatUpgrades=!_harborBoatUpgrades;
            for (int t = 0; t < 3; t++)
            {
                y = layout.UpgradeY(t);
                var track = (UpgradeTrack)(t+(_harborBoatUpgrades?3:0));
                int level = career.Level(track);
                int cost = career.NextUpgradeCost(track);
                Label(x, y, 110, 24, FishingText.UpgradeName(track), 17, Color.white, true);
                Pips(x + 76, y + 7, level, FishingCareer.MaxLevel, Gold);
                string effect = FishingText.UpgradeEffect(track, level) + (cost > 0 ? "  →  " + FishingText.UpgradeEffect(track, level + 1) : "");
                Label(x, y + 26, 260, 20, effect, 13, Muted);
                bool locked=career.SpecializationBlocked(track);
                string label = locked ? "专精互斥" : cost > 0 ? cost + " 金币" : "已满级";
                if (Button(new Rect(x + w - 136, y + 4, 136, 38), label, !locked && cost > 0 && career.Coins >= cost, false, 15)) c.BuyUpgrade(track);
            }
            if(Button(layout.Footer,c.Expedition.PortLabel,true,false,13))c.Expedition.PortAction();
        }

        private static string FightTitle(CatchSession s)
        {
            if (s.Beat == FishBeat.Leap) return "跃出水面！ · 立刻松开左键";
            if (s.Tension > 0.78f) return "张力过高！ · 松开左键";
            if (s.Beat == FishBeat.Rest) return s.Species.Legendary && s.FightTime>=10 ? "短暂回气 · 收线并防备反扑" : "鱼在回气 · 按住左键收线";
            if (s.Beat == FishBeat.Windup) return "鱼在蓄力 · 准备松开左键";
            return "鱼在冲刺 · 放线避开拉力";
        }

        private void OnDestroy() { ReleaseTerrainMap(); ReleaseOptics(); }
    }
}
