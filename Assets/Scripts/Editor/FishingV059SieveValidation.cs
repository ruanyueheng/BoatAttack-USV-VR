using System;
using System.Collections;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV059SieveValidation
    {
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("[V059-SIEVE] "+label);Debug.Log("[V059-SIEVE] CHECK: "+label);}
        private static FieldInfo Field(string name)=>typeof(FishingHandGames).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        private static bool BeginGrip(FishingHandGames games,Vector3 palm,bool left)
            =>(bool)typeof(FishingHandGames).GetMethod("TryBeginTrackedGrab",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(games,new object[]{palm,left});
        private static bool MoveGrip(FishingHandGames games,Vector3 palm,Quaternion rotation,bool held)
            =>(bool)typeof(FishingHandGames).GetMethod("TrackedHandMotion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(games,new object[]{palm,rotation,held,Vector2.zero});
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Sieve regression uses an isolated production career");
            if(c.HandGames.Active)c.HandGames.Cancel();
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Sieve fixture reaches a real supported landing");yield return null;
            var games=c.HandGames;var station=games.Station(0);
            Check(c.Shore.Relocate(games.Approach(0),Quaternion.LookRotation(-station.forward)),"Both sieve handles are reachable from the porch");
            c.View.ResetLook();yield return null;
            if(c.Career.Backpack.Count(FieldItem.Sand)<8)Check(c.Career.Backpack.TryAdd(FieldItem.Sand,8),"Fixture supplies real sieve input");
            var tool=((Transform[])Field("_tools").GetValue(games))[0];
            var grips=new[]{(Transform)Field("_sieveGripA").GetValue(games),(Transform)Field("_sieveGripB").GetValue(games)};
            var home=tool.localPosition;var rest=tool.localRotation;
            Check(!BeginGrip(games,tool.position+Vector3.up*.8f,true) && !games.Active,"A distant hand cannot start the sieve");
            try
            {
                foreach(bool left in new[]{false,true})foreach(var grip in grips)
                {
                    int sand=c.Career.Backpack.Count(FieldItem.Sand);int shells=c.Career.Backpack.Count(FieldItem.Shell);
                    var palm=grip.position+Vector3.up*.026f;
                    Check(BeginGrip(games,palm,left) && games.Task.Kind==FishingHandTaskKind.Sieve,"Either hand directly grabs either visible sieve handle: "+left+" / "+grip.name);
                    Check((bool)Field("_sieveHeldLeft").GetValue(games)==left,"The entering hand owns the sieve grip");
                    Check(MoveGrip(games,palm,Quaternion.identity,true) && games.XRToolHeld,"The entry squeeze attaches immediately without a second grip");
                    var lifted=palm+Vector3.up*.24f+station.right*.12f;
                    var rotation=Quaternion.Euler(12,28,-17);
                    Check(MoveGrip(games,lifted,rotation,true) && games.XRToolHeld,"The sieve stays held while lifted and tilted");
                    Check(Vector3.Distance(grip.position,lifted+rotation*new Vector3(0,-.026f,0))<.002f,"The actual nested model handle follows the palm through translation and rotation");
                    Check(tool.position.y>station.position.y+1.25f,"The sieve lifts clear of the fixed tabletop rest pose");
                    var task=games.Task;var frame=Quaternion.Euler(0,31,0);var native=new Vector3(.25f,1.10f,.35f);
                    for(int i=0;i<30;i++)
                    {
                        var point=FishingHandGames.SieveMotionPoint(native,Vector3.zero,frame);
                        task.SampleContact(point.x,point.y,.02f,true);
                    }
                    Check(task.Strokes==0,"A stationary native hand produces no sieve progress");
                    // Losing tracking disarms the grip. Keeping the button squeezed must not reattach it.
                    Field("_ready").SetValue(games,false);Field("_trackedToolHeld").SetValue(games,false);
                    Check(!MoveGrip(games,lifted,rotation,true) && !games.XRToolHeld,"Tracking recovery requires release before regrabbing");
                    MoveGrip(games,lifted,rotation,false);
                    Check(!games.XRToolHeld && Vector3.Distance(tool.localPosition,home)<.0001f && Quaternion.Angle(tool.localRotation,rest)<.01f,"Opening the gripping hand releases the sieve back into its tray");
                    Check(c.Career.Backpack.Count(FieldItem.Sand)==sand && c.Career.Backpack.Count(FieldItem.Shell)==shells,"Grabbing, lifting and cancelling consume no materials");
                    games.Cancel();yield return null;
                }
                var start=grips[0].position+Vector3.up*.026f;
                Check(BeginGrip(games,start,true) && MoveGrip(games,start,Quaternion.identity,true),"Left-hand sieve can start a complete material transaction");
                var active=games.Task;int input=c.Career.Backpack.Count(FieldItem.Sand),output=c.Career.Backpack.Count(FieldItem.Shell);
                var calibrated=Quaternion.Euler(0,37,0);var center=new Vector3(-.2f,1.05f,.4f);
                for(int stroke=0;stroke<14;stroke++)for(int sample=0;sample<12;sample++)
                {
                    var native=center+calibrated*new Vector3(stroke%2==0?-.09f:.09f,0,0);
                    var point=FishingHandGames.SieveMotionPoint(native,center,calibrated);
                    active.SampleContact(point.x,point.y,.02f,true);
                }
                Check(active.Complete && games.XRToolHeld,"Alternating native hand strokes complete the sieve while its handle remains attached");
                Check(active.Claim(c.Career.Backpack) && c.Career.Backpack.Count(FieldItem.Sand)==input-2 && c.Career.Backpack.Count(FieldItem.Shell)==output+1,"Completed shaking exchanges exactly two real sand for one shell");
                Check(!active.Claim(c.Career.Backpack) && c.Career.Backpack.Count(FieldItem.Shell)==output+1,"The same sieve round cannot pay twice");
            }
            finally{games.Cancel();}
            yield return null;
        }
    }
}
