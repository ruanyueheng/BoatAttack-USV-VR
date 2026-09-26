using System;
using BoatAttack.Fishing;
internal static class Program
{
    private static int count;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
    static CatchSession Bite(FishKind kind, int level = 0)
    {
        var s = new CatchSession(); s.Cast(kind, false, level, Guid.NewGuid().ToString("N"));
        for (int i = 0; i < 300 && s.Phase == CatchPhase.Waiting; i++) s.Tick(0.02f, false);
        return s;
    }
    public static int Main()
    {
        try
        {
            foreach (FishKind kind in Enum.GetValues(typeof(FishKind)))
            {
                var s = Bite(kind); Check(s.Phase == CatchPhase.Bite, "Bite window " + kind);
                Check(s.Hook(), "Hook " + kind);
                for (int i = 0; i < 3000 && s.Phase == CatchPhase.Fighting; i++) s.Tick(0.02f, s.Tension < 0.6f);
                Check(s.Phase == CatchPhase.Landing, "Balanced reeling reaches landing " + kind);
                Check(s.CanLand && s.Land(), "Timed landing " + kind);
                Check(s.Claim() != null && s.Claim() == null, "Catch claim exactly once " + kind);
            }
            var missed = Bite(FishKind.Coral); for(int i=0;i<150;i++) missed.Tick(0.02f,false);
            Check(missed.Phase == CatchPhase.Escaped, "Missed bite escapes");
            var snapped=Bite(FishKind.Puffer); snapped.Hook(); for(int i=0;i<500;i++)snapped.Tick(0.02f,true);
            Check(snapped.Phase==CatchPhase.Escaped,"Continuous reeling breaks line");
            var slack=Bite(FishKind.Coral);slack.Hook();for(int i=0;i<500;i++)slack.Tick(0.02f,false);
            Check(slack.Phase==CatchPhase.Escaped,"Slack line loses fish");
            var invalid=new CatchSession();Check(!invalid.Hook() && !invalid.Land() && invalid.Claim()==null,"Commands reject invalid phases");
            Check(!invalid.Cast((FishKind)99,false,0,"id"),"Invalid species rejected");
            Check(invalid.Cast(FishKind.Coral,false,0,"id") && !invalid.Cast(FishKind.Coral,false,0,"other"),"Cast cannot overlap");
            invalid.Tick(float.NaN,true);invalid.Tick(float.PositiveInfinity,true);Check(invalid.Phase==CatchPhase.Waiting,"Nonfinite delta ignored");
            invalid.Cancel();Check(invalid.Phase==CatchPhase.Idle,"Cancel returns to idle");
            var weak=Bite(FishKind.Puffer);var strong=Bite(FishKind.Puffer,1);weak.Hook();strong.Hook();weak.Tick(0.1f,true);strong.Tick(0.1f,true);
            Check(strong.Tension<weak.Tension,"Upgrade reduces tension growth");
            var c=new FishingCareer();var record=new CatchRecord{id="fish-1",kind=FishKind.Coral,rare=true,value=30};
            Check(c.Store(record),"Store valid catch");Check(!c.Store(record),"Duplicate catch rejected");record.value=999;
            Check(c.HoldValue==30,"Stored data copied");Check(c.Sell()==30 && c.Coins==30 && c.Count==0,"Sell updates coins and empties hold");
            Check(c.Sell()==0 && c.Coins==30,"Sell cannot pay twice");Check(c.Upgrade() && c.RodLevel==1 && c.Coins==0,"Upgrade spends exact cost");
            Check(!c.Upgrade(),"Upgrade cannot repeat");
            Check(FishingCareer.TryRestore(c.Snapshot(),out var loaded) && loaded.RodLevel==1,"Progress restores");
            record.value=30;Check(!loaded.Store(record),"Sold ID remains deduplicated after restore");
            Check(!FishingCareer.TryRestore(new FishingSaveData{version=2},out _),"Unknown save version rejected");
            Check(!FishingCareer.TryRestore(new FishingSaveData{coins=-1},out _),"Negative balance rejected");
            for(int i=0;i<FishingCareer.Capacity;i++)Check(c.Store(new CatchRecord{id="extra"+i,kind=FishKind.Coral,value=10}),"Fill inventory "+i);
            Check(!c.Store(new CatchRecord{id="overflow",kind=FishKind.Coral,value=10}),"Hold capacity enforced");
            Check(FishingCareer.TryRestore(c.Snapshot(),out loaded) && loaded.Count==6 && loaded.HoldValue==60,"Inventory round trip");
            Check(!new CatchSession().Cast(FishKind.Coral,false,0,new string('x',65)),"Oversized catch ID rejected at cast");
            Check(!new FishingCareer().Upgrade(),"Upgrade without coins rejected");
            var malformed=new FishingCareer();Check(!malformed.Store(new CatchRecord{id="bad",kind=FishKind.Coral,value=999}),"Forged sale value rejected");
            var duplicateSave=new FishingSaveData();duplicateSave.inventory.Add(new CatchRecord{id="duplicate",kind=FishKind.Coral,value=10});duplicateSave.inventory.Add(new CatchRecord{id="duplicate",kind=FishKind.Coral,value=10});
            Check(!FishingCareer.TryRestore(duplicateSave,out _),"Duplicate inventory in save rejected");
            var badLanding=Bite(FishKind.Coral);badLanding.Hook();
            for(int i=0;i<3000 && badLanding.Phase==CatchPhase.Fighting;i++)badLanding.Tick(0.02f,badLanding.Tension<0.6f);
            for(int i=0;i<20;i++)badLanding.Tick(0.02f,false);
            Check(!badLanding.CanLand && !badLanding.Land(),"Landing outside target misses");
            badLanding.Land();badLanding.Land();Check(badLanding.Phase==CatchPhase.Escaped,"Three misses lose the fish");
            var noLanding=Bite(FishKind.Coral);noLanding.Hook();
            for(int i=0;i<3000 && noLanding.Phase==CatchPhase.Fighting;i++)noLanding.Tick(0.02f,noLanding.Tension<0.6f);
            for(int i=0;i<600;i++)noLanding.Tick(0.02f,false);
            Check(noLanding.Phase==CatchPhase.Escaped,"Landing deadline enforced");
            Console.WriteLine("TOTAL: "+count+" passed. Pure fishing rules only.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
