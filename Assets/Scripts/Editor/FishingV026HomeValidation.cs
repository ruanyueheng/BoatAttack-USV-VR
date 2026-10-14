using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV026HomeValidation
    {
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V026-HOME] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Home migration uses an isolated profile");
            c.ReturnToHarbor();yield return null;yield return null;
            var original=c.Career.Snapshot();var world=c.WorkshopWorld;var home=c.Home;
            string evidence=Path.GetFullPath("Logs/v026-home-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            try
            {
                var old=c.Career.Snapshot();
                old.workshop.stations.RemoveAll(s=>s.permanent && (s.kind!=CraftStation.Forge || Vector3.Distance(new Vector3(s.x,s.y,s.z),home.StationPosition(s.kind))<1));
                old.workshop.initialized=true;
                Check(old.workshop.stations.Count(s=>s.permanent)==1,"Reproduce initialized legacy profile containing only the public forge");
                string bagBefore=string.Join(";",old.backpack.Select(s=>s.item+":"+s.count));
                c.RestoreShopTestProfile(old);
                foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
                {
                    Check(world.State.Stations.Count(s=>s.permanent && s.kind==kind && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),c.Shore.Sites[0].Landing)<45)==1,"Legacy coast restores exactly one "+kind);
                    Check(world.State.Stations.Count(s=>s.permanent && s.kind==kind && Vector3.Distance(new Vector3(s.x,s.y,s.z),home.StationPosition(kind))<.2f)==1,"Home installs exactly one "+kind);
                }
                Check(string.Join(";",c.Career.Snapshot().backpack.Select(s=>s.item+":"+s.count))==bagBefore,"Migration does not charge or replace backpack items");
                string installed=JsonUtility.ToJson(world.State.Snapshot());world.RefreshFromCareer();world.RefreshFromCareer();
                Check(JsonUtility.ToJson(world.State.Snapshot())==installed,"Repeated refresh preserves every station ID and does not duplicate facilities");

                PlaceBoat(c,home.Berth);for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Home can be reached from its actual berth");
                if(!c.FirstPerson)c.ToggleView();
                foreach(var point in new[]{home.Entry,home.Origin+new Vector3(0,.20f,-3.5f),home.Origin+new Vector3(0,.20f,0)})
                    Check(c.Shore.Relocate(point,Quaternion.identity),"Home entrance and central work aisle remain walkable "+point);
                c.Shore.Relocate(home.Origin+new Vector3(0,.20f,-3.5f),Quaternion.identity);c.View.ResetLook();yield return null;
                Capture(c,evidence,"home-front");
                Check(c.Shore.Relocate(home.Origin+new Vector3(0,.20f,1.8f),Quaternion.Euler(0,180,0)),"Home rear aisle has clearance");
                c.View.ResetLook();yield return null;Capture(c,evidence,"home-rear");
                var bag=c.Career.Backpack;bag.TryAdd(FieldItem.Stone,8);bag.TryAdd(FieldItem.IronOre,3);bag.TryAdd(FieldItem.Coal,2);
                foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
                {
                    var station=world.State.Stations.Single(s=>s.kind==kind && s.permanent && Vector3.Distance(new Vector3(s.x,s.y,s.z),home.StationPosition(kind))<.2f);
                    Check(c.Shore.Relocate(home.StationPosition(kind)+Vector3.back*1.25f,Quaternion.identity),"Walk to the front of home "+kind);
                    c.View.ResetLook();yield return null;world.Sense();
                    Check(world.ReachStation(station.id) && c.RequestWorldInteraction() && c.InventoryOpen && world.Context==kind,"Production interaction opens home "+kind);
                    if(kind==CraftStation.Workbench)Check(world.Craft("furnace"),"Home workbench crafts using the existing recipe transaction");
                    else if(kind==CraftStation.Forge)Check(world.Craft("forge-iron"),"Home forge converts actual ore and coal");
                    else
                    {
                        Check(world.Smelt("iron",1,FieldItem.Coal),"Home furnace reserves ore and fuel");
                        var queued=world.State.Station(station.id);c.ToggleInventory();world.RefreshFromCareer();world.RefreshFromCareer();
                        Check(world.State.Station(station.id).remaining==queued.remaining && world.State.Station(station.id).progress==queued.progress,"Facility repair keeps in-flight furnace queue and progress");
                        Check(world.OpenStation(station.id),"Refreshed home furnace reopens");
                        for(int i=0;i<8;i++)world.State.Tick(1);
                        Check(world.Collect() && !world.Collect(),"Home furnace output can be collected exactly once");
                    }
                    c.ToggleInventory();yield return null;
                    Capture(c,evidence,"home-"+kind,world.StationRoot(station.id));
                }
                var forge=world.State.Stations.Single(s=>s.kind==CraftStation.Forge && s.permanent && Vector3.Distance(new Vector3(s.x,s.y,s.z),home.StationPosition(s.kind))<.2f);
                c.Shore.Body.enabled=false;c.Shore.Actor.position=home.Origin+new Vector3(6.8f,.28f,.65f);c.Shore.Body.enabled=true;Physics.SyncTransforms();
                Check(!world.ReachStation(forge.id) && !world.OpenStation(forge.id),"Actual exterior house wall blocks station reach");

                var spots=new[]{home.WakePoint,home.Origin+new Vector3(-1.55f,.20f,-3.5f),home.Origin+new Vector3(3.65f,.20f,-2.4f),home.Origin+new Vector3(-4.35f,.20f,.65f)};
                for(int item=1;item<4;item++)
                {
                    Check(c.Shore.Relocate(spots[item],Quaternion.identity),"Furniture interaction apron has clearance "+item);yield return null;
                    Check(home.TryTarget(out int id,out _) && id==item && home.ReachInteraction(item),"Nearest furniture selection identifies "+item);
                    bool before=home.LightsOn;
                    if(item==2 && c.ChartExpanded)c.ToggleChart();
                    Check(c.RequestWorldInteraction(),"Desktop E dispatches home interaction "+item);
                    if(item==1)Check(c.VisibleMessage.Contains("明日白昼"),"Radio reports the real calendar forecast");
                    if(item==2)Check(c.ChartExpanded,"Chart desk opens the existing navigation chart");
                    if(item==3)
                    {
                        Check(home.LightsOn!=before && home.House.GetComponentsInChildren<Light>().Where(l=>l.name=="Warm home practical").All(l=>l.intensity==0),"Lamp switch immediately turns off the real home lights");
                        typeof(FishingXRNotebook).GetMethod("InteractTracked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.XRNotebook,null);
                        Check(home.LightsOn==before,"Tracked A entry shares desktop furniture selection and toggles the same lights");
                        c.TogglePause();Check(!home.InteractAt(item),"Paused home interaction cannot mutate lights");c.TogglePause();
                    }
                }
                Check(c.Shore.Relocate(home.WakePoint,Quaternion.identity),"Bedside is still accessible");yield return null;
                Check(c.RequestWorldInteraction() && home.Sleeping,"Bed remains a real interaction after adding furnishings");home.CancelSleep();yield return null;
                foreach(var station in world.State.Stations.Where(s=>s.permanent && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),c.Shore.Sites[0].Landing)<45))
                {
                    var apron=new Vector3(station.x,station.y,station.z)+Vector3.back*1.25f;
                    Check(c.Shore.Relocate(apron,Quaternion.identity) && world.ReachStation(station.id),"Restored public "+station.kind+" has a reachable front apron");
                    c.View.ResetLook();yield return null;Capture(c,evidence,"public-"+station.kind,world.StationRoot(station.id));
                }
                FishingSaveStore.Save(c.SavePath,c.Career);var restored=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning=="" && restored.Workshop.Stations.Count(s=>s.permanent)==6,"Six fixed facilities survive a real JSON save/load");
                Debug.Log("[V026-HOME] EVIDENCE: "+evidence);
            }
            finally
            {
                home.CancelSleep();if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
                c.ReturnToHarbor();c.RestoreShopTestProfile(original);
            }
        }
        private static void Capture(FishingPrototype c,string directory,string label,Transform station=null)
        {
            c.SelectFieldSlot(Enumerable.Range(0,FishingBackpack.HotbarCount).First(i=>c.Career.Backpack.At(i).count==0));
            Check(c.Career.Backpack.Equipped==FieldItem.Empty,"Capture uses an empty hotbar slot without discarding equipment");
            c.PresentCharacterFrame();var rotation=c.viewCamera.transform.rotation;
            try
            {
                if(station!=null)c.viewCamera.transform.LookAt(station.position+Vector3.up*.6f);
                FishingV016Validation.Capture(c,directory,label);
            }
            finally{c.viewCamera.transform.rotation=rotation;}
        }
        private static void PlaceBoat(FishingPrototype c,Vector3 point)
        {
            var rb=c.Player.engine.RB;rb.position=point+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();c.View.ResetLook();
        }
    }
}
