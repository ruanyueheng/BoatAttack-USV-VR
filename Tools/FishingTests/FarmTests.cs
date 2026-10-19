using System;
using BoatAttack.Fishing;

internal static class FarmTests
{
    public static void Run(Action<bool,string> check)
    {
        var farm=new FishingFarm();var bag=new FishingBackpack(false);
        check(!farm.Till(-1) && !farm.Plant(0,bag) && !farm.WaterPlot(0) && !farm.Harvest(0,bag),"Farm: invalid order and plot ids cannot mutate the farm");
        check(farm.ClaimStarter(bag) && !farm.ClaimStarter(bag) && bag.Count(FieldItem.TurnipSeeds)==12,"Farm: the starter packet is awarded once");
        check(farm.Till(0) && !farm.Till(0) && farm.Plant(0,bag) && !farm.Plant(0,bag) && bag.Count(FieldItem.TurnipSeeds)==11,"Farm: planting consumes exactly one seed on tilled soil");
        check(!farm.Harvest(0,bag) && farm.AdvanceDay(1) && farm.Plot(0).growth==0,"Farm: a dry night neither grows nor destroys the crop");
        check(farm.WaterPlot(0) && !farm.WaterPlot(0) && farm.Water==11,"Farm: repeated input cannot charge watering twice");
        check(farm.AdvanceDay(2) && !farm.AdvanceDay(2) && !farm.AdvanceDay(1) && farm.Plot(0).growth==1,"Farm: growth happens once at a forward day boundary");
        check(farm.AdvanceDay(8) && farm.Plot(0).growth==1,"Farm: skipping unwatered days does not invent growth");
        farm.WaterPlot(0);farm.AdvanceDay(9);farm.WaterPlot(0);farm.AdvanceDay(10);
        check(farm.Ready(0) && !farm.WaterPlot(0),"Farm: three watered nights produce a mature crop");
        var full=new FishingBackpack(false);for(int i=0;i<FishingBackpack.SlotCount;i++)full.TryAdd(FieldItem.Stone,64);
        check(!farm.Harvest(0,full) && farm.Ready(0),"Farm: a full bag leaves the mature crop and seed safe in the ground");
        full.Discard(0,FieldItem.Stone,64);
        check(!farm.Harvest(0,full) && full.Count(FieldItem.Turnip)==0 && farm.Ready(0),"Farm: both harvest outputs must fit atomically");
        check(farm.Harvest(0,bag) && !farm.Harvest(0,bag) && bag.Count(FieldItem.Turnip)==1 && bag.Count(FieldItem.TurnipSeeds)==12 && farm.Plot(0).tilled,"Farm: harvest grants one vegetable and one renewable seed exactly once");
        check(farm.Refill() && !farm.Refill() && farm.Water==FishingFarm.WaterCapacity,"Farm: the well refills a bounded can");
        var snap=farm.Snapshot();snap.plots[0].growth=3;
        check(farm.Plot(0).growth==0 && !FishingFarm.TryRestore(snap,10,out _),"Farm: copied snapshots cannot modify live soil and impossible growth is rejected");
        snap=farm.Snapshot();snap.day=11;check(!FishingFarm.TryRestore(snap,10,out _),"Farm: future-day save exploits are rejected");
        snap=farm.Snapshot();snap.water=13;check(!FishingFarm.TryRestore(snap,10,out _),"Farm: oversized water payload is rejected");
        snap=farm.Snapshot();snap.plots.RemoveAt(0);check(!FishingFarm.TryRestore(snap,10,out _),"Farm: a truncated plot payload is rejected");
        var legacy=new FishingCareer().Snapshot();legacy.version=13;legacy.calendar.day=37;
        check(FishingCareer.TryRestore(legacy,out var career) && career.Farm.Day==37 && !career.Farm.StarterClaimed,"Farm: v13 migration preserves the date and offers an unused starter packet");
        career.Farm.ClaimStarter(career.Backpack);career.Farm.Till(4);career.Farm.Plant(4,career.Backpack);career.Farm.WaterPlot(4);
        check(FishingCareer.TryRestore(career.Snapshot(),out var loaded) && loaded.Farm.Plot(4).wateredDay==37 && loaded.Farm.Water==11 && loaded.Farm.StarterClaimed,"Farm: soil, planting, water and packet survive career restore");
        var forged=career.Snapshot();forged.version=13;check(!FishingCareer.TryRestore(forged,out _),"Farm: legacy schema cannot hide paid seed or crop state");
        int coins=career.Coins;check(!career.BuyFarmSeeds() && career.Coins==coins,"Farm: missing money never creates purchased seeds");
        var wallet=new FishingCareer().Snapshot();wallet.coins=24;check(FishingCareer.TryRestore(wallet,out var shop) && shop.BuyFarmSeeds() && shop.Coins==12 && shop.Backpack.Count(FieldItem.TurnipSeeds)==6,"Farm: seed purchase uses the real wallet and backpack");
        shop.Backpack.TryAdd(FieldItem.Turnip,2);check(shop.SellTurnips()==36 && shop.Coins==48 && shop.SellTurnips()==0,"Farm: the produce crate sells each vegetable once");
        wallet=shop.Snapshot();wallet.coins=100000000;FishingCareer.TryRestore(wallet,out shop);shop.Backpack.TryAdd(FieldItem.Turnip,1);
        check(shop.SellTurnips()==0 && shop.Backpack.Count(FieldItem.Turnip)==1,"Farm: wallet cap preserves unsold produce");
        for(int i=0;i<FishingFarm.PlotCount;i++){var p=farm.Plot(i);check(p!=null,"Farm: all twelve plots have independent state "+i);}
    }
}
