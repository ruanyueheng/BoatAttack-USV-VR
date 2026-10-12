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
        check(!c.CollectHuntedAnimal(0) && c.Backpack.Count(FieldItem.RabbitMeat)==0 && !c.HuntedAnimal(0).collected,"Hunting two-item loot remains atomic with only one free slot");
        c.Backpack.Discard(3,FieldItem.Stone,64);
        check(c.CollectHuntedAnimal(0) && c.Backpack.Count(FieldItem.RabbitMeat)==1 && c.Backpack.Count(FieldItem.RabbitFur)==1,"Hunting rabbit yields meat and fur");
        check(!c.CollectHuntedAnimal(0) && c.Backpack.Count(FieldItem.RabbitMeat)==1,"Hunting no duplicate loot");
        check(FishingCareer.TryRestore(c.Snapshot(),out var reloaded) && reloaded.HuntedAnimal(0).collected && !reloaded.CollectHuntedAnimal(0),"Hunting kill and loot receipts survive save round trip");
        check(c.KillLandAnimal(1,LandAnimalKind.Fox,4,1,5,40),"Hunting fox dies once");
        c.Backpack.Discard(4,FieldItem.Stone,64);c.Backpack.Discard(5,FieldItem.Stone,64);
        check(c.CollectHuntedAnimal(1) && c.Backpack.Count(FieldItem.FoxMeat)==2 && c.Backpack.Count(FieldItem.FoxFur)==1 && c.Backpack.Count(FieldItem.RabbitMeat)==1,"Hunting fox loot is separate from rabbit loot");
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
        var legacy=new FishingCareer().Snapshot();legacy.version=8;legacy.calendar=null;legacy.hunting=null;
        legacy.backpack[1]=new FieldStack{item=(FieldItem)23,count=1};
        legacy.backpack[2]=new FieldStack{item=(FieldItem)24,count=4};
        legacy.backpack[3]=new FieldStack{item=(FieldItem)25,count=2};legacy.selectedSlot=1;
        check(FishingCareer.TryRestore(legacy,out var migratedLocal) && migratedLocal.Backpack.Equipped==FieldItem.HuntingRifle
            && migratedLocal.Backpack.Count(FieldItem.Meat)==4 && migratedLocal.Backpack.Count(FieldItem.Fur)==2,"Local v8 rifle and generic loot migrate without ID collisions");
        check((int)legacy.backpack[1].item==23,"Migration does not mutate caller save");
        var main=new FishingCareer().Snapshot();main.backpack[1]=new FieldStack{item=FieldItem.HuntingSpear,count=1};
        main.backpack[2]=new FieldStack{item=FieldItem.RawMeat,count=3};
        check(FishingCareer.TryRestore(main,out var migratedMain) && migratedMain.Backpack.Count(FieldItem.HuntingSpear)==1
            && migratedMain.Backpack.Count(FieldItem.RawMeat)==3,"Latest main spear/raw meat IDs remain stable");
        save=new FishingCareer().Snapshot();save.coins=300;FishingCareer.TryRestore(save,out var hunter);hunter.BuyHuntingRifle();hunter.Backpack.Select(1);
        foreach(var pair in new[]{new[]{2,(int)LandAnimalKind.Tortoise},new[]{8,(int)LandAnimalKind.Gull},new[]{11,(int)LandAnimalKind.SeaTurtle}})
        {
            var kind=(LandAnimalKind)pair[1];
            check(hunter.KillLandAnimal(pair[0],kind,2,1,3,0) && hunter.CollectHuntedAnimal(pair[0]),"New hunt target and atomic species loot "+kind);
            foreach(var drop in FishingAnimalLoot.Drops(kind))check(hunter.Backpack.Count(drop.item)>=drop.count,"Species drop "+FishingBackpack.Name(drop.item));
        }
        check(hunter.Backpack.Count(FieldItem.TurtleShell)==2 && hunter.Backpack.Count(FieldItem.GullFeather)==1,"Turtle shells and gull feathers are not generic fur");
        check(!hunter.KillLandAnimal(9,LandAnimalKind.Fox,0,1,0,0) && !hunter.KillLandAnimal(12,LandAnimalKind.Gull,0,1,0,0),"Target IDs cannot change species or exceed population");
        check(FishingCareer.TryRestore(hunter.Snapshot(),out var allLoaded) && allLoaded.HuntedAnimal(8).collected && allLoaded.Backpack.Count(FieldItem.GullMeat)==1,"Gull and sea turtle loot persist");
        double due=hunter.HuntedAnimal(8).respawnAt;
        while(hunter.Calendar.TotalMinutes<due)hunter.Calendar.Tick(1,false);
        check(hunter.HuntedAnimal(8)==null && hunter.KillLandAnimal(8,LandAnimalKind.Gull,2,1,3,0),"Collected wildlife can return only after the saved recovery clock");
        foreach(var meat in new[]{FieldItem.RabbitMeat,FieldItem.FoxMeat,FieldItem.TurtleMeat,FieldItem.GullMeat})
            check(Array.Exists(FishingRecipes.All,r=>r.Output==FieldItem.CookedMeat && r.Costs[0].item==meat),"Categorized meat retains furnace compatibility "+meat);
    }
}
