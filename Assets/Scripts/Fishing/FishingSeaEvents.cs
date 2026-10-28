using System;

namespace BoatAttack.Fishing
{
    public enum SeaEventKind { Shoal, Bottle, Fog, Whales, Glow }

    public struct SeaConditions
    {
        public bool AtSea, Night, Mist, Rain, GlowNight, NearHarbor;
    }

    /// <summary>Unity-free, seeded event scheduler: the same seed and conditions always produce the same events.</summary>
    public sealed class FishingSeaSchedule
    {
        public const int KindCount = 5;
        public const float FirstDelay = 60, GlobalCooldown = 150, RollInterval = 20;
        private readonly Random _random;
        private readonly float[] _cooldown = new float[KindCount];
        private float _global = FirstDelay, _roll;
        public SeaEventKind? Active { get; private set; }
        public float Remaining { get; private set; }
        public FishingSeaSchedule(int seed) { _random = new Random(seed); }

        public static float Duration(SeaEventKind k) => k == SeaEventKind.Shoal ? 60 : k == SeaEventKind.Bottle ? 180 : k == SeaEventKind.Fog ? 110 : k == SeaEventKind.Whales ? 75 : 300;
        public static float Cooldown(SeaEventKind k) => k == SeaEventKind.Shoal ? 300 : k == SeaEventKind.Bottle ? 240 : k == SeaEventKind.Fog ? 600 : k == SeaEventKind.Whales ? 420 : 900;
        public static float Chance(SeaEventKind k) => k == SeaEventKind.Shoal ? .35f : k == SeaEventKind.Bottle ? .3f : k == SeaEventKind.Fog ? .6f : k == SeaEventKind.Whales ? .25f : .8f;
        public static bool Eligible(SeaEventKind k, SeaConditions c)
        {
            if (!c.AtSea) return false;
            switch (k)
            {
                case SeaEventKind.Shoal: case SeaEventKind.Whales: return !c.Night && !c.Rain && !c.Mist;
                case SeaEventKind.Bottle: return !c.Rain;
                case SeaEventKind.Fog: return c.Mist && !c.NearHarbor;
                default: return c.Night && c.GlowNight && !c.Rain;
            }
        }
        /// <summary>About one night in three glows; derived from the day so it needs no save field.</summary>
        public static bool GlowNight(int day) => (uint)(day * 2654435761u) % 3 == 0;
        public float CooldownLeft(SeaEventKind k) => _cooldown[(int)k];
        public float GlobalLeft => _global;

        public SeaEventKind? Tick(float dt, SeaConditions c)
        {
            if (float.IsNaN(dt) || dt <= 0 || dt > 1) return null;
            for (int i = 0; i < KindCount; i++) _cooldown[i] = Math.Max(0, _cooldown[i] - dt);
            if (Active.HasValue)
            {
                var k = Active.Value;
                Remaining -= dt;
                // Fog lifts with the mist and the glow fades at dawn; the others run their clock.
                if (Remaining <= 0 || (k == SeaEventKind.Fog && !c.Mist) || (k == SeaEventKind.Glow && !c.Night)) Finish();
                return null;
            }
            if (!c.AtSea) return null;
            if ((_global -= dt) > 0 || (_roll -= dt) > 0) return null;
            _roll = RollInterval;
            foreach (var k in new[] { SeaEventKind.Glow, SeaEventKind.Fog, SeaEventKind.Whales, SeaEventKind.Shoal, SeaEventKind.Bottle })
            {
                if (_cooldown[(int)k] > 0 || !Eligible(k, c)) continue;
                if (_random.NextDouble() < Chance(k)) { Start(k); return k; }
            }
            return null;
        }
        public void Start(SeaEventKind k) { Active = k; Remaining = Duration(k); }
        public void Finish()
        {
            if (!Active.HasValue) return;
            _cooldown[(int)Active.Value] = Cooldown(Active.Value); _global = GlobalCooldown; _roll = 0; Active = null; Remaining = 0;
        }
    }

    /// <summary>Drift-bottle letters and the bit layout of LoreData.seaLog.</summary>
    public static class FishingSeaLetters
    {
        // seaLog bits: 0-4 first sighting per event, 5 whale recorded, 8-15 letter read, 16-18 treasure claimed, 19-20 commission paid.
        public const int WhaleBit = 5, LetterBit = 8, TreasureBit = 16, CommissionBit = 19, LogLimit = 1 << 21;
        public const int Count = 8, TreasureReward = 80, CommissionReward = 60, GhostReward = 100;
        public static bool IsTreasure(int id) => id >= 3 && id <= 5;
        public static bool IsCommission(int id) => id >= 6 && id < Count;
        /// <summary>Commission 6 wants the whale recorded; commission 7 wants the glow sea witnessed.</summary>
        public static int CommissionGoal(int id) => id == 6 ? WhaleBit : (int)SeaEventKind.Glow;
        public static readonly string[] Titles =
        {
            "漂流瓶 · 守灯日志残页", "漂流瓶 · 铃声", "漂流瓶 · 沙洲刻字",
            "漂流瓶 · 藏宝坐标（珊瑚浅湾）", "漂流瓶 · 藏宝坐标（飞梭水道）", "漂流瓶 · 藏宝坐标（鼓鼓礁）",
            "漂流瓶 · 委托：鲸回来了吗", "漂流瓶 · 委托：夜光海",
        };
        public static readonly string[] Senders = { "沈望潮", "无署名", "无署名", "旧航船水手", "旧航船水手", "旧航船水手", "老舵手·林舟", "望星" };
        public static readonly string[] Bodies =
        {
            "海每涨一次潮，都会轻声问一句：今晚，有人在等你回家吗？\n\n我把灯点亮，就算回答了。可灯要有人守着，才不会再熄。\n\n捡到这页的人，回港时抬头看看灯塔吧。",
            "暴风夜，船听不见港，港也看不见船。\n\n有人摇了一整夜的铃。铃声替她说：这边有人。\n\n等待，要让对方听得见。",
            "风暴那夜，货都推进了海里，孩子们都上了岸。\n\n退潮时沙上会露出一行字：\n“放下的，海会替你记着。”",
            "我们把一箱旧货沉在珊瑚浅湾北面二十多米的浅水里，系着一只红浮标。\n\n船上的人回不去了，箱子留给回得来的人。",
            "飞梭水道东侧，旧航标往外二十多米，浮着一只没人认领的木箱。\n\n里面是我们最后的工钱。",
            "鼓鼓礁西面，有一只钉了铜角的木箱，随潮漂了三十年。\n\n若你找到，替我们好好过日子。",
            "村长：\n\n我年轻时，晴天总有鲸在湾外喷水，后来就不见了。你出海时若碰到，慢慢靠近，替我记一笔吧。\n\n——林舟",
            "村长：\n\n听说有些夜里，海会自己发蓝光，船尾拖出一道光痕。望远镜里看不清，替我亲眼看一看。\n\n——望星",
        };
        public static int NextLetter(Func<int, bool> read)
        {
            for (int i = 0; i < Count; i++) if (!read(i)) return i;
            return -1;
        }
    }
}
