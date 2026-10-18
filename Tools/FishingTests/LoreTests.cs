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
            if (island == 0) { check(!trial.Use(0) && trial.Progress == 0, "V19: wrong bell resets"); trial.Use(2);trial.Tick(1);trial.Use(0);trial.Tick(1);trial.Use(1); }
            if (island == 1) { trial.Use(0);trial.Use(1);trial.Use(1);trial.Use(2);trial.Use(2);trial.Use(2);check(!trial.Ready,"V032: light needs sustained growth");trial.Tick(5); }
            if (island == 2) {check(!trial.Use(0),"V032: submerged cargo cannot be pushed");trial.Use(1);trial.Tick(4);trial.Use(0);check(!trial.Ready,"V032: one cargo step does not load the plate");trial.Use(0);trial.Use(1);trial.Tick(4);}
            if (island == 3) {trial.Use(0);trial.Use(1);trial.Use(1);}
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
        check(prism.State(1)==0,"V032: coupled dial also wraps without overflow");
        var rhythm=new FishingShrineTrial(0);rhythm.Use(2);check(!rhythm.Use(0) && rhythm.Progress==0,"V032: mashed melody is rejected");
        rhythm.Use(2);rhythm.Tick(8.1f);check(rhythm.Progress==0,"V032: abandoned melody resets");
        var growth=new FishingShrineTrial(1);growth.Use(0);growth.Use(1);growth.Use(1);for(int i=0;i<3;i++)growth.Use(2);growth.Tick(2.5f);
        check(Math.Abs(growth.Growth-.5f)<.001f,"V032: seedlings grow with time, not button counts");growth.Use(0);growth.Tick(1.5f);check(growth.Growth==0,"V032: misdirected light loses growth");
        var hydraulic=new FishingShrineTrial(2);hydraulic.Use(1);hydraulic.Tick(4);hydraulic.Use(0);hydraulic.Use(0);check(!hydraulic.Use(0),"V032: cargo stops on the plate");
        hydraulic.Use(1);hydraulic.Tick(2);check(!hydraulic.Ready && Math.Abs(hydraulic.WaterLevel-.5f)<.001f,"V032: float bridge needs enough water");
        hydraulic.Tick(float.PositiveInfinity);check(Math.Abs(hydraulic.WaterLevel-.5f)<.001f,"V032: invalid time cannot solve hydraulics");
    }
}
