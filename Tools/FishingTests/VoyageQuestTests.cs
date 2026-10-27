using System;
using BoatAttack.Fishing;

internal static class VoyageQuestTests
{
    private static int Slot(FishingBackpack bag, FieldItem item)
    {
        for (int i = 0; i < FishingBackpack.SlotCount; i++) if (bag.At(i).item == item) return i;
        return -1;
    }
    private static FishingCareer Ready()
    {
        var c = new FishingCareer(); c.AcceptMayorship(); c.DiscoverChart();
        foreach (int island in new[] { 2, 0, 3, 1 }) { c.CompleteShrine(island); c.ClaimShrineReward(island); }
        return c;
    }

    public static void Run(Action<bool, string> check)
    {
        var c = new FishingCareer();
        check(c.PlayerTitle == "返乡旅人" && c.MainObjective.Contains("接任") && !c.DiscoverChart(), "Quest: new arrival must accept the village role before discovering the chart");
        check(c.AcceptMayorship() && !c.AcceptMayorship() && c.PlayerTitle == "回潮村村长" && c.MainObjective.Contains("书桌"), "Quest: acceptance advances the objective exactly once");
        check(c.DiscoverChart() && !c.DiscoverChart() && c.MainObjective.Contains("0 / 4"), "Quest: the old chart permanently reveals the four-realm objective");
        check(!c.ClaimShrineReward(-1) && !c.ClaimShrineReward(4) && !c.ClaimShrineReward(0) && c.OpenGrandTreasure() == 0, "Quest: invalid, incomplete and premature claims grant nothing");
        int coins = 0;
        foreach (int island in new[] { 3, 1, 0, 2 })
        {
            check(c.CompleteShrine(island) == 90 && c.CompleteShrine(island) == 0, "Quest: nonlinear completion preserves the original once-only coin reward " + island);
            coins += 90;
            check(c.ClaimShrineReward(island) && !c.ClaimShrineReward(island) && c.Coins == coins
                && c.Backpack.Count(FishingLore.Relic(island)) == 1 && c.Lore.BlueprintUnlocked(island), "Quest: relic, materials and permanent blueprint are claimed together once " + island);
            check(FishingCareer.TryRestore(c.Snapshot(), out var loaded) && loaded.Lore.BlueprintUnlocked(island)
                && loaded.Coins == coins && !loaded.ClaimShrineReward(island), "Quest: claimed rewards survive reload without repeat materials " + island);
            c = loaded;
        }
        check(c.Lore.RewardedShrines == 15 && c.MainObjective.Contains("工作台") && !c.PirateKing, "Quest: four completed realms still require assembling the compass and reaching the treasure");
        check(!c.CraftVoyageCompass(CraftStation.Forge) && c.Backpack.Count(FieldItem.WindRelic) == 1, "Quest: the wrong assembly station cannot consume unique relics");
        check(c.CraftVoyageCompass(CraftStation.Workbench) && !c.CraftVoyageCompass(CraftStation.Workbench)
            && c.Backpack.Count(FieldItem.VoyageCompass) == 1 && c.Backpack.Count(FieldItem.IronIngot) == 2 && c.Backpack.Count(FieldItem.Glass) == 4, "Quest: one compass consumes four relics, two iron ingots and two glass exactly once");
        for (int i = 0; i < 4; i++) check(c.Backpack.Count(FishingLore.Relic(i)) == 0 && c.Lore.BlueprintUnlocked(i), "Quest: assembled relics leave permanent facility blueprints " + i);
        bool restoredCompass = FishingCareer.TryRestore(c.Snapshot(), out var assembled);
        check(c.MainObjective.Contains("白帆拱湾") && restoredCompass && assembled.Backpack.Count(FieldItem.VoyageCompass) == 1, "Quest: the assembled compass and destination survive reload");
        c = assembled;
        int compassSlot = Slot(c.Backpack, FieldItem.VoyageCompass);
        check(!c.Backpack.Discard(compassSlot, FieldItem.VoyageCompass, 1) && c.Backpack.TransferTo(c.HomeStorage, compassSlot)
            && c.MainObjective.Contains("储物箱") && c.OpenGrandTreasure() == 0, "Quest: a compass is protected from discard, may be stored safely, and must be carried to open treasure");
        check(FishingCareer.TryRestore(c.Snapshot(), out var stored) && stored.HomeStorage.Count(FieldItem.VoyageCompass) == 1
            && stored.HomeStorage.TransferTo(stored.Backpack, Slot(stored.HomeStorage, FieldItem.VoyageCompass)), "Quest: storage and withdrawal preserve one compass across reload");
        c = stored;
        check(c.SellAll() == 0 && c.SellTurnips() == 0 && !c.OpenSeaTreasure() && c.Backpack.Count(FieldItem.VoyageCompass) == 1, "Quest: fish, produce and sea-find transactions cannot sell or open a quest compass");
        var capped = c.Snapshot(); capped.coins = 100000000 - FishingCareer.GrandTreasureCoins + 1;
        check(FishingCareer.TryRestore(capped, out var rich) && rich.OpenGrandTreasure() == 0 && !rich.PirateKing
            && rich.Backpack.Count(FieldItem.VoyageCompass) == 1, "Quest: insufficient wallet headroom preserves the key and unpaid finale");
        int before = c.Coins;
        check(c.OpenGrandTreasure() == FishingCareer.GrandTreasureCoins && c.Coins == before + 1200 && c.PirateKing
            && c.PlayerTitle == "海贼王" && c.Backpack.Count(FieldItem.VoyageCompass) == 0 && c.OpenGrandTreasure() == 0, "Quest: the final chest consumes one key, pays 1200 once and grants the permanent title");
        check(!c.Lore.Finale && c.CompleteEchoVoyage() == 180 && c.CompleteEchoVoyage() == 0, "Quest: treasure ownership remains independent of the original 180-coin echo-report finale");
        check(FishingCareer.TryRestore(c.Snapshot(), out var king) && king.PirateKing && king.Lore.Finale
            && king.OpenGrandTreasure() == 0 && king.MainObjective.Contains("继续建设"), "Quest: completed treasure and old report receipts survive reload independently");

        var packed = new FishingCareer(); packed.AcceptMayorship(); packed.DiscoverChart(); packed.CompleteShrine(0);
        packed.Backpack.TryAdd(FieldItem.Stone, 64 * 34);
        check(!packed.ClaimShrineReward(0) && packed.Backpack.Count(FieldItem.WindRelic) == 0 && packed.Backpack.Count(FieldItem.Rope) == 0
            && !packed.Lore.RewardClaimed(0) && packed.Coins == 90, "Quest: a one-free-slot backpack rolls back the whole two-stack shrine package");
        packed.Backpack.Discard(Slot(packed.Backpack, FieldItem.Stone), FieldItem.Stone, 64);
        check(packed.ClaimShrineReward(0) && packed.Backpack.Count(FieldItem.Rope) == 4, "Quest: deferred shrine loot remains available after making room");
        check(!packed.Backpack.Discard(Slot(packed.Backpack, FieldItem.WindRelic), FieldItem.WindRelic, 1), "Quest: shrine relics cannot be discarded accidentally");
        check(packed.Backpack.TransferTo(packed.HomeStorage, Slot(packed.Backpack, FieldItem.WindRelic))
            && FishingCareer.TryRestore(packed.Snapshot(), out _), "Quest: a deposited relic remains valid without duplicating the reward receipt");

        foreach (int mask in new[] { 0, 5, 15 })
        {
            var old = new FishingCareer();
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) old.CompleteShrine(i);
            if (mask == 15) old.CompleteEchoVoyage();
            var legacy = old.Snapshot(); legacy.version = 15;
            check(FishingCareer.TryRestore(legacy, out var upgraded) && upgraded.Coins == old.Coins && upgraded.Lore.ClearCount == old.Lore.ClearCount
                && upgraded.Lore.Finale == old.Lore.Finale && !upgraded.PirateKing && upgraded.Lore.RewardedShrines == 0, "Quest: v15 migration preserves paid money and old finale without inventing new rewards " + mask);
            check(!legacy.lore.mayorAccepted && !legacy.lore.chartFound && legacy.lore.rewardedShrines == 0, "Quest: legacy migration never writes into the input DTO " + mask);
            if (mask != 0)
            {
                check(upgraded.Lore.MayorAccepted && upgraded.Lore.ChartFound, "Quest: an established old voyage receives its missing narrative facts " + mask);
                for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0)
                    check(upgraded.ClaimShrineReward(i) && !upgraded.ClaimShrineReward(i) && upgraded.Coins == old.Coins, "Quest: an old cleared realm owes one new loot package and zero repeat coins " + mask + "/" + i);
            }
            check(FishingCareer.TryRestore(upgraded.Snapshot(), out _), "Quest: migrated progress can be saved and loaded as v16 " + mask);
        }
        var sailed = new FishingCareer(); sailed.DiscoverIsland(0); var sailedSave = sailed.Snapshot(); sailedSave.version = 15;
        check(FishingCareer.TryRestore(sailedSave, out var sailor) && sailor.Lore.ChartFound && sailor.Coins == sailed.Coins, "Quest: old island-only progress receives the narrative migration without replaying payment");

        var bad = new FishingCareer().Snapshot(); bad.lore.chartFound = true;
        check(!FishingCareer.TryRestore(bad, out _), "Quest: a chart without an accepted role is rejected");
        bad = new FishingCareer().Snapshot(); bad.lore.rewardedShrines = 1;
        check(!FishingCareer.TryRestore(bad, out _), "Quest: loot receipts without a cleared realm are rejected");
        bad = new FishingCareer().Snapshot(); bad.lore.rewardedShrines = 16;
        check(!FishingCareer.TryRestore(bad, out _), "Quest: unknown shrine-reward bits are rejected");
        bad = new FishingCareer().Snapshot(); bad.lore.grandTreasureFound = true;
        check(!FishingCareer.TryRestore(bad, out _), "Quest: an unearned final treasure cannot grant the title");
        bad = Ready().Snapshot(); bad.homeStorage[0] = new FieldStack { item = FieldItem.WindRelic, count = 1 };
        check(!FishingCareer.TryRestore(bad, out _), "Quest: unique relics cannot be duplicated across backpack and home storage");
        bad = Ready().Snapshot(); bad.backpack[Slot(Ready().Backpack, FieldItem.WindRelic)] = new FieldStack();
        check(!FishingCareer.TryRestore(bad, out _), "Quest: a lost claimed relic cannot silently destroy the route to the finale");
        bad = Ready().Snapshot(); bad.version = 15;
        check(!FishingCareer.TryRestore(bad, out _), "Quest: old schemas cannot conceal new quest receipts or items");
        bad = new FishingCareer().Snapshot(); bad.version = 15; bad.backpack[1] = new FieldStack { item = FieldItem.VoyageCompass, count = 1 };
        check(!FishingCareer.TryRestore(bad, out _), "Quest: a legacy backpack cannot smuggle a final key");
        var independent = Ready(); var copy = independent.Snapshot(); copy.lore.rewardedShrines = 0;
        check(independent.Lore.RewardedShrines == 15, "Quest: quest snapshots do not alias live narrative or reward facts");

        check((int)FieldItem.IslandFruit == 46 && (int)FieldItem.WindRelic == 47 && (int)FieldItem.VoyageCompass == 51, "Quest: new item IDs append after all v15 items");
        for (int i = 0; i < 5; i++)
        {
            var item = (FieldItem)((int)FieldItem.WindRelic + i);
            check(FishingBackpack.IsQuestItem(item) && FishingBackpack.Limit(item) == 1 && FishingBackpack.Name(item) != "空槽", "Quest: protected quest items have names and one-per-slot limits " + item);
        }
        check(FishingSupplies.Items.Length >= 24 && FishingSupplies.CategorySize == 6 && FishingSupplies.CategoryCount == FishingSupplies.Items.Length / FishingSupplies.CategorySize
            && FishingSupplies.CategoryName(3) == "建设物资" && FishingSupplies.CategoryName(-1) == "", "Quest: construction supplies extend the stable product catalog by one six-item category");
        var spending = new FishingCareer().Snapshot(); spending.coins = 1200; FishingCareer.TryRestore(spending, out var buyer);
        int spent = 0;
        for (int product = 18; product < 24; product++)
        {
            spent += FishingSupplies.Price(product);
            check(buyer.BuySupply(product) && buyer.Coins == 1200 - spent && buyer.Backpack.Count(FishingSupplies.Items[product]) == FishingSupplies.Quantity(product), "Quest: treasure income buys a real construction product with exact settlement " + product);
        }
        check(FishingCareer.TryRestore(buyer.Snapshot(), out _), "Quest: construction purchases preserve readable saves");
    }
}
