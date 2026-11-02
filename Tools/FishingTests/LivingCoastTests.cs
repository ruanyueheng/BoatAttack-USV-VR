using System;
using System.Collections.Generic;
using BoatAttack.Fishing;
internal static class LivingCoastTests
{
    public static void Run(Action<bool,string> check)
    {
        for(int version=9;version<=19;version++)
        {
            var legacySave=new FishingCareer().Snapshot();Program.Legacy(legacySave,version);legacySave.coins=87;
            legacySave.hunting.availableDays=new List<int>(new int[8]);legacySave.hunting.availableMinutes=new List<double>(new double[8]);
            legacySave.hunting.availableDays[1]=1;legacySave.hunting.availableMinutes[1]=legacySave.calendar.minute+200;legacySave.hunting.harvests=3;
            check(FishingCareer.TryRestore(legacySave,out var loaded),"Eight-entry wildlife saves remain readable: schema "+version);
            if(loaded!=null)
            {
                check(loaded.Coins==87 && loaded.Hunting.Harvests==3 && !loaded.Hunting.Available(1,legacySave.calendar.minute+199.99)
                    && loaded.Hunting.Available(1,legacySave.calendar.minute+200),"Legacy migration preserves coins, harvests and exact cooldown: "+version);
                check(legacySave.hunting.availableDays.Count==8 && legacySave.hunting.availableMinutes.Count==8
                    && FishingCareer.TryRestore(loaded.Snapshot(),out _),"Legacy migration leaves its source intact and can resave: "+version);
            }
        }
        foreach(int count in new[]{0,7,9,15,17})
        {
            var invalidHunting=new HuntingData{availableDays=new List<int>(new int[count]),availableMinutes=new List<double>(new double[count])};
            check(!FishingHunting.TryRestore(invalidHunting,out _),"Unknown wildlife array lengths still fail: "+count);
        }
        var mismatched=new HuntingData{availableDays=new List<int>(new int[8]),availableMinutes=new List<double>(new double[16])};
        check(!FishingHunting.TryRestore(mismatched,out _),"Migration rejects mismatched wildlife day and minute arrays");
        mismatched.availableMinutes=new List<double>(new double[8]);mismatched.availableMinutes[2]=double.NaN;
        check(!FishingHunting.TryRestore(mismatched,out _),"Legacy wildlife migration rejects nonfinite cooldowns");
        var manual=new FishingCalendar();
        manual.ToggleAutomatic();double held=manual.TotalMinutes;manual.Tick(.25f,false);
        check(manual.TotalMinutes==held && !manual.Automatic,"Manual clock holds during active play");
        check(manual.AdvanceToHour(21) && manual.Night && manual.Day==0,"Manual night updates the real calendar");
        double night=manual.TotalMinutes;
        check(manual.AdvanceToHour(8) && !manual.Night && manual.Day==1 && manual.TotalMinutes>night,"Manual dawn advances monotonically across midnight");
        check(FishingCalendar.TryRestore(manual.Snapshot(),out var heldClock) && !heldClock.Automatic,"Manual clock preference survives save reload");
        heldClock.ToggleAutomatic();held=heldClock.TotalMinutes;heldClock.Tick(.25f,false);
        check(heldClock.TotalMinutes>held,"Automatic time resumes from chosen hour");
        held=heldClock.TotalMinutes;
        check(!heldClock.AdvanceToHour(float.NaN) && !heldClock.AdvanceToHour(float.PositiveInfinity) && !heldClock.AdvanceToHour(-1) && !heldClock.AdvanceToHour(24) && heldClock.TotalMinutes==held,"Invalid manual hours cannot mutate time");
        FishingCalendar.TryRestore(new CalendarData{day=FishingCalendar.MaxDay,minute=1320},out var limit);
        check(!limit.AdvanceToHour(8) && limit.Minute==1320,"Manual clock rejects overflow at maximum day");
        check(!new CalendarData().timeFixed,"Old calendar saves default to automatic time");
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
        clock=new FishingCalendar();double start=clock.TotalMinutes;
        for(int i=0;i<3600;i++)clock.Tick(.25f,false);
        check(clock.Day==1 && Math.Abs(clock.TotalMinutes-start-1440)<.0001,"One full day takes exactly 15 active real minutes");
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
        float gentlePower=gesture.Power;
        for(int i=0;i<5;i++)gesture.Step(true,true,3.5f,.02f);
        check(gesture.Step(true,false,0,.02f) && gesture.Power>gentlePower && gesture.Power<=1,"A stronger controller swing increases bounded cast power");
        for(int i=0;i<5;i++)gesture.Step(true,true,.4f,.02f);
        check(gesture.Step(true,false,0,.02f),"A deliberate trigger press-release casts without a fast wrist swing");
        float shortPower=gesture.Power;
        for(int i=0;i<80;i++)check(!gesture.Step(true,true,0,.02f),"Holding trigger charges without auto-casting "+i);
        check(gesture.Step(true,false,0,.02f) && gesture.Power>shortPower && gesture.Power<=1,"A stationary long trigger charge casts farther on release");
        gesture.Step(true,true,0,.02f);gesture.Step(true,true,40,.02f);
        check(gesture.Step(true,false,0,.02f) && gesture.Power<.5f,"A tracking speed jump cannot add swing power or swallow the trigger cast");
        gesture.Step(true,true,0,.02f);gesture.Step(true,true,0,float.NaN);
        check(!gesture.Step(true,false,0,.02f),"Invalid sample time cancels a prepared cast");
        gesture.Step(true,true,2,.02f);gesture.Step(false,true,0,.02f);
        check(!gesture.Step(true,false,0,.02f),"Tracking loss cancels an armed cast");
        gesture.Step(true,false,0,.02f);gesture.Step(true,true,0,.02f);gesture.Reset();
        check(!gesture.Step(true,true,0,.02f) && !gesture.Step(true,false,0,.02f),"A menu or travel reset with trigger held never casts on release");
        var career=new FishingCareer();var bag=career.Backpack;
        bag.TryAdd(FieldItem.HuntingSpear,1);bag.Select(1);
        double now=career.Calendar.TotalMinutes;
        check(career.Hunting.Harvest(bag,0,LandAnimalKind.Rabbit,now) && bag.Count(FieldItem.RabbitMeat)==2,"Successful hunt commits meat and return time together");
        check(!career.Hunting.Harvest(bag,0,LandAnimalKind.Rabbit,now) && !career.Hunting.Harvest(bag,1,LandAnimalKind.Tortoise,now),"No repeat hunt or protected animal reward");
        check(FishingCareer.TryRestore(career.Snapshot(),out var restored) && !restored.Hunting.Available(0,now+479.99) && restored.Hunting.Available(0,now+480),"Five-minute recovery survives reload and has an exact boundary");
        double pausedTime=restored.Calendar.TotalMinutes;restored.Calendar.Tick(300,true);
        check(restored.Calendar.TotalMinutes==pausedTime && !restored.Hunting.Available(0,restored.Calendar.TotalMinutes),"Pausing cannot advance animal recovery");
        var legacy=career.Snapshot();legacy.hunting.availableMinutes=null;
        check(FishingCareer.TryRestore(legacy,out restored) && !restored.Hunting.Available(0,now) && restored.Hunting.Available(0,now+480),"v14 return dates migrate to bounded recovery without free meat");
        var invalid=career.Snapshot();invalid.hunting.availableMinutes[0]=double.NaN;
        check(!FishingCareer.TryRestore(invalid,out _),"Invalid saved recovery is rejected");
        var midnight=new FishingHunting();midnight.Harvest(bag,2,LandAnimalKind.Fox,1400);
        check(!midnight.Available(2,1440) && midnight.Available(2,1880) && FishingHunting.TryRestore(midnight.Snapshot(),out _,1400),"Recovery spans midnight without premature respawn");
        var old=career.Snapshot();old.version=8;old.calendar=null;old.hunting=null;
        check(FishingCareer.TryRestore(old,out restored) && restored.Calendar.Hour==8 && restored.Coins==career.Coins,"v8 save migrates without losing progress");
        var full=new FishingBackpack(false);full.TryAdd(FieldItem.HuntingSpear,1);
        for(int i=1;i<FishingBackpack.SlotCount;i++)full.TryAdd(FieldItem.Stone,64);
        var hunting=new FishingHunting();check(!hunting.Harvest(full,1,LandAnimalKind.Fox,0) && hunting.Available(1,0),"Full bag leaves animal available");
        bag.TryAdd(FieldItem.Coal,1);int furnace=career.Workshop.Place(bag,CraftStation.Furnace,1,1,1,0,true);
        check(career.Workshop.Start(bag,furnace,"roast-rabbit",2,FieldItem.Coal),"Furnace consumes raw meat plus fuel");
        for(int i=0;i<16;i++)career.Workshop.Tick(1);
        check(career.Workshop.Collect(bag,furnace) && bag.Count(FieldItem.CookedMeat)==2 && !career.Workshop.Collect(bag,furnace),"Cooking completion is collected exactly once");
        var motion=new FishingCastMotion();motion.Prepare();
        for(int i=0;i<12;i++)motion.Tick(.1f);
        check(motion.Charge>.99f && motion.RodPitch<-70,"Held input winds the rod back");
        motion.Launch(motion.Release());check(motion.Casting && motion.Power>.99f && motion.FlightProgress==0,"Release starts full-power flight");
        motion.Tick(float.NaN);check(motion.FlightProgress==0,"Invalid motion time cannot advance flight");
        for(int i=0;i<13;i++)motion.Tick(.1f);
        check(!motion.Casting && motion.FlightProgress==1,"Bite clock may run only after landing");
        motion.Prepare();motion.CancelPreparation();check(!motion.Preparing,"Cancelled preparation does not launch");
        motion.Reset();motion.Prepare();motion.Launch(motion.Release());check(motion.Power<.4f,"A short tap keeps an accessible near cast");
    }
}
