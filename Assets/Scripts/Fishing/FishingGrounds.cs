using System;

namespace BoatAttack.Fishing
{
    /// <summary>Fishing grounds are not species IDs. The fourth ground reuses an existing fish.</summary>
    public static class FishingGrounds
    {
        public const int NorthIsland = 3;
        public const int Count = 4;
        // Candidate offshore location; scene reachability is verified separately from these rules.
        public const float NorthX = 42f, NorthZ = 102f, TargetRadius = 22f;
        public static string Name(int ground) => ground == NorthIsland ? "北岛信标渔场" : FishingSpecies.AreaName(ground);
        public static FishKind Species(int ground) => ground == NorthIsland ? FishKind.Coral : FishingSpecies.AreaSpecies(ground);
        public static bool Available(int ground, FishingExpedition expedition) =>
            ground >= 0 && ground < Count && (ground != NorthIsland || (expedition != null && expedition.Has(FishingExpedition.BeaconRepaired)));
        public static bool AvailableForCareer(int ground, FishingCareer career) => Available(ground, career?.Expedition)
            || (ground == NorthIsland && FishingSurvey.Stage(career) >= 1);
        public static bool MatchesTarget(int castGround, FishKind kind, float x, float z)
        {
            if (castGround != NorthIsland || kind != FishKind.Coral || float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z)) return false;
            double dx = (double)x - NorthX, dz = (double)z - NorthZ;
            return dx * dx + dz * dz <= TargetRadius * TargetRadius;
        }
    }
}
