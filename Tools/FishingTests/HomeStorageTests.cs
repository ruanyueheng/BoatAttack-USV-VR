using System;
using BoatAttack.Fishing;

internal static class HomeStorageTests
{
    public static void Run(Action<bool,string> check)
    {
        var bag=new FishingBackpack(false);var chest=new FishingBackpack(false);
        bag.TryAdd(FieldItem.Shell,64);chest.TryAdd(FieldItem.Shell,63);
        check(bag.TransferTo(chest,0) && bag.Count(FieldItem.Shell)==0 && chest.Count(FieldItem.Shell)==127,"Storage: deposit conserves a full stack while merging into partial stacks");
        check(chest.TransferTo(bag,0) && bag.Count(FieldItem.Shell)==64 && chest.Count(FieldItem.Shell)==63,"Storage: withdrawal conserves both inventories");
        var full=new FishingBackpack(false);full.TryAdd(FieldItem.Stone,64*FishingBackpack.SlotCount);
        check(!bag.TransferTo(full,0) && bag.Count(FieldItem.Shell)==64 && full.Count(FieldItem.Stone)==2304,"Storage: full destination leaves both inventories untouched");
        check(!bag.TransferTo(bag,0) && !bag.TransferTo(null,0) && !bag.TransferTo(chest,-1) && !bag.TransferTo(chest,36) && !bag.TransferTo(chest,1),"Storage: self, missing, out-of-range and empty transfers are rejected");
        var career=new FishingCareer();career.HomeStorage.TryAdd(FieldItem.Shell,127);
        check(career.TryHarvestHomeFruit(0) && !career.TryHarvestHomeFruit(0) && !career.TryHarvestHomeFruit(-1) && !career.TryHarvestHomeFruit(5),"Orchard: one pickup per tree and day, with bounded tree ids");
        var data=career.Snapshot();
        bool restored=FishingCareer.TryRestore(data,out var loaded);
        check(data.version==FishingSaveData.CurrentVersion && restored && loaded.HomeStorage.Count(FieldItem.Shell)==127 && !loaded.CanHarvestHomeFruit(0) && loaded.Backpack.Count(FieldItem.IslandFruit)==1,"Storage: v15 career round trip preserves chest and fruit pickup receipt");
        data.homeStorage[0].count=1;data.homeFruitDays[0]=-1;
        check(career.HomeStorage.Count(FieldItem.Shell)==127 && !career.CanHarvestHomeFruit(0) && loaded.HomeStorage.Count(FieldItem.Shell)==127 && !loaded.CanHarvestHomeFruit(0),"Storage: save and restored storage never alias the live inventory or receipts");
        career.Calendar.RestUntilMorning();
        check(career.CanHarvestHomeFruit(0) && career.TryHarvestHomeFruit(0),"Orchard: advancing the saved calendar renews a picked tree");
        var packed=new FishingCareer();packed.Backpack.TryAdd(FieldItem.Stone,64*35);
        check(!packed.TryHarvestHomeFruit(0) && packed.CanHarvestHomeFruit(0),"Orchard: full backpack cannot consume the daily pickup");
        var legacy=new FishingCareer().Snapshot();legacy.version=14;legacy.homeStorage=null;legacy.homeFruitDays=null;
        check(FishingCareer.TryRestore(legacy,out var upgraded) && upgraded.HomeStorage.UsedSlots==0 && upgraded.CanHarvestHomeFruit(0),"Storage: v14 migrates to an empty chest and unpicked orchard");
        legacy.homeStorage=new FishingBackpack(false).Snapshot();legacy.homeStorage[0]=new FieldStack{item=FieldItem.Shell,count=1};
        check(!FishingCareer.TryRestore(legacy,out _),"Storage: pre-v15 schemas cannot smuggle chest items");
        legacy.homeStorage=null;legacy.homeFruitDays=new[]{0,-1,-1,-1,-1};
        check(!FishingCareer.TryRestore(legacy,out _),"Orchard: pre-v15 schemas cannot smuggle harvest receipts");
        data=career.Snapshot();data.homeStorage[0].count=65;
        check(!FishingCareer.TryRestore(data,out _),"Storage: over-limit saved chest stacks are rejected");
        data=career.Snapshot();data.homeFruitDays[0]=career.Calendar.Day+1;
        check(!FishingCareer.TryRestore(data,out _),"Orchard: future harvest receipts are rejected");
        data=career.Snapshot();data.homeFruitDays=new[]{-1};
        check(!FishingCareer.TryRestore(data,out _),"Orchard: truncated saved receipt arrays are rejected");
        data=career.Snapshot();data.homeStorage=null;
        check(!FishingCareer.TryRestore(data,out _),"Storage: missing v15 inventory fails validation");
        for(int version=1;version<15;version++)
        {
            var old=new FishingCareer().Snapshot();old.version=version;if(version<6)old.backpack=null;
            check(FishingCareer.TryRestore(old,out _),"Storage: pristine schema still migrates from v"+version);
            old.homeStorage[0]=new FieldStack{item=FieldItem.Shell,count=1};
            check(!FishingCareer.TryRestore(old,out _),"Storage: chest items cannot be hidden in v"+version);
            old.homeStorage[0]=new FieldStack();old.homeFruitDays[0]=0;
            check(!FishingCareer.TryRestore(old,out _),"Orchard: harvest receipts cannot be hidden in v"+version);
        }
    }
}
