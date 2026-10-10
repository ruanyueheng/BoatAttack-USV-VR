using System;
using System.Collections.Generic;
using BoatAttack.Fishing;
internal static class LivingCoastTests
{
    public static void Run(Action<bool,string> check)
    {
        check(FishingCalendar.TryRestore(new CalendarData{day=27,minute=1439.9},out var clock),"Calendar accepts season boundary");
        clock.Tick(.25f,false);
        check(clock.Day==28 && clock.Season==1 && clock.Date==1 && clock.Minute<1,"Midnight advances season once");
        var saved=clock.Minute;clock.Tick(100,true);clock.Tick(float.NaN,false);clock.Tick(-1,false);
        check(clock.Minute==saved,"Paused and invalid clock ticks are inert");
        FishingCalendar.TryRestore(new CalendarData{day=111,minute=1439.9},out clock);clock.Tick(.25f,false);
        check(clock.Year==2 && clock.Season==0 && clock.Weekday==0,"Calendar year and weekday rollover");
        FishingCalendar.TryRestore(new CalendarData{minute=1320},out clock);
        check(clock.Night && clock.Daylight<.001f,"Night really has a dark environment factor");
        clock.RestUntilMorning();check(clock.Day==1 && clock.Hour==6,"Sleep crosses midnight to 06:00");
        check(!FishingCalendar.TryRestore(new CalendarData{minute=double.NaN},out _) && !FishingCalendar.TryRestore(new CalendarData{day=-1},out _),"Bad calendar save rejected");
        var bits=new HashSet<int>();
        for(int i=0;i<FishingSpecies.Count;i++)
        {
            var kind=(FishKind)i;check(bits.Add(FishingSpecies.DiscoveryBit(kind,false)),"Unique normal species bit "+kind);
            if(kind!=FishKind.Sailfish)check(bits.Add(FishingSpecies.DiscoveryBit(kind,true)),"Unique pearl species bit "+kind);
            check(FishingSpecies.LengthMeters(kind)*FishingSpecies.SizeScale(kind,FishingSpecies.Of(kind).MaxGrams)<1.21f,"Metre-scale catch bound "+kind);
        }
        check(FishingSpecies.DiscoveryBit(FishKind.Dart,true)==4 && FishingSpecies.DiscoveryBit(FishKind.Sailfish,false)==6,"v8 discovery bits preserved");
        FishingCalendar.TryRestore(new CalendarData{minute=1320},out clock);
        check(FishingSpecies.InHabitat(FishKind.Lanternfish,0,clock,BayWeather.Clear) && !FishingSpecies.InHabitat(FishKind.Sardine,0,clock,BayWeather.Clear),"Night-only fish and daytime shoals");
        for(int i=0;i<=100;i++)check(FishingSpecies.InHabitat(FishingSpecies.Select(0,clock,BayWeather.Clear,i/100f),0,clock,BayWeather.Clear),"Selection stays in habitat "+i);
        foreach(CatchFailure failure in Enum.GetValues(typeof(CatchFailure)))
            check(!FishingText.Failure(failure,true).Contains("鼠标") && !FishingText.Failure(failure,true).Contains("按 E"),"Controller failure hints have no desktop bindings "+failure);
        var gesture=new FishingCastGesture();
        check(!gesture.Step(true,true,2,.02f),"Held trigger cannot cast after tracking recovery");
        gesture.Step(true,false,0,.02f);
        for(int i=0;i<5;i++)gesture.Step(true,true,1.5f,.02f);
        check(gesture.Step(true,false,.2f,.02f) && !gesture.Step(true,false,2,.02f),"Forward swing casts once on release");
        for(int i=0;i<5;i++)gesture.Step(true,true,.4f,.02f);
        check(!gesture.Step(true,false,0,.02f),"Slow movements do not cast accidentally");
        gesture.Step(true,true,2,.02f);gesture.Step(false,true,0,.02f);
        check(!gesture.Step(true,false,0,.02f),"Tracking loss cancels an armed cast");
        var career=new FishingCareer();var bag=career.Backpack;
        bag.TryAdd(FieldItem.HuntingSpear,1);bag.Select(1);
        check(career.Hunting.Harvest(bag,0,LandAnimalKind.Rabbit,0) && bag.Count(FieldItem.RawMeat)==2,"Successful hunt commits meat and return date together");
        check(!career.Hunting.Harvest(bag,0,LandAnimalKind.Rabbit,0) && !career.Hunting.Harvest(bag,1,LandAnimalKind.Tortoise,0),"No repeat hunt or protected animal reward");
        check(FishingCareer.TryRestore(career.Snapshot(),out var restored) && !restored.Hunting.Available(0,0) && restored.Hunting.Available(0,1),"Hunting dates survive reload");
        var old=career.Snapshot();old.version=8;old.calendar=null;old.hunting=null;
        check(FishingCareer.TryRestore(old,out restored) && restored.Calendar.Hour==8 && restored.Coins==career.Coins,"v8 save migrates without losing progress");
        var full=new FishingBackpack(false);full.TryAdd(FieldItem.HuntingSpear,1);
        for(int i=1;i<FishingBackpack.SlotCount;i++)full.TryAdd(FieldItem.Stone,64);
        var hunting=new FishingHunting();check(!hunting.Harvest(full,1,LandAnimalKind.Fox,0) && hunting.Available(1,0),"Full bag leaves animal available");
        bag.TryAdd(FieldItem.Coal,1);int furnace=career.Workshop.Place(bag,CraftStation.Furnace,1,1,1,0,true);
        check(career.Workshop.Start(bag,furnace,"roast-meat",2,FieldItem.Coal),"Furnace consumes raw meat plus fuel");
        for(int i=0;i<16;i++)career.Workshop.Tick(1);
        check(career.Workshop.Collect(bag,furnace) && bag.Count(FieldItem.CookedMeat)==2 && !career.Workshop.Collect(bag,furnace),"Cooking completion is collected exactly once");
    }
}
