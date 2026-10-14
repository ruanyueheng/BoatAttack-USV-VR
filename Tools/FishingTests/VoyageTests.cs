using System;
using BoatAttack.Fishing;
internal static class VoyageTests
{
    private static CatchRecord Fish(string id, FishKind kind, bool rare = false) => new CatchRecord { id = id, kind = kind, rare = rare, value = (10 + (int)kind * 6) * (rare ? 3 : 1) };
    internal static void Run(Action<bool,string> check)
    {
        foreach (FishKind kind in Enum.GetValues(typeof(FishKind)))
        foreach (BaitKind bait in Enum.GetValues(typeof(BaitKind)))
        {
            var session = new CatchSession();
            check(session.Cast(kind, false, 0, kind + "-" + bait, bait), "V3 cast " + kind + " / " + bait);
            for (int i = 0; i < 300 && session.Phase == CatchPhase.Waiting; i++) session.Tick(0.02f, false);
            session.Hook(); bool rest = false, windup = false, surge = false;
            for (int i = 0; i < 4000 && session.Phase == CatchPhase.Fighting; i++)
            {
                rest |= session.Beat == FishBeat.Rest; windup |= session.Beat == FishBeat.Windup; surge |= session.Beat == FishBeat.Surge;
                session.Tick(0.02f, session.Tension < 0.6f);
            }
            check(rest && (kind == FishKind.Coral || kind == FishKind.Sardine || (windup && surge)), "Fight telegraphs every required beat " + kind + " / " + bait);
            check(session.Phase == CatchPhase.Landing && session.Land() && session.Claim() != null, "V3 every bait/species combination catchable " + kind + " / " + bait);
        }
        check(!new CatchSession().Cast(FishKind.Coral, false, 0, "bad-bait", (BaitKind)20), "V3 invalid bait rejected");
        check(FishingVoyage.RareChance(BaitKind.Pearl) > FishingVoyage.RareChance(BaitKind.Balanced), "V3 pearl bait raises rare chance");
        check(FishingVoyage.TensionScale(BaitKind.Gentle) < 1 && FishingVoyage.ReelScale(BaitKind.Gentle) < 1, "V3 gentle bait trades speed for tension");
        check(FishingVoyage.TensionScale(BaitKind.Pearl) > 1, "V3 pearl bait adds risk");
        check(FishingVoyage.Beat(FishKind.Puffer, 4f) == FishBeat.Windup && FishingVoyage.Beat(FishKind.Puffer, 5f) == FishBeat.Surge, "V3 puffer windup precedes surge");
        var career = new FishingCareer();
        check(career.DeliverOrder() == 0 && career.Coins == 0 && career.OrdersCompleted == 0, "V3 empty order cannot pay");
        career.Store(Fish("rare-coral", FishKind.Coral, true));
        career.Store(Fish("normal-coral", FishKind.Coral));
        check(career.DeliverOrder() == 0 && career.Count == 2, "V3 incomplete order is atomic");
        career.Store(Fish("dart", FishKind.Dart)); career.Store(Fish("puffer", FishKind.Puffer));
        check(career.OrderReady, "V3 mixed species order ready");
        check(career.DeliverOrder() == 50 && career.Coins == 50 && career.OrdersCompleted == 1, "V3 first order pays fish value plus bonus");
        check(career.Count == 2 && career.HoldValue == 52 && career.Inventory[0].rare, "V3 delivery preserves rare and unrelated cargo");
        check(career.DeliverOrder() == 0 && career.Coins == 50, "V3 no duplicate order reward");
        check(career.Discovered(FishKind.Coral) && career.Discovered(FishKind.Coral,true) && career.Discovered(FishKind.Dart), "V3 discoveries survive delivery");
        career.Store(Fish("dart2", FishKind.Dart));
        check(career.DeliverOrder() == 70 && career.OrdersCompleted == 2 && career.Count == 1, "V3 second order changes requirements");
        career.Store(Fish("dart3", FishKind.Dart)); career.Store(Fish("puffer3", FishKind.Puffer));
        check(career.DeliverOrder() == 118 && career.OrdersCompleted == 3 && career.Count == 0, "V3 feast pays rare cargo full value");
        check(FishingOrders.For(0).Name == "海湾早餐" && FishingOrders.For(3).Name == FishingOrders.For(3).Name, "V4 tutorial orders stay first; later requests are deterministic");
        check(!career.Store(Fish("normal-coral", FishKind.Coral)), "V3 delivered IDs stay deduplicated");
        var data = career.Snapshot();
        check(data.version == 11 && FishingCareer.TryRestore(data, out var restored) && restored.DiscoveryMask == career.DiscoveryMask && restored.OrdersCompleted == 3 && restored.Catches == 7, "V3 extended progress round trip");
        var legacy = new FishingSaveData { version = 1, coins = 44, rodLevel = 1 };
        legacy.inventory.Add(Fish("legacy", FishKind.Dart, true)); legacy.recordedIds.Add("historical-sale");
        check(FishingCareer.TryRestore(legacy, out restored) && restored.Coins == 44 && restored.LineLevel == 1 && restored.Count == 1, "V3 migrates v1 wallet equipment hold");
        check(restored.Discovered(FishKind.Dart,true) && !restored.Discovered(FishKind.Coral) && restored.Catches == 1 && restored.OrdersCompleted == 0, "V3 migration never invents sold fish history");
        check(!restored.Store(Fish("historical-sale", FishKind.Coral)), "V3 migration retains sold ID deduplication");
        data = restored.Snapshot(); data.discoveryMask = 0;
        check(!FishingCareer.TryRestore(data,out _), "V3 rejects missing discovery for current cargo");
        data = restored.Snapshot(); data.catches = 0;
        check(!FishingCareer.TryRestore(data,out _), "V3 rejects count below cargo");
        data = restored.Snapshot(); data.discoveryMask = 128;
        check(!FishingCareer.TryRestore(data,out _), "V3 rejects unknown discovery bits");
        data = restored.Snapshot(); data.ordersCompleted = -1;
        check(!FishingCareer.TryRestore(data,out _), "V3 rejects negative completed orders");
        data = restored.Snapshot(); data.inventory[0].value = 999;
        check(restored.HoldValue == 48 && !FishingCareer.TryRestore(data,out _), "V3 snapshots isolated and forged payouts rejected");
    }
}
