using System;
using System.Collections.Generic;
using BoatAttack.Fishing;

internal static class AdventureTests
{
    public static void Run(Action<bool,string> check)
    {
        var legacy=new FishingCareer().Snapshot();legacy.version=9;legacy.coins=300;legacy.rodLevel=1;
        check(FishingCareer.TryRestore(legacy,out var c) && c.LineLevel==1 && c.EngineLevel==0 && !c.Adventure.Visited(0),"V18: v9 preserves money/gear with no invented island rewards");
        check(c.Upgrade(UpgradeTrack.Engine) && c.Coins==220 && c.EngineLevel==1,"V18: engine uses shared coin wallet");
        check(c.Upgrade(UpgradeTrack.Engine) && c.Coins==40 && !c.Upgrade(UpgradeTrack.Engine),"V18: engine cap never charges twice");
        check(FishingFleet.Power(0)>7 && FishingFleet.Power(2)>FishingFleet.Power(1) && FishingFleet.SpeedLimit(2)>FishingFleet.SpeedLimit(0)
            && FishingFleet.Rudder(2)>FishingFleet.Rudder(0),"V18: base and purchased propulsion/steering increase");
        check(!c.BuySupply(-1) && !c.BuySupply(99) && c.Coins==40,"V18: unknown supplies reject without spending");
        check(c.BuySupply(1) && c.Coins==0 && c.Backpack.Count(FieldItem.Lantern)==1,"V18: purchased lantern is a real inventory item");
        check(!c.BuySupply(1) && c.Coins==0,"V18: insufficient funds reject supply purchase");
        int total=0;
        foreach(int island in new[]{3,0,2,1})
        {
            check(c.OpenIslandTreasure(island)==0 && c.RestoreIslandBeacon(island)==0,"V18: reward prerequisites "+island);
            total+=c.DiscoverIsland(island);
            check(c.DiscoverIsland(island)==0 && c.Adventure.Visited(island),"V18: island discovery is paid once "+island);
            total+=c.OpenIslandTreasure(island);
            check(c.OpenIslandTreasure(island)==0,"V18: treasure paid once "+island);
            int desired=island==0?3:island==1?0:island==2?1:2;
            for(int d=0;d<desired;d++){check(c.RestoreIslandBeacon(island)==0,"V18: wrong compass gives no reward");c.Adventure.Rotate(island);}
            total+=c.RestoreIslandBeacon(island);
            check(!c.Adventure.Rotate(island) && c.RestoreIslandBeacon(island)==0,"V18: restored compass cannot mint more coins "+island);
            check(FishingCareer.TryRestore(c.Snapshot(),out var restored) && restored.Coins==c.Coins && restored.Adventure.Restored(island),"V18: island progress survives reload "+island);c=restored;
        }
        check(total==500 && c.Coins==500 && c.CompleteIslandVoyage()==150 && c.CompleteIslandVoyage()==0 && c.Coins==650,"V18: non-linear adventure pays 650 total, finale once");
        var snap=c.Snapshot();snap.adventure.directions[0]=2;
        check(!FishingCareer.TryRestore(snap,out _),"V18: impossible restored compass rejected");
        snap=c.Snapshot();snap.engineLevel=3;check(!FishingCareer.TryRestore(snap,out _),"V18: corrupt fleet level rejected");
        snap=c.Snapshot();snap.adventure.visited=0;check(!FishingCareer.TryRestore(snap,out _),"V18: rewards without island discovery rejected");
        var full=new FishingCareer().Snapshot();full.coins=1000;full.backpack=new List<FieldStack>();
        for(int i=0;i<FishingBackpack.SlotCount;i++)full.backpack.Add(new FieldStack{item=FieldItem.Stone,count=64});
        check(FishingCareer.TryRestore(full,out c) && !c.BuySupply(0) && c.Coins==1000 && c.Backpack.UsedSlots==36,"V18: full bag purchase is atomic, no lost coins");
        for(int i=0;i<FishingSupplies.Items.Length;i++)check(FishingSupplies.Price(i)>0 && FishingSupplies.Quantity(i)>0 && FishingBackpack.Defined(FishingSupplies.Items[i]),"V18: valid supply product "+i);
        var capped=new FishingCareer().Snapshot();capped.coins=100000000;
        check(FishingCareer.TryRestore(capped,out c) && c.DiscoverIsland(0)==0 && !c.Adventure.Visited(0) && FishingCareer.TryRestore(c.Snapshot(),out _),"V18: wallet limit cannot consume a reward or create an unreadable save");
        legacy.adventure.visited=1;check(!FishingCareer.TryRestore(legacy,out _),"V18: older schema cannot conceal new paid progress");
    }
}
