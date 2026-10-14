namespace BoatAttack.Fishing
{
    /// <summary>Discoveries are permanent, so survey rewards need no second set of save flags.</summary>
    public static class FishingSurvey
    {
        private static readonly FishKind[] Bay = { FishKind.Coral, FishKind.Sardine, FishKind.SeaBream };
        private static readonly FishKind[] Channel = { FishKind.Dart, FishKind.Mackerel, FishKind.Lanternfish };
        public static bool Known(FishingCareer career, FishKind kind) => career != null
            && (career.Discovered(kind) || career.Discovered(kind, true));
        private static bool Complete(FishingCareer career, FishKind[] fish)
        {
            foreach (var kind in fish) if (!Known(career, kind)) return false;
            return true;
        }
        public static int Stage(FishingCareer career) => !Complete(career, Bay) ? 0 : Complete(career, Channel) ? 2 : 1;
        public static bool LegendCoordinates(FishingCareer career) => Stage(career) == 2;
        public static FishKind NextFish(FishingCareer career)
        {
            foreach (var kind in Stage(career) == 0 ? Bay : Channel) if (!Known(career, kind)) return kind;
            return FishKind.Sailfish;
        }
        public static string Goal(FishingCareer career) => Stage(career) == 2
            ? "调查完成 · 夕照到飞梭水道，用珍珠饵和强化鱼线必遇金帆旗鱼"
            : (Stage(career) == 0 ? "浅湾调查 → 北岛渔场" : "水道调查 → 传说坐标") + " · 下一条：" + FishingSpecies.Name(NextFish(career));
        public static string Checklist(FishingCareer career)
        {
            string result = "";
            foreach (var kind in Stage(career) == 0 ? Bay : Channel)
                result += (Known(career, kind) ? "✓ " : "○ ") + FishingSpecies.Name(kind) + "  ";
            return result;
        }
    }
}
