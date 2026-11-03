using System;
using System.Linq;
using System.Text.Json;
using BoatAttack.Fishing;

internal static class RealmDemoTests
{
    private static string Json(object value)=>JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true});
    private static FishingCareer Restore(FishingSaveData data)
    {if(!FishingCareer.TryRestore(data,out var career))throw new Exception("Realm demo fixture must be a valid save");return career;}
    public static void Run(Action<bool,string> check)
    {
        var fresh=new FishingCareer();fresh.Lore.SaveRealmStage(3,2);fresh.Lore.FindShell(3,0);fresh.Lore.RealmVariant(3,()=>2);
        check(!fresh.Lore.ResetRealm(-1) && !fresh.Lore.ResetRealm(4),"Replay rejects invalid realm indices");
        check(fresh.Lore.ResetRealm(3) && fresh.Lore.RealmStage(3)==0 && fresh.Lore.ShellCount(3)==0
            && fresh.Lore.Snapshot().realmVariants==0 && !fresh.Lore.Replaying(3),"Reset unfinished realm clears checkpoint, shells and variant without inventing prior completion");
        check(fresh.CompleteShrine(3)==90 && fresh.CompleteShrine(3)==0,"First actual completion after reset still pays the original reward once");
        check(FishingLore.TryRestore(new LoreData{shrines=15,realmStages=218,realmShells=4095,realmVariants=255},out var mixed)
            && mixed.ResetRealm(1) && mixed.Snapshot().realmStages==210 && mixed.Snapshot().realmShells==4039
            && mixed.Snapshot().realmVariants==243 && mixed.Snapshot().realmReplay==2 && mixed.Snapshot().realmShellRewards==2
            && mixed.Cleared(0) && mixed.Cleared(2) && mixed.Cleared(3),"Reset masks only the chosen realm and preserves the other three puzzles");

        var c=new FishingCareer();c.AcceptMayorship();c.DiscoverChart();
        for(int island=0;island<4;island++)
        {
            check(c.CompleteShrine(island)==90 && c.ClaimShrineReward(island),"Seed actual unique shrine rewards "+island);
            for(int shell=0;shell<3;shell++)check(c.CollectRealmShell(island,shell)==(shell==2?30:0),"First shell set pays only on completion "+island+"/"+shell);
            c.Lore.RealmVariant(island,()=>2);
        }
        check(c.CompleteEchoVoyage()==180 && c.CraftVoyageCompass(CraftStation.Workbench)
            && c.OpenGrandTreasure()==FishingCareer.GrandTreasureCoins,"Replay fixture includes consumed relics, claimed blueprints and the completed finale");
        var original=c.Snapshot();int coins=c.Coins;string bag=Json(original.backpack);
        for(int island=0;island<4;island++)
            check(c.Lore.ResetRealm(island) && !c.Lore.Cleared(island) && c.Lore.Replaying(island)
                && c.Lore.RealmStage(island)==0 && c.Lore.ShellCount(island)==0 && c.Lore.BlueprintUnlocked(island),"Paid realm becomes an unsolved replay while its blueprint remains owned "+island);
        var replay=c.Snapshot();
        check(replay.lore.shrines==15 && replay.lore.rewardedShrines==15 && replay.lore.finale && replay.lore.grandTreasureFound
            && replay.lore.realmReplay==15 && replay.lore.realmShellRewards==15 && replay.lore.realmVariants==0
            && c.Coins==coins && Json(replay.backpack)==bag,"Reset preserves quest/finale facts, coins and consumed unique items");
        c=Restore(replay);
        check(c.Lore.Replaying(3) && c.Lore.RealmStage(3)==0 && c.PirateKing,"Paid unsolved replay survives complete save validation");
        for(int island=0;island<4;island++)
        {
            for(int shell=0;shell<3;shell++)check(c.CollectRealmShell(island,shell)==0,"Replay shell collection cannot mint another 30 coins "+island+"/"+shell);
            check(c.CompleteShrine(island)==0 && c.Lore.Cleared(island) && !c.Lore.Replaying(island)
                && !c.ClaimShrineReward(island) && c.Coins==coins,"Replayed echo completes without paying money, relics or materials again "+island);
            c.Lore.ResetRealm(island);c.CompleteShrine(island);
        }
        var capped=c.Snapshot();capped.coins=100000000;c=Restore(capped);c.Lore.ResetRealm(2);
        check(c.CompleteShrine(2)==0 && c.Lore.Cleared(2) && c.Coins==100000000,"Wallet cap cannot block a zero-reward replay completion");
        var old=original.lore;old.realmShellRewards=0;old.realmReplay=0;
        check(FishingLore.TryRestore(old,out var legacy) && legacy.ResetRealm(0) && (legacy.Snapshot().realmShellRewards&1)!=0,"Old complete shell sets migrate to a paid flag before reset");
        var unpaid=new FishingCareer();unpaid.Lore.ResetRealm(0);
        for(int shell=0;shell<3;shell++)unpaid.CollectRealmShell(0,shell);
        check(unpaid.Coins==30 && !unpaid.Lore.TryClaimShellReward(0),"A never-paid realm still grants its first shell reward once");
        foreach(int invalid in new[]{-1,16})
        {
            var data=new LoreData{realmReplay=invalid};check(!FishingLore.TryRestore(data,out _),"Replay mask rejects out-of-range bits "+invalid);
            data=new LoreData{realmShellRewards=invalid};check(!FishingLore.TryRestore(data,out _),"Shell reward mask rejects out-of-range bits "+invalid);
        }
        check(!FishingLore.TryRestore(new LoreData{realmReplay=1},out _),"A replay bit requires an actual historical completion");
        check(!FishingLore.TryRestore(new LoreData{realmShellRewards=1},out _,19)
            && FishingLore.TryRestore(new LoreData(),out _,19),"Old schemas accept missing optional fields but reject hidden replay state");
        var demo=FishingClassroomDemo.Create();
        check(demo.Lore.ClearCount==0 && demo.Lore.RewardedShrines==15 && demo.Lore.Snapshot().realmReplay==15
            && FishingCareer.TryRestore(demo.Snapshot(),out _),"Classroom starts with four replayable puzzles and retained paid rewards");
        var residents=demo.HomeAnimals.Select(a=>Json(a)).ToArray();demo.SetCreativeMode(true);
        check(demo.PrepareAnimalShowcase() && demo.HomeAnimalCount==10 && demo.HomeAnimals.Take(6).Select(a=>Json(a)).SequenceEqual(residents)
            && FishingCareer.TryRestore(demo.Snapshot(),out _),"Existing classroom residents are retained while missing adult sexes are completed");
        Animals(check);
    }
    private static void Animals(Action<bool,string> check)
    {
        var c=new FishingCareer();string before=Json(c.Snapshot());
        check(!c.PrepareAnimalShowcase() && Json(c.Snapshot())==before,"Ordinary mode rejects showcase population without changing the save");
        c.SetCreativeMode(true);check(c.PrepareAnimalShowcase() && c.HomeAnimalCount==10,"Creative showcase creates exactly five adult pairs");
        for(int kind=0;kind<5;kind++)
        {
            var animals=c.HomeAnimals.Where(a=>(int)a.kind==kind).ToArray();
            check(c.HabitatLevel((LandAnimalKind)kind)==1 && animals.Length==2 && animals.All(a=>a.housed && c.AnimalAdult(a.id))
                && animals.Any(a=>a.female) && animals.Any(a=>!a.female),"Showcase provides an adult housed female and male "+kind);
        }
        before=Json(c.HomeEcology);check(c.PrepareAnimalShowcase() && Json(c.HomeEcology)==before,"Repeated showcase is idempotent and does not create IDs");
        check(FishingCareer.TryRestore(c.Snapshot(),out var loaded) && loaded.HomeAnimalCount==10,"Generated animal source IDs, ages and capacities survive strict save validation");

        var data=new FishingCareer().Snapshot();data.homeEcology.nextAnimalId=999999;c=Restore(data);c.SetCreativeMode(true);before=Json(c.HomeEcology);
        check(!c.PrepareAnimalShowcase() && Json(c.HomeEcology)==before,"Exhausted animal IDs reject the whole operation without partial habitats");
        data=new FishingCareer().Snapshot();data.homeEcology.nextAnimalId=999990;c=Restore(data);c.SetCreativeMode(true);
        check(c.PrepareAnimalShowcase() && c.HomeEcology.nextAnimalId==1000000 && c.PrepareAnimalShowcase()
            && FishingCareer.TryRestore(c.Snapshot(),out _),"Last ten legal IDs work and an already-complete showcase needs no new ID");

        data=new FishingCareer().Snapshot();
        for(int kind=0;kind<4;kind++)for(int n=0;n<4;n++)AddAnimal(data,(LandAnimalKind)kind,n%2==0,true);
        c=Restore(data);c.SetCreativeMode(true);before=Json(c.HomeEcology);
        check(!c.PrepareAnimalShowcase() && Json(c.HomeEcology)==before,"Sixteen existing animals are all retained and prevent an over-capacity showcase");
        data=new FishingCareer().Snapshot();for(int n=0;n<4;n++)AddAnimal(data,LandAnimalKind.Rabbit,false,true);
        c=Restore(data);c.SetCreativeMode(true);before=Json(c.HomeEcology);
        check(!c.PrepareAnimalShowcase() && Json(c.HomeEcology)==before,"A full same-sex habitat rejects atomically when its upgrade blueprint is missing");
        c.AcceptMayorship();c.DiscoverChart();c.CompleteShrine(1);check(c.ClaimShrineReward(1),"Showcase full-habitat fixture earns the real expansion blueprint");
        check(c.PrepareAnimalShowcase() && c.HomeAnimalCount==13 && c.HabitatLevel(LandAnimalKind.Rabbit)==2
            && c.HomeAnimals.Take(4).All(a=>!a.female && a.housed) && FishingCareer.TryRestore(c.Snapshot(),out _),"Owned blueprint permits required capacity while all existing adults remain unchanged");
        data=new FishingCareer().Snapshot();AddAnimal(data,LandAnimalKind.Rabbit,false,false);AddAnimal(data,LandAnimalKind.Rabbit,true,false);
        c=Restore(data);c.SetCreativeMode(true);var originals=c.HomeAnimals.Select(a=>Json(a)).ToArray();
        check(c.PrepareAnimalShowcase() && c.HomeAnimalCount==12 && c.CarriedAnimalCount==2
            && c.HomeAnimals.Take(2).Select(a=>Json(a)).SequenceEqual(originals),"Showcase preserves existing carried animals instead of relocating or replacing them");
    }
    private static void AddAnimal(FishingSaveData data,LandAnimalKind kind,bool female,bool housed)
    {
        var ecology=data.homeEcology;ecology.habitatLevels[(int)kind]=1;
        ecology.animals.Add(new HomeAnimalData{id=ecology.nextAnimalId++,sourceId=new[]{0,1,2,8,11}[(int)kind],
            kind=kind,female=female,birthDay=data.calendar.day-FishingCareer.AnimalAdultDays,housed=housed});
    }
}
