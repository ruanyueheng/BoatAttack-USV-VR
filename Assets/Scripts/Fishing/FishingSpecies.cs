using System;

namespace BoatAttack.Fishing
{
    public enum UpgradeTrack { Line, Reel, Hold, Engine, Rudder, Cabin }

    /// <summary>Immutable tuning for one species. Unity-free so desktop, future XR tools and tests share it.</summary>
    public sealed class FishSpecies
    {
        public FishSpecies(FishKind kind, string name, string habitat, int baseValue, int minGrams, int maxGrams,
            float biteSeconds, float lineMeters, float tensionGain, float beatSeconds, float surgeEffort, float fightSeconds, bool legendary)
        {
            Kind = kind;
            Name = name;
            Habitat = habitat;
            BaseValue = baseValue;
            MinGrams = minGrams;
            MaxGrams = maxGrams;
            BiteSeconds = biteSeconds;
            LineMeters = lineMeters;
            TensionGain = tensionGain;
            BeatSeconds = beatSeconds;
            SurgeEffort = surgeEffort;
            FightSeconds = fightSeconds;
            Legendary = legendary;
        }

        public FishKind Kind { get; }
        public string Name { get; }
        public string Habitat { get; }
        public int BaseValue { get; }
        public int MinGrams { get; }
        public int MaxGrams { get; }
        public float BiteSeconds { get; }
        public float LineMeters { get; }
        public float TensionGain { get; }
        public float BeatSeconds { get; }
        public float SurgeEffort { get; }
        public float FightSeconds { get; }
        public bool Legendary { get; }
    }

    /// <summary>The single source of species numbers; prices and saves are validated against this table.</summary>
    public static class FishingSpecies
    {
        public const int Count = 20;
        // Fishing areas 0-2 each hold one common species. The legend shares area 1.
        public const int AreaCount = 3;
        public const int LegendArea = 1;
        // Discovery bits: common normal 0-2, common pearl 3-5 (the v2 layout), legend 6.
        public const int LegendDiscoveryBit = 6;
        public const long DiscoveryMaskLimit = (1L << (Count * 2 - 1)) - 1;

        private static readonly FishSpecies[] Table =
        {
            new FishSpecies(FishKind.Coral, "珊瑚鱼", "浅湾环礁 · 短冲刺，适合初学", 10, 600, 1400, 2.2f, 6f, 0.36f, 5.4f, 1.55f, 75f, false),
            new FishSpecies(FishKind.Dart, "飞梭鱼", "航标水道 · 冲刺频繁，提前放线", 16, 900, 2200, 2.55f, 12f, 0.45f, 3.6f, 1.55f, 75f, false),
            new FishSpecies(FishKind.Puffer, "鼓鼓鱼", "鼓鼓礁群 · 蓄力较久，冲刺更强", 22, 1200, 3000, 2.9f, 14f, 0.54f, 6.4f, 1.8f, 75f, false),
            new FishSpecies(FishKind.Sailfish, "金帆旗鱼", "航标水道 · 金色夕照 · 追逐珍珠饵", 120, 18000, 32000, 3.2f, 24f, 0.5f, 6.8f, 1.7f, 120f, true),
            new FishSpecies(FishKind.Mackerel, "青花鲭", "航标水道 · 仅雨天 · 银腹与青纹", 18, 300, 1100, 2.3f, 12, .41f, 4.2f, 1.5f, 80, false),
            new FishSpecies(FishKind.SeaBream, "金线海鲷", "珊瑚浅湾 · 春夏白昼 · 高身金鳍", 24, 500, 1800, 2.6f, 12, .43f, 5.5f, 1.6f, 80, false),
            new FishSpecies(FishKind.Sardine, "银沙丁", "各海域 · 白昼 · 小型群游鱼", 8, 80, 240, 1.8f, 4, .29f, 5.8f, 1.3f, 70, false),
            new FishSpecies(FishKind.Grouper, "斑点石斑", "鼓鼓礁 · 涨潮且秋冬 / 薄雾 · 底栖伏击", 32, 900, 3800, 3.2f, 15, .5f, 6.2f, 1.8f, 90, false),
            new FishSpecies(FishKind.Butterfly, "月纹蝶鱼", "珊瑚浅湾 · 夏季白昼 · 蝶形侧影", 20, 120, 400, 2.5f, 10, .34f, 5.2f, 1.4f, 75, false),
            new FishSpecies(FishKind.Tuna, "蓝鳍鲔", "航标水道 · 夏秋白昼 · 急速冲刺", 45, 2000, 7000, 3.1f, 18, .51f, 4.1f, 1.8f, 100, false),
            new FishSpecies(FishKind.Lanternfish, "星灯鱼", "各海域 · 仅夜间 · 腹部星斑", 28, 100, 450, 2.4f, 11, .38f, 5.8f, 1.5f, 80, false),
            new FishSpecies(FishKind.Lionfish, "赤羽狮子鱼", "鼓鼓礁 · 夜间 / 夏季 · 羽扇胸鳍", 36, 400, 1500, 3, 14, .48f, 6.4f, 1.75f, 90, false),
            new FishSpecies(FishKind.SeaTurtle, "青背海湾龟", "珊瑚浅湾 · 白昼 · 宽鳍与九片背甲", 38, 1800, 6500, 3.6f, 15, .39f, 7.2f, 1.5f, 95, false),
            new FishSpecies(FishKind.Octopus, "玫瑰八腕章鱼", "鼓鼓礁 · 夜间 / 薄雾 · 八腕吸盘", 35, 500, 2400, 3.1f, 11, .45f, 5.9f, 1.65f, 90, false),
            new FishSpecies(FishKind.Crab, "赤钳沙蟹", "珊瑚浅湾 · 落潮 · 横行的红色双钳", 16, 180, 800, 2.7f, 12, .33f, 6.1f, 1.4f, 75, false),
            new FishSpecies(FishKind.Lobster, "绯壳岩龙虾", "鼓鼓礁 · 夜间 / 秋冬 · 长触须与分节尾", 42, 600, 2200, 3.2f, 12, .46f, 5.7f, 1.6f, 90, false),
            new FishSpecies(FishKind.Seahorse, "琥珀冠海马", "珊瑚浅湾 · 春夏 · 卷尾与冠状背脊", 24, 30, 130, 2.8f, 14, .28f, 6.8f, 1.35f, 75, false),
            new FishSpecies(FishKind.Stingray, "蓝斑燕鳐", "飞梭水道 · 涨潮 · 菱形双翼与细长尾", 48, 1600, 5800, 3.4f, 19, .48f, 5.8f, 1.75f, 100, false),
            new FishSpecies(FishKind.Jellyfish, "月伞水母", "各海域 · 夜间 · 珍珠蓝伞与十二条触须", 18, 100, 600, 2.4f, 15, .27f, 7.3f, 1.3f, 75, false),
            new FishSpecies(FishKind.Cuttlefish, "翡翠纹乌贼", "飞梭水道 · 春秋 / 薄雾 · 波浪裙鳍", 29, 250, 1300, 2.9f, 13, .43f, 4.8f, 1.55f, 85, false),
        };
        private static readonly string[] Areas = { "珊瑚浅湾", "飞梭水道", "鼓鼓礁" };

        private static readonly float[] Lengths = { .24f, .32f, .21f, .82f, .29f, .27f, .17f, .35f, .20f, .38f, .18f, .26f, .62f, .54f, .49f, .65f, .46f, .78f, .41f, .50f };
        public static float LengthMeters(FishKind kind) => IsDefined(kind) ? Lengths[(int)kind] : .3f;
        public static float SizeScale(FishKind kind, int grams) => .85f + .3f * SizePermille(kind, grams) / 1000f;
        public static bool InHabitat(FishKind kind, int area, FishingCalendar calendar, BayWeather weather)
        {
            int zone = Math.Max(0, area) % 3;
            int season = calendar.Season; bool night = calendar.Night;
            switch (kind)
            {
                case FishKind.Mackerel: return zone == 1 && weather == BayWeather.Rain;
                case FishKind.SeaBream: return zone == 0 && !night && season < 2;
                case FishKind.Sardine: return !night;
                case FishKind.Grouper: return zone == 2 && calendar.RisingTide && (season >= 2 || weather == BayWeather.Mist);
                case FishKind.Butterfly: return zone == 0 && season == 1 && !night;
                case FishKind.Tuna: return zone == 1 && !night && (season == 1 || season == 2);
                case FishKind.Lanternfish: return night;
                case FishKind.Lionfish: return zone == 2 && (night || season == 1);
                case FishKind.SeaTurtle: return zone == 0 && !night;
                case FishKind.Octopus: return zone == 2 && (night || weather == BayWeather.Mist);
                case FishKind.Crab: return zone == 0 && !calendar.RisingTide;
                case FishKind.Lobster: return zone == 2 && (night || season >= 2);
                case FishKind.Seahorse: return zone == 0 && season < 2;
                case FishKind.Stingray: return zone == 1 && calendar.RisingTide;
                case FishKind.Jellyfish: return night;
                case FishKind.Cuttlefish: return zone == 1 && (season == 0 || season == 2 || weather == BayWeather.Mist);
                default: return kind == AreaSpecies(zone);
            }
        }
        public static FishKind Select(int area, FishingCalendar calendar, BayWeather weather, float roll)
        {
            var fallback = AreaSpecies(Math.Max(0, area) % 3);
            if (calendar == null || float.IsNaN(roll) || roll < .4f || roll > 1) return fallback;
            int count = 0;
            for (int i = 4; i < Count; i++) if (InHabitat((FishKind)i, area, calendar, weather)) count++;
            int pick = Math.Min(count - 1, (int)((roll - .4f) / .6f * count));
            for (int i = 4; i < Count; i++) if (InHabitat((FishKind)i, area, calendar, weather) && pick-- == 0) return (FishKind)i;
            return fallback;
        }
        public static bool IsDefined(FishKind kind) => (int)kind >= 0 && (int)kind < Count;

        public static FishSpecies Of(FishKind kind)
        {
            if (!IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown fish species.");
            return Table[(int)kind];
        }

        public static string Name(FishKind kind) => IsDefined(kind) ? Table[(int)kind].Name : "未知鱼种";
        public static string AreaName(int area) => area >= 0 && area < AreaCount ? Areas[area] : "开阔海域";
        public static FishKind AreaSpecies(int area) => (FishKind)Math.Max(0, Math.Min(AreaCount - 1, area));
        public static int DiscoveryBit(FishKind kind, bool rare) => kind == FishKind.Sailfish ? LegendDiscoveryBit : (int)kind < 3 ? (int)kind + (rare ? 3 : 0) : 7 + ((int)kind - 4) * 2 + (rare ? 1 : 0);

        /// <summary>Whole grams from a 0..1 size roll. Stored as an integer so saves match on every runtime.</summary>
        public static int Grams(FishKind kind, float size)
        {
            var species = Of(kind);
            // Math.Max/Min propagate NaN; an invalid roll falls back to an average fish.
            double t = float.IsNaN(size) ? 0.5 : Math.Max(0.0, Math.Min(1.0, size));
            return species.MinGrams + (int)Math.Round((species.MaxGrams - species.MinGrams) * t);
        }

        /// <summary>Zero marks a record from before v0.4 sizes; legends never had such records.</summary>
        public static bool ValidGrams(FishKind kind, int grams)
        {
            var species = Of(kind);
            return grams == 0 ? !species.Legendary : grams >= species.MinGrams && grams <= species.MaxGrams;
        }

        /// <summary>Position inside the species range, 0-1000. Weightless legacy records count as average.</summary>
        public static int SizePermille(FishKind kind, int grams)
        {
            if (grams == 0) return 500;
            var species = Of(kind);
            long offset = Math.Max(0, Math.Min(species.MaxGrams, grams) - species.MinGrams);
            return (int)(offset * 1000 / (species.MaxGrams - species.MinGrams));
        }

        /// <summary>Base price × 3 for pearl variants × 0.8-1.2 by size. Integer math; legacy records keep flat prices.</summary>
        public static int Value(FishKind kind, bool rare, int grams)
        {
            var species = Of(kind);
            int price = species.BaseValue * (rare && !species.Legendary ? 3 : 1);
            if (grams == 0) return price;
            int factor = 800 + 400 * SizePermille(kind, grams) / 1000;
            return (price * factor + 500) / 1000;
        }
    }

    /// <summary>When and how the golden-sail legend appears, and what its release bounty pays.</summary>
    public static class LegendRules
    {
        public const float StrikeChance = 0.4f;
        public const int ClueCount = 3;

        /// <summary>Area, weather and bait line up. The strike also needs a reinforced line.</summary>
        public static bool Conditions(int area, BayWeather weather, BaitKind bait)
            => area == FishingSpecies.LegendArea && weather == BayWeather.Sunset && bait == BaitKind.Pearl;

        public static bool CanStrike(int area, BayWeather weather, BaitKind bait, int lineLevel)
            => Conditions(area, weather, bait) && lineLevel >= 1;

        /// <summary>The legend is measured and released; the marine station pays a bounty instead of a sale.</summary>
        public static int Bounty(bool firstCatch, int grams)
        {
            int permille = FishingSpecies.SizePermille(FishKind.Sailfish, grams);
            return firstCatch ? 150 + 60 * permille / 1000 : 40 + 30 * permille / 1000;
        }

        public static bool ClueUnlocked(int clue, FishingCareer career)
        {
            if (career == null) return false;
            switch (clue)
            {
                case 0: return career.Discovered(FishKind.Dart) || career.Discovered(FishKind.Dart, true);
                case 1: return career.OrdersCompleted >= 2;
                case 2: return career.LineLevel >= 1 || career.LegendCatches > 0;
                default: return false;
            }
        }

        public static string Clue(int clue)
        {
            switch (clue)
            {
                case 0: return "渔民传闻：它追着飞梭鱼群，出没在航标水道。";
                case 1: return "灯塔看守：只有金色夕照时，海面才映得出它的金帆。";
                case 2: return "海洋学者：它只追珍珠饵；普通鱼线撑不住它的跃水。";
                default: return string.Empty;
            }
        }

        public static string ClueHint(int clue)
        {
            switch (clue)
            {
                case 0: return "捕获一条飞梭鱼后解锁";
                case 1: return "完成两份港口委托后解锁";
                case 2: return "升级一次鱼线后解锁";
                default: return string.Empty;
            }
        }
    }
}
