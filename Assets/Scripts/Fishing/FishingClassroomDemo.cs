using System;

namespace BoatAttack.Fishing
{
    /// <summary>A reproducible teaching save, separate from the player's voyage.</summary>
    public static class FishingClassroomDemo
    {
        public const int Coins = 100000;
        public static bool ValidDate(int year,int season,int date,int hour) => year>=1 && year<=999
            && season>=0 && season<4 && date>=1 && date<=28 && hour>=0 && hour<24;
        public static FishingCareer Create(int year=1,int season=0,int date=14,int hour=10)
        {
            if(!ValidDate(year,season,date,hour))throw new ArgumentOutOfRangeException(nameof(date));
            int day=(year-1)*FishingCalendar.DaysPerYear+season*FishingCalendar.DaysPerSeason+date-1;
            var data=new FishingCareer().Snapshot();
            data.coins=Coins;data.rodLevel=data.reelLevel=data.holdLevel=2;
            data.engineLevel=data.rudderLevel=data.cabinLevel=2;data.prologueSeen=true;
            data.calendar=new CalendarData{day=day,minute=hour*60,timeFixed=true};
            data.farm=new FishingFarm(day).Snapshot();
            Require(FishingCareer.TryRestore(data,out var career),"initial career");
            var bag=career.Backpack;
            // Keep the nine tools together on the first hotbar page.
            foreach(var item in new[]{FieldItem.Binoculars,FieldItem.Lantern,FieldItem.IronPickaxe,FieldItem.HuntingRifle,
                FieldItem.CaptureNet,FieldItem.Axe,FieldItem.Shovel,FieldItem.Rake})Add(bag,item,1);
            career.AcceptMayorship();career.DiscoverChart();
            for(int i=0;i<4;i++)
            {
                career.DiscoverIsland(i);career.Lore.Clear(i);
                Require(career.ClaimShrineReward(i),"shrine rewards");
            }
            foreach(var item in new[]{FieldItem.Workbench,FieldItem.Furnace,FieldItem.Forge,FieldItem.HuntingSpear})Add(bag,item,1);
            foreach(var item in new[]{FieldItem.FishFeed,FieldItem.TurnipSeeds,FieldItem.Turnip,FieldItem.RabbitMeat,
                FieldItem.GrilledFish,FieldItem.IslandFruit,FieldItem.CookedMeat,FieldItem.AnimalTrap})Add(bag,item,16);
            Add(bag,FieldItem.Aquarium,1);Require(career.BuildAquarium(),"aquarium");
            Require(career.UpgradeAquarium(),"aquarium upgrade");
            foreach(LandAnimalKind kind in Enum.GetValues(typeof(LandAnimalKind)))
            {
                Add(bag,FishingCareer.HabitatKit(kind),1);Require(career.BuildAnimalHabitat(kind),"habitat");
            }
            bag.Select(5);
            foreach(int source in new[]{0,3,1,2,8,11})
            {
                var kind=source<8?(LandAnimalKind)(source%3):source==8?LandAnimalKind.Gull:LandAnimalKind.SeaTurtle;
                int id=career.CaptureAnimalWithNet(source,kind);Require(id>0 && career.HouseAnimal(id),"resident animal");
            }
            bag.Select(0);
            // A few fish remain in the hold for trading, cooking, and moving into the tank.
            for(int i=0;i<8;i++)
            {
                var kind=new[]{FishKind.Coral,FishKind.Dart,FishKind.Puffer,FishKind.Mackerel}[i%4];int grams=FishingSpecies.Grams(kind,.7f);
                var fish=new CatchRecord{id="classroom-fish-"+i,kind=kind,grams=grams,value=FishingSpecies.Value(kind,false,grams)};
                Require(career.Store(fish),"fish hold");if(i<4)Require(career.DisplayFish(fish.id),"display fish");
            }
            foreach(var item in new[]{FieldItem.Shell,FieldItem.Driftwood,FieldItem.Stone,FieldItem.Fiber,FieldItem.Clay,
                FieldItem.IronOre,FieldItem.Coal,FieldItem.Sand,FieldItem.Plank,FieldItem.Stick,FieldItem.Rope,
                FieldItem.Charcoal,FieldItem.IronIngot,FieldItem.Brick,FieldItem.Glass,FieldItem.RabbitFur,
                FieldItem.FoxFur,FieldItem.TurtleShell,FieldItem.GullFeather,FieldItem.GunParts,FieldItem.IronPickHead,
                FieldItem.ReefChitin,FieldItem.GuardianPearl,FieldItem.GuardianTrophy})Add(career.HomeStorage,item,Math.Min(32,FishingBackpack.Limit(item)));
            // Seed mature crops through the same persisted plot schema, then validate the complete save.
            data=career.Snapshot();data.farm.starterClaimed=true;
            for(int i=0;i<FishingFarm.PlotCount;i++)data.farm.plots[i]=new FarmPlotData{tilled=true,planted=true,growth=i<6?3:0};
            Require(FishingCareer.TryRestore(data,out career),"complete classroom save");
            for(int island=0;island<4;island++)Require(career.Lore.ResetRealm(island),"replayable classroom realm");
            return career;
        }
        private static void Add(FishingBackpack bag,FieldItem item,int count)=>Require(bag.TryAdd(item,count),FishingBackpack.Name(item));
        private static void Require(bool ok,string item){if(!ok)throw new InvalidOperationException("Classroom save: "+item);}
    }
}
