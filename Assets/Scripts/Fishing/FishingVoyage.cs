namespace BoatAttack.Fishing
{
    public enum BaitKind { Balanced, Gentle, Pearl }
    // Leap only occurs for legendary fish; appended so earlier values keep their meaning.
    public enum FishBeat { Rest, Windup, Surge, Leap }

    /// <summary>Platform-independent bait and fight rhythm, shared by the desktop and future tracked tools.</summary>
    public static class FishingVoyage
    {
        public static string BaitName(BaitKind bait) => bait == BaitKind.Gentle ? "舒缓饵" : bait == BaitKind.Pearl ? "珍珠饵" : "均衡饵";
        public static string BaitHelp(BaitKind bait) => bait == BaitKind.Gentle ? "张力 -20% · 收线慢 15%" : bait == BaitKind.Pearl ? "稀有率 25% · 张力 +18%" : "标准张力 · 稀有率 12%";
        public static float RareChance(BaitKind bait) => bait == BaitKind.Pearl ? 0.25f : 0.12f;
        public static float TensionScale(BaitKind bait) => bait == BaitKind.Gentle ? 0.8f : bait == BaitKind.Pearl ? 1.18f : 1f;
        public static float ReelScale(BaitKind bait) => bait == BaitKind.Gentle ? 0.85f : 1f;
        public static float BeatDuration(FishKind kind) => FishingSpecies.Of(kind).BeatSeconds;
        // The legend's leap window inside its beat cycle; the airborne arc uses the same bounds.
        public const float LeapStart = 0.55f;
        public const float LeapEnd = 0.72f;

        /// <summary>
        /// Common fish: rest, windup, surge. The legend adds a leap between windup and surge,
        /// during which reeling snaps the line.
        /// </summary>
        public static FishBeat Beat(FishKind kind, float time)
        {
            var species = FishingSpecies.Of(kind);
            float phase = time % species.BeatSeconds / species.BeatSeconds;
            if (species.Legendary)
                return phase < 0.4f ? FishBeat.Rest : phase < LeapStart ? FishBeat.Windup : phase < LeapEnd ? FishBeat.Leap : FishBeat.Surge;
            return phase < 0.55f ? FishBeat.Rest : phase < 0.76f ? FishBeat.Windup : FishBeat.Surge;
        }
    }
}
