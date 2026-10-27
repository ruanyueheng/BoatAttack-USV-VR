using System;
using System.Linq;
using BoatAttack.Fishing;
internal static class ForgeTests
{
    public static void Run(Action<bool,string> check)
    {
        var bag=new FishingBackpack();bag.TryAdd(FieldItem.Stone,20);bag.TryAdd(FieldItem.Clay,4);bag.TryAdd(FieldItem.Plank,4);
        check(FishingRecipes.Craft(bag,"forge",CraftStation.Workbench) && bag.Count(FieldItem.Forge)==1 && bag.Count(FieldItem.Stone)==12,"Workbench builds forge installation");
        var state=new FishingWorkshop();int id=state.Place(bag,CraftStation.Forge,3,1,4,20);
        check(id>0 && bag.Count(FieldItem.Forge)==0,"Forge placement consumes one item");
        check(FishingWorkshop.TryRestore(state.Snapshot(),out var restored),"Forge placement persists");
        bag.TryAdd(FieldItem.Stick,8);bag.TryAdd(FieldItem.Rope,3);int stone=bag.Count(FieldItem.Stone);
        check(!FishingRecipes.Craft(bag,"stone-pick",CraftStation.Forge) && bag.Count(FieldItem.Stone)==stone,"Forge cannot assemble stone tools");
        check(FishingRecipes.Craft(bag,"stone-pick",CraftStation.Workbench),"Workbench assembles the mining starter");
        bag.TryAdd(FieldItem.IronOre,9);bag.TryAdd(FieldItem.Coal,3);
        check(!FishingRecipes.Craft(bag,"forge-iron",CraftStation.Forge) && bag.Count(FieldItem.IronOre)==9,"Old forge smelting shortcut now requires furnace");
        int furnace=state.Place(null,CraftStation.Furnace,9,1,11,0,true);
        check(state.Start(bag,furnace,"iron",8,FieldItem.Coal) && bag.Count(FieldItem.Coal)==1 && bag.Count(FieldItem.IronIngot)==0,"Furnace reserves exact fuel but cannot smelt instantly");
        for(int i=0;i<64;i++)state.Tick(1);
        check(state.Collect(bag,furnace) && bag.Count(FieldItem.IronIngot)==8,"Furnace delivers ingots after heating");
        check(state.Start(bag,furnace,"iron",1,FieldItem.Coal),"Final ore batch starts");for(int i=0;i<8;i++)state.Tick(1);state.Collect(bag,furnace);
        check(!FishingRecipes.Craft(bag,"iron-pick-head",CraftStation.Workbench) && bag.Count(FieldItem.IronIngot)==9,"Workbench cannot shape raw metal");
        check(FishingRecipes.Craft(bag,"iron-pick-head",CraftStation.Forge) && bag.Count(FieldItem.IronPickHead)==1 && bag.Count(FieldItem.IronIngot)==6,"Forge shapes ingots into a separate pick head");
        check(!FishingRecipes.Craft(bag,"iron-pick",CraftStation.Forge) && bag.Count(FieldItem.IronPickHead)==1,"Forge cannot assemble a wood-handled pick");
        check(FishingRecipes.Craft(bag,"iron-pick",CraftStation.Workbench) && bag.Count(FieldItem.IronPickaxe)==1 && bag.Count(FieldItem.IronPickHead)==0,"Workbench assembles forged head and handle");
        check(FishingRecipes.Craft(bag,"gun-parts",CraftStation.Forge) && bag.Count(FieldItem.GunParts)==1 && bag.Count(FieldItem.IronIngot)==0,"Forge shapes game gun parts");
        bag.TryAdd(FieldItem.Plank,2);
        check(!FishingRecipes.Craft(bag,"hunting-rifle",CraftStation.Forge) && bag.Count(FieldItem.GunParts)==1,"Forge cannot bypass rifle assembly");
        check(FishingRecipes.Craft(bag,"hunting-rifle",CraftStation.Workbench) && bag.Count(FieldItem.HuntingRifle)==1 && bag.Count(FieldItem.GunParts)==0,"Workbench assembles equippable game rifle");
        check(!FishingRecipes.Craft(bag,"hunting-rifle",CraftStation.Workbench),"No inputs means no duplicate rifle");
        check(restored.Remove(bag,id) && !restored.Remove(bag,id),"Owned forge recovers once");
        int publicId=restored.Place(null,CraftStation.Forge,6,1,7,0,true);check(!restored.Remove(bag,publicId),"Public forge cannot be dismantled");
        var full=new FishingBackpack(false);full.TryAdd(FieldItem.IronIngot,4);full.TryAdd(FieldItem.Shell,35*64);
        check(!FishingRecipes.Craft(full,"iron-pick-head",CraftStation.Forge) && full.Count(FieldItem.IronIngot)==4,"Full bag rolls back metal shaping");
        var produced=new System.Collections.Generic.HashSet<FieldItem>();
        foreach(var station in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
        {
            var recipes=FishingRecipes.ForStation(station);
            check(recipes.Length>0 && recipes.All(r=>r.Station==station),"Desktop/XR catalog is station-specific: "+station);
            foreach(var output in recipes.Select(r=>r.Output).Distinct())check(produced.Add(output),"No product crosses facility catalogs: "+output);
            check(FishingRecipes.ForStation(station,true).All(r=>r.Station==CraftStation.Hand),"Basic tab only contains hand processing: "+station);
        }
        check(FishingRecipes.ForgeRecipes.Length==2 && FishingRecipes.ForgeRecipes.All(r=>r.Output==FieldItem.IronPickHead || r.Output==FieldItem.GunParts),"Forge only produces shaped metal components");
        check(FishingRecipes.FurnaceRecipes[0].Id=="iron","Furnace menu prioritizes ore smelting instead of starting on cooked meat");
        foreach(var pair in new[]{new[]{"coal-rabbit-lantern",FieldItem.RabbitFur.ToString()},new[]{"coal-fox-lantern",FieldItem.FoxFur.ToString()},new[]{"coal-legacy-lantern",FieldItem.Fur.ToString()}})
        {
            var fur=(FieldItem)Enum.Parse(typeof(FieldItem),pair[1]);var lamps=new FishingBackpack(false);lamps.TryAdd(FieldItem.Coal,2);lamps.TryAdd(fur,1);
            check(!FishingRecipes.Craft(lamps,pair[0],CraftStation.Forge) && lamps.Count(FieldItem.Coal)==2,"Lantern is no longer made at forge: "+fur);
            check(!FishingRecipes.Craft(lamps,pair[0],CraftStation.Workbench) && lamps.Count(FieldItem.Coal)==2 && lamps.Count(fur)==1,"Coal and fur cannot become a complete lantern: "+fur);
            lamps.TryAdd(FieldItem.IronIngot,2);lamps.TryAdd(FieldItem.Glass,2);lamps.TryAdd(FieldItem.Charcoal,1);
            check(FishingRecipes.Craft(lamps,pair[0],CraftStation.Workbench) && lamps.Count(FieldItem.Lantern)==1 && lamps.Count(FieldItem.IronIngot)==0 && lamps.Count(FieldItem.Glass)==0 && lamps.Count(FieldItem.Charcoal)==0 && lamps.Count(fur)==1,"Legacy lantern id uses real frame, glass and fuel: "+fur);
        }
        var fullLamp=new FishingBackpack(false);fullLamp.TryAdd(FieldItem.Coal,3);fullLamp.TryAdd(FieldItem.RabbitFur,2);fullLamp.TryAdd(FieldItem.Shell,34*64);
        check(!FishingRecipes.Craft(fullLamp,"coal-rabbit-lantern",CraftStation.Workbench) && fullLamp.Count(FieldItem.Coal)==3 && fullLamp.Count(FieldItem.RabbitFur)==2,"Full lantern bag returns inputs");
        var notFur=new FishingBackpack(false);notFur.TryAdd(FieldItem.Coal,2);notFur.TryAdd(FieldItem.GullFeather,1);notFur.TryAdd(FieldItem.TurtleShell,1);
        check(!FishingRecipes.Craft(notFur,"coal-rabbit-lantern",CraftStation.Workbench),"Shells and feathers are not fur");
        check((int)FieldItem.Forge==37 && (int)FieldItem.IronPickHead==38 && (int)FieldItem.GunParts==39,"Component IDs append without renumbering saves");
        var save=new FishingCareer().Snapshot();save.backpack[1]=new FieldStack{item=FieldItem.IronPickHead,count=2};save.backpack[2]=new FieldStack{item=FieldItem.GunParts,count=3};
        check(FishingCareer.TryRestore(save,out var loaded) && loaded.Backpack.Count(FieldItem.IronPickHead)==2 && loaded.Backpack.Count(FieldItem.GunParts)==3,"Components persist in career saves");
        var jobs=new FishingWorkshop();int f=jobs.Place(null,CraftStation.Furnace,1,1,2,0,true);var oldBag=new FishingBackpack(false);oldBag.TryAdd(FieldItem.IronOre,1);oldBag.TryAdd(FieldItem.Coal,1);
        jobs.Start(oldBag,f,"iron",1,FieldItem.Coal);jobs.Tick(1);
        check(FishingWorkshop.TryRestore(jobs.Snapshot(),out var legacy) && legacy.Station(f).progress==1,"Existing reserved furnace job retains progress");
    }
}
