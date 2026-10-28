using System;
using System.Linq;
using BoatAttack.Fishing;
internal static class CraftingTests
{
    public static void Run(Action<bool,string> check)
    {
        var bag=new FishingBackpack(false);bag.TryAdd(FieldItem.Driftwood,8);
        var rawWood=new FishingBackpack(false);rawWood.TryAdd(FieldItem.Driftwood,1);
        check(FishingRecipes.Craft(rawWood,"sticks",CraftStation.Hand) && rawWood.Count(FieldItem.Stick)==4 && rawWood.Count(FieldItem.Driftwood)==0,"Single driftwood directly makes four sticks");
        check(!FishingRecipes.Craft(rawWood,"sticks",CraftStation.Hand),"Direct sticks cannot duplicate without driftwood");
        var plankWood=new FishingBackpack(false);plankWood.TryAdd(FieldItem.Plank,2);
        check(FishingRecipes.Craft(plankWood,"sticks-from-planks",CraftStation.Hand) && plankWood.Count(FieldItem.Stick)==4 && plankWood.Count(FieldItem.Plank)==0,"Prepared planks retain alternate stick recipe");
        var fullSticks=new FishingBackpack(false);fullSticks.TryAdd(FieldItem.Driftwood,2);fullSticks.TryAdd(FieldItem.Shell,35*64);
        check(!FishingRecipes.Craft(fullSticks,"sticks",CraftStation.Hand) && fullSticks.Count(FieldItem.Driftwood)==2,"Full bag preserves direct-stick materials");
        check(FishingRecipes.Craft(bag,"planks",CraftStation.Hand) && bag.Count(FieldItem.Driftwood)==7 && bag.Count(FieldItem.Plank)==4,"Craft planks consumes one log and makes four planks");
        check(FishingRecipes.Craft(bag,"bench",CraftStation.Hand) && bag.Count(FieldItem.Workbench)==1,"Hand crafts workbench");
        check(!FishingRecipes.Craft(bag,"furnace",CraftStation.Hand),"Furnace recipe is not hand-craftable");
        bag.TryAdd(FieldItem.Stone,16);bag.TryAdd(FieldItem.Fiber,6);
        check(!FishingRecipes.Craft(bag,"furnace",CraftStation.Hand) && bag.Count(FieldItem.Stone)==16,"Wrong station does not consume ingredients");
        check(FishingRecipes.Craft(bag,"furnace",CraftStation.Workbench),"Workbench crafts furnace");
        FishingRecipes.Craft(bag,"planks",CraftStation.Hand);FishingRecipes.Craft(bag,"sticks",CraftStation.Hand);FishingRecipes.Craft(bag,"rope",CraftStation.Hand);
        check(FishingRecipes.Craft(bag,"stone-pick",CraftStation.Workbench) && bag.Count(FieldItem.StonePickaxe)==1,"Reachable bootstrap recipe produces mining tool");
        check(!FishingRecipes.Craft(bag,"missing",CraftStation.Hand),"Unknown recipe fails safely");
        foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))
            check(FishingBackpack.Defined(item) && (item==FieldItem.Empty || FishingBackpack.Name(item)!="空槽"),"Every appended item has a display name: "+item);
        var full=new FishingBackpack(false);full.TryAdd(FieldItem.Shell,35*64);full.TryAdd(FieldItem.Driftwood,2);
        check(!FishingRecipes.Craft(full,"planks",CraftStation.Hand) && full.Count(FieldItem.Driftwood)==2,"Full output bag rolls back crafting input");
        check(!bag.Exchange(new[]{FishingRecipes.Stack(FieldItem.Driftwood,4),FishingRecipes.Stack(FieldItem.Driftwood,99)},FieldItem.Empty,0),"Duplicate input costs cannot bypass total availability");
        var exact=new FishingBackpack(false);exact.TryAdd(FieldItem.Shell,35*64);exact.TryAdd(FieldItem.Driftwood,1);
        check(FishingRecipes.Craft(exact,"planks",CraftStation.Hand) && exact.Count(FieldItem.Plank)==4,"Input-freed slot can receive output in full inventory");
        var state=new FishingWorkshop();
        int bench=state.Place(bag,CraftStation.Workbench,1,1,2,45),furnace=state.Place(bag,CraftStation.Furnace,4,1,2,0);
        check(bench>0 && furnace>bench && bag.Count(FieldItem.Furnace)==0,"Placing facilities consumes exactly one owned item");
        check(state.Place(bag,CraftStation.Furnace,float.NaN,1,0,0)<0 && state.Place(bag,CraftStation.Furnace,0,-1,0,0)<0,"Invalid facility coordinates rejected");
        bag.TryAdd(FieldItem.IronOre,8);bag.TryAdd(FieldItem.Coal,1);
        check(!state.Start(bag,bench,"iron",4,FieldItem.Coal) && bag.Count(FieldItem.Coal)==1,"Workbench cannot act as furnace");
        check(!state.Start(bag,furnace,"iron",0,FieldItem.Coal) && !state.Start(bag,furnace,"iron",9,FieldItem.Coal),"Furnace batch limits enforced");
        check(state.Start(bag,furnace,"iron",4,FieldItem.Coal) && bag.Count(FieldItem.IronOre)==4 && bag.Count(FieldItem.Coal)==0,"One coal reserves four ore atomically");
        check(!state.Start(bag,furnace,"iron",1,FieldItem.Driftwood) && !state.Remove(bag,furnace),"Busy furnace cannot restart or dismantle");
        check(!state.Tick(float.NaN) && !state.Tick(-1) && state.Station(furnace).progress==0,"Invalid furnace times cannot advance work");
        for(int i=0;i<7;i++)state.Tick(1);
        check(state.Station(furnace).ready==0 && !state.Collect(bag,furnace),"Smelting is timed, not instant");
        var snapshot=state.Snapshot();check(FishingWorkshop.TryRestore(snapshot,out var loaded),"In-flight job restores");
        snapshot.stations[1].progress=0;check(loaded.Station(furnace).progress==7,"Restore does not alias caller save data");
        loaded.Tick(1);check(loaded.Station(furnace).ready==1 && loaded.Station(furnace).remaining==3,"Restored job finishes exactly its next item");
        check(!loaded.Collect(full,furnace) && loaded.Station(furnace).ready==1,"Full bag preserves finished furnace output");
        check(loaded.Collect(bag,furnace) && !loaded.Collect(bag,furnace) && bag.Count(FieldItem.IronIngot)==1,"Furnace output collected exactly once");
        for(int i=0;i<24;i++)loaded.Tick(1);
        check(loaded.Station(furnace).remaining==0 && loaded.Station(furnace).ready==3,"Fuel-backed batch completes without extra charge");
        check(!loaded.Remove(bag,furnace) && loaded.Collect(bag,furnace) && loaded.Remove(bag,furnace),"Must collect before dismantling; empty furnace returns to bag");
        check(!loaded.Remove(bag,furnace) && bag.Count(FieldItem.Furnace)==1,"Dismantled item cannot be duplicated");
        int publicId=loaded.Place(null,CraftStation.Workbench,2,1,2,0,true);
        check(publicId>0 && !loaded.Remove(bag,publicId),"Public facilities cannot be taken");
        var markerKind=loaded.StationAt(loaded.StationCount-1,out var markerX,out var markerY,out var markerZ);
        check(markerKind==CraftStation.Workbench && markerX==2 && markerY==1 && markerZ==2,"Chart station lookup preserves kind and coordinates without Unity dependency");
        var crowded=new FishingWorkshop();var builder=new FishingBackpack(false);builder.TryAdd(FieldItem.Workbench,33);
        for(int i=0;i<FishingWorkshop.MaxPlayerStations;i++)check(crowded.Place(builder,CraftStation.Workbench,i,1,5,0)>0,"Owned station slot "+i);
        check(crowded.Place(builder,CraftStation.Workbench,40,1,5,0)<0 && builder.Count(FieldItem.Workbench)==1,"Owned facility cap leaves rejected item in the bag");
        int homeFurnace=-1;
        for(int i=0;i<6;i++)
        {
            int id=crowded.Place(null,(CraftStation)(1+i%3),i,1,10,0,true);
            check(id>0,"Full owned collection reserves independent fixed facility slot "+i);if(i==4)homeFurnace=id;
        }
        crowded.FinishInitialization();builder.TryAdd(FieldItem.IronOre,2);builder.TryAdd(FieldItem.Coal,1);
        check(crowded.Start(builder,homeFurnace,"iron",2,FieldItem.Coal),"Home furnace uses the same reserved-input queue");crowded.Tick(1);
        check(crowded.Place(null,CraftStation.Forge,0,1,0,0,true)<0,"Only six fixed facilities can be installed");
        check(FishingWorkshop.TryRestore(crowded.Snapshot(),out var crowdedRestored) && crowdedRestored.Initialized
            && crowdedRestored.Stations.Count()==FishingWorkshop.MaxStations && crowdedRestored.Station(homeFurnace).remaining==2
            && crowdedRestored.Station(homeFurnace).progress==1,"Full save restores all owned and fixed stations without losing the home smelting queue");
        check(state.Harvest(bag,0,FieldItem.Stone,3) && !state.Harvest(bag,0,FieldItem.Stone,3),"Resource harvest enters cooldown once");
        check(!state.Harvest(full,1,FieldItem.Stone,3) && state.ResourceReady(1),"Full bag does not deplete resource node");
        check(!state.Harvest(bag,64,FieldItem.Stone,3) && !state.Harvest(bag,1,FieldItem.IronIngot,3),"Unknown nodes and non-natural yields rejected");
        for(int i=0;i<89;i++)state.Tick(1);
        check(!state.ResourceReady(0),"Cooldown remains before ninety simulation seconds");state.Tick(1);
        check(state.ResourceReady(0),"Resource regenerates after ninety seconds");
        var malformed=state.Snapshot();malformed.stations[0].x=float.NaN;check(!FishingWorkshop.TryRestore(malformed,out _),"Save rejects nonfinite placement");
        malformed=state.Snapshot();malformed.stations[1].id=malformed.stations[0].id;check(!FishingWorkshop.TryRestore(malformed,out _),"Save rejects duplicate station IDs");
        malformed=state.Snapshot();malformed.resourceCooldowns[0]=91;check(!FishingWorkshop.TryRestore(malformed,out _),"Save rejects unbounded cooldown");
        malformed=state.Snapshot();malformed.stations[1].recipe="unknown";check(!FishingWorkshop.TryRestore(malformed,out _),"Save rejects unknown active smelting recipe");
        malformed=state.Snapshot();malformed.stations[0].remaining=1;malformed.stations[0].recipe="iron";check(!FishingWorkshop.TryRestore(malformed,out _),"Save rejects workbench pretending to be furnace");
        var career=new FishingCareer();var legacy=career.Snapshot();legacy.version=7;legacy.workshop=null;
        check(FishingCareer.TryRestore(legacy,out var migrated) && migrated.Workshop!=null && migrated.Snapshot().version == FishingSaveData.CurrentVersion,"v7 progress migrates to current workshop state");
        var v8=career.Snapshot();v8.workshop=null;check(!FishingCareer.TryRestore(v8,out _),"Current save requires workshop payload");
        check(FishingRecipes.All.Select(r=>r.Id).Distinct().Count()==FishingRecipes.All.Length,"Stable recipe IDs are unique");
        foreach(var recipe in FishingRecipes.All)
        {
            check(recipe.Count>0 && recipe.Costs.Length>0 && recipe.Costs.All(s=>s.count>0),"Recipe has positive inputs/outputs: "+recipe.Id);
            var stocked=new FishingBackpack(false);foreach(var cost in recipe.Costs)stocked.TryAdd(cost.item,cost.count);
            if(recipe.Station!=CraftStation.Furnace)check(FishingRecipes.Craft(stocked,recipe.Id,recipe.Station),"Executable recipe: "+recipe.Id);
            else
            {
                var furnaceState=new FishingWorkshop();int id=furnaceState.Place(null,CraftStation.Furnace,0,1,0,0,true);stocked.TryAdd(FieldItem.Coal,1);
                check(furnaceState.Start(stocked,id,recipe.Id,1,FieldItem.Coal),"Executable smelting recipe: "+recipe.Id);
            }
        }
    }
}
