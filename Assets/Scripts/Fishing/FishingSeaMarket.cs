using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public enum MarketGoods { Bait, Seeds, Souvenir, Keepsake, Buyback, Letter }

    /// <summary>Unity-free market-ship and letter-post rules. Bits live in LoreData.seaPost / seaPostDay.</summary>
    public static class FishingSeaMarket
    {
        public const int SouvenirBit = 0, KeepsakeBit = 4, ReplyBit = 8, PendingShift = 12, PostLimit = 1 << 15, ReplyDays = 2;
        public const int BaitPrice = 40, SeedPrice = 30, SouvenirPrice = 120, KeepsakePrice = 60;
        public const string Owner = "潮商·阿汛";
        /// <summary>Saturdays and festival days, 08:00–19:00.</summary>
        public static bool Open(int day, float hour) => (day % 7 == 5 || FishingFestival.On(day) >= 0) && hour >= 8 && hour < 19;
        /// <summary>The ship moors off a different ground each week.</summary>
        public static int Ground(int day) => day / 7 % 3;
        public static readonly string[] BaitNames = { "樱潮珍珠饵", "萤光饵", "枫汐饵", "雪灯饵" };
        public static readonly string[] SouvenirNames = { "海鸥羽笔", "夜光螺", "旧航海铜铃", "守灯人的火柴盒" };
        public static readonly string[] SouvenirNotes =
        {
            "阿汛说，北边港口的人用海鸥掉下的羽毛写信。“写给谁都行，海会帮你送。”",
            "贴在耳边，能听见很远很远的潮声。阿汛说这是夜光海退潮时留下的。",
            "一只生了绿锈的船铃。阿汛说是在沉船沙洲外捞的，“摇一摇，港口就知道你回来了”。",
            "火柴盒上印着一座灯塔。里面只剩一根火柴，阿汛说：“留着，等哪天灯真的灭了再用。”",
        };
        // story-reviewed cards for the festival keepsakes, matching KeepsakeNames.
        public static readonly string[] KeepsakeNotes =
        {
            "阿汛说，北边港口的孩子把信写在风筝上放出去。“线断了也没关系，海会帮你送。”",
            "夜里会发淡蓝的光。阿汛说这是夜光海退潮时捡的，“挂在船尾，回港的人一眼就能找到你”。",
            "篮底编进了一圈红线。阿汛说，渔村的规矩是丰收那天，第一篮鱼要先送给守灯的人。",
            "灯笼上画着一座小灯塔。阿汛说：“留着，等哪天灯真的灭了，就先点这一盏。”",
        };
        /// <summary>The unsigned fourth reply quotes the finale, so it only drifts back after the grand treasure.</summary>
        public static bool ReplyReady(int phrase, FishingLore lore) => phrase != 3 || lore.GrandTreasureFound;
        public static readonly string[] KeepsakeNames = { "风筝", "夜光浮漂", "丰收篮", "纸灯笼" };
        public static readonly string[] Phrases = { "灯还亮着", "我回来了", "今天的海很安静", "有人在等你吗" };
        public static readonly string[] ReplySenders = { "老舵手·林舟", "铃音", "望星", "无署名" };
        public static readonly string[] Replies =
        {
            "收到你的瓶子了。“灯还亮着”——我在港口念了三遍。\n\n这么多年，头一回是别人告诉我灯亮着。\n\n——林舟",
            "“我回来了。”\n\n风铃修好以后，我每天都在等这句话。原来不是在等某一个人，是在等有人愿意说出口。\n\n——铃音",
            "安静的海最适合看星星。那晚我在望远镜里看见一只瓶子漂过星光，原来是你的。\n\n——望星",
            "有。\n\n一直都有。\n\n（纸背面是一行褪色的字：换你来答。）",
        };
        /// <summary>Every letter the player has read, in reading-order groups, for the journal's re-read page.</summary>
        public static List<(string title, string sender, string body)> Archive(FishingLore lore)
        {
            var list = new List<(string, string, string)>();
            for (int i = 0; i < FishingSeaLetters.Count; i++)
                if (lore.SeaFlag(FishingSeaLetters.LetterBit + i)) list.Add((FishingSeaLetters.Titles[i], FishingSeaLetters.Senders[i], FishingSeaLetters.Bodies[i]));
            for (int i = 0; i < Phrases.Length; i++)
                if (Bit(lore.SeaPost, ReplyBit + i) == 1) list.Add(("回信 · “" + Phrases[i] + "”", ReplySenders[i], Replies[i]));
            for (int i = 0; i < 4; i++)
                if (Bit(lore.SeaPost, SouvenirBit + i) == 1) list.Add((SouvenirNames[i], Owner, SouvenirNotes[i]));
            for (int i = 0; i < 4; i++)
                if (Bit(lore.SeaPost, KeepsakeBit + i) == 1) list.Add((KeepsakeNames[i], Owner, KeepsakeNotes[i]));
            return list;
        }
        public static int Pending(int post) => (post >> PendingShift) & 7;
        public static bool ReplyDue(int post, int sentDay, int today) => Pending(post) > 0 && today >= sentDay + ReplyDays;
        public static int Bit(int post, int bit) => (post >> bit) & 1;

        public static string Name(MarketGoods g, int season, int festival) => g == MarketGoods.Bait ? BaitNames[season] : g == MarketGoods.Seeds ? (season < 2 ? "萝卜种子 ×3" : "鱼食 ×3")
            : g == MarketGoods.Souvenir ? SouvenirNames[season] : g == MarketGoods.Keepsake ? (festival >= 0 ? KeepsakeNames[festival] : "") : g == MarketGoods.Buyback ? "收购稀有鱼 ×2 价" : "写一封漂流信";
        public static int Price(MarketGoods g) => g == MarketGoods.Bait ? BaitPrice : g == MarketGoods.Seeds ? SeedPrice : g == MarketGoods.Souvenir ? SouvenirPrice : g == MarketGoods.Keepsake ? KeepsakePrice : 0;
    }

    /// <summary>The roaming market ship: lanterns and flags guide from afar, music near, gaze-and-press to trade.</summary>
    public sealed class FishingSeaMarketWorld : MonoBehaviour
    {
        private FishingPrototype _c;
        private readonly List<Object> _owned = new List<Object>();
        private Transform _ship, _marker;
        private readonly List<(MarketGoods goods, int phrase, Transform slot, TextMesh label)> _stalls = new List<(MarketGoods, int, Transform, TextMesh)>();
        private int _shownDay = -1, _shownSeason = -1, _shownFestival = -2, _baitDay = -1;
        public bool Visible => _ship != null && _ship.gameObject.activeSelf;
        public Vector3 Position => _ship.position;
        public int Trades { get; private set; }
        public FishingResidentBrain Merchant { get; private set; }
        private bool _greeted;
        public MarketGoods? Target { get; private set; }
        public int TargetPhrase { get; private set; }
        /// <summary>Tests force the market open regardless of weekday.</summary>
        public bool ForceOpen;

        public void Build(FishingPrototype c)
        {
            _c = c;
            var palette = new Material(c.PaletteShader) { name = "Market ship palette" }; _owned.Add(palette);
            var glow = new Material(Resources.Load<Shader>("FishingNightSky")) { name = "Market lantern glow" }; _owned.Add(glow);
            glow.SetColor("_Tint", new Color(1.8f, .9f, .35f)); glow.SetFloat("_Solid", 1); glow.SetFloat("_Visibility", 1);
            _ship = new GameObject("V044 roaming market ship").transform; _ship.SetParent(transform, false);
            // scene-art's AI ship with named mount points wins; the procedural hull is the fallback.
            var model = Resources.Load<GameObject>("FishingV044/Sea/MarketShip");
            if (model != null) { var m = Instantiate(model, _ship); m.name = "Market ship model"; FishingV035Materials.Apply(m.transform); m.transform.localPosition = Vector3.down * 1.1f; } // ride deeper so the rail sits below a seated skiff eye line
            else
            {
                var hull = new GameObject("Market ship hull", typeof(MeshFilter), typeof(MeshRenderer)); hull.transform.SetParent(_ship, false);
                var mesh = ShipMesh(); _owned.Add(mesh); hull.GetComponent<MeshFilter>().sharedMesh = mesh;
                var hr = hull.GetComponent<MeshRenderer>(); hr.sharedMaterial = palette; hr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Vector3 Mount(string name, Vector3 fallback)
            {
                foreach (var t in _ship.GetComponentsInChildren<Transform>()) if (t.name == name) return _ship.InverseTransformPoint(t.position);
                return fallback;
            }
            Vector3 lanternA = Mount("LanternA", new Vector3(0, 8.6f, 4)), lanternB = Mount("LanternB", new Vector3(0, 8.6f, -4));
            // Lantern strings between the masts and a tall lantern visible across the bay.
            for (int i = 0; i < 9; i++)
            {
                float u = i / 8f; var p = Vector3.Lerp(lanternA, lanternB, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 1.4f;
                Lamp(glow, p, .32f);
            }
            Lamp(glow, lanternA + Vector3.up * 3.8f, .9f);
            _marker = Lamp(glow, Vector3.zero, .14f); _marker.name = "Gazed goods marker";
            var sb = new FishingMeshBuilder();
            sb.Tube(new Vector3(0, -.18f, 0), new Vector3(0, .18f, 0), .34f, .34f, new Color(.6f, .42f, .26f), 4);
            sb.Ellipsoid(new Vector3(0, .24f, 0), new Vector3(.2f, .1f, .2f), new Color(.95f, .78f, .4f), 10, 5);
            var stallMesh = sb.Finish("Market stall crate"); _owned.Add(stallMesh);
            var goods = new[] { MarketGoods.Bait, MarketGoods.Seeds, MarketGoods.Souvenir, MarketGoods.Keepsake, MarketGoods.Buyback, MarketGoods.Letter, MarketGoods.Letter, MarketGoods.Letter, MarketGoods.Letter };
            for (int i = 0; i < goods.Length; i++)
            {
                var slot = new GameObject("Stall " + goods[i] + " " + i).transform; slot.SetParent(_ship, false);
                // Five goods along the port rail; four letter slips on the stern table.
                slot.localPosition = i < 5 ? Mount("Stall" + i, new Vector3(-2.3f, 2.1f, 3.4f - i * 1.35f)) : Mount("Letter" + (i - 5), new Vector3(-1.6f + (i - 5) * 1.05f, 2.1f, -6.6f));
                // The AI ship already carries its stalls; extra crates would sink into its counters.
                if (model == null)
                {
                    var crate = new GameObject("Stall crate", typeof(MeshFilter), typeof(MeshRenderer)); crate.transform.SetParent(slot, false);
                    crate.GetComponent<MeshFilter>().sharedMesh = stallMesh; crate.GetComponent<MeshRenderer>().sharedMaterial = palette;
                }
                var label = FishingV019Art.Sign("", slot, new Vector3(0, .8f, 0), .02f, new Color(.95f, .9f, .78f));
                label.transform.localRotation = Quaternion.Euler(0, -90, 0);
                if (i >= 5) label.transform.localRotation = Quaternion.Euler(0, 180, 0);
                _stalls.Add((goods[i], i - 5, slot, label));
            }
            FishingV019Art.Sign(FishingSeaMarket.Owner + " · 海上集市", _ship, new Vector3(-2.4f, 4.2f, 0), .03f, new Color(.95f, .9f, .78f)).transform.localRotation = Quaternion.Euler(0, -90, 0);
            // scene-art opened a port-side window in front of this mount so traders see her from the skiff.
            var owner = Mount("Owner", new Vector3(-1.2f, 1.95f, 0.5f));
            // Stand the merchant on the deck surface under the mount, not inside counters.
            var probe = _ship.TransformPoint(owner + Vector3.up * 2);
            if (Physics.Raycast(probe, Vector3.down, out var deckHit, 4, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore)) owner = _ship.InverseTransformPoint(deckHit.point);
            Merchant = FishingResidents.SpawnMerchant(c, _ship, owner, Quaternion.LookRotation(Vector3.left));
            _ship.gameObject.SetActive(false);
        }
        private Transform Lamp(Material m, Vector3 local, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_ship, false); go.transform.localPosition = local; go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; return go.transform;
        }
        private static Mesh ShipMesh()
        {
            var b = new FishingMeshBuilder(); var wood = new Color(.42f, .26f, .16f); var deck = new Color(.74f, .58f, .38f);
            var red = new Color(.78f, .24f, .18f); var cream = new Color(.93f, .87f, .72f); var teal = new Color(.2f, .55f, .55f); var gold = new Color(.86f, .66f, .3f);
            b.Ellipsoid(new Vector3(0, .5f, 0), new Vector3(3.1f, 1.5f, 8.5f), wood, 24, 12);
            b.Ellipsoid(new Vector3(0, 1.25f, 0), new Vector3(3.05f, .12f, 8.3f), cream, 24, 4);
            b.Ellipsoid(new Vector3(0, 1.75f, 0), new Vector3(2.7f, .18f, 7.6f), deck, 18, 4);
            b.Tube(new Vector3(0, 1.8f, -4.2f), new Vector3(0, 3.6f, -4.2f), 1.6f, 1.6f, red, 4);
            b.Tube(new Vector3(0, 3.6f, -4.2f), new Vector3(0, 4.4f, -4.2f), 1.9f, .2f, cream, 4);
            foreach (float z in new[] { 4f, -4f }) b.Tube(new Vector3(0, 1.8f, z), new Vector3(0, 12.5f, z), .14f, .08f, wood, 8);
            // Awning over the stalls and a string of pennants.
            b.Fin(new Vector3(-3, 3.4f, 4.2f), new Vector3(-3, 3.4f, -2.4f), new Vector3(-1, 4.1f, -2.4f), teal);
            b.Fin(new Vector3(-3, 3.4f, 4.2f), new Vector3(-1, 4.1f, -2.4f), new Vector3(-1, 4.1f, 4.2f), teal);
            for (int i = 0; i < 10; i++)
            {
                float z = 4 - i * .8f, y = 11.5f - Mathf.Sin(i / 9f * Mathf.PI) * 1.2f;
                var c = i % 3 == 0 ? red : i % 3 == 1 ? gold : teal;
                b.Fin(new Vector3(0, y, z), new Vector3(0, y, z - .7f), new Vector3(0, y - .7f, z - .35f), c);
            }
            b.Fin(new Vector3(0, 12.3f, -4), new Vector3(0, 11.3f, -4), new Vector3(0, 11.8f, -6.2f), red);
            return b.Finish("Roaming market ship");
        }

        private int Festival => FishingFestival.On(_c.Career.Calendar.Day);
        private bool ShouldShow => !_c.Realms.Inside && (ForceOpen || FishingSeaMarket.Open(_c.Career.Calendar.Day, _c.Career.Calendar.Hour));
        private void Update()
        {
            if (_c == null || _c.Career == null || _c.Player == null) return;
            bool show = ShouldShow;
            if (show != Visible) { _ship.gameObject.SetActive(show); if (show) Moor(); }
            if (!show) { Target = null; return; }
            var cal = _c.Career.Calendar;
            if (_shownDay != cal.Day) Moor();
            if (_shownSeason != cal.Season || _shownFestival != Festival) Relabel();
            var p = _ship.position; p.y = Mathf.Sin(Time.time * .7f) * .08f; _ship.position = p;
            _ship.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * .5f) * 1.2f, _ship.localEulerAngles.y, Mathf.Sin(Time.time * .6f) * 1.5f);
            Gaze();
        }
        private void Moor()
        {
            _shownDay = _c.Career.Calendar.Day;
            var spot = _c.fishingSpots[FishingSeaMarket.Ground(_shownDay)];
            Vector3 best = new Vector3(spot.x, 0, spot.z + 34);
            for (int i = 0; i < 24; i++)
            {
                var q = spot + Quaternion.Euler(0, i % 8 * 45 + 20, 0) * Vector3.forward * (34 + i / 8 * 16); q.y = 0;
                // Clear of reefs and rocks across the whole hull footprint.
                bool clear = FishingPrototype.FlatDistance(q, _c.harbor) > 70 && (_c.TackleShop == null || FishingPrototype.FlatDistance(q, _c.TackleShop.Position) > 60);
                if (clear && FishingSeaEventWorld.OpenWater(q, 12)) { best = q; break; }
            }
            _ship.position = best; _ship.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, spot - best).normalized);
            Relabel();
        }
        private void Relabel()
        {
            var cal = _c.Career.Calendar; _shownSeason = cal.Season; _shownFestival = Festival;
            foreach (var s in _stalls)
            {
                bool offered = s.goods != MarketGoods.Keepsake || _shownFestival >= 0;
                s.slot.gameObject.SetActive(offered);
                int price = FishingSeaMarket.Price(s.goods);
                s.label.text = FishingSeaMarket.Name(s.goods, _shownSeason, _shownFestival) + (price > 0 ? "\n" + price + " 金币" : s.goods == MarketGoods.Letter ? "\n“" + FishingSeaMarket.Phrases[s.phrase] + "”" : "");
            }
        }
        private bool InReach => !_c.Ashore && Visible && FishingPrototype.FlatDistance(_c.Player.transform.position, _ship.position) < 14 && _c.Speed < 2.5f;
        private void Gaze()
        {
            Target = null; _marker.gameObject.SetActive(false);
            if (!InReach) { if (_greeted && Merchant != null) Merchant.ShowBark("bye"); _greeted = false; return; }
            if (!_greeted && Merchant != null) { _greeted = true; Merchant.ShowBark(Random.value < .5f ? "hail_1" : "season_" + _c.Career.Calendar.Season); }
            var eye = _c.viewCamera.transform; float best = .97f;
            foreach (var s in _stalls)
            {
                if (!s.slot.gameObject.activeSelf) continue;
                float dot = Vector3.Dot(eye.forward, (s.slot.position - eye.position).normalized);
                if (dot > best) { best = dot; Target = s.goods; TargetPhrase = s.phrase; _marker.position = s.slot.position + Vector3.up * 1.35f; }
            }
            if (Target == null) { best = -1; foreach (var s in _stalls) if (s.slot.gameObject.activeSelf) { float dot = Vector3.Dot(eye.forward, (s.slot.position - eye.position).normalized); if (dot > best) { best = dot; Target = s.goods; TargetPhrase = s.phrase; _marker.position = s.slot.position + Vector3.up * 1.35f; } } }
            _marker.gameObject.SetActive(true);
        }

        /// <summary>Interact hook: true when the press was spent on the market.</summary>
        public bool TryInteract() { if (!InReach || Target == null) return false; Trade(Target.Value, TargetPhrase); return true; }
        public bool Trade(MarketGoods g, int phrase = 0)
        {
            var career = _c.Career; var lore = career.Lore; var cal = career.Calendar; int season = cal.Season, festival = Festival;
            string text; bool ok = true;
            switch (g)
            {
                case MarketGoods.Bait:
                    if (_baitDay == cal.Day) { text = "今天已经用上" + FishingSeaMarket.BaitNames[season] + "了。"; ok = false; break; }
                    ok = career.SpendSeaCoins(FishingSeaMarket.BaitPrice); if (ok) _baitDay = cal.Day;
                    text = ok ? FishingSeaMarket.BaitNames[season] + "：今天之内抛竿更容易遇到稀有鱼。" : "金币不够。"; break;
                case MarketGoods.Seeds:
                    var item = season < 2 ? FieldItem.TurnipSeeds : FieldItem.FishFeed;
                    ok = career.CanAfford(FishingSeaMarket.SeedPrice) && career.Backpack.TryAdd(item, 3) && career.SpendSeaCoins(FishingSeaMarket.SeedPrice);
                    text = ok ? "买下 " + FishingSeaMarket.Name(g, season, festival) + "。" : "金币不够，或者背包满了。"; break;
                case MarketGoods.Souvenir:
                case MarketGoods.Keepsake:
                    int bit = g == MarketGoods.Souvenir ? FishingSeaMarket.SouvenirBit + season : FishingSeaMarket.KeepsakeBit + festival;
                    string name = FishingSeaMarket.Name(g, season, festival);
                    if (FishingSeaMarket.Bit(lore.SeaPost, bit) == 1) { text = name + "已经收藏了。错过的季节，下次再来。"; ok = false; break; }
                    ok = career.SpendSeaCoins(FishingSeaMarket.Price(g)) && lore.SetSeaPost(lore.SeaPost | 1 << bit, lore.SeaPostDay);
                    text = ok ? "收藏纪念品：" + name : "金币不够。";
                    if (ok && _c.Letter != null && _c.Letter.CanRead) _c.Letter.Show(name, FishingSeaMarket.Owner, g == MarketGoods.Souvenir ? FishingSeaMarket.SouvenirNotes[season] : FishingSeaMarket.KeepsakeNotes[festival]);
                    break;
                case MarketGoods.Buyback:
                    int paid = career.SellRareToMarket(); ok = paid > 0; if (ok) _c.RefreshHoldForSea();
                    text = ok ? "阿汛收下稀有鱼 +" + paid + " 金币（双倍价）。" : "船舱里没有稀有鱼。阿汛只收珍珠个体。"; break;
                default:
                    if (FishingSeaMarket.Pending(lore.SeaPost) > 0) { text = "上一封信还在海上漂着，过几天再看看漂流瓶。"; ok = false; break; }
                    ok = lore.SetSeaPost((lore.SeaPost & ~(7 << FishingSeaMarket.PendingShift)) | (Mathf.Clamp(phrase, 0, 3) + 1) << FishingSeaMarket.PendingShift, cal.Day);
                    text = ok ? "把“" + FishingSeaMarket.Phrases[Mathf.Clamp(phrase, 0, 3)] + "”塞进瓶子，放进了海里。" : "";
                    if (ok) _c.Effects?.Splash(_ship.position + _ship.right * -3.5f, .6f);
                    break;
            }
            if (ok && Merchant != null) Merchant.ShowBark(Trades % 2 == 0 ? "deal_1" : "deal_2");
            if (ok) { Trades++; _c.Save(); _c.WorldAudio?.Play("SeaMarketTrade", _c.viewCamera.transform.position + _c.viewCamera.transform.forward * 2, .6f); _c.Effects?.Celebrate(_marker.position); }
            if (!FishingSeaEventWorld.Vr && text.Length > 0) _c.Notice(text, 6);
            Relabel();
            return ok;
        }
        public bool BaitActive => _baitDay == _c.Career.Calendar.Day;
        public void Hide() { _ship.gameObject.SetActive(false); }
        private void OnDestroy() { foreach (var o in _owned) if (o != null) Destroy(o); }
    }

    public sealed partial class FishingCareer
    {
        public bool SpendSeaCoins(int amount)
        {
            return SpendCoins(amount);
        }
        /// <summary>Sells every pearl-variant catch at double value; returns coins paid.</summary>
        public int SellRareToMarket()
        {
            int paid = 0;
            for (int i = _inventory.Count - 1; i >= 0; i--)
                if (_inventory[i].rare && Coins + paid <= 100000000 - _inventory[i].value * 2) { paid += _inventory[i].value * 2; _inventory.RemoveAt(i); }
            Coins += paid; return paid;
        }
    }

    public sealed partial class FishingPrototype
    {
        internal void RefreshHoldForSea() => RefreshHold();
    }
}
