using System;
using System.Collections.Generic;
using BoatAttack.Fishing;

internal static class ProgressionTests
{
    private static CatchRecord Sized(string id, FishKind kind, int grams, bool rare = false)
        => new CatchRecord { id = id, kind = kind, rare = rare, grams = grams, value = FishingSpecies.Value(kind, rare, grams) };

    private static CatchSession Hooked(FishKind kind, BaitKind bait = BaitKind.Balanced, int line = 0, float size = 0.5f,
        int reel = 0, BayWeather weather = BayWeather.Clear)
    {
        var s = new CatchSession();
        s.Cast(kind, false, line, "hooked-" + Guid.NewGuid().ToString("N"), bait, weather, size, reel);
        for (int i = 0; i < 400 && s.Phase == CatchPhase.Waiting; i++) s.Tick(0.02f, false);
        s.Hook();
        return s;
    }

    private static FishingCareer Wallet(int coins)
    {
        FishingCareer.TryRestore(new FishingSaveData { backpack = new FishingBackpack().Snapshot(),  coins = coins }, out var career);
        return career;
    }

    internal static void Run(Action<bool, string> check)
    {
        Species(check);
        Sessions(check);
        Legend(check);
        Upgrades(check);
        Orders(check);
        Saves(check);
        Text(check);
    }

    private static void Species(Action<bool, string> check)
    {
        check(FishingSpecies.Count == Enum.GetValues(typeof(FishKind)).Length, "V4 species table covers every kind");
        bool complete = true;
        foreach (FishKind kind in Enum.GetValues(typeof(FishKind)))
        {
            var s = FishingSpecies.Of(kind);
            complete &= s.Kind == kind && s.Name.Length > 0 && s.Habitat.Length > 0 && s.MinGrams > 0 && s.MaxGrams > s.MinGrams && s.BaseValue > 0;
        }
        check(complete, "V4 species entries are complete and ordered");
        check(FishingSpecies.Of(FishKind.Sailfish).Legendary && !FishingSpecies.Of(FishKind.Puffer).Legendary, "V4 only the golden sail is legendary");
        check(FishingSpecies.AreaSpecies(0) == FishKind.Coral && FishingSpecies.AreaSpecies(2) == FishKind.Puffer && FishingSpecies.AreaSpecies(9) == FishKind.Puffer, "V4 fishing areas map to common species");
        check(!FishingSpecies.IsDefined((FishKind)FishingSpecies.Count) && FishingSpecies.Name((FishKind)999) == "未知鱼种", "V4 unknown species rejected");
        check(FishingSpecies.DiscoveryBit(FishKind.Coral, false) == 0 && FishingSpecies.DiscoveryBit(FishKind.Puffer, true) == 5 && FishingSpecies.DiscoveryBit(FishKind.Sailfish, false) == 6, "V4 discovery bits keep the v2 layout");
        check(FishingSpecies.Grams(FishKind.Coral, 0f) == 600 && FishingSpecies.Grams(FishKind.Coral, 1f) == 1400 && FishingSpecies.Grams(FishKind.Coral, float.NaN) == 1000, "V4 size roll maps to whole grams");
        check(FishingSpecies.ValidGrams(FishKind.Dart, 0) && !FishingSpecies.ValidGrams(FishKind.Sailfish, 0) && !FishingSpecies.ValidGrams(FishKind.Dart, 899) && FishingSpecies.ValidGrams(FishKind.Dart, 2200), "V4 weight bounds per species");
        check(FishingSpecies.Value(FishKind.Coral, false, 0) == 10 && FishingSpecies.Value(FishKind.Puffer, true, 0) == 66, "V4 weightless legacy prices unchanged");
        check(FishingSpecies.Value(FishKind.Coral, false, 600) == 8 && FishingSpecies.Value(FishKind.Coral, false, 1400) == 12 && FishingSpecies.Value(FishKind.Coral, true, 1400) == 36, "V4 size moves price from 80% to 120%");
        check(FishingSpecies.Value(FishKind.Sailfish, true, 18000) == FishingSpecies.Value(FishKind.Sailfish, false, 18000), "V4 legend has no pearl multiplier");
        check(BayClimate.BiteScale(BayWeather.Sunset, FishKind.Sailfish) == 1f && BayClimate.RareBonus(BayWeather.Sunset, FishKind.Sailfish) == 0f, "V4 weather favour never targets the legend");
    }

    private static void Sessions(Action<bool, string> check)
    {
        var small = new CatchSession();
        check(small.Cast(FishKind.Coral, false, 0, "small", BaitKind.Balanced, BayWeather.Clear, 0f) && small.Grams == 600, "V4 cast fixes the fish size");
        check(!new CatchSession().Cast(FishKind.Coral, false, 0, "nan", BaitKind.Balanced, BayWeather.Clear, float.NaN)
            && !new CatchSession().Cast(FishKind.Coral, false, 0, "huge", BaitKind.Balanced, BayWeather.Clear, 1.5f), "V4 invalid size rolls rejected");
        check(!new CatchSession().Cast(FishKind.Sailfish, true, 1, "pearl-legend"), "V4 legend cannot be a pearl variant");
        var big = Hooked(FishKind.Coral, size: 1f);
        for (int i = 0; i < 3000 && big.Phase == CatchPhase.Fighting; i++) big.Tick(0.02f, big.Tension < 0.6f);
        var claim = big.Phase == CatchPhase.Landing && big.Land() ? big.Claim() : null;
        check(claim != null && claim.grams == 1400 && claim.value == 12, "V4 claim carries weight and sized price");
        check(Hooked(FishKind.Coral, size: 1f).MaxDistance > Hooked(FishKind.Coral, size: 0f).MaxDistance, "V4 bigger fish start farther away");

        var missed = new CatchSession();
        missed.Cast(FishKind.Coral, false, 0, "missed");
        for (int i = 0; i < 400; i++) missed.Tick(0.02f, false);
        check(missed.Phase == CatchPhase.Escaped && missed.Failure == CatchFailure.MissedBite, "V4 missed bite reports its code");
        var snapped = Hooked(FishKind.Puffer);
        for (int i = 0; i < 500; i++) snapped.Tick(0.02f, true);
        check(snapped.Failure == CatchFailure.LineSnapped, "V4 snapped line reports its code");
        var slack = Hooked(FishKind.Coral);
        for (int i = 0; i < 500; i++) slack.Tick(0.02f, false);
        check(slack.Failure == CatchFailure.SlackLine, "V4 slack line reports its code");
        var late = Hooked(FishKind.Coral);
        for (int i = 0; i < 3000 && late.Phase == CatchPhase.Fighting; i++) late.Tick(0.02f, late.Tension < 0.6f);
        for (int i = 0; i < 600; i++) late.Tick(0.02f, false);
        check(late.Failure == CatchFailure.LandingTimeout, "V4 landing timeout reports its code");
        var clumsy = Hooked(FishKind.Coral);
        for (int i = 0; i < 3000 && clumsy.Phase == CatchPhase.Fighting; i++) clumsy.Tick(0.02f, clumsy.Tension < 0.6f);
        for (int i = 0; i < 20; i++) clumsy.Tick(0.02f, false);
        clumsy.Land(); clumsy.Land(); clumsy.Land();
        check(clumsy.Failure == CatchFailure.NetMissed, "V4 three missed scoops report their code");
        var reset = new CatchSession();
        reset.Cast(FishKind.Coral, false, 0, "reset");
        reset.Cancel();
        check(reset.Failure == CatchFailure.None && reset.Phase == CatchPhase.Idle, "V4 cancel clears the failure code");

        check(CatchSession.LineFactor(0) == 1f && CatchSession.LineFactor(1) == 0.72f && CatchSession.LineFactor(2) == 0.58f, "V4 line levels reduce tension growth");
        check(CatchSession.ReelFactor(2) > CatchSession.ReelFactor(1) && CatchSession.ReelFactor(1) > CatchSession.ReelFactor(0), "V4 reel levels speed up retrieval");
        var slow = Hooked(FishKind.Coral);
        var fast = Hooked(FishKind.Coral, reel: 2);
        for (int i = 0; i < 20; i++) { slow.Tick(0.02f, true); fast.Tick(0.02f, true); }
        check(fast.Distance < slow.Distance, "V4 upgraded reel retrieves more line");
        bool commonLeap = false;
        foreach (var kind in new[] { FishKind.Coral, FishKind.Dart, FishKind.Puffer })
            for (float t = 0; t < 20f; t += 0.05f) commonLeap |= FishingVoyage.Beat(kind, t) == FishBeat.Leap;
        check(!commonLeap, "V4 common fish never leap");
    }

    private static void Legend(Action<bool, string> check)
    {
        check(LegendRules.Conditions(1, BayWeather.Sunset, BaitKind.Pearl) && !LegendRules.Conditions(0, BayWeather.Sunset, BaitKind.Pearl)
            && !LegendRules.Conditions(1, BayWeather.Clear, BaitKind.Pearl) && !LegendRules.Conditions(1, BayWeather.Sunset, BaitKind.Balanced), "V4 legend needs channel, sunset and pearl bait");
        check(!LegendRules.CanStrike(1, BayWeather.Sunset, BaitKind.Pearl, 0) && LegendRules.CanStrike(1, BayWeather.Sunset, BaitKind.Pearl, 1), "V4 legend strikes only on a reinforced line");
        bool leaps = false;
        for (float t = 0; t < 6.8f; t += 0.05f) leaps |= FishingVoyage.Beat(FishKind.Sailfish, t) == FishBeat.Leap;
        check(leaps, "V4 legend rhythm includes a leap");
        var arc = Hooked(FishKind.Sailfish, BaitKind.Pearl, 1, 0.5f, 0, BayWeather.Sunset);
        float peak = 0f;
        bool bounded = true;
        for (int i = 0; i < 400 && arc.Phase == CatchPhase.Fighting; i++)
        {
            arc.Tick(0.02f, arc.Beat != FishBeat.Leap && arc.Tension < 0.6f);
            float p = arc.LeapProgress;
            bounded &= p >= 0f && p <= 1f && (arc.Beat == FishBeat.Leap || p == 0f);
            peak = Math.Max(peak, p);
        }
        check(bounded && peak > 0.9f, "V4 leap progress spans only the airborne window");

        var careless = Hooked(FishKind.Sailfish, BaitKind.Pearl, 1, 0.5f, 0, BayWeather.Sunset);
        bool sawLeap = false;
        for (int i = 0; i < 8000 && careless.Phase == CatchPhase.Fighting; i++)
        {
            bool leap = careless.Beat == FishBeat.Leap;
            sawLeap |= leap;
            careless.Tick(0.02f, leap || careless.Tension < 0.6f);
        }
        check(sawLeap && careless.Failure == CatchFailure.LeapSnapped, "V4 reeling through a leap snaps the line");
        var patient = Hooked(FishKind.Sailfish, BaitKind.Pearl, 1, 0.5f, 0, BayWeather.Sunset);
        for (int i = 0; i < 8000 && patient.Phase == CatchPhase.Fighting; i++)
            patient.Tick(0.02f, patient.Beat != FishBeat.Leap && patient.Tension < 0.6f);
        check(patient.Phase == CatchPhase.Landing && patient.LandZone < 0.25f, "V4 releasing during leaps lands the legend with a narrower net window");
        var legendClaim = patient.Land() ? patient.Claim() : null;
        check(legendClaim != null && legendClaim.kind == FishKind.Sailfish && legendClaim.grams >= 18000, "V4 legend claim is measured");

        var career = new FishingCareer();
        check(!career.Store(legendClaim), "V4 legend never takes hold space");
        int first = career.RecordLegend(legendClaim);
        check(first == LegendRules.Bounty(true, legendClaim.grams) && career.Coins == first && career.LegendCatches == 1
            && career.Discovered(FishKind.Sailfish) && career.BestGrams(FishKind.Sailfish) == legendClaim.grams, "V4 first release pays the full bounty and records the legend");
        check(career.RecordLegend(legendClaim) == 0 && career.LegendCatches == 1, "V4 a legend claim pays once");
        int again = career.RecordLegend(Sized("legend-2", FishKind.Sailfish, 32000));
        check(again == 70 && again < first && career.BestGrams(FishKind.Sailfish) == 32000, "V4 repeat releases pay a smaller bounty and keep records");
        check(career.RecordLegend(Sized("coral-not-legend", FishKind.Coral, 800)) == 0, "V4 common fish cannot claim a legend bounty");
        check(LegendRules.Bounty(true, 18000) == 150 && LegendRules.Bounty(true, 32000) == 210 && LegendRules.Bounty(false, 18000) == 40, "V4 legend bounty scale");

        var fresh = new FishingCareer();
        check(!LegendRules.ClueUnlocked(0, fresh) && !LegendRules.ClueUnlocked(1, fresh) && !LegendRules.ClueUnlocked(2, fresh) && !LegendRules.ClueUnlocked(9, fresh), "V4 legend clues start hidden");
        fresh.Store(Sized("clue-dart", FishKind.Dart, 1000));
        check(LegendRules.ClueUnlocked(0, fresh), "V4 catching a dart reveals the channel clue");
        check(FishingCareer.TryRestore(new FishingSaveData { version = 3, ordersCompleted = 2, rodLevel = 1 }, out var veteran)
            && LegendRules.ClueUnlocked(1, veteran) && LegendRules.ClueUnlocked(2, veteran), "V4 orders and line upgrades reveal the remaining clues");
        bool clueText = true;
        for (int i = 0; i < LegendRules.ClueCount; i++) clueText &= LegendRules.Clue(i).Length > 0 && LegendRules.ClueHint(i).Length > 0;
        check(clueText, "V4 every clue has text and an unlock hint");
    }

    private static void Upgrades(Action<bool, string> check)
    {
        check(FishingCareer.UpgradeCost(UpgradeTrack.Line, 0) == 30 && FishingCareer.UpgradeCost(UpgradeTrack.Reel, 1) == 100
            && FishingCareer.UpgradeCost(UpgradeTrack.Hold, 2) == 0 && FishingCareer.UpgradeCost((UpgradeTrack)7, 0) == 0, "V4 upgrade price table");
        var rich = Wallet(1000);
        check(rich.Upgrade(UpgradeTrack.Line) && rich.Upgrade(UpgradeTrack.Line) && rich.LineLevel == 2 && rich.Coins == 880, "V4 line upgrades in two paid steps");
        check(!rich.Upgrade(UpgradeTrack.Line) && rich.NextUpgradeCost(UpgradeTrack.Line) == 0 && rich.Coins == 880, "V4 completed track stops charging");
        check(rich.Upgrade(UpgradeTrack.Reel) && rich.Upgrade(UpgradeTrack.Reel) && rich.ReelLevel == 2 && rich.Coins == 740, "V4 reel upgrades");
        check(rich.Capacity == 6 && rich.Upgrade(UpgradeTrack.Hold) && rich.Capacity == 8 && rich.Upgrade(UpgradeTrack.Hold) && rich.Capacity == 10 && rich.Coins == 570, "V4 hold upgrades add two slots per level");
        check(!rich.Upgrade((UpgradeTrack)7) && rich.Coins == 570, "V4 unknown upgrade track rejected");
        int stored = 0;
        for (int i = 0; i < 12; i++) if (rich.Store(Sized("roomy" + i, FishKind.Coral, 700))) stored++;
        check(stored == 10 && rich.Count == 10, "V4 largest hold carries ten fish");
        var poor = Wallet(35);
        check(poor.Upgrade(UpgradeTrack.Line) && !poor.Upgrade(UpgradeTrack.Reel) && poor.Coins == 5, "V4 upgrades require the full price");
    }

    private static void Orders(Action<bool, string> check)
    {
        var o0 = FishingOrders.For(0);
        var o1 = FishingOrders.For(1);
        var o2 = FishingOrders.For(2);
        check(o0.Name == "海湾早餐" && o0.Bonus == 24 && o0.Required(FishKind.Coral) == 1 && o0.Required(FishKind.Dart) == 1 && o0.Required(FishKind.Puffer) == 0, "V4 first tutorial order preserved");
        check(o1.Bonus == 32 && o1.Required(FishKind.Coral) == 0 && o2.Bonus == 50 && o2.FishCount == 3, "V4 remaining tutorial orders preserved");
        check(FishingOrders.For(-4).Name == o0.Name, "V4 negative order index clamps to the first order");
        bool deterministic = true, valid = true, trophy = false, pearl = false, pair = false, feast = false;
        var names = new HashSet<string>();
        for (int n = 3; n < 600; n++)
        {
            var a = FishingOrders.For(n);
            var b = FishingOrders.For(n);
            deterministic &= a.Name == b.Name && a.Client == b.Client && a.Bonus == b.Bonus && a.LineCount == b.LineCount;
            valid &= a.Index == n && a.LineCount >= 1 && a.LineCount <= 3 && a.FishCount <= FishingCareer.BaseCapacity && a.Bonus > 0 && a.Name.Length > 0 && a.Client.Length > 0;
            var kinds = new HashSet<FishKind>();
            for (int l = 0; l < a.LineCount; l++)
            {
                var line = a.Line(l);
                var other = b.Line(l);
                deterministic &= line.Kind == other.Kind && line.Count == other.Count && line.MinGrams == other.MinGrams && line.Rare == other.Rare;
                valid &= kinds.Add(line.Kind) && line.Kind != FishKind.Sailfish && line.Count >= 1 && line.Count <= 2
                    && (line.MinGrams == 0 || line.MinGrams == FishingOrders.TrophyGrams(line.Kind));
                trophy |= line.MinGrams > 0;
                pearl |= line.Rare;
            }
            pair |= a.LineCount == 2;
            feast |= a.LineCount == 3;
            names.Add(a.Name);
        }
        check(deterministic, "V4 generated orders are deterministic");
        check(valid, "V4 generated orders are well formed and fit the base hold");
        check(trophy && pearl && pair && feast && names.Count >= 10, "V4 generated orders use all request types");

        int trophyIndex = 3;
        while (FishingOrders.For(trophyIndex).Line(0).MinGrams == 0) trophyIndex++;
        var trophyLine = FishingOrders.For(trophyIndex).Line(0);
        var species = FishingSpecies.Of(trophyLine.Kind);
        check(!trophyLine.Accepts(Sized("light", trophyLine.Kind, species.MinGrams)) && trophyLine.Accepts(Sized("heavy", trophyLine.Kind, species.MaxGrams))
            && !trophyLine.Accepts(new CatchRecord { id = "legacy", kind = trophyLine.Kind, value = species.BaseValue }), "V4 big-fish requests need a measured heavy fish");

        var hold = new List<CatchRecord> { Sized("pearl", FishKind.Coral, 1400, true), Sized("tiny", FishKind.Coral, 600), Sized("mid", FishKind.Coral, 1000), Sized("dart", FishKind.Dart, 900) };
        var picked = new List<int>();
        var perLine = new int[3];
        bool ready = FishingOrders.Select(FishingOrders.For(0), hold, picked, perLine);
        check(ready && picked.Count == 2 && picked.Contains(1) && picked.Contains(3) && perLine[0] == 1 && perLine[1] == 1, "V4 orders consume the cheapest eligible fish");
        bool partial = FishingOrders.Select(FishingOrders.For(1), hold, picked, perLine);
        check(!partial && picked.Count == 1 && perLine[0] == 1 && perLine[1] == 0, "V4 short orders report partial progress");

        var shop = new FishingCareer();
        shop.Store(Sized("s-coral", FishKind.Coral, 1000));
        shop.Store(Sized("s-dart", FishKind.Dart, 1000));
        shop.Store(Sized("s-puffer", FishKind.Puffer, 1200));
        check(shop.OrderReady && shop.ReservedCount == 2, "V4 open order reserves matching cargo");
        int puffer = FishingSpecies.Value(FishKind.Puffer, false, 1200);
        check(shop.SaleValue == puffer && shop.Sell() == puffer && shop.Count == 2 && shop.OrderReady, "V4 normal sale keeps order fish aboard and pays the previewed value");
        int rest = shop.HoldValue;
        check(shop.SellAll() == rest && shop.Count == 0 && shop.Coins == puffer + rest, "V4 sell-all empties the hold");
    }

    private static void Saves(Action<bool, string> check)
    {
        var full = Wallet(2000);
        full.Upgrade(UpgradeTrack.Line);
        full.Upgrade(UpgradeTrack.Reel);
        full.Upgrade(UpgradeTrack.Hold);
        full.Upgrade(UpgradeTrack.Hold);
        for (int i = 0; i < 10; i++) full.Store(Sized("full" + i, i % 2 == 0 ? FishKind.Dart : FishKind.Puffer, 1500 + i * 50));
        full.RecordLegend(Sized("full-legend", FishKind.Sailfish, 25000));
        full.Observe(MarineKind.Ray);
        var snap = full.Snapshot();
        check(snap.version == 12 && snap.holdLevel == 2 && snap.reelLevel == 1 && snap.rodLevel == 1 && snap.legendCatches == 1
            && snap.bestGrams.Count == FishingSpecies.Count && snap.inventory[0].grams == 1500, "V4 snapshot carries upgrades, sizes and legends");
        check(FishingCareer.TryRestore(snap, out var back) && back.Count == 10 && back.Capacity == 10 && back.LegendCatches == 1
            && back.BestGrams(FishKind.Sailfish) == 25000 && back.BestGrams(FishKind.Puffer) == snap.bestGrams[2] && back.Coins == full.Coins && back.Seen(MarineKind.Ray), "V4 v3 round trip");
        var shrunk = full.Snapshot();
        shrunk.holdLevel = 0;
        check(!FishingCareer.TryRestore(shrunk, out _), "V4 cargo above the saved hold size rejected");
        var bad = full.Snapshot();
        bad.bestGrams[0] = 5;
        check(!FishingCareer.TryRestore(bad, out _), "V4 impossible personal record rejected");
        bad = full.Snapshot();
        bad.bestGrams.Add(0);
        check(!FishingCareer.TryRestore(bad, out _), "V4 extra record slots rejected");
        bad = full.Snapshot();
        bad.legendCatches = 0;
        check(!FishingCareer.TryRestore(bad, out _), "V4 legend discovery without a release rejected");
        bad = full.Snapshot();
        bad.discoveryMask &= ~(1 << FishingSpecies.LegendDiscoveryBit);
        check(!FishingCareer.TryRestore(bad, out _), "V4 legend release without discovery rejected");
        bad = full.Snapshot();
        bad.catches = bad.inventory.Count;
        check(!FishingCareer.TryRestore(bad, out _), "V4 catch count must include released legends");
        bad = full.Snapshot();
        bad.inventory[1].grams = 99999;
        check(!FishingCareer.TryRestore(bad, out _), "V4 out-of-range cargo weight rejected");
        bad = full.Snapshot();
        bad.rodLevel = 3;
        check(!FishingCareer.TryRestore(bad, out _), "V4 unknown line level rejected");

        var oldV2 = new FishingSaveData { version = 2, coins = 12, rodLevel = 1 };
        check(FishingCareer.TryRestore(oldV2, out var migrated) && migrated.LineLevel == 1 && migrated.ReelLevel == 0 && migrated.Capacity == 6 && migrated.LegendCatches == 0, "V4 migrates v2 line upgrade");
        oldV2.reelLevel = 1;
        check(!FishingCareer.TryRestore(oldV2, out _), "V4 v2 save cannot claim v3 upgrades");
        oldV2.reelLevel = 0;
        oldV2.rodLevel = 2;
        check(!FishingCareer.TryRestore(oldV2, out _), "V4 v2 save keeps its single line level");
        var legacyHold = new FishingSaveData { version = 2, catches = 1, discoveryMask = 4 };
        legacyHold.inventory.Add(new CatchRecord { id = "old", kind = FishKind.Puffer, value = 22 });
        check(FishingCareer.TryRestore(legacyHold, out var old) && old.Inventory[0].grams == 0 && old.HoldValue == 22 && old.BestGrams(FishKind.Puffer) == 0, "V4 weightless v2 cargo keeps its old price");

        var busy = new FishingCareer();
        for (int i = 0; i < 300; i++) { busy.Store(Sized("bulk" + i, FishKind.Coral, 800)); busy.SellAll(); }
        check(busy.Snapshot().recordedIds.Count == FishingCareer.RecentIdLimit && !busy.Store(Sized("bulk299", FishKind.Coral, 800)), "V4 replay history is bounded and keeps recent IDs");
        for (int i = 0; i < 6; i++) busy.Store(Sized("aboard" + i, FishKind.Coral, 800));
        for (int i = 0; i < 300; i++) busy.RecordLegend(Sized("release" + i, FishKind.Sailfish, 20000));
        check(busy.Upgrade(UpgradeTrack.Hold) && busy.Capacity == 8, "V4 bounties can fund a larger hold");
        bool keptAboard = true;
        for (int i = 0; i < 6; i++) keptAboard &= !busy.Store(Sized("aboard" + i, FishKind.Coral, 800));
        check(keptAboard && busy.Count == 6, "V4 history trimming never forgets fish still aboard");
        var trimmed = busy.Snapshot();
        check(trimmed.recordedIds.Count <= FishingCareer.RecentIdLimit && FishingCareer.TryRestore(trimmed, out var reloaded) && reloaded.Count == 6, "V4 bounded history survives a round trip");
    }

    private static void Text(Action<bool, string> check)
    {
        bool texts = true;
        foreach (CatchFailure failure in Enum.GetValues(typeof(CatchFailure)))
            texts &= failure == CatchFailure.None ? FishingText.Failure(failure).Length == 0 : FishingText.Failure(failure).Length > 0;
        check(texts, "V4 every failure code has player wording");
        check(FishingText.Weight(1250) == "1.25 kg" && FishingText.Weight(25000) == "25.0 kg" && FishingText.Weight(0) == "—", "V4 weight formatting");
        check(FishingText.UpgradeName(UpgradeTrack.Hold) == "鱼箱" && FishingText.UpgradeEffect(UpgradeTrack.Hold, 2) == "容量 10 条", "V4 upgrade wording");
    }
}
