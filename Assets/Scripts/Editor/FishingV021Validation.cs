using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV021Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V021");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[V021] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var driving=FishingV020Validation.Checks(c);while(driving.MoveNext())yield return null;
            c.ReturnToHarbor();
            foreach(var kind in new[]{FishKind.Coral,FishKind.Sardine,FishKind.SeaBream,FishKind.Dart,FishKind.Mackerel,FishKind.Lanternfish})
            {
                int grams=FishingSpecies.Grams(kind,.5f);
                Check(c.Career.Store(new CatchRecord{id="v21-survey-"+kind,kind=kind,grams=grams,value=FishingSpecies.Value(kind,false,grams)}),"Record survey specimen "+kind);
            }
            c.SellAll();
            Check(FishingSurvey.LegendCoordinates(c.Career) && FishingGrounds.AvailableForCareer(3,c.Career),"Sold collections unlock north and legend coordinates");
            c.DockConsole.PreviewForValidation();
            Check(!c.Career.Expedition.Has(FishingExpedition.BeaconRepaired) && Array.Exists(c.DockConsole.GetComponentsInChildren<UnityEngine.UI.Text>(),label=>label.text.Contains("NORTH UNLOCKED")),"XR console shows survey-only north unlock without repairing the beacon");
            var loaded=FishingSaveStore.Load(c.SavePath,out string warning);
            Check(warning=="" && FishingSurvey.LegendCoordinates(loaded),"Production save persists derived unlocks");
            c.ToggleJournal();c.NavigateSurvey();
            Check(!c.JournalOpen && !c.Paused && c.NavigationTarget==2,"Survey navigation closes the paused journal before setting the waypoint");
            Check(c.BuyUpgrade(UpgradeTrack.Line),"Reinforced line bought with fish income");
            float until=Time.time+13;while(Time.time<until)yield return null;
            c.Weather.Climate.Select(BayWeather.Sunset);
            c.MoveToSpotForAutomation(1);c.Interact();
            while(c.Bait!=BaitKind.Pearl)c.CycleBait();
            Check(!c.ForceLegendForAutomation && c.Cast() && c.Session.Kind==FishKind.Sailfish,"Survey coordinates trigger actual legend cast without force flag");
            for(int i=0;i<100 && c.Session.Phase==CatchPhase.Waiting;i++)c.Session.Tick(.1f,false);
            Check(c.Hook(),"Actual legend hook");
            for(int i=0;i<6500 && c.Session.Phase==CatchPhase.Fighting;i++)c.Session.Tick(.02f,c.Session.Tension<.55f && c.Session.Beat!=FishBeat.Leap);
            Check(c.Session.Phase==CatchPhase.Landing && c.Session.FightTime>=20,"Legend reaches a bounded net window after at least 20 seconds");
            c.DockConsole.Present();
            var status=Array.Find(c.DockConsole.GetComponentsInChildren<UnityEngine.UI.Text>(),label=>label.text.StartsWith("TIDE & FIN"));
            Check(status!=null && status.text.Contains("NET: "+c.Session.Remaining.ToString("0.0")+"s"),"Actual XR console shows the remaining net time");
            Check(status.preferredHeight<=status.rectTransform.rect.height,"XR status fits above its buttons: text="+status.preferredHeight+", area="+status.rectTransform.rect.height);
            while(c.Session.Tension<.95f)c.Session.Tick(.02f,true);
            var center=c.LandingTarget;
            for(int i=0;i<10;i++)c.CatchRig.ApplyNetPose(center,Quaternion.identity,.02f);
            for(int i=1;i<=20;i++)c.CatchRig.ApplyNetPose(center+Vector3.up*(i*.01f),Quaternion.identity,.02f);
            Check(!c.HasPendingCatch && !c.Session.CanNet,"High-tension scoop gives no fish or reward");
            for(int i=0;i<4;i++)c.Session.Tick(.1f,false);
            Check(c.Session.CanNet,"Releasing the line restores scoop eligibility");
            for(int i=19;i>=0;i--)c.CatchRig.ApplyNetPose(center+Vector3.up*(i*.01f),Quaternion.identity,.02f);
            for(int i=0;i<10;i++)c.CatchRig.ApplyNetPose(center,Quaternion.identity,.02f);
            for(int i=1;i<=30 && !c.HasPendingCatch;i++)c.CatchRig.ApplyNetPose(center+Vector3.up*(i*.01f),Quaternion.identity,.02f);
            Check(c.HasPendingCatch,"Physical net can retry after releasing excess tension without resetting the catch");
            Check(c.MeasureCatch() && c.ResolveCatch(false) && !c.ResolveCatch(false) && c.Career.LegendCatches==1 && !c.HasPendingCatch,"Actual physical net, measurement and release pay once");
            c.ReturnToHarbor();
            for(int island=0;island<2;island++){c.Career.DiscoverIsland(island);c.Career.OpenIslandTreasure(island);}
            Check(c.BuyUpgrade(UpgradeTrack.Engine) && c.BuyUpgrade(UpgradeTrack.Engine),"Choose tier-two propulsion at actual harbor");
            Check(c.BuyUpgrade(UpgradeTrack.Hold),"Cargo first tier remains available");
            int coins=c.Career.Coins;
            Check(!c.BuyUpgrade(UpgradeTrack.Hold) && c.Career.Coins==coins,"Actual purchase handler rejects conflicting specialization without spending");
            Check(c.Player.engine.horsePower==FishingFleet.Power(2),"Chosen specialization reaches actual engine");
            c.MoveToSpotForAutomation(0);c.Interact();Check(c.Cast(),"Start final net timeout case");
            for(int i=0;i<100 && c.Session.Phase==CatchPhase.Waiting;i++)c.Session.Tick(.1f,false);c.Hook();
            for(int i=0;i<6500 && c.Session.Phase==CatchPhase.Fighting;i++)c.Session.Tick(.02f,c.Session.Tension<.55f);
            until=Time.time+12;while(Time.time<until)yield return null;
            Check(c.Session.Failure==CatchFailure.LandingTimeout && !c.HasPendingCatch,"Production Update no longer freezes fish at the net");
            c.ReturnToHarbor();
        }
    }
}
