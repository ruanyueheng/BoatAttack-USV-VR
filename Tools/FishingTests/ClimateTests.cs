using System;
using BoatAttack.Fishing;
internal static class ClimateTests
{
    internal static void Run(Action<bool,string> check)
    {
        var climate=new BayClimate();
        check(climate.Current==BayWeather.Clear && climate.Forecast==BayWeather.Cloudy && climate.Blend==1,"Climate clear startup and forecast");
        float age=climate.Age;climate.Tick(1,true);climate.Tick(float.NaN,false);climate.Tick(float.PositiveInfinity,false);climate.Tick(-1,false);
        check(climate.Age==age,"Climate pause/nonfinite/negative input does not advance");
        climate.Tick(900,false);check(climate.Age-age<=.251f,"Climate hitch cannot skip weather windows");
        check(!climate.Select((BayWeather)8) && climate.Current==BayWeather.Clear,"Climate unknown preset rejected");
        check(climate.Select(BayWeather.Rain) && climate.Previous==BayWeather.Clear && climate.Blend==0 && !climate.Automatic,"Climate preview starts a continuous transition");
        check(!climate.Select(BayWeather.Sunset) && climate.Current==BayWeather.Rain,"Climate transition cannot be interrupted by spam");
        for(int i=0;i<48;i++)climate.Tick(.25f,false);
        check(climate.Blend==1,"Climate completes twelve second blend");
        climate.Hold();for(int i=0;i<900;i++)climate.Tick(.25f,false);
        check(climate.Current==BayWeather.Rain && climate.Remaining==0,"Climate fixed preset stays fixed");
        climate.ResumeCycle();climate.Tick(.1f,false);
        check(climate.Current==BayWeather.Mist && climate.Previous==BayWeather.Rain,"Climate resumes forecast after fixed preset");
        int visited=0;
        for(int i=0;i<8000;i++){climate.Tick(.25f,false);visited|=1<<(int)climate.Current;}
        check(visited==31,"Climate cycle visits all five presets");
        foreach(BayWeather weather in Enum.GetValues(typeof(BayWeather)))
        {
            check(BayClimate.Duration(weather)>BayClimate.TransitionSeconds*4,"Climate sufficiently slow hold "+weather);
            check(BayClimate.BiteScale(weather,BayClimate.Favored(weather))==.75f && BayClimate.RareBonus(weather,BayClimate.Favored(weather))==.04f,"Climate favored-species benefit "+weather);
            foreach(FishKind kind in Enum.GetValues(typeof(FishKind)))
            {
                var s=new CatchSession();s.Cast(kind,false,0,"weather-"+weather+kind,BaitKind.Pearl,weather);
                check(s.WeatherAtCast==weather,"Cast snapshots climate "+weather+kind);
                for(int i=0;i<300 && s.Phase==CatchPhase.Waiting;i++)s.Tick(.02f,false);
                s.Hook();for(int i=0;i<5000 && s.Phase==CatchPhase.Fighting;i++)s.Tick(.02f,s.Tension<.6f);
                check(s.Phase==CatchPhase.Landing && s.Land(),"Every climate/species remains catchable "+weather+kind);
            }
        }
        check(!new CatchSession().Cast(FishKind.Coral,false,0,"bad",BaitKind.Balanced,(BayWeather)90),"Invalid climate cannot enter session");
        check(BayClimate.Activity(MarineKind.School,BayWeather.Rain)>BayClimate.Activity(MarineKind.School,BayWeather.Clear),"Rain raises schooling activity");
        check(BayClimate.Activity(MarineKind.Dolphin,BayWeather.Sunset)>BayClimate.Activity(MarineKind.Dolphin,BayWeather.Rain),"Dolphins more active in sunset than rain");
        check(BayClimate.CanObserve(28,.82f,true),"Wildlife observation boundary accepted");
        check(!BayClimate.CanObserve(28.01f,1,true) && !BayClimate.CanObserve(12,.81f,true),"Observation requires proximity and facing");
        check(!BayClimate.CanObserve(4,1,false) && !BayClimate.CanObserve(float.NaN,1,true),"Observation rejects occlusion and invalid range");
        var c=new FishingCareer();
        foreach(MarineKind kind in Enum.GetValues(typeof(MarineKind)))
        {
            check(c.Observe(kind) && c.Seen(kind) && !c.Observe(kind),"Wildlife record exactly once "+kind);
        }
        check(c.Coins==0 && c.Count==0 && c.Catches==0 && c.WildlifeMask==31,"Observing cannot farm money or fishing progress");
        check(!c.Observe((MarineKind)99) && !c.Seen((MarineKind)99),"Invalid wildlife rejected");
        check(FishingCareer.TryRestore(c.Snapshot(),out var restored) && restored.WildlifeMask==31,"Wildlife save roundtrip");
        var oldV2=new FishingSaveData{version=2,coins=30};
        check(FishingCareer.TryRestore(oldV2,out restored) && restored.WildlifeMask==0 && restored.Coins==30,"Earlier v2 save remains compatible");
        var malformed=c.Snapshot();malformed.wildlifeMask=32;
        check(!FishingCareer.TryRestore(malformed,out _),"Unknown wildlife bits rejected");
        malformed=c.Snapshot();malformed.wildlifeMask=-1;
        check(!FishingCareer.TryRestore(malformed,out _),"Negative wildlife mask rejected");
    }
}
