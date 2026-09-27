using System;
namespace BoatAttack.Fishing
{
    public enum BaitKind { Balanced, Gentle, Pearl }
    public enum FishBeat { Rest, Windup, Surge }

    /// <summary>Platform-independent tuning, shared by the desktop and future tracked tools.</summary>
    public static class FishingVoyage
    {
        public static string BaitName(BaitKind bait) => bait == BaitKind.Gentle ? "舒缓饵" : bait == BaitKind.Pearl ? "珍珠饵" : "均衡饵";
        public static string BaitHelp(BaitKind bait) => bait == BaitKind.Gentle ? "张力 -20% · 收线慢 15%" : bait == BaitKind.Pearl ? "稀有率 25% · 张力 +18%" : "标准张力 · 稀有率 12%";
        public static float RareChance(BaitKind bait) => bait == BaitKind.Pearl ? 0.25f : 0.12f;
        public static float TensionScale(BaitKind bait) => bait == BaitKind.Gentle ? 0.8f : bait == BaitKind.Pearl ? 1.18f : 1f;
        public static float ReelScale(BaitKind bait) => bait == BaitKind.Gentle ? 0.85f : 1f;
        public static string OrderName(int completed) => completed % 3 == 0 ? "海湾早餐" : completed % 3 == 1 ? "远航补给" : "海岛盛宴";
        public static int OrderBonus(int completed) => completed % 3 == 0 ? 24 : completed % 3 == 1 ? 32 : 50;
        public static int Required(int completed, FishKind kind)
        {
            int order = completed % 3;
            return order == 0 ? (kind == FishKind.Puffer ? 0 : 1) : order == 1 ? (kind == FishKind.Coral ? 0 : 1) : 1;
        }
        public static string Habitat(FishKind kind) => kind == FishKind.Coral ? "浅湾环礁 · 短冲刺，适合初学" : kind == FishKind.Dart ? "航标水道 · 冲刺频繁，提前放线" : "鼓鼓礁群 · 蓄力较久，冲刺更强";
        public static float BeatDuration(FishKind kind) => kind == FishKind.Dart ? 3.6f : kind == FishKind.Puffer ? 6.4f : 5.4f;
        public static FishBeat Beat(FishKind kind, float time)
        {
            float phase = time % BeatDuration(kind) / BeatDuration(kind);
            return phase < 0.55f ? FishBeat.Rest : phase < 0.76f ? FishBeat.Windup : FishBeat.Surge;
        }
    }
}
