using System;
using BoatAttack.Fishing;

internal static class CreativeMaterialTests
{
    private static FishingCareer Creative()
    {var c=new FishingCareer();c.SetCreativeMode(true);return c;}
    private static FishingHandTask Completed(FishingHandTaskKind kind,bool gunParts=false)
    {
        var task=new FishingHandTask(kind,gunParts);
        if(kind==FishingHandTaskKind.Winder)
            for(int i=0;i<250;i++){double a=i*Math.PI/36;task.Sample((float)Math.Cos(a)*.1f,(float)Math.Sin(a)*.1f,.02f);}
        else
            for(int stroke=0;stroke<30;stroke++)for(int frame=0;frame<12;frame++)
                task.Sample(kind==FishingHandTaskKind.Forge?0:stroke%2==0?-.12f:.12f,kind==FishingHandTaskKind.Forge && stroke%2==0?.12f:0,.02f);
        return task;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(var recipe in FishingRecipes.All)
        {
            if(recipe.Station==CraftStation.Furnace || recipe.Id=="voyage-compass")continue;
            var c=Creative();var bag=c.Backpack;
            check(FishingRecipes.CanCraft(bag,recipe,recipe.Station) && FishingRecipes.Craft(bag,recipe.Id,recipe.Station)
                && bag.Count(recipe.Output)==recipe.Count,"Creative: zero-material recipe works at its station "+recipe.Id);
            var supplied=Creative();supplied.Backpack.TryAddBundle(recipe.Costs);
            check(FishingRecipes.Craft(supplied.Backpack,recipe.Id,recipe.Station),"Creative: supplied recipe succeeds "+recipe.Id);
            foreach(var cost in recipe.Costs)
                check(supplied.Backpack.Count(cost.item)==cost.count,"Creative: owned ingredient is preserved "+recipe.Id+"/"+cost.item);
        }
        foreach(FishingHandTaskKind kind in Enum.GetValues(typeof(FishingHandTaskKind)))
        {
            var c=Creative();var task=Completed(kind);
            check(task.Complete && task.HasMaterials(c.Backpack) && task.Claim(c.Backpack) && !task.Claim(c.Backpack)
                && c.Backpack.Count(task.Output)==task.OutputCount,"Creative: deliberate hand activity grants one output without input "+kind);
            check(!Completed(kind).Claim(new FishingBackpack()),"Creative: normal hand activity still requires materials "+kind);
        }
        var forge=Completed(FishingHandTaskKind.Forge,true);var gun=Creative();
        check(forge.Claim(gun.Backpack) && gun.Backpack.Count(FieldItem.GunParts)==1 && gun.Backpack.Count(FieldItem.IronIngot)==0,"Creative: alternate forge output also works with zero ingots");

        var workshop=Creative();int furnace=workshop.Workshop.Place(workshop.Backpack,CraftStation.Furnace,0,1,0,0);
        check(furnace>0 && workshop.Backpack.Count(FieldItem.Furnace)==0,"Creative: placing a facility does not require a consumed kit");
        check(workshop.Workshop.Start(workshop.Backpack,furnace,"charcoal",4,FieldItem.Driftwood)
            && workshop.Backpack.Count(FieldItem.Driftwood)==0,"Creative: furnace accepts zero input and zero fuel even when they share an item");
        check(!workshop.Workshop.Start(workshop.Backpack,furnace,"iron",1,FieldItem.Coal),"Creative: busy furnace remains unavailable");
        for(int i=0;i<32;i++)workshop.Workshop.Tick(1);
        check(workshop.Workshop.Collect(workshop.Backpack,furnace) && workshop.Backpack.Count(FieldItem.Charcoal)==4,"Creative: furnace work remains timed and collectible");
        check(!workshop.Workshop.Start(workshop.Backpack,furnace,"iron",9,FieldItem.Coal)
            && !workshop.Workshop.Start(workshop.Backpack,furnace,"iron",1,FieldItem.Empty),"Creative: invalid furnace batches and fuel stay rejected");

        var ecology=Creative();
        check(ecology.BuildAquarium() && ecology.Backpack.Count(FieldItem.Aquarium)==0 && !ecology.BuildAquarium(),"Creative: aquarium can be built without a kit exactly once");
        foreach(LandAnimalKind kind in Enum.GetValues(typeof(LandAnimalKind)))
            check(ecology.BuildAnimalHabitat(kind) && ecology.Backpack.Count(FishingCareer.HabitatKit(kind))==0 && !ecology.BuildAnimalHabitat(kind),"Creative: zero-kit habitat preserves the built-state guard "+kind);
        check(!ecology.UpgradeAquarium() && !ecology.UpgradeAnimalHabitat(LandAnimalKind.Rabbit),"Creative: free materials do not invent earned blueprints");
        int grams=FishingSpecies.Grams(FishKind.Coral,.5f);
        var fish=new CatchRecord{id="creative-care",kind=FishKind.Coral,grams=grams,value=FishingSpecies.Value(FishKind.Coral,false,grams)};
        check(ecology.Store(fish) && ecology.DisplayFish(fish.id) && ecology.FeedDisplayedFish(fish.id)
            && !ecology.FeedDisplayedFish(fish.id) && ecology.Backpack.Count(FieldItem.FishFeed)==0,"Creative: zero-food fish care retains once-per-day feeding");
        int trap=ecology.PlaceAnimalTrap(LandAnimalKind.Rabbit,0,1,0);
        check(trap>0 && ecology.BaitAnimalTrap(trap) && !ecology.BaitAnimalTrap(trap) && ecology.Backpack.Count(FieldItem.AnimalTrap)==0
            && ecology.Backpack.Count(FieldItem.TurnipSeeds)==0,"Creative: zero-kit trap and zero-food bait keep their state guards");
        int captured=ecology.CaptureAnimalInTrap(trap,0,LandAnimalKind.Rabbit,0,1,0);
        check(captured>0 && ecology.CollectTrappedAnimal(trap) && ecology.HouseAnimal(captured)
            && ecology.FeedHomeAnimal(captured) && !ecology.FeedHomeAnimal(captured),"Creative: a real captured animal can be housed and fed without food");
        check(FishingCareer.TryRestore(ecology.Snapshot(),out _),"Creative: built habitats, caught animal and fed fish remain valid save data");

        var garden=Creative();
        check(garden.Farm.Till(0) && garden.Farm.Plant(0,garden.Backpack) && garden.Farm.WaterPlot(0)
            && garden.Backpack.Count(FieldItem.TurnipSeeds)==0 && garden.Farm.Water==FishingFarm.WaterCapacity,"Creative: planting and watering preserve both stored seeds and water");
        var emptyCan=new FishingCareer().Snapshot();emptyCan.farm.water=0;
        check(FishingCareer.TryRestore(emptyCan,out var dry),"Creative: empty-can save fixture is valid");
        dry.SetCreativeMode(true);
        check(dry.Farm.Till(0) && dry.Farm.Plant(0,dry.Backpack) && dry.Farm.WaterPlot(0) && dry.Farm.Water==0
            && dry.Farm.AvailableWater==FishingFarm.WaterCapacity,"Creative: an empty stored can gains usable water without changing its saved amount");
        dry.SetCreativeMode(false);
        check(!dry.Backpack.FreeMaterials && !dry.Farm.FreeMaterials && dry.Farm.AvailableWater==0
            && dry.Farm.Till(1) && !dry.Farm.Plant(1,dry.Backpack),"Creative: disabling mode immediately restores ordinary material limits");

        var transactions=Creative();transactions.Backpack.TryAdd(FieldItem.Turnip,2);transactions.Backpack.TryAdd(FieldItem.SeaTreasure,1);
        check(transactions.SellTurnips()==36 && transactions.Backpack.Count(FieldItem.Turnip)==0 && transactions.SellTurnips()==0,"Creative: a produce sale still transfers real goods once");
        check(transactions.OpenSeaTreasure() && transactions.Backpack.Count(FieldItem.SeaTreasure)==0 && !transactions.OpenSeaTreasure(),"Creative: sea treasure cannot mint repeated rewards without another chest");
        check(!transactions.Backpack.CanSupply(null) && !transactions.Backpack.CanSupply(new FieldStack[]{null})
            && !transactions.Backpack.CanSupply(new[]{FishingRecipes.Stack(FieldItem.Empty,1)})
            && !transactions.Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.Driftwood,0)},FieldItem.Plank,4),"Creative: invalid costs remain rejected before a free exchange");
        var normal=new FishingBackpack(false);normal.TryAdd(FieldItem.Fiber,4);
        check(!normal.CanSupply(new[]{FishingRecipes.Stack(FieldItem.Fiber,3),FishingRecipes.Stack(FieldItem.Fiber,3)}),"Creative: duplicate ordinary costs are totalled for eligibility");
        var full=Creative();full.Backpack.TryAdd(FieldItem.Stone,35*64);
        check(!FishingRecipes.Craft(full.Backpack,"rope",CraftStation.Hand) && full.Backpack.Count(FieldItem.Stone)==35*64,"Creative: a full output bag is still transactional");
        check(FishingCareer.TryRestore(garden.Snapshot(),out var restored) && !restored.CreativeMode && !restored.Backpack.FreeMaterials
            && !restored.Farm.FreeMaterials,"Creative: resource policy remains session-only across domain restore");

        var quest=Creative();
        check(!quest.CraftVoyageCompass(CraftStation.Workbench) && quest.OpenGrandTreasure()==0
            && !quest.Backpack.CanSupply(new[]{FishingRecipes.Stack(FieldItem.WindRelic,1)}),"Creative: zero resources cannot bypass unique main-story keys");
        quest.AcceptMayorship();quest.DiscoverChart();
        for(int island=0;island<4;island++){quest.CompleteShrine(island);quest.ClaimShrineReward(island);}
        int ingots=quest.Backpack.Count(FieldItem.IronIngot),glass=quest.Backpack.Count(FieldItem.Glass);
        check(quest.CraftVoyageCompass(CraftStation.Workbench) && quest.Backpack.Count(FieldItem.IronIngot)==ingots
            && quest.Backpack.Count(FieldItem.Glass)==glass && !quest.CraftVoyageCompass(CraftStation.Workbench),"Creative: compass waives common materials but remains unique");
        for(int island=0;island<4;island++)check(quest.Backpack.Count(FishingLore.Relic(island))==0,"Creative: compass consumes its real unique relic "+island);
        check(FishingCareer.TryRestore(quest.Snapshot(),out _),"Creative: assembled compass preserves strict quest save invariants");
        check(quest.OpenGrandTreasure()==FishingCareer.GrandTreasureCoins && quest.Backpack.Count(FieldItem.VoyageCompass)==0
            && quest.OpenGrandTreasure()==0 && FishingCareer.TryRestore(quest.Snapshot(),out _),"Creative: final treasure consumes its key once and remains restorable");
        BarbecueChecks(check);
    }
    private static void BarbecueChecks(Action<bool,string> check)
    {
        var c=Creative();
        check(c.BeginBarbecue(null) && c.PracticeBarbecue && c.Count==0 && !c.BeginBarbecue(null),"Creative: an empty fishbox starts one practice roast without replacing an active grill");
        var saved=c.Snapshot();
        check(saved.barbecue.fish==null && saved.barbecue.remaining==0 && saved.catches==0 && saved.discoveryMask==0
            && saved.recordedIds.Count==0 && saved.coins==0 && FishingCareer.TryRestore(saved,out var loaded) && loaded.Barbecue.fish==null,
            "Creative: practice progress is omitted from valid saves without fake captures, discoveries or money");
        check(!c.CollectGrilledFish(),"Creative: a practice meal cannot be collected before the normal roast time");
        for(int i=0;i<72;i++)c.TickBarbecue(.25f);
        c.Backpack.TryAdd(FieldItem.Stone,35*64);
        check(c.BarbecueReady && !c.CollectGrilledFish() && c.PracticeBarbecue,"Creative: a full bag safely retains the ready practice meal");
        c.Backpack.Discard(1,FieldItem.Stone,64);
        check(c.CollectGrilledFish() && !c.PracticeBarbecue && c.Backpack.Count(FieldItem.GrilledFish)==1 && !c.CollectGrilledFish()
            && c.Catches==0 && c.DiscoveryMask==0 && c.Coins==0 && FishingCareer.TryRestore(c.Snapshot(),out _),
            "Creative: the practice meal is collected exactly once without changing catch statistics and saves normally");

        int grams=FishingSpecies.Grams(FishKind.Coral,.5f);
        var real=new CatchRecord{id="creative-bbq-real",kind=FishKind.Coral,grams=grams,value=FishingSpecies.Value(FishKind.Coral,false,grams)};
        var keep=Creative();keep.Store(real);var before=keep.Snapshot();
        check(keep.BeginBarbecue(real.id) && keep.PracticeBarbecue && keep.Barbecue.fish.id!=real.id && keep.Count==1
            && keep.Inventory[0].id==real.id,"Creative: requesting an owned fish uses an independent practice identity and preserves the real fish");
        saved=keep.Snapshot();
        check(saved.catches==before.catches && saved.discoveryMask==before.discoveryMask && saved.coins==before.coins
            && saved.recordedIds.Count==before.recordedIds.Count && FishingCareer.TryRestore(saved,out loaded) && loaded.Count==1,
            "Creative: a retained real fish and practice grill cannot invalidate the saved capture receipts");
        var normal=new FishingCareer();normal.Store(real);normal.BeginBarbecue(real.id);normal.SetCreativeMode(true);
        check(!normal.BeginBarbecue(null) && !normal.PracticeBarbecue && normal.Barbecue.fish.id==real.id
            && FishingCareer.TryRestore(normal.Snapshot(),out loaded) && loaded.Barbecue.fish.id==real.id,
            "Creative: enabling mode never replaces a real reserved fish or its persisted progress");
        check(!new FishingCareer().BeginBarbecue(null),"Creative: an ordinary empty fishbox still cannot start a roast");
    }
}
