using System;
using BoatAttack.Fishing;

internal static class HomeEcologyTests
{
    private static FishingCareer Seed(int coins=3000)
    {
        var data=new FishingCareer().Snapshot();data.coins=coins;
        if(!FishingCareer.TryRestore(data,out var c))throw new Exception("Ecology seed must be a valid current save");
        return c;
    }
    private static CatchRecord Fish(string id,bool rare=false)
    {
        int grams=FishingSpecies.Grams(FishKind.Coral,.45f);
        return new CatchRecord{id=id,kind=FishKind.Coral,rare=rare,grams=grams,value=FishingSpecies.Value(FishKind.Coral,rare,grams)};
    }
    private static void Equip(FishingCareer c,FieldItem item)
    {
        if(c.Backpack.Count(item)==0 && !c.Backpack.TryAdd(item,1))throw new Exception("Ecology tool fixture cannot be added");
        for(int i=0;i<FishingBackpack.HotbarCount;i++)if(c.Backpack.At(i).item==item){c.Backpack.Select(i);return;}
        throw new Exception("Ecology tool fixture must be in the hotbar");
    }
    private static void Aquarium(FishingCareer c)
    {if(!c.Backpack.TryAdd(FieldItem.Aquarium,1) || !c.BuildAquarium())throw new Exception("Ecology aquarium fixture cannot be built");}
    private static void Habitat(FishingCareer c,LandAnimalKind kind)
    {if(!c.Backpack.TryAdd(FishingCareer.HabitatKit(kind),1) || !c.BuildAnimalHabitat(kind))throw new Exception("Ecology habitat fixture cannot be built");}
    private static void Unlock(FishingCareer c,int island)
    {c.AcceptMayorship();c.DiscoverChart();c.CompleteShrine(island);if(!c.ClaimShrineReward(island))throw new Exception("Ecology blueprint fixture cannot be claimed");}
    private static void Fill(FishingBackpack bag)
    {while(bag.TryAdd(FieldItem.Stone,64)){} }
    private static LandAnimalKind Kind(int id)=>id<8?(LandAnimalKind)(id%3):id<11?LandAnimalKind.Gull:LandAnimalKind.SeaTurtle;
    private static FishingCareer Pair(out int first,out int second)
    {
        var c=Seed();Habitat(c,LandAnimalKind.Rabbit);Equip(c,FieldItem.CaptureNet);
        first=c.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit);second=c.CaptureAnimalWithNet(3,LandAnimalKind.Rabbit);
        if(first==0 || second==0 || !c.HouseAnimal(first) || !c.HouseAnimal(second))throw new Exception("Ecology rabbit pair fixture failed");
        return c;
    }
    public static void Run(Action<bool,string> check)
    {
        FishRules(check);BlueprintGrowthRules(check);AnimalRules(check);TrapRules(check);SaveRules(check);CatalogRules(check);
    }
    private static void BlueprintGrowthRules(Action<bool,string> check)
    {
        var c=Seed();Aquarium(c);const string id="star-blueprint-fish";c.Store(Fish(id));c.DisplayFish(id);c.Backpack.TryAdd(FieldItem.FishFeed,4);
        check(c.AquariumGrowthDays==3 && c.FeedDisplayedFish(id),"Blueprint: ordinary aquarium care still requires three feeding days");
        c.Calendar.RestUntilMorning();c.FeedDisplayedFish(id);c.Calendar.RestUntilMorning();
        c.AcceptMayorship();c.DiscoverChart();c.CompleteShrine(3);
        check(c.AquariumGrowthDays==3 && c.AquariumGrowth(id)<1,"Blueprint: clearing star alone cannot accelerate existing display fish");
        int food=c.Backpack.Count(FieldItem.FishFeed);
        check(c.ClaimShrineReward(3) && c.AquariumGrowthDays==2 && c.AquariumGrowth(id)==1 && !c.FeedDisplayedFish(id) && c.Backpack.Count(FieldItem.FishFeed)==food,"Blueprint: claiming star matures a twice-fed two-day fish without another ration");
        check(FishingCareer.TryRestore(c.Snapshot(),out var loaded) && loaded.AquariumGrowthDays==2 && loaded.AquariumGrowth(id)==1 && !loaded.FeedDisplayedFish(id),"Blueprint: star care and mature fish survive save reload without repeated feeding");
        const string fresh="star-fresh-fish";c.Store(Fish(fresh));c.DisplayFish(fresh);c.FeedDisplayedFish(fresh);
        check(c.AquariumGrowth(fresh)==0 && !c.FeedDisplayedFish(fresh),"Blueprint: improved care still requires elapsed days and prevents duplicate same-day feeding");
        c.Calendar.RestUntilMorning();c.FeedDisplayedFish(fresh);
        check(c.AquariumGrowth(fresh)==.5f,"Blueprint: a newly displayed fish does not mature after only one day");
        c.Calendar.RestUntilMorning();
        check(c.AquariumGrowth(fresh)==1 && !c.FeedDisplayedFish(fresh),"Blueprint: new fish mature with two actual days and two daily rations");
        var old=Seed();Aquarium(old);const string mature="old-mature-fish";old.Store(Fish(mature));old.DisplayFish(mature);old.Backpack.TryAdd(FieldItem.FishFeed,3);
        for(int i=0;i<3;i++){old.FeedDisplayedFish(mature);old.Calendar.RestUntilMorning();}
        check(old.AquariumGrowth(mature)==1,"Blueprint: existing three-day fish begin fully mature");
        Unlock(old,3);
        check(old.AquariumGrowth(mature)==1 && !old.FeedDisplayedFish(mature) && FishingCareer.TryRestore(old.Snapshot(),out loaded) && loaded.AquariumGrowth(mature)==1,"Blueprint: unlocking improved care never shrinks or invalidates previously mature fish");
    }
    private static void FishRules(Action<bool,string> check)
    {
        var c=Seed();var fish=Fish("display-original",true);
        check(c.Store(fish) && !c.DisplayFish(fish.id) && c.Count==1,"Ecology: an unbuilt aquarium cannot remove a fish from the hold");
        Aquarium(c);
        check(c.AquariumCapacity==4 && !c.BuildAquarium() && c.DisplayFish(fish.id) && c.Count==0 && c.AquariumCount==1,
            "Ecology: one real fish moves atomically into a four-place aquarium");
        check(!c.DisplayFish(fish.id) && !c.Store(fish) && c.SellAll()==0 && c.DeliverOrder()==0 && !c.BeginBarbecue(fish.id),
            "Ecology: a display fish cannot be inserted twice, sold, cooked or submitted");
        check(!c.FeedDisplayedFish(fish.id) && c.HomeEcology.fish[0].fedDays==0,"Ecology: missing fish food leaves growth and feeding receipts unchanged");
        var view=c.HomeEcology;view.fish[0].fish.id="changed";view.fish.Clear();
        check(c.AquariumCount==1 && c.HomeEcology.fish[0].fish.id==fish.id,"Ecology: aquarium readers cannot mutate owned fish");
        check(c.Backpack.TryAdd(FieldItem.FishFeed,4) && c.FeedDisplayedFish(fish.id) && !c.FeedDisplayedFish(fish.id)
            && c.Backpack.Count(FieldItem.FishFeed)==3 && c.AquariumGrowth(fish.id)==0,"Ecology: one daily feeding consumes one ration and growth still waits for game time");
        double minutes=c.Calendar.TotalMinutes;c.Calendar.Tick(20,true);c.TickHomeEcology(20,true);
        check(c.Calendar.TotalMinutes==minutes && c.AquariumGrowth(fish.id)==0,"Ecology: pause does not age display fish");
        c.Calendar.RestUntilMorning();check(c.AquariumGrowth(fish.id)>0 && c.AquariumGrowth(fish.id)<.34f,"Ecology: sleeping advances the actual aquarium day");
        check(c.FeedDisplayedFish(fish.id),"Ecology: the next game day allows another feeding");
        c.Calendar.RestUntilMorning();check(c.FeedDisplayedFish(fish.id),"Ecology: three daily rations can be supplied over time");
        c.Calendar.RestUntilMorning();
        check(c.AquariumGrowth(fish.id)==1 && !c.FeedDisplayedFish(fish.id) && c.Backpack.Count(FieldItem.FishFeed)==1,
            "Ecology: mature fish stop consuming growth rations");
        var data=c.Snapshot();check(FishingCareer.TryRestore(data,out var loaded) && loaded.AquariumGrowth(fish.id)==1
            && loaded.Calendar.TotalMinutes==c.Calendar.TotalMinutes,"Ecology: reload preserves growth and does not invent offline time");
        check(loaded.ReturnDisplayedFish(fish.id) && !loaded.ReturnDisplayedFish(fish.id) && loaded.Inventory[0].grams==fish.grams
            && loaded.Inventory[0].rare==fish.rare && loaded.Inventory[0].value==fish.value,"Ecology: returning a fish preserves its original weight, rarity and sale value");

        c=Seed();Aquarium(c);
        for(int i=0;i<4;i++){var f=Fish("tank-"+i);if(!c.Store(f) || !c.DisplayFish(f.id))throw new Exception("Ecology full aquarium fixture failed");}
        var spare=Fish("tank-spare");c.Store(spare);
        check(!c.DisplayFish(spare.id) && c.Count==1 && c.AquariumCount==4,"Ecology: a full tank leaves the source fish aboard");
        int before=c.Coins;check(!c.UpgradeAquarium() && c.Coins==before,"Ecology: capacity upgrade needs its permanent tide blueprint");
        Unlock(c,2);before=c.Coins;
        check(c.UpgradeAquarium() && c.AquariumCapacity==8 && c.Coins==before-FishingCareer.AquariumUpgradeCost
            && !c.UpgradeAquarium(),"Ecology: a blueprint and exact coins upgrade the tank once to eight places");
        check(c.DisplayFish(spare.id),"Ecology: the upgraded aquarium accepts the previously blocked fish");
        for(int i=0;c.Count<c.Capacity;i++)if(!c.Store(Fish("full-hold-"+i)))throw new Exception("Ecology hold fixture failed");
        check(!c.ReturnDisplayedFish("tank-0") && c.Count==c.Capacity && c.AquariumCount==5,"Ecology: a full return hold preserves the display fish");

        c=Seed();Aquarium(c);var protectedFish=Fish("long-lived-fish");c.Store(protectedFish);c.DisplayFish(protectedFish.id);
        for(int i=0;i<FishingCareer.RecentIdLimit+32;i++){if(!c.Store(Fish("later-"+i)))throw new Exception("Ecology history fixture failed");c.SellAll();}
        check(!c.Store(protectedFish) && c.Snapshot().recordedIds.Contains(protectedFish.id)
            && FishingCareer.TryRestore(c.Snapshot(),out loaded) && loaded.AquariumCount==1,
            "Ecology: long-lived display fish keep replay protection after the bounded catch history rolls over");
        check(c.BeginPending(Fish("unsettled-fish"),0,0,0) && !c.ReturnDisplayedFish(protectedFish.id)
            && c.AquariumCount==1 && c.Count==0,"Ecology: pending settlement blocks container transfers without losing displayed fish");
    }
    private static void AnimalRules(Action<bool,string> check)
    {
        var c=Seed();check(c.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit)==0,"Ecology: live capture requires the equipped net");
        Equip(c,FieldItem.CaptureNet);
        check(c.CaptureAnimalWithNet(1,LandAnimalKind.Rabbit)==0 && c.CaptureAnimalWithNet(-1,LandAnimalKind.Rabbit)==0,
            "Ecology: source ID and animal kind must match");
        int captured=c.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit);
        check(captured>0 && c.IsWildAnimalCaptured(0) && c.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit)==0
            && !c.Hunting.Available(0,c.Calendar.TotalMinutes),"Ecology: one live source is reserved once across net and hunting");
        check(c.Backpack.Seen(LandAnimalKind.Rabbit) && c.Hunting.Harvests==0,
            "Ecology: a live land capture records its discovery without counting a kill");
        Equip(c,FieldItem.HuntingRifle);
        check(!c.KillLandAnimal(0,LandAnimalKind.Rabbit,0,0,0,0),"Ecology: a rifle cannot kill a source already reserved by live capture");
        Equip(c,FieldItem.HuntingSpear);
        check(!c.Hunting.Harvest(c.Backpack,0,LandAnimalKind.Rabbit,c.Calendar.TotalMinutes),"Ecology: a spear cannot bypass a live-source receipt");
        c.TickHomeEcology(1,true);c.TickHomeEcology(float.NaN);c.TickHomeEcology(float.PositiveInfinity);c.Calendar.RestUntilMorning();
        check(c.HomeEcology.captures[0].remaining==FishingAnimalLoot.RespawnSeconds,"Ecology: pause, invalid deltas and sleep cannot skip live-source recovery");
        for(int i=0;i<299;i++)c.TickHomeEcology(1);
        FishingCareer loaded=null;
        check(c.IsWildAnimalCaptured(0) && FishingCareer.TryRestore(c.Snapshot(),out loaded)
            && loaded.HomeEcology.captures[0].remaining==1,"Ecology: source recovery remainder survives reload");
        check(loaded.TickHomeEcology(1) && !loaded.IsWildAnimalCaptured(0) && loaded.Hunting.Available(0,loaded.Calendar.TotalMinutes),
            "Ecology: the source becomes available only after all active recovery seconds");
        Equip(loaded,FieldItem.HuntingRifle);check(loaded.KillLandAnimal(0,LandAnimalKind.Rabbit,0,0,0,0),"Ecology: an actual recovered wild source can be hunted again");
        Equip(loaded,FieldItem.CaptureNet);check(loaded.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit)==0,"Ecology: an existing corpse blocks live capture");

        c=Pair(out int first,out int second);
        check(c.BreedHomeAnimals(first,second)==0 && c.HomeAnimalCount==2 && c.HomeAnimal(first).lastBreedDay==-1,
            "Ecology: missing breeding food leaves parents and next birth unchanged");
        c.Backpack.TryAdd(FieldItem.TurnipSeeds,1);c.FeedHomeAnimal(first);
        check(c.BreedHomeAnimals(first,second)==0 && c.HomeAnimal(first).lastFedDay==c.Calendar.Day && c.HomeAnimal(second).lastFedDay==-1,
            "Ecology: a partially fed pair cannot charge missing food or forge a second feeding receipt");
        int sameSex=c.CaptureAnimalWithNet(6,LandAnimalKind.Rabbit);c.HouseAnimal(sameSex);c.Backpack.TryAdd(FieldItem.TurnipSeeds,2);
        check(c.BreedHomeAnimals(first,sameSex)==0 && c.Backpack.Count(FieldItem.TurnipSeeds)==2,
            "Ecology: a same-sex pair cannot consume food or produce a baby");
        c=Pair(out first,out second);c.Backpack.TryAdd(FieldItem.TurnipSeeds,8);
        check(c.HabitatCapacity(LandAnimalKind.Rabbit)==4 && c.HomeAnimal(first).female!=c.HomeAnimal(second).female
            && c.AnimalAdult(first) && c.AnimalAdult(second),"Ecology: captured adult rabbits occupy the appropriate species habitat");
        int child=c.BreedHomeAnimals(first,second);
        check(child>0 && c.HousedAnimalCount(LandAnimalKind.Rabbit)==3 && c.Backpack.Count(FieldItem.TurnipSeeds)==6
            && c.HomeAnimal(child).parentA==first && !c.AnimalAdult(child),"Ecology: an adult opposite-sex pair consumes two seeds and creates one baby");
        check(c.BreedHomeAnimals(first,second)==0 && c.BreedHomeAnimals(first,child)==0 && c.Backpack.Count(FieldItem.TurnipSeeds)==6,
            "Ecology: same-day rebreeding and immature parents never consume another meal");
        check(FishingCareer.TryRestore(c.Snapshot(),out loaded) && loaded.BreedHomeAnimals(first,second)==0 && loaded.HomeAnimalCount==3,
            "Ecology: reloading cannot repeat a birth");
        c=loaded;c.Calendar.RestUntilMorning();
        check(!c.AnimalAdult(child) && c.FeedHomeAnimal(first) && c.FeedHomeAnimal(second),"Ecology: the baby remains young after one day and each parent can be fed once");
        int afterMeals=c.Backpack.Count(FieldItem.TurnipSeeds);int secondChild=c.BreedHomeAnimals(first,second);
        check(secondChild>0 && c.Backpack.Count(FieldItem.TurnipSeeds)==afterMeals && c.HousedAnimalCount(LandAnimalKind.Rabbit)==4,
            "Ecology: parents already fed today produce one baby without double-charging seeds");
        c.Calendar.RestUntilMorning();int seedCount=c.Backpack.Count(FieldItem.TurnipSeeds);
        check(c.AnimalAdult(child) && c.BreedHomeAnimals(first,second)==0 && c.Backpack.Count(FieldItem.TurnipSeeds)==seedCount,
            "Ecology: babies mature in two game days while a full habitat prevents a paid birth");
        beforeUpgrade(c,check);
        check(c.CarryHomeAnimal(child) && !c.CarryHomeAnimal(child) && c.HouseAnimal(child),"Ecology: live animals move between carrier and their habitat once");
        var animalView=c.HomeAnimal(child);animalView.housed=false;
        check(c.HomeAnimal(child).housed,"Ecology: animal readers cannot change housing ownership");
        check(c.ReleaseHomeAnimal(first) && FishingCareer.TryRestore(c.Snapshot(),out loaded)
            && loaded.HomeAnimal(child).parentA==first,"Ecology: releasing a parent preserves the offspring's historical lineage");

        c=Seed();Equip(c,FieldItem.CaptureNet);
        int fox=c.CaptureAnimalWithNet(1,LandAnimalKind.Fox),gull=c.CaptureAnimalWithNet(8,LandAnimalKind.Gull);
        c.Backpack.TryAdd(FieldItem.RabbitMeat,2);
        check(fox>0 && gull>0 && c.FeedHomeAnimal(fox) && c.FeedHomeAnimal(gull) && c.Backpack.Count(FieldItem.RabbitMeat)==0,
            "Ecology: foxes and sea birds eat meat actually produced by current hunting loot");
        check(!c.HouseAnimal(fox),"Ecology: an animal cannot be put in an unbuilt species habitat");
        Habitat(c,LandAnimalKind.Fox);check(c.HouseAnimal(fox) && c.HousedAnimalCount(LandAnimalKind.Rabbit)==0,"Ecology: fox housing does not use rabbit capacity");

        c=Seed();Unlock(c,1);Equip(c,FieldItem.CaptureNet);
        foreach(LandAnimalKind kind in Enum.GetValues(typeof(LandAnimalKind))){Habitat(c,kind);if(!c.UpgradeAnimalHabitat(kind))throw new Exception("Ecology total-capacity fixture upgrade failed");}
        var ids=new int[FishingAnimalLoot.TargetCount];
        for(int i=0;i<ids.Length;i++){ids[i]=c.CaptureAnimalWithNet(i,Kind(i));if(ids[i]==0 || !c.HouseAnimal(ids[i]))throw new Exception("Ecology total-capacity fixture capture failed");}
        for(int i=0;i<300;i++)c.TickHomeEcology(1);
        foreach(int source in new[]{0,3,6,1}){int id=c.CaptureAnimalWithNet(source,Kind(source));if(id==0 || !c.HouseAnimal(id))throw new Exception("Ecology total cap refill failed");}
        c.Backpack.TryAdd(FieldItem.TurnipSeeds,2);
        check(c.HomeAnimalCount==16 && c.CaptureAnimalWithNet(4,LandAnimalKind.Fox)==0 && c.BreedHomeAnimals(ids[0],ids[3])==0
            && c.Backpack.Count(FieldItem.TurnipSeeds)==2,"Ecology: the total home population cap blocks captures and births without payment");
    }
    private static void beforeUpgrade(FishingCareer c,Action<bool,string> check)
    {
        int coins=c.Coins;check(!c.UpgradeAnimalHabitat(LandAnimalKind.Rabbit) && c.Coins==coins,"Ecology: habitat upgrades require the forest blueprint");
        Unlock(c,1);coins=c.Coins;
        check(c.UpgradeAnimalHabitat(LandAnimalKind.Rabbit) && c.HabitatCapacity(LandAnimalKind.Rabbit)==8
            && c.Coins==coins-FishingCareer.HabitatUpgradeCost && !c.UpgradeAnimalHabitat(LandAnimalKind.Rabbit),
            "Ecology: one exact-price habitat upgrade doubles species capacity");
    }
    private static void TrapRules(Action<bool,string> check)
    {
        var c=Seed();c.Backpack.TryAdd(FieldItem.AnimalTrap,4);c.Backpack.TryAdd(FieldItem.TurnipSeeds,3);
        check(c.PlaceAnimalTrap(LandAnimalKind.SeaTurtle,0,0,0)==0 && c.PlaceAnimalTrap(LandAnimalKind.Rabbit,float.NaN,0,0)==0
            && c.PlaceAnimalTrap(LandAnimalKind.Rabbit,2001,0,0)==0 && c.Backpack.Count(FieldItem.AnimalTrap)==4,
            "Ecology: land traps reject sea turtles, invalid coordinates and out-of-world positions without payment");
        int trap=c.PlaceAnimalTrap(LandAnimalKind.Rabbit,0,0,0);
        check(trap>0 && c.PlaceAnimalTrap(LandAnimalKind.Rabbit,1,0,0)==0 && c.Backpack.Count(FieldItem.AnimalTrap)==3,
            "Ecology: trap placement consumes one kit and enforces spacing");
        check(c.CaptureAnimalInTrap(trap,0,LandAnimalKind.Rabbit,0,0,0)==0 && c.BaitAnimalTrap(trap) && !c.BaitAnimalTrap(trap)
            && c.Backpack.Count(FieldItem.TurnipSeeds)==2,"Ecology: only an armed trap consumes one bait and it cannot be baited twice");
        check(c.CaptureAnimalInTrap(trap,0,LandAnimalKind.Rabbit,2,0,0)==0
            && c.CaptureAnimalInTrap(trap,1,LandAnimalKind.Fox,0,0,0)==0 && c.AnimalTrap(trap).baited,
            "Ecology: a distant or wrong-species animal does not consume the armed trap");
        int animal=c.CaptureAnimalInTrap(trap,0,LandAnimalKind.Rabbit,.4f,0,.3f);
        check(animal>0 && c.AnimalTrap(trap).animalId==animal && !c.AnimalTrap(trap).baited
            && c.CaptureAnimalInTrap(trap,3,LandAnimalKind.Rabbit,0,0,0)==0 && !c.RemoveAnimalTrap(trap),
            "Ecology: one trap holds exactly one live animal and cannot be removed while occupied");
        Equip(c,FieldItem.CaptureNet);
        foreach(int source in new[]{3,6,1,4})if(c.CaptureAnimalWithNet(source,Kind(source))==0)throw new Exception("Ecology carrier fixture failed");
        check(!c.CollectTrappedAnimal(trap) && c.AnimalTrap(trap).animalId==animal && c.CarriedAnimalCount==4,
            "Ecology: a full carrier leaves the animal safely inside its trap");
        int release=0;foreach(var a in c.HomeAnimals)if(a.trapId==0){release=a.id;break;}
        c.ReleaseHomeAnimal(release);
        check(c.CollectTrappedAnimal(trap) && !c.CollectTrappedAnimal(trap) && c.HomeAnimal(animal).trapId==0,
            "Ecology: collecting a trap animal transfers ownership once when carrier space is free");
        check(c.RemoveAnimalTrap(trap) && !c.RemoveAnimalTrap(trap),"Ecology: an empty trap returns one reusable kit once");
        while(c.AnimalTrapCount<4){int i=c.AnimalTrapCount;if(c.PlaceAnimalTrap(LandAnimalKind.Rabbit,10+i*3,0,0)==0)throw new Exception("Ecology four-trap fixture failed");}
        check(c.PlaceAnimalTrap(LandAnimalKind.Rabbit,40,0,0)==0 && c.AnimalTrapCount==4,"Ecology: only four active traps may exist");
        var first=c.HomeEcology.traps[0];Fill(c.Backpack);
        check(c.Backpack.UsedSlots==FishingBackpack.SlotCount && !c.RemoveAnimalTrap(first.id) && c.AnimalTrapCount==4,
            "Ecology: removing a trap into a full backpack leaves the world trap intact");
        check(FishingCareer.TryRestore(c.Snapshot(),out var loaded) && loaded.AnimalTrapCount==4 && loaded.CarriedAnimalCount==4,
            "Ecology: trap positions and carried live animals survive a save round trip");
    }
    private static void SaveRules(Action<bool,string> check)
    {
        foreach(int version in new[]{15,16})
        {
            var data=new FishingCareer().Snapshot();data.version=version;data.homeEcology=null;
            check(FishingCareer.TryRestore(data,out var loaded) && loaded.Snapshot().version==FishingSaveData.CurrentVersion && loaded.AquariumCount==0
                && loaded.HomeAnimalCount==0 && data.homeEcology==null,"Ecology: v"+version+" migrates empty facilities without editing the caller DTO");
            data=new FishingCareer().Snapshot();data.version=version;data.homeEcology.aquariumLevel=1;
            check(!FishingCareer.TryRestore(data,out _),"Ecology: old schema cannot carry invented ecology state "+version);
            data=new FishingCareer().Snapshot();data.version=version;data.backpack[1]=new FieldStack{item=FieldItem.CaptureNet,count=1};
            check(!FishingCareer.TryRestore(data,out _),"Ecology: old schema cannot introduce future tools "+version);
        }
        var c=Seed();Aquarium(c);var f=Fish("saved-display");c.Store(f);c.DisplayFish(f.id);
        var save=c.Snapshot();save.inventory.Add(f.Copy());
        check(!FishingCareer.TryRestore(save,out _),"Ecology: one catch ID cannot be both aboard and displayed");
        save=c.Snapshot();save.homeEcology.fish.Add(save.homeEcology.fish[0].Copy());
        check(!FishingCareer.TryRestore(save,out _),"Ecology: duplicate display fish IDs are rejected");
        save=c.Snapshot();save.homeEcology.fish[0].lastFedDay=save.calendar.day+1;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: future fish feeding dates are rejected");
        save=c.Snapshot();save.discoveryMask=0;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: displayed fish must agree with their recorded discovery");
        save=c.Snapshot();save.homeEcology=null;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: a current schema needs a valid ecology container");

        c=Pair(out int first,out int second);c.Backpack.TryAdd(FieldItem.TurnipSeeds,2);int child=c.BreedHomeAnimals(first,second);
        save=c.Snapshot();save.homeEcology.animals.Add(save.homeEcology.animals[0].Copy());
        check(!FishingCareer.TryRestore(save,out _),"Ecology: duplicate live animal IDs are rejected");
        save=c.Snapshot();save.homeEcology.animals.Find(a=>a.id==child).parentB=first;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: a baby cannot have the same parent twice");
        save=c.Snapshot();save.homeEcology.animals.Find(a=>a.id==second).female=save.homeEcology.animals.Find(a=>a.id==first).female;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: present parents must still be an eligible opposite-sex pair");
        save=c.Snapshot();save.homeEcology.captures[0].remaining=double.NaN;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: nonfinite source cooldown is rejected");
        save=c.Snapshot();save.huntedAnimals.Add(new HuntedAnimalData{id=0,kind=LandAnimalKind.Rabbit,collected=false});
        check(!FishingCareer.TryRestore(save,out _),"Ecology: a source cannot be both captured alive and an existing corpse");
        save=c.Snapshot();save.homeEcology.animals.Find(a=>a.id==child).trapId=77;save.homeEcology.animals.Find(a=>a.id==child).housed=false;
        check(!FishingCareer.TryRestore(save,out _),"Ecology: captive animals cannot reference a missing trap");
        check(FishingCareer.TryRestore(c.Snapshot(),out var loadedPair) && loadedPair.HomeAnimalCount==3,
            "Ecology: validation failures never mutate a valid running family");

        c=Seed();Equip(c,FieldItem.CaptureNet);save=c.Snapshot();save.homeEcology.nextAnimalId=999999;
        if(!FishingCareer.TryRestore(save,out var lastAnimal))throw new Exception("Ecology final animal ID seed failed");
        check(lastAnimal.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit)==999999 && lastAnimal.CaptureAnimalWithNet(3,LandAnimalKind.Rabbit)==0
            && FishingCareer.TryRestore(lastAnimal.Snapshot(),out _),"Ecology: the final animal ID leaves a readable exhausted counter");
        c=Seed();c.Backpack.TryAdd(FieldItem.AnimalTrap,2);save=c.Snapshot();save.homeEcology.nextTrapId=999999;
        if(!FishingCareer.TryRestore(save,out var lastTrap))throw new Exception("Ecology final trap ID seed failed");
        check(lastTrap.PlaceAnimalTrap(LandAnimalKind.Rabbit,0,0,0)==999999 && lastTrap.PlaceAnimalTrap(LandAnimalKind.Rabbit,3,0,0)==0
            && lastTrap.Backpack.Count(FieldItem.AnimalTrap)==1 && FishingCareer.TryRestore(lastTrap.Snapshot(),out _),
            "Ecology: exhausted trap IDs preserve the remaining kit and a readable save");
    }
    private static void CatalogRules(Action<bool,string> check)
    {
        check((int)FieldItem.VoyageCompass==51 && (int)FieldItem.CaptureNet==52 && FishingSupplies.Items.Length>=30
            && FishingSupplies.CategoryCount>=5 && FishingSupplies.CategoryName(4)=="家园生态",
            "Ecology: tool IDs and a fifth six-product page append to the stable catalog");
        foreach(string id in new[]{"capture-net","animal-trap","aquarium","fish-feed","rabbit-hutch","fox-den","tortoise-pen","bird-aviary","sea-turtle-pond"})
        {
            var recipe=FishingRecipes.Find(id);var bag=new FishingBackpack(false);
            foreach(var cost in recipe.Costs)bag.TryAdd(cost.item,cost.count);
            check(FishingRecipes.Craft(bag,id,recipe.Station) && bag.Count(recipe.Output)==recipe.Count
                && !string.IsNullOrEmpty(FishingBackpack.Name(recipe.Output)),"Ecology: practical recipe produces its actual named item "+id);
        }
        var c=Seed();int before=c.Coins;
        check(c.BuySupply(24) && c.Backpack.Count(FieldItem.FishFeed)==8 && c.Coins==before-12,
            "Ecology: purchased fish feed permits aquarium care without hunting");
    }
}
