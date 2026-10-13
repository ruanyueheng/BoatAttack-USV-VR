using System;
using System.Collections.Generic;
using BoatAttack.Fishing;

internal static class V023Tests
{
    internal static void Run(Action<bool,string> check)
    {
        var bits=new HashSet<int>();
        for(int i=0;i<FishingSpecies.Count;i++)
        {
            var kind=(FishKind)i;
            check(bits.Add(FishingSpecies.DiscoveryBit(kind,false)),"Unique discovery bit "+kind);
            if(kind!=FishKind.Sailfish)check(bits.Add(FishingSpecies.DiscoveryBit(kind,true)),"Unique pearl bit "+kind);
        }
        check(bits.Count==39 && FishingSpecies.DiscoveryMaskLimit==(1L<<39)-1,"20 species use 39 distinct positive long bits");
        var career=new FishingCareer();
        for(int i=12;i<FishingSpecies.Count;i++)
        {
            var kind=(FishKind)i;int grams=FishingSpecies.Grams(kind,.5f);
            foreach(bool rare in new[]{false,true})
            {
                var fish=new CatchRecord{id="v23-"+kind+rare,kind=kind,grams=grams,rare=rare,value=FishingSpecies.Value(kind,rare,grams)};
                check(career.BeginPending(fish,0,0,0) && career.ResolvePending(false,out _,out _),"New species uses existing settlement "+kind+rare);
                check(career.Discovered(kind,rare) && career.BestGrams(kind)==grams,"New species discovery and weight recorded "+kind+rare);
            }
        }
        career.FinishPrologue();var data=career.Snapshot();
        check(data.discoveryMask>int.MaxValue && FishingCareer.TryRestore(data,out var restored)
            && restored.DiscoveryMask==career.DiscoveryMask && restored.PrologueSeen,"High discovery bits and finished prologue survive restore");
        data.discoveryMask|=1L<<39;check(!FishingCareer.TryRestore(data,out _),"Reject unknown discovery bits");
        data=career.Snapshot();data.version=11;data.prologueSeen=false;check(!FishingCareer.TryRestore(data,out _),"Older schema rejects new discoveries");
        data=new FishingCareer().Snapshot();data.version=11;data.bestGrams.RemoveRange(12,data.bestGrams.Count-12);
        data.coins=731;data.rodLevel=1;data.discoveryMask=1;data.catches=1;data.bestGrams[0]=900;
        check(FishingCareer.TryRestore(data,out restored) && restored.Coins==731 && restored.LineLevel==1
            && restored.Discovered(FishKind.Coral) && restored.BestGrams(FishKind.Coral)==900 && !restored.PrologueSeen
            && restored.Snapshot().bestGrams.Count==20,"Actual twelve-entry v11 record extends without losing progress");
        var day=new FishingCalendar();var night=new FishingCalendar();night.AdvanceToHour(21);
        check(FishingSpecies.InHabitat(FishKind.SeaTurtle,0,day,BayWeather.Clear) && !FishingSpecies.InHabitat(FishKind.SeaTurtle,0,night,BayWeather.Clear),"Turtle daytime habitat");
        check(FishingSpecies.InHabitat(FishKind.Octopus,2,night,BayWeather.Clear) && !FishingSpecies.InHabitat(FishKind.Octopus,1,night,BayWeather.Clear),"Octopus night reef habitat");
        check(FishingSpecies.InHabitat(FishKind.Jellyfish,1,night,BayWeather.Clear) && !FishingSpecies.InHabitat(FishKind.Jellyfish,1,day,BayWeather.Clear),"Jellyfish night habitat");
        var reachable=new HashSet<FishKind>();
        foreach(int hour in new[]{8,15,21})foreach(int area in new[]{0,1,2})foreach(BayWeather weather in Enum.GetValues(typeof(BayWeather)))
        {
            var calendar=new FishingCalendar();calendar.AdvanceToHour(hour);
            for(int n=0;n<=600;n++)reachable.Add(FishingSpecies.Select(area,calendar,weather,.4f+n/1000f));
        }
        for(int i=12;i<FishingSpecies.Count;i++)check(reachable.Contains((FishKind)i),"Real selection can encounter "+(FishKind)i);
    }
}
