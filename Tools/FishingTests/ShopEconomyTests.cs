using System;
using BoatAttack.Fishing;

internal static class ShopEconomyTests
{
    public static void Run(Action<bool, string> check)
    {
        var career = new FishingCareer();
        check(!career.Upgrade(UpgradeTrack.Line) && career.Coins == 0, "Shop: empty wallet cannot buy equipment");
        int earned = 0;
        for (int i = 0; i < 80; i++)
        {
            var fish = new CatchRecord { id = "shop-sale-" + i, kind = FishKind.Puffer, grams = 2000,
                value = FishingSpecies.Value(FishKind.Puffer, false, 2000) };
            check(career.BeginPending(fish, 2, 0, 0) && career.ResolvePending(true, out _, out _), "Shop: settle catch " + i);
            check(career.Coins == earned, "Shop: storing fish does not mint coins " + i);
            int sale = career.SellAll(); earned += sale;
            check(sale == fish.value && career.Coins == earned && career.SellAll() == 0, "Shop: sale credits shared wallet exactly once " + i);
        }
        int spent = 0;
        foreach (UpgradeTrack track in Enum.GetValues(typeof(UpgradeTrack)))
        {
            check(FishingText.ShopProductName(track).Length > 0 && FishingText.ShopProductDescription(track).Length > 0, "Shop: existing equipment has product text " + track);
            for (int level = 1; level <= FishingCareer.MaxLevel; level++)
            {
                int price = career.NextUpgradeCost(track);
                spent += price;
                check(career.Upgrade(track) && career.Level(track) == level && career.Coins == earned - spent, "Shop: fishing income buys " + track + " level " + level);
                check(FishingCareer.TryRestore(career.Snapshot(), out var restored) && restored.Coins == career.Coins && restored.Level(track) == level, "Shop: wallet/equipment survive reload " + track + level);
                career = restored;
            }
            int balance = career.Coins;
            check(!career.Upgrade(track) && career.Coins == balance, "Shop: max-level purchase cannot charge twice " + track);
        }
        check(career.Capacity == FishingCareer.BaseCapacity + 4 && CatchSession.LineFactor(career.LineLevel) < 1f && CatchSession.ReelFactor(career.ReelLevel) > 1f, "Shop: purchases affect hold, line and reel gameplay");
        check(!career.Upgrade((UpgradeTrack)99) && career.Coins == earned - spent, "Shop: invalid product cannot spend money");
    }
}
