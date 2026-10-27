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
            if (island == 0) { check(!trial.Use(0) && trial.Progress == 0, "V19: wrong bell resets");foreach(int note in new[]{2,0,1,2,0,1,0,1}){trial.Tick(1);trial.Use(note);} }
            if (island == 1) { Align(trial,1,2,3);check(!trial.Ready,"V032: light needs sustained growth");trial.Tick(5);trial.Use(3);Align(trial,3,1,2);trial.Tick(5); }
            if (island == 2) {check(!trial.Use(0),"V035: submerged cargo cannot be pushed");trial.Use(1);trial.Tick(4);trial.Use(0);check(trial.CargoProgress==0,"V035: a tap cannot teleport cargo");for(int i=0;i<72;i++)trial.PushCargo(.1f);trial.Use(1);trial.Tick(4);}
            if (island == 3) for(int stage=0;stage<3;stage++)for(int frame=0;frame<16;frame++)trial.Observe(FishingShrineTrial.StarTargetYaw(stage),FishingShrineTrial.StarTargetPitch(stage),.1f,true);
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
        var prism = new FishingShrineTrial(3); for (int i = 0; i < 40; i++) prism.Use(0);
        check(prism.StarStage==0 && !prism.Ready,"V035: button presses cannot bypass telescope observation");
        for(int i=0;i<30;i++)prism.Observe(0,10,.1f,true);
        check(prism.StarStage==0 && prism.StarFocus==0,"V035: focusing on the wrong sky direction never advances");
        float yaw=FishingShrineTrial.StarTargetYaw(0),pitch=FishingShrineTrial.StarTargetPitch(0);
        prism.Observe(yaw,pitch,.1f,true);check(prism.StarFocus>0,"V035: the release test first establishes real focus");prism.Observe(yaw,pitch,.1f,false);
        check(prism.StarFocus==0,"V035: releasing focus breaks an incomplete observation");
        prism.Observe(float.NaN,pitch,.1f,true);prism.Observe(yaw,pitch,9,true);
        check(prism.StarFocus==0 && prism.StarStage==0,"V035: invalid coordinates and long frame gaps cannot finish an observation");
        var rhythm=new FishingShrineTrial(0);rhythm.Use(2);check(!rhythm.Use(0) && rhythm.Progress==0,"V032: mashed melody is rejected");
        rhythm.Use(2);rhythm.Tick(8.1f);check(rhythm.Progress==1,"V035: the melody allows real traversal between platforms");
        rhythm.Tick(22);check(rhythm.Progress==0,"V035: an abandoned traversal melody still resets");
        var growth=new FishingShrineTrial(1);growth.Use(0);growth.Use(1);growth.Use(1);for(int i=0;i<3;i++)growth.Use(2);growth.Tick(2.5f);
        check(Math.Abs(growth.Growth-.5f)<.001f,"V032: seedlings grow with time, not button counts");growth.Use(0);growth.Tick(1.5f);check(growth.Growth==0,"V032: misdirected light loses growth");
        var twoRounds=new FishingShrineTrial(0);
        foreach(int note in new[]{2,0,1}){twoRounds.Tick(1);twoRounds.Use(note);}
        check(twoRounds.WindRound==1 && twoRounds.Progress==0 && !twoRounds.Ready,"Feedback: first three-note melody unlocks the five-note round without completing the shrine");
        foreach(int note in new[]{2,0,1}){twoRounds.Tick(1);twoRounds.Use(note);}
        twoRounds.Tick(1);check(!twoRounds.Use(2) && twoRounds.Progress==0 && twoRounds.WindRound==1,"Feedback: wrong second melody clears its progress while retaining the first round checkpoint");
        foreach(int note in new[]{2,0,1,0,1}){twoRounds.Tick(1);twoRounds.Use(note);}
        check(twoRounds.Ready && !twoRounds.Use(0),"Feedback: all five ordered steps are required and solved melody cannot advance again");
        var twoLights=new FishingShrineTrial(1);check(!twoLights.Use(3),"Feedback: inaccessible shutter cannot be used before its side path opens");
        Align(twoLights,1,2,3);twoLights.Tick(5);
        check(twoLights.LightStage==1 && twoLights.Growth==0 && !twoLights.Ready,"Feedback: first receiver opens a checkpoint side path, not the final crossing");
        twoLights.Tick(10);check(twoLights.LightStage==1 && twoLights.Growth==0,"Feedback: leaving the old light arrangement cannot complete the second receiver");
        Align(twoLights,3,1,2);twoLights.Tick(5);check(twoLights.Growth==0,"Feedback: the second optical path is blocked by the closed shutter");
        twoLights.Use(3);twoLights.Tick(2.5f);check(Math.Abs(twoLights.Growth-.5f)<.001f,"Feedback: opened shutter and rearranged mirrors illuminate the second receiver continuously");
        twoLights.Use(3);twoLights.Tick(1.5f);check(twoLights.Growth==0 && twoLights.LightStage==1,"Feedback: closing the shutter resets only the current light charge");
        twoLights.Use(3);twoLights.Tick(5);check(twoLights.Ready && !twoLights.Use(3),"Feedback: second sustained receiver completes exactly once");
        var hydraulic=new FishingShrineTrial(2);hydraulic.Use(1);hydraulic.Tick(4);
        check(!hydraulic.PushCargo(float.PositiveInfinity) && !hydraulic.PushCargo(5) && hydraulic.CargoProgress==0,"V035: invalid or unobserved cargo movement is rejected");
        for(int i=0;i<35;i++)hydraulic.PushCargo(.1f);
        check(hydraulic.CargoProgress>.49f && hydraulic.CargoProgress<.51f && !hydraulic.Ready,"V035: cargo progress reflects sustained physical pushing time");
        for(int i=0;i<40;i++)hydraulic.PushCargo(.1f);check(!hydraulic.Use(0),"V035: cargo stops on the plate");
        hydraulic.Use(1);hydraulic.Tick(2);check(!hydraulic.Ready && Math.Abs(hydraulic.WaterLevel-.5f)<.001f,"V032: float bridge needs enough water");
        hydraulic.Tick(float.PositiveInfinity);check(Math.Abs(hydraulic.WaterLevel-.5f)<.001f,"V032: invalid time cannot solve hydraulics");
        hydraulic.Tick(2);check(hydraulic.Ready,"V038: loaded floating bridge reaches its upper landing");
        hydraulic.Use(1);hydraulic.Tick(1);
        check(hydraulic.State(1)==0 && hydraulic.WaterLevel==1 && hydraulic.Ready,"V038: closing the filled valve preserves the floating crossing");
        hydraulic.Use(1);hydraulic.Tick(1);
        check(!hydraulic.Ready && hydraulic.WaterLevel<.95f,"V038: actually draining the hold lowers the crossing");
    }
    private static void Align(FishingShrineTrial trial,int first,int second,int third)
    {int[] targets={first,second,third};for(int device=0;device<3;device++)for(int turns=0;turns<4 && trial.State(device)!=targets[device];turns++)trial.Use(device);}
}
