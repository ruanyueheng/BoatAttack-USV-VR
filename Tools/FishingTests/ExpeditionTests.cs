using System;
using BoatAttack.Fishing;
internal static class ExpeditionTests
{
    public static void Run(Action<bool, string> check)
    {
        var c = new FishingCareer();
        var e = c.Expedition;
        check(!e.RepairBeacon() && !e.Deliver() && !e.RecordCatch(true, true), "Expedition rejects premature actions");
        check(e.FindComponent() && e.ReadRecord() && e.Facts == 6, "Expedition permits exploration before acceptance");
        check(!e.FindComponent() && !e.ReadRecord() && !e.RepairBeacon(), "Expedition repeat discovery and unaccepted repair rejected");
        check(e.Accept() && !e.Accept() && e.RepairBeacon() && !e.RepairBeacon(), "Expedition acceptance and repair exactly once");
        check(!e.RecordCatch(false, true) && !e.RecordCatch(true, false) && !e.Deliver(), "Expedition rejects failed storage and wrong target");
        check(e.RecordCatch(true, true) && !e.RecordCatch(true, true), "Expedition capture evidence exactly once");
        int coins = c.Coins;
        c.SellAll();
        check(e.Deliver() && !e.Deliver() && c.Coins == coins, "Expedition evidence survives sale; no implicit currency reward");
        check(FishingCareer.TryRestore(c.Snapshot(), out var restored) && restored.Expedition.Facts == 63 && !restored.Expedition.Deliver(), "Expedition complete save cannot deliver twice");
        int validCount = 0;
        for (int mask = 0; mask < 64; mask++)
        {
            // Independent enumeration: arbitrary first three facts, then repair/catch/delivery in order.
            bool expected = mask <= 7 || mask == 15 || mask == 31 || mask == 63;
            bool accepted = FishingCareer.TryRestore(new FishingSaveData { expeditionFacts = mask }, out var state);
            check(accepted == expected, "Expedition save combination " + mask);
            if (!accepted) continue;
            validCount++;
            check(state.Expedition.Facts == mask && FishingCareer.TryRestore(state.Snapshot(), out var again) && again.Expedition.Facts == mask,
                "Expedition round trip phase " + mask);
        }
        check(validCount == 11, "Expedition exactly eleven valid fact combinations");
        foreach (int invalid in new[] { -1, 64, 127, int.MaxValue, int.MinValue })
            check(!FishingCareer.TryRestore(new FishingSaveData { expeditionFacts = invalid }, out _), "Expedition rejects unknown facts " + invalid);
        for (int version = 1; version <= 4; version++)
        {
            check(FishingCareer.TryRestore(new FishingSaveData { version = version, coins = 44 }, out restored)
                && restored.Coins == 44 && restored.Expedition.Facts == 0, "Expedition migrates legacy v" + version);
            check(!FishingCareer.TryRestore(new FishingSaveData { version = version, expeditionFacts = 1 }, out _), "Expedition rejects facts in legacy v" + version);
        }
        var old = new FishingCareer();
        for (int i = 0; i < 8; i++) old.CompleteShoreTask(i);
        old.ClaimShoreRewards();
        var legacy = old.Snapshot(); legacy.version = 4;
        check(FishingCareer.TryRestore(legacy, out restored) && restored.Coins == 160 && restored.ShoreTaskMask == 255
            && restored.Expedition.Facts == 0 && restored.ClaimShoreRewards() == 0, "Expedition preserves completed v4 shore rewards");
        check(!FishingCareer.TryRestore(new FishingSaveData { version = 6 }, out _), "Expedition rejects future v6 save");
        for (int ground = 0; ground < 3; ground++)
        {
            check(FishingGrounds.Available(ground, new FishingExpedition()), "Original ground remains open " + ground);
            check(FishingGrounds.Species(ground) == FishingSpecies.AreaSpecies(ground), "Original ground species unchanged " + ground);
            check(!FishingGrounds.MatchesTarget(ground, FishKind.Coral, 42, 102), "Wrong cast ground rejected " + ground);
        }
        check(!FishingGrounds.Available(3, new FishingExpedition()) && FishingGrounds.Available(3, e), "North ground repair lock");
        check(!FishingGrounds.Available(-1, e) && !FishingGrounds.Available(4, e) && !FishingGrounds.Available(3, null), "Invalid or missing ground state rejected");
        check(FishingGrounds.Species(3) == FishKind.Coral && FishingSpecies.AreaCount == 3, "North ground reuses coral without adding species");
        check(FishingGrounds.MatchesTarget(3, FishKind.Coral, 42, 102), "North center qualifies");
        check(FishingGrounds.MatchesTarget(3, FishKind.Coral, 64, 102), "North inclusive boundary qualifies");
        check(!FishingGrounds.MatchesTarget(3, FishKind.Coral, 64.01f, 102), "North outside boundary rejected");
        check(!FishingGrounds.MatchesTarget(3, FishKind.Coral, 64, 124), "North square corner outside circle rejected");
        check(!FishingGrounds.MatchesTarget(3, FishKind.Coral, 42, 70), "North landing is not fishing ground");
        foreach (var kind in new[] { FishKind.Dart, FishKind.Puffer, FishKind.Sailfish })
            check(!FishingGrounds.MatchesTarget(3, kind, 42, 102), "Wrong target species rejected " + kind);
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
            check(!FishingGrounds.MatchesTarget(3, FishKind.Coral, invalid, 102) && !FishingGrounds.MatchesTarget(3, FishKind.Coral, 42, invalid), "Invalid target position rejected " + invalid);
        var target = new FishingExpedition();
        bool qualifies = FishingGrounds.MatchesTarget(3, FishKind.Coral, 42, 102);
        check(!target.RecordCatch(true, qualifies), "Qualified location before repair cannot record");
        target.Accept(); target.ReadRecord(); target.FindComponent(); target.RepairBeacon();
        check(!target.RecordCatch(false, qualifies), "Full hold cannot record qualified catch");
        check(target.RecordCatch(true, qualifies) && !target.RecordCatch(true, qualifies), "Qualified stored catch records exactly once");
        var dialFacts=new FishingExpedition();
        dialFacts.Accept();dialFacts.ReadRecord();dialFacts.FindComponent();
        var dial=new FishingBeaconDial(5);
        check(!dial.Active && !dial.Confirm(dialFacts), "Dial requires active interaction");
        float originalHeading=dial.Heading;dial.Rotate(90);
        check(dial.Heading==originalHeading,"Inactive dial ignores rotation");
        dial.Begin();
        check(!dial.Confirm(dialFacts) && dialFacts.Facts==7,"Misaligned confirmation preserves component");
        dial.Rotate(70);
        check(dial.Aligned && Math.Abs(dial.Error-5)<.001,"Dial wraps across north and gives signed error");
        dial.Cancel();
        check(!dial.Active && dialFacts.Facts==7 && !dial.Confirm(dialFacts),"Cancel does not repair or consume component");
        dial.Begin();check(dial.Confirm(dialFacts) && !dial.Active && dialFacts.Facts==15,"Aligned explicit confirm repairs once");
        dial.Begin();check(!dial.Confirm(dialFacts),"Repeated repair does not grant progress");dial.Cancel();
        var boundary=new FishingBeaconDial(0,8);boundary.Begin();boundary.Rotate(67);
        check(boundary.Aligned && boundary.Error==8,"Dial inclusive tolerance boundary");
        boundary.Rotate(-.01f);check(!boundary.Aligned,"Dial rejects just outside tolerance");
        boundary.Rotate(float.NaN);boundary.Rotate(float.PositiveInfinity);
        check(!float.IsNaN(boundary.Heading) && !float.IsInfinity(boundary.Heading),"Dial ignores invalid input");
        boundary.Rotate(720.01f);check(boundary.Aligned,"Dial handles multiple rotations");
        check(!boundary.Confirm(new FishingExpedition()) && !boundary.Confirm(null),"Dial cannot bypass prerequisites");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            bool threw=false;try{new FishingBeaconDial(invalid);}catch(ArgumentOutOfRangeException){threw=true;}
            check(threw,"Dial rejects invalid target "+invalid);
        }
        foreach(float invalid in new[]{-1f,46f,float.NaN})
        {
            bool threw=false;try{new FishingBeaconDial(0,invalid);}catch(ArgumentOutOfRangeException){threw=true;}
            check(threw,"Dial rejects invalid tolerance "+invalid);
        }
        var trialDial = new FishingBeaconDial(5);
        var repair = new FishingExpedition();
        check(!trialDial.Active && !trialDial.Aligned && trialDial.Heading == 290, "Dial starts inactive and away from target");
        trialDial.Rotate(75); check(trialDial.Heading == 290, "Inactive trialDial ignores input");
        trialDial.Begin(); check(trialDial.Active && trialDial.Error == 75, "Dial begins with directional error");
        trialDial.Rotate(40); check(!trialDial.Confirm(repair) && repair.Facts == 0 && trialDial.Active, "Wrong heading cannot repair or close trialDial");
        trialDial.Rotate(35); check(trialDial.Aligned && !trialDial.Confirm(repair), "Aligned trialDial still requires quest facts");
        repair.Accept(); repair.ReadRecord(); repair.FindComponent();
        trialDial.Cancel(); check(!trialDial.Confirm(repair) && repair.Facts == 7, "Cancel preserves component and rejects confirm");
        trialDial.Begin(); check(trialDial.Confirm(repair) && !trialDial.Active && repair.Has(FishingExpedition.BeaconRepaired), "Explicit aligned confirm repairs once");
        trialDial.Begin(); check(!trialDial.Confirm(repair), "Repeated trialDial repair has no effect"); trialDial.Cancel();
        var edge = new FishingBeaconDial(359,8); edge.Begin(); edge.Rotate(75+8);
        check(edge.Aligned && edge.Error == -8, "Dial accepts tolerance across north wrap");
        edge.Rotate(1f); check(!edge.Aligned && edge.Error < 0, "Dial rejects beyond tolerance and indicates left");
        edge.Rotate(-18f); check(!edge.Aligned && edge.Error > 0, "Dial indicates right on other side");
        edge.Rotate(1f); check(edge.Aligned, "Dial accepts opposite tolerance boundary");
        float heading = edge.Heading;
        edge.Rotate(float.NaN); edge.Rotate(float.PositiveInfinity);
        check(edge.Heading == heading, "Dial ignores nonfinite input");
        edge.Rotate(720); check(edge.Heading == heading, "Dial normalizes full rotations");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity})
        {
            bool rejected=false; try { new FishingBeaconDial(invalid); } catch(ArgumentOutOfRangeException) { rejected=true; }
            check(rejected,"Dial rejects invalid target "+invalid);
        }
        foreach(float invalid in new[]{-1f,46f,float.NaN})
        {
            bool rejected=false; try { new FishingBeaconDial(0,invalid); } catch(ArgumentOutOfRangeException) { rejected=true; }
            check(rejected,"Dial rejects invalid tolerance "+invalid);
        }
        var normal = new FishingExpedition();
        check(normal.Accept() && normal.ReadRecord() && normal.FindComponent() && normal.RepairBeacon()
            && normal.RecordCatch(true, true) && normal.Deliver(), "Expedition normal route completes");
    }
}
