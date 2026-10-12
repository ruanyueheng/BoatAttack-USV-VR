using System;
using BoatAttack.Fishing;
internal static class HuntingTests
{
    public static void Run(Action<bool,string> check)
    {
        check(FishingCareer.HuntingRiflePrice==3*FishingCareer.UpgradeCost(UpgradeTrack.Line,0),"Hunting price is three base rod upgrades");
        var save=new FishingCareer().Snapshot();save.coins=300;
        check(FishingCareer.TryRestore(save,out var c),"Hunting seeded valid wallet");
        check(c.BuyHuntingRifle() && c.Coins==210 && c.Backpack.Count(FieldItem.HuntingRifle)==1,"Hunting purchase atomically charges 90 and grants rifle");
        check(!c.BuyHuntingRifle() && c.Coins==210,"Hunting duplicate ownership never charges");
        check(!c.KillLandAnimal(0,LandAnimalKind.Rabbit,0,1,0,0),"Hunting requires equipped rifle");
        c.Backpack.Select(1);
        check(c.KillLandAnimal(0,LandAnimalKind.Rabbit,2,1,3,20),"Hunting records first hit");
        check(!c.KillLandAnimal(0,LandAnimalKind.Rabbit,2,1,3,20),"Hunting cannot kill same animal twice");
        check(!c.KillLandAnimal(8,LandAnimalKind.Rabbit,0,1,0,0) && !c.KillLandAnimal(1,LandAnimalKind.Rabbit,0,1,0,0)
            && !c.KillLandAnimal(1,LandAnimalKind.Fox,float.NaN,1,0,0),"Hunting rejects invalid IDs kinds and positions");
        check(c.Backpack.TryAdd(FieldItem.Stone,34*64),"Hunting fill remaining bag");
        check(!c.CollectHuntedAnimal(0) && !c.HuntedAnimal(0).collected,"Hunting full bag preserves corpse");
        c.Backpack.Discard(2,FieldItem.Stone,64);
        check(!c.CollectHuntedAnimal(0) && c.Backpack.Count(FieldItem.Meat)==0 && !c.HuntedAnimal(0).collected,"Hunting two-item loot remains atomic with only one free slot");
        c.Backpack.Discard(3,FieldItem.Stone,64);
        check(c.CollectHuntedAnimal(0) && c.Backpack.Count(FieldItem.Meat)==1 && c.Backpack.Count(FieldItem.Fur)==1,"Hunting rabbit yields meat and fur");
        check(!c.CollectHuntedAnimal(0) && c.Backpack.Count(FieldItem.Meat)==1,"Hunting no duplicate loot");
        check(FishingCareer.TryRestore(c.Snapshot(),out var reloaded) && reloaded.HuntedAnimal(0).collected && !reloaded.CollectHuntedAnimal(0),"Hunting kill and loot receipts survive save round trip");
        check(c.KillLandAnimal(1,LandAnimalKind.Fox,4,1,5,40),"Hunting fox dies once");
        check(c.CollectHuntedAnimal(1) && c.Backpack.Count(FieldItem.Meat)==3 && c.Backpack.Count(FieldItem.Fur)==2,"Hunting fox yields two meat and one fur");
        var isolated=c.HuntedAnimal(0);isolated.collected=false;
        check(c.HuntedAnimal(0).collected,"Hunting readers cannot mutate receipts");
        var duplicate=c.Snapshot();duplicate.huntedAnimals.Add(duplicate.huntedAnimals[0].Copy());
        check(!FishingCareer.TryRestore(duplicate,out _),"Hunting duplicate persisted receipts rejected");
        var old=new FishingCareer().Snapshot();old.huntedAnimals=null;
        check(FishingCareer.TryRestore(old,out var migrated) && migrated.HuntedAnimal(0)==null,"Hunting previous v8 saves load without invented kills");
        save=new FishingCareer().Snapshot();save.coins=300;
        FishingCareer.TryRestore(save,out var full);full.Backpack.TryAdd(FieldItem.Stone,35*64);
        check(!full.BuyHuntingRifle() && full.Coins==300,"Hunting full bag cannot lose purchase money");
        check(!new FishingCareer().BuyHuntingRifle(),"Hunting empty wallet cannot buy");
    }
}
