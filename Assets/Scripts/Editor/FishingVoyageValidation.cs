using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingVoyageValidation
    {
        public static void Run()=>FishingV011Validation.Begin("VOYAGE-QUEST");
        private static void Check(bool ok,string text)
        {if(!ok)throw new InvalidOperationException("[VOYAGE-QUEST] "+text);Debug.Log("[VOYAGE-QUEST] CHECK: "+text);}

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.Voyage!=null,"The real voyage world uses an isolated production career");
            Directory.CreateDirectory("Logs/VoyageQuest");
            if(c.StartMenuOpen)c.FinishTitleVoyage();
            c.CloseHomeChart(true);if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();yield return null;
            try
            {
                Check(!c.Career.Lore.ChartFound,"A new career starts without discovering the secret chart");
                Check(c.OpenPortableChart(),"Ordinary M-map entry is available before the story discovery");
                c.PortableChart.Tick(FishingPortableChart.OpenSeconds);c.CloseHomeChart(true);yield return null;
                Check(!c.Career.Lore.ChartFound,"Opening the ordinary portable chart does not grant story progress");
                Check(c.Prologue.Begin(),"The existing six-shot prologue can begin");c.Prologue.Finish();yield return null;
                Check(c.Career.Lore.MayorAccepted && c.Career.PrologueSeen && !c.Career.Lore.ChartFound,"Normal completion and skip share the appointment receipt without granting the hidden chart");

                var home=GoToBerth(c,c.Home.Berth);while(home.MoveNext())yield return home.Current;
                Check(c.Shore.Relocate(c.Home.Origin+new Vector3(3.65f,.20f,-2.4f),Quaternion.identity),"The original chart desk has a supported approach");
                c.View.ResetLook();yield return null;FishingV036FarmValidation.Aim(c,c.Home.InteractionPoint(2));yield return null;
                Check(c.RequestWorldInteraction() && c.Career.Lore.ChartFound && c.HomeChartOpen,"One real desk interaction records the appointment/chart and opens the existing global chart");
                c.CloseHomeChart(true);yield return null;

                for(int i=0;i<4;i++)c.Career.CompleteShrine(i);
                int coins=c.Career.Coins;
                c.Shore.BoardImmediately();
                var island=GoToBerth(c,FishingIslandWorld.Berth(0));while(island.MoveNext())yield return island.Current;
                Check(c.Shore.Relocate(c.Realms.PortalApproach(0),Quaternion.identity) && c.Realms.Enter(0),"A cleared-save fixture enters the real wind sanctuary");
                for(int i=0;i<240 && c.Realms.Transitioning;i++)yield return null;
                Check(c.Realms.Inside && !c.Realms.Transitioning,"The real portal transition completes");
                var approach=ApproachRealmTerminal(c);while(approach.MoveNext())yield return approach.Current;
                Check(c.RequestWorldInteraction() && c.Career.Lore.RewardClaimed(0),"The real terminal lets an already-cleared save claim its new relic and blueprint");
                Check(c.Career.Coins==coins && c.Career.Backpack.Count(FieldItem.WindRelic)==1,"Supplemental rewards do not repay legacy shrine coins");
                c.RequestWorldInteraction();Check(c.Career.Coins==coins && c.Career.Backpack.Count(FieldItem.WindRelic)==1,"Repeated terminal interaction cannot duplicate the relic or coins");
                c.Realms.Abort();yield return null;
                for(int i=1;i<4;i++)c.Voyage.ClaimRealmReward(i);
                Check(c.Career.Lore.RewardedShrines==15,"All four permanent blueprint receipts are present");

                c.Shore.BoardImmediately();home=GoToBerth(c,c.Home.Berth);while(home.MoveNext())yield return home.Current;
                var workshop=ApproachHomeWorkbench(c);while(workshop.MoveNext())yield return workshop.Current;
                c.WorkshopWorld.SelectNearby();c.ToggleInventory();yield return null;
                Check(c.InventoryOpen && c.WorkshopWorld.Context==CraftStation.Workbench && c.Voyage.CraftCompass(),"The real workbench page crafts the voyage compass through the quest transaction");
                Check(c.Career.Backpack.Count(FieldItem.VoyageCompass)==1,"The compass appears once in the actual backpack");
                c.ToggleInventory();yield return null;

                c.Shore.BoardImmediately();var cove=GoToBerth(c,FishingCoastalPresentation.Berth(0));while(cove.MoveNext())yield return cove.Current;
                var chest=c.Voyage.TreasureChest;
                Check(c.Shore.Relocate(c.Voyage.TreasureApproach,Quaternion.LookRotation(Vector3.ProjectOnPlane(chest.position-c.Voyage.TreasureApproach,Vector3.up))),"The treasure has a real clear standing approach on White Sail Cove");
                c.View.ResetLook();yield return null;FishingV036FarmValidation.Aim(c,chest.position+Vector3.up*1.35f);yield return null;
                Check(chest.GetComponentsInChildren<Collider>().Length>=6,"The table and chest have actual solid collision");
                coins=c.Career.Coins;
                Check(c.RequestWorldInteraction() && c.Career.PirateKing && c.Career.Coins==coins+FishingCareer.GrandTreasureCoins
                    && c.Career.Backpack.Count(FieldItem.VoyageCompass)==0,"Arriving and interacting consumes the compass once and awards the title and 1200 coins");
                float until=Time.time+1.3f;while(Time.time<until)yield return null;
                Check(Quaternion.Angle(c.Voyage.TreasureLid.localRotation,c.Voyage.TreasureLidOpenRotation)<1,"The independent lid visibly opens around its hinge");
                for(int i=0;i<9;i++)if(c.Career.Backpack.At(i).count==0){c.SelectFieldSlot(i);break;}
                c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/VoyageQuest","grand-voyage-treasure-open");
                var loaded=FishingSaveStore.Load(c.SavePath,out _);
                Check(loaded.PirateKing && loaded.Lore.RewardedShrines==15 && loaded.Backpack.Count(FieldItem.VoyageCompass)==0,"Saved progress retains the title, blueprint receipts and consumed compass");
                c.RequestWorldInteraction();Check(c.Career.Coins==coins+FishingCareer.GrandTreasureCoins,"Revisiting the opened chest never awards the treasure again");
                c.Letter.Close();yield return null;
                Debug.Log("[VOYAGE-QUEST] EVIDENCE: "+Path.GetFullPath("Logs/VoyageQuest")+"; this isolated suite does not certify physical keyboard or headset input.");
            }
            finally
            {
                c.CloseHomeChart(true);c.Prologue.Suspend();c.Letter.Close();c.Realms.Abort();
                if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();c.ReturnToHarbor();
            }
        }

        private static IEnumerator GoToBerth(FishingPrototype c,Vector3 berth)
        {
            var rb=c.Player.engine.RB;var point=berth+Vector3.up*.7f;
            rb.position=point;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(point,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Production landing is available at "+berth);yield return null;
        }
        private static IEnumerator ApproachRealmTerminal(FishingPrototype c)
        {
            var devices=(Transform[])typeof(FishingRealmWorld).GetField("_devices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.Realms);
            var point=devices[3].position;
            for(int i=0;i<8;i++)
            {
                var direction=c.Realms.Room.rotation*Quaternion.Euler(0,i*45,0)*Vector3.back;
                if(!c.Shore.Ground(point+direction*1.65f,out var floor) || !c.Shore.Relocate(floor,Quaternion.LookRotation(-direction)))continue;
                c.View.ResetLook();yield return null;FishingV036FarmValidation.Aim(c,point);yield return null;
                if(c.Realms.TryTarget(out int target,out _) && target==3)yield break;
            }
            throw new InvalidOperationException("The wind sanctuary terminal lacks a reachable production interaction approach.");
        }
        private static IEnumerator ApproachHomeWorkbench(FishingPrototype c)
        {
            foreach(var station in c.Career.Workshop.Stations)
            {
                if(station.kind!=CraftStation.Workbench || Vector3.Distance(new Vector3(station.x,station.y,station.z),c.Home.Origin)>30)continue;
                var point=new Vector3(station.x,station.y,station.z);
                for(int i=0;i<8;i++)
                {
                    var direction=Quaternion.Euler(0,i*45,0)*Vector3.back;
                    if(!c.Shore.Ground(point+direction*1.2f,out var floor) || !c.Shore.Relocate(floor,Quaternion.LookRotation(-direction)))continue;
                    c.View.ResetLook();yield return null;c.WorkshopWorld.SelectNearby();
                    if(c.WorkshopWorld.Context==CraftStation.Workbench)yield break;
                }
            }
            throw new InvalidOperationException("The home workbench lacks a supported crafting approach.");
        }
    }
}
