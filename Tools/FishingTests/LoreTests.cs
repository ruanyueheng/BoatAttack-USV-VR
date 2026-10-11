using System;
using BoatAttack.Fishing;

internal static class LoreTests
{
    public static void Run(Action<bool, string> check)
    {
        var old = new FishingCareer().Snapshot(); old.version = 10; old.coins = 420; old.engineLevel = 2;
        check(FishingCareer.TryRestore(old, out var c) && c.Coins == 420 && c.EngineLevel == 2 && c.Lore.ClearCount == 0, "V19: v10 inherits fleet and money without invented shrine rewards");
        check(!c.Lore.Meet(-1) && !c.Lore.Meet(6) && c.Lore.Meet(4) && !c.Lore.Meet(4), "V19: resident ids bounded and discovery once");
        check(c.CompleteEchoVoyage() == 0, "V19: finale requires four echoes");
        foreach (int island in new[] { 2, 0, 3, 1 })
        {
            var trial = new FishingShrineTrial(island);
            check(!trial.Ready && !trial.Use(-1) && !trial.Use(3), "V19: trial starts unsolved and rejects invalid devices " + island);
            if (island == 0) { check(!trial.Use(0) && trial.Progress == 0, "V19: wrong bell resets"); trial.Use(2); trial.Use(0); trial.Use(1); }
            if (island == 1) { trial.Use(0); trial.Use(2); }
            if (island == 2) { trial.Use(0); trial.Use(1); check(!trial.Ready, "V19: load plate requires second cargo step"); trial.Use(0); }
            if (island == 3) { trial.Use(0); for (int i = 0; i < 3; i++) trial.Use(1); for (int i = 0; i < 2; i++) trial.Use(2); }
            check(trial.Ready, "V19: each distinct puzzle has a reachable solution " + island);
            check(c.CompleteShrine(island) == 90 && c.CompleteShrine(island) == 0, "V19: shrine pays once " + island);
            check(FishingCareer.TryRestore(c.Snapshot(), out var restored) && restored.Lore.Cleared(island), "V19: shrine survives reload " + island); c = restored;
        }
        check(c.CompleteEchoVoyage() == 180 && c.CompleteEchoVoyage() == 0 && c.Coins == 960, "V19: nonlinear voyage pays 540 once");
        var data = c.Snapshot(); data.lore.shrines = 7;
        check(!FishingCareer.TryRestore(data, out _), "V19: finale without four echoes rejected");
        data = c.Snapshot(); data.lore.residents = 64;
        check(!FishingCareer.TryRestore(data, out _), "V19: oversized NPC facts rejected");
        data = c.Snapshot(); data.version = 10;
        check(!FishingCareer.TryRestore(data, out _), "V19: older schema cannot conceal paid shrine progress");
        data = new FishingCareer().Snapshot(); data.coins = 100000000;
        check(FishingCareer.TryRestore(data, out c) && c.CompleteShrine(0) == 0 && !c.Lore.Cleared(0), "V19: wallet cap never consumes unpaid echo");
        var reset = new FishingShrineTrial(0); reset.Use(2); reset.Use(1);
        check(reset.Progress == 0 && reset.State(2) == 0, "V19: failed melody clears visible bell state");
        var prism = new FishingShrineTrial(3); for (int i = 0; i < 4; i++) prism.Use(0);
        check(prism.State(0) == 0, "V19: star dial wraps at north");
    }
}
