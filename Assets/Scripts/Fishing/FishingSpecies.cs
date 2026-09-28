using System;

namespace BoatAttack.Fishing
{
    public enum UpgradeTrack { Line, Reel, Hold }

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
        public const int Count = 4;
        // Fishing areas 0-2 each hold one common species. The legend shares area 1.
        public const int AreaCount = 3;
        public const int LegendArea = 1;
        // Discovery bits: common normal 0-2, common pearl 3-5 (the v2 layout), legend 6.
        public const int LegendDiscoveryBit = 6;
        public const int DiscoveryMaskLimit = 127;

        private static readonly FishSpecies[] Table =
        {
            new FishSpecies(FishKind.Coral, "珊瑚鱼", "浅湾环礁 · 短冲刺，适合初学", 10, 600, 1400, 2.2f, 10f, 0.36f, 5.4f, 1.55f, 75f, false),
            new FishSpecies(FishKind.Dart, "飞梭鱼", "航标水道 · 冲刺频繁，提前放线", 16, 900, 2200, 2.55f, 12f, 0.45f, 3.6f, 1.55f, 75f, false),
            new FishSpecies(FishKind.Puffer, "鼓鼓鱼", "鼓鼓礁群 · 蓄力较久，冲刺更强", 22, 1200, 3000, 2.9f, 14f, 0.54f, 6.4f, 1.8f, 75f, false),
            new FishSpecies(FishKind.Sailfish, "金帆旗鱼", "航标水道 · 金色夕照 · 追逐珍珠饵", 120, 18000, 32000, 3.2f, 24f, 0.5f, 6.8f, 1.7f, 120f, true),
        };
        private static readonly string[] Areas = { "珊瑚浅湾", "飞梭水道", "鼓鼓礁" };

        public static bool IsDefined(FishKind kind) => (int)kind >= 0 && (int)kind < Count;

        public static FishSpecies Of(FishKind kind)
        {
            if (!IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown fish species.");
            return Table[(int)kind];
        }

        public static string Name(FishKind kind) => IsDefined(kind) ? Table[(int)kind].Name : "未知鱼种";
        public static string AreaName(int area) => area >= 0 && area < AreaCount ? Areas[area] : "开阔海域";
        public static FishKind AreaSpecies(int area) => (FishKind)Math.Max(0, Math.Min(AreaCount - 1, area));
        public static int DiscoveryBit(FishKind kind, bool rare) => kind == FishKind.Sailfish ? LegendDiscoveryBit : (int)kind + (rare ? 3 : 0);

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
