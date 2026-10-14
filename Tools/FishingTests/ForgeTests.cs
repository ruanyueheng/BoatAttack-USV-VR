using System;
using System.Linq;
using BoatAttack.Fishing;
internal static class ForgeTests
{
    public static void Run(Action<bool,string> check)
    {
        var bag=new FishingBackpack();bag.TryAdd(FieldItem.Stone,20);bag.TryAdd(FieldItem.Clay,4);bag.TryAdd(FieldItem.Plank,4);
        check(FishingRecipes.Craft(bag,"forge",CraftStation.Workbench) && bag.Count(FieldItem.Forge)==1 && bag.Count(FieldItem.Stone)==12,"Forge built from gatherable stone/clay and wood");
        var state=new FishingWorkshop();int id=state.Place(bag,CraftStation.Forge,3,1,4,20);
        check(id>0 && bag.Count(FieldItem.Forge)==0 && state.Station(id).kind==CraftStation.Forge,"Forge placement consumes one item");
        check(FishingWorkshop.TryRestore(state.Snapshot(),out var restored) && restored.Station(id).kind==CraftStation.Forge,"Forge placement persists through restore");
        bag.TryAdd(FieldItem.Stick,8);bag.TryAdd(FieldItem.Fiber,2);
        int stone=bag.Count(FieldItem.Stone);
        check(!FishingRecipes.Craft(bag,"forge-stone-pick",CraftStation.Hand) && bag.Count(FieldItem.Stone)==stone,"Remote forge crafting cannot consume materials");
        check(FishingRecipes.Craft(bag,"forge-stone-pick",CraftStation.Forge) && bag.Count(FieldItem.StonePickaxe)==1 && bag.Count(FieldItem.Stone)==stone-3,"Forge makes usable stone pickaxe with exact cost");
        bag.TryAdd(FieldItem.IronOre,6);bag.TryAdd(FieldItem.Coal,3);bag.TryAdd(FieldItem.Rope,1);
        for(int i=0;i<3;i++)check(FishingRecipes.Craft(bag,"forge-iron",CraftStation.Forge),"Forge ore and coal into ingot "+i);
        check(FishingRecipes.Craft(bag,"forge-iron-pick",CraftStation.Forge) && bag.Count(FieldItem.IronPickaxe)==1 && bag.Count(FieldItem.IronIngot)==0,"Forge upgrades gathered minerals to usable iron pickaxe");
        check(!FishingRecipes.Craft(bag,"forge-iron",CraftStation.Forge) && bag.Count(FieldItem.IronOre)==0,"Missing forge input cannot manufacture free output");
        check(restored.Remove(bag,id) && bag.Count(FieldItem.Forge)==1 && !restored.Remove(bag,id),"Owned forge recovers once");
        int publicId=restored.Place(null,CraftStation.Forge,6,1,7,0,true);
        check(!restored.Remove(bag,publicId),"Public forge cannot be dismantled");
        var full=new FishingBackpack(false);full.TryAdd(FieldItem.Stone,4);full.TryAdd(FieldItem.Stick,3);full.TryAdd(FieldItem.Fiber,3);full.TryAdd(FieldItem.Shell,33*64);
        check(!FishingRecipes.Craft(full,"forge-stone-pick",CraftStation.Forge) && full.Count(FieldItem.Stone)==4 && full.Count(FieldItem.Fiber)==3,"Forge full output bag rolls back all inputs");
        check(FishingRecipes.ForgeRecipes.Length==6 && FishingRecipes.ForgeRecipes.All(r=>r.Station==CraftStation.Forge),"Forge exposes tools and three compatible coal/fur lantern recipes");
        check(FishingRecipes.ForgePageRecipes.Any(r=>r.Id=="sticks") && FishingRecipes.ForgePageRecipes.All(r=>r.Station==CraftStation.Forge || r.Station==CraftStation.Hand),"Forge page also shows hand crafting so gathered driftwood can supply sticks on site");
        foreach(var pair in new[]{new[]{"coal-rabbit-lantern",FieldItem.RabbitFur.ToString()},new[]{"coal-fox-lantern",FieldItem.FoxFur.ToString()},new[]{"coal-legacy-lantern",FieldItem.Fur.ToString()}})
        {
            var fur=(FieldItem)Enum.Parse(typeof(FieldItem),pair[1]);var lampBag=new FishingBackpack(false);
            lampBag.TryAdd(FieldItem.Coal,2);lampBag.TryAdd(fur,1);
            check(!FishingRecipes.Craft(lampBag,pair[0],CraftStation.Hand) && lampBag.Count(FieldItem.Coal)==2,"Lantern requires forge without spending "+fur);
            check(FishingRecipes.Craft(lampBag,pair[0],CraftStation.Forge) && lampBag.Count(FieldItem.Lantern)==1 && lampBag.Count(FieldItem.Coal)==0 && lampBag.Count(fur)==0,"Coal and species fur produce held lantern "+fur);
            check(!FishingRecipes.Craft(lampBag,pair[0],CraftStation.Forge),"Lantern missing materials cannot duplicate "+fur);
        }
        var fullLamp=new FishingBackpack(false);fullLamp.TryAdd(FieldItem.Coal,3);fullLamp.TryAdd(FieldItem.RabbitFur,2);fullLamp.TryAdd(FieldItem.Shell,34*64);
        check(!FishingRecipes.Craft(fullLamp,"coal-rabbit-lantern",CraftStation.Forge) && fullLamp.Count(FieldItem.Coal)==3 && fullLamp.Count(FieldItem.RabbitFur)==2,"Full lantern bag returns every coal and fur input");
        var notFur=new FishingBackpack(false);notFur.TryAdd(FieldItem.Coal,2);notFur.TryAdd(FieldItem.GullFeather,1);notFur.TryAdd(FieldItem.TurtleShell,1);
        check(!FishingRecipes.Craft(notFur,"coal-rabbit-lantern",CraftStation.Forge),"Feathers and shells are not silently consumed as fur");
    }
}
