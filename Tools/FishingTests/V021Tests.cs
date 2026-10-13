using System;
using BoatAttack.Fishing;
using BoatAttack.VR;

internal static class V021Tests
{
    public static void Run(Action<bool,string> check)
    {
        float smallTime=0, legendTime=0;
        for(int species=0;species<FishingSpecies.Count;species++)
        for(int level=0;level<3;level++)
        for(int seed=0;seed<8;seed++)
        {
            var s=Hooked((FishKind)species,level,"fight-"+seed,(BaitKind)(seed%3),seed/7f);
            for(int frame=0;frame<6500 && s.Phase==CatchPhase.Fighting;frame++)
                s.Tick(.02f,s.Tension<.55f && s.Beat!=FishBeat.Leap);
            check(s.Phase==CatchPhase.Landing,"V21: readable strategy lands species/line/seed "+species+"/"+level+"/"+seed);
            if(s.Species.Legendary)check(s.FightTime>=20f,"V21: legend cannot skip its 20-second fight");
            if(species==(int)FishKind.Sardine && level==0 && seed==0)smallTime=s.FightTime;
            if(species==(int)FishKind.Sailfish && level==0 && seed==0)legendTime=s.FightTime;
            s.Tick(.1f,false);
            check(s.CanNet && s.LandFromNet() && s.Claim()!=null && s.Claim()==null,"V21: physical net stages one claim after a real fight");
        }
        check(smallTime<8 && legendTime>smallTime*2,"V21: small fish resolve quickly, legends sustain a duel");
        var first=Hooked(FishKind.Puffer,0,"repeat");var repeat=Hooked(FishKind.Puffer,0,"repeat");var other=Hooked(FishKind.Puffer,0,"different");
        bool different=false;
        for(int i=0;i<120;i++)
        {
            first.Tick(.02f,first.Tension<.55f);repeat.Tick(.02f,repeat.Tension<.55f);other.Tick(.02f,other.Tension<.55f);
            different|=Math.Abs(first.Tension-other.Tension)>.0001f;
        }
        check(first.Tension==repeat.Tension && first.Distance==repeat.Distance && different,"V21: replayable catch seed varies actual tension between casts");
        var hard=Hooked(FishKind.Sailfish,2,"hard");
        for(int i=0;i<2000 && hard.Phase==CatchPhase.Fighting;i++)hard.Tick(.02f,true);
        check(hard.Phase==CatchPhase.Escaped && (hard.Failure==CatchFailure.LineSnapped || hard.Failure==CatchFailure.LeapSnapped),"V21: upgraded line cannot brute force a legend");
        var net=Landing();float remaining=net.Remaining;
        net.Tick(float.NaN,false);net.Tick(float.PositiveInfinity,false);net.Tick(0,false);
        check(net.Remaining==remaining,"V21: invalid or paused ticks do not consume net time");
        for(int i=0;i<50;i++)net.Tick(.1f,false);
        check(net.CanNet && net.Remaining<remaining && net.Tension>=.2f,"V21: automatic drag supports changing hands, without freezing time");
        for(int i=0;i<60;i++)net.Tick(.1f,false);
        check(net.Failure==CatchFailure.LandingTimeout && !net.LandFromNet() && net.Claim()==null,"V21: delayed net loses fish without a reward");
        net=Landing();for(int i=0;i<90;i++)net.Tick(.1f,true);
        check(net.Failure==CatchFailure.LineSnapped,"V21: continued hard reeling at the boat snaps the line");
        Boost(check);Survey(check);Upgrades(check);Habitats(check);
    }

    private static CatchSession Hooked(FishKind kind,int level,string id,BaitKind bait=BaitKind.Balanced,float size=.5f)
    {
        var s=new CatchSession();if(!s.Cast(kind,false,level,id,bait,BayWeather.Clear,size,level))throw new Exception("Invalid fixture");
        for(int i=0;i<100 && s.Phase==CatchPhase.Waiting;i++)s.Tick(.1f,false);
        if(!s.Hook())throw new Exception("Fixture missed bite");return s;
    }
    private static CatchSession Landing()
    {
        var s=Hooked(FishKind.Coral,0,"net");
        for(int i=0;i<4000 && s.Phase==CatchPhase.Fighting;i++)s.Tick(.02f,s.Tension<.55f);
        if(s.Phase!=CatchPhase.Landing)throw new Exception("Fixture failed fight");return s;
    }
    private static void Boost(Action<bool,string> check)
    {
        var b=new USVBoost();for(int i=0;i<60;i++)b.Tick(.1f,true,false);
        check(b.Active && b.Heat>.7f && !b.Overheated,"V21: sustained forward boost generates heat");
        float heat=b.Heat;b.Tick(.1f,true,true);
        check(!b.Active && b.Heat==heat,"V21: modal/paused boost neither thrusts nor cools");
        b.Tick(float.NaN,true,false);b.Tick(float.PositiveInfinity,true,false);
        check(b.Heat==heat && !b.Active,"V21: nonfinite thermal time rejected");
        for(int i=0;i<25;i++)b.Tick(.1f,true,false);
        check(b.Overheated && !b.Active,"V21: overheating forces cruise even with boost held");
        for(int i=0;i<60;i++)b.Tick(.1f,true,false);
        check(b.Overheated && !b.Active,"V21: heat lock prevents rapid on/off boost");
        for(int i=0;i<40;i++)b.Tick(.1f,false,false);
        check(!b.Overheated && b.Heat<=.25f,"V21: enough cooling restores boost");
        b.Tick(.1f,true,false);check(b.Active,"V21: boost works again after cooling");
        b.Tick(.1f,false,false);check(!b.Active,"V21: releasing boost stops it immediately");
    }
    private static void Survey(Action<bool,string> check)
    {
        var c=new FishingCareer();check(FishingSurvey.Stage(c)==0 && !FishingGrounds.AvailableForCareer(3,c),"V21: new career has a reachable bay survey");
        foreach(var kind in new[]{FishKind.Coral,FishKind.Sardine,FishKind.SeaBream})
            check(c.Store(new CatchRecord{id="survey-"+kind,kind=kind,rare=true,value=FishingSpecies.Value(kind,true,0)}),"V21: pearl variant counts toward survey "+kind);
        c.SellAll();check(FishingSurvey.Stage(c)==1 && FishingGrounds.AvailableForCareer(3,c),"V21: bay collection opens north even after selling the fish");
        foreach(var kind in new[]{FishKind.Dart,FishKind.Mackerel,FishKind.Lanternfish})
            c.Store(new CatchRecord{id="survey-"+kind,kind=kind,value=FishingSpecies.Value(kind,false,0)});
        check(FishingSurvey.LegendCoordinates(c),"V21: channel collection finishes the unlock chain");
        check(FishingCareer.TryRestore(c.Snapshot(),out var loaded) && FishingSurvey.LegendCoordinates(loaded),"V21: survey rewards survive existing v11 save reload");
        check(!FishingGrounds.AvailableForCareer(-1,c) && !FishingGrounds.AvailableForCareer(4,c) && !FishingGrounds.AvailableForCareer(3,null),"V21: invalid survey destinations stay closed");
    }
    private static void Upgrades(Action<bool,string> check)
    {
        foreach(var chosen in new[]{UpgradeTrack.Hold,UpgradeTrack.Engine,UpgradeTrack.Rudder})
        {
            var data=new FishingCareer().Snapshot();data.coins=5000;FishingCareer.TryRestore(data,out var c);
            check(c.Upgrade(chosen) && c.Upgrade(chosen),"V21: can choose tier-two specialization "+chosen);
            foreach(var track in new[]{UpgradeTrack.Hold,UpgradeTrack.Engine,UpgradeTrack.Rudder})if(track!=chosen)
            {
                check(c.Upgrade(track),"V21: other tracks still allow first tier "+track);
                int coins=c.Coins;check(c.SpecializationBlocked(track) && !c.Upgrade(track) && c.Coins==coins,"V21: conflicting specialization cannot spend coins "+track);
            }
            check(c.Upgrade(UpgradeTrack.Line) && c.Upgrade(UpgradeTrack.Line) && c.Upgrade(UpgradeTrack.Cabin),"V21: fishing gear and cosmetic deck stay independent");
        }
        var old=new FishingCareer().Snapshot();old.holdLevel=old.engineLevel=old.rudderLevel=2;
        check(FishingCareer.TryRestore(old,out var legacy) && legacy.Capacity==10 && legacy.EngineLevel==2 && legacy.RudderLevel==2,"V21: prior fully upgraded boats retain every paid level");
    }
    private static void Habitats(Action<bool,string> check)
    {
        var calendar=new FishingCalendar();
        check(!FishingSpecies.InHabitat(FishKind.Mackerel,1,calendar,BayWeather.Clear) && FishingSpecies.InHabitat(FishKind.Mackerel,1,calendar,BayWeather.Rain),"V21: rain-only target cannot bite under clear skies");
        calendar.AdvanceToHour(12);
        check(calendar.RisingTide && FishingSpecies.InHabitat(FishKind.Grouper,2,calendar,BayWeather.Mist),"V21: rising tide and mist bring spring grouper");
        calendar.AdvanceToHour(18);
        check(!calendar.RisingTide && !FishingSpecies.InHabitat(FishKind.Grouper,2,calendar,BayWeather.Mist),"V21: falling tide removes grouper from the actual selection pool");
        bool valid=true;
        for(int area=0;area<4;area++)for(int hour=0;hour<24;hour++)for(int weather=0;weather<5;weather++)
        {
            calendar.AdvanceToHour(hour);
            for(int roll=0;roll<=100;roll++)
                valid &= FishingSpecies.InHabitat(FishingSpecies.Select(area,calendar,(BayWeather)weather,roll/100f),area,calendar,(BayWeather)weather);
        }
        check(valid,"V21: 48,480 weather/time/area rolls always return an eligible species");
    }
}
