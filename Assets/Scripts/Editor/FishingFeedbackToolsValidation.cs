using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace BoatAttack.EditorTools
{
    public static class FishingFeedbackToolsValidation
    {
        public static void Run()=>FishingV011Validation.Begin("FEEDBACK-TOOLS");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("[FEEDBACK-TOOLS] "+label);Debug.Log("[FEEDBACK-TOOLS] CHECK: "+label);}
        private static void Equip(FishingPrototype c,FieldItem item)
        {
            if(c.Career.Backpack.Count(item)==0)Check(c.Career.Backpack.TryAdd(item,1),"Fixture adds "+item);
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==item){if(i>=9)c.Career.Backpack.QuickMove(i);break;}
            for(int i=0;i<9;i++)if(c.Career.Backpack.At(i).item==item){c.SelectFieldSlot(i);return;}throw new InvalidOperationException("Tool has no hotbar slot");
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Only the isolated production career is used");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);if(c.InventoryOpen)c.ToggleInventory();if(c.Paused)c.TogglePause();c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Home has a real supported landing");yield return null;
            for(int game=2;game<4;game++)
            {
                c.Career.Backpack.TryAdd(game==2?FieldItem.Driftwood:FieldItem.IronIngot,game==2?2:6);
                var station=c.HandGames.Station(game);Check(c.Shore.Relocate(c.HandGames.Approach(game),Quaternion.LookRotation(-station.forward)),"New handcraft station has a supported approach "+game);c.View.ResetLook();yield return null;
                FishingV036FarmValidation.Aim(c,station.TransformPoint(new Vector3(0,1.1f,0)));yield return null;
                Check(c.HandGames.TryTarget(out int target,out _) && target==game && c.HandGames.Begin(game),"World interaction starts the intended new handcraft "+game);
                for(int i=0;i<20;i++)yield return null;
                Check(Vector3.Distance(c.HandGames.ToolContact,c.HandGames.WorkPoint)<.012f,"The teeth/striking face rests on the real material instead of beside it "+game);
                var support=(Transform[])typeof(FishingHandGames).GetField("_workLeft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.HandGames);
                Check(Vector3.Distance(support[game-2].position,c.HandGames.WorkPoint)>.25f,"The resting hand stays safely beside the cut/strike "+game);
                if(game==2)Check(Vector3.Dot(c.HandGames.ActiveTool.up,-station.forward)>.99f && Vector3.Dot(c.HandGames.ActiveTool.right,Vector3.down)>.99f,"Saw length follows a forward/back kerf with its teeth downward");
                var view=c.viewCamera.WorldToViewportPoint(c.HandGames.HandlePoint);Check(view.z>.25f && view.x>0 && view.x<1 && view.y>0 && view.y<1,"The working handle stays on screen and away from the eye "+game+" "+view);
                Directory.CreateDirectory("Logs/V057");FishingV016Validation.Capture(c,"Logs/V057",game==2?"saw-rest":"forge-rest");
                if(game==3)foreach(var filter in c.HandGames.ActiveTool.GetComponentsInChildren<MeshFilter>())
                {
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        var point=c.viewCamera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                        if(point.z<=.25f || point.x<=0 || point.x>=1 || point.y<=0 || point.y>=1)throw new InvalidOperationException("[FEEDBACK-TOOLS] The complete hammer must stay visible · "+point);
                    }
                }
                if(game==3)Check(true,"The complete hammer stays visible at its resting strike pose");
                Directory.CreateDirectory("Logs/FeedbackToolsWorld");c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/FeedbackToolsWorld",game==2?"saw-table":"forge-table");
                Check(Vector3.Distance(c.Shore.Character.PalmPosition(false),c.HandGames.HandlePoint)<.09f,"The real skinned working palm reaches the tool grip "+game+" palm="+c.Shore.Character.PalmPosition(false).ToString("F4")+" grip="+c.HandGames.HandlePoint.ToString("F4")+" shoulder="+c.Shore.Character.ShoulderPosition(false).ToString("F4")+" reach="+c.Shore.Character.ArmReach(false));
                foreach(float edge in game==2?new[]{-.1f,.1f}:new[]{-.1f,.1f,.18f})
                {
                    typeof(FishingHandGames).GetField("_motion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c.HandGames,game==2?new Vector2(.1f+edge,0):new Vector2(.1f,edge));yield return null;c.PresentCharacterFrame();
                    Check(Vector3.Distance(c.Shore.Character.PalmPosition(false),c.HandGames.HandlePoint)<.09f,"The real working palm still reaches the tool at motion extreme "+game+" "+edge+" distance="+Vector3.Distance(c.Shore.Character.PalmPosition(false),c.HandGames.HandlePoint));
                    Check(Vector3.Dot(c.Shore.Character.PalmPosition(false)-c.Shore.Character.PalmPosition(true),c.Shore.Actor.right)>.12f,"Working arms do not cross at the motion extreme "+game+" "+edge);
                    var delta=station.InverseTransformPoint(c.HandGames.ToolContact)-station.InverseTransformPoint(c.HandGames.WorkPoint);
                    if(game==2)Check(Mathf.Abs(delta.x)<.005f && Mathf.Abs(delta.y)<.005f && Mathf.Abs(delta.z)<=.095f,"Saw teeth stay in the kerf throughout the forward/back stroke");
                    else if(edge>0)Check(delta.y>.15f && Quaternion.Angle(c.HandGames.ActiveTool.localRotation,Quaternion.LookRotation(Vector3.right,Vector3.back))>15,"Hammer lifts in a wrist arc above the billet");
                    if(game==3)foreach(var filter in c.HandGames.ActiveTool.GetComponentsInChildren<MeshFilter>())foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        var p=c.viewCamera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                        if(p.z<=.25f || p.x<=0 || p.x>=1 || p.y<=0 || p.y>=1)throw new InvalidOperationException("Complete hammer must stay in view during the wrist arc "+edge+" "+p);
                    }
                    Directory.CreateDirectory("Logs/V057");FishingV016Validation.Capture(c,"Logs/V057",(game==2?"saw":"forge")+(edge<0?"-contact":"-raised"));
                }
                int input=c.Career.Backpack.Count(game==2?FieldItem.Driftwood:FieldItem.IronIngot);c.HandGames.Cancel();Check(c.Career.Backpack.Count(game==2?FieldItem.Driftwood:FieldItem.IronIngot)==input,"Cancel leaves all source material untouched "+game);yield return null;
                FishingV036FarmValidation.Aim(c,station.TransformPoint(new Vector3(0,1.1f,0)));Check(c.HandGames.Begin(game),"The table can be reopened after cancellation "+game);var task=c.HandGames.Task;
                for(int i=0;i<40;i++)for(int f=0;f<12;f++){float motion=i%2==0?-.1f:.1f;task.Sample(game==2?motion:0,game==3?motion:0,.02f);}Check(task.Complete,"Bounded physical-motion samples satisfy the production gesture rule "+game);
                int output=c.Career.Backpack.Count(task.Output);var mouse=InputSystem.AddDevice<Mouse>();
                try{c.HandGames.Tick(null,mouse,true,false);InputState.Change(mouse,new MouseState{buttons=1});c.HandGames.Tick(null,mouse,true,false);}
                finally{InputSystem.RemoveDevice(mouse);c.HandGames.Cancel();}
                Check(task.Claimed && c.Career.Backpack.Count(task.Output)==output+task.OutputCount,"Real completion input commits exactly one useful item bundle "+game);yield return null;
            }
            foreach(var pair in new[]{(FieldItem.Axe,FieldItem.Driftwood),(FieldItem.Shovel,FieldItem.Sand)})
            {
                Equip(c,pair.Item1);FishingWorkshopWorld.ResourceNode node=null;foreach(var candidate in c.WorkshopWorld.Nodes)if(candidate.Item==pair.Item2 && c.Career.Workshop.ResourceReady(candidate.Id)){node=candidate;break;}Check(node!=null,"A real resource node exists for "+pair.Item1);
                bool began=false;
                for(int angle=0;angle<8 && !began;angle++)
                {
                    var offset=Quaternion.Euler(0,angle*45,0)*Vector3.back;if(!c.Shore.Ground(node.Feet+offset*.85f,out var ground) || !c.Shore.Relocate(ground,Quaternion.LookRotation(-offset)))continue;
                    c.View.ResetLook();for(int settled=0;settled<12;settled++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}FishingV036FarmValidation.Aim(c,node.Root.position+Vector3.up*.15f);yield return null;began=c.RequestHarvest();
                    if(!began)Debug.Log("[FEEDBACK-TOOLS] CONTACT DIAGNOSTIC: "+pair.Item1+" angle="+angle+" message="+c.VisibleMessage+" preflight="+c.InspectWorldInteractionStart());
                }
                Check(began,"The real tool contact can start at the resource "+pair.Item1);float until=Time.time+3;while(c.WorldActionBusy && Time.time<until)yield return null;
                Check(c.LastActionSucceeded && c.Career.Backpack.Count(pair.Item2)>=4,"Actual tool-head contact gathers usable material "+pair.Item1);
            }
            Equip(c,FieldItem.Rake);int plot=-1;for(int i=0;i<FishingFarm.PlotCount;i++)if(c.Farm.Work(i)==FarmWork.Till){plot=i;break;}Check(plot>=0,"An untilled real garden plot is available");
            var seed=c.Farm.Target(plot).position+Vector3.back*1.2f;Check(c.Shore.Ground(seed,out var soil) && c.Shore.Relocate(soil,Quaternion.identity),"The rake can approach a real garden plot");c.View.ResetLook();for(int settled=0;settled<12;settled++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}FishingV036FarmValidation.Aim(c,c.Farm.Aim(plot));yield return null;
            bool found=c.Farm.TryTarget(out int aimed,out _);c.UseFieldItem();Debug.Log("[FEEDBACK-TOOLS] RAKE DIAGNOSTIC: fixture="+plot+" found="+found+" aimed="+aimed+" busy="+c.WorldActionBusy+" message="+c.VisibleMessage+" preflight="+c.InspectWorldInteractionStart());
            bool touchedSoil=false;float deadline=Time.time+3;
            while(c.WorldActionBusy && Time.time<deadline)
            {
                yield return null;
                if(c.LastActionSucceeded && c.ActionContactBlend>.999f)
                {
                    c.PresentCharacterFrame();var rake=(Transform)typeof(FishingFarmWorld).GetField("_rake",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.Farm);
                    var tip=rake.TransformPoint(FishingFieldWorld.WorkToolTip(FieldItem.Rake));var local=c.Farm.Target(plot).InverseTransformPoint(tip);
                    Check(Mathf.Abs(local.y-.12f)<.09f && Mathf.Abs(local.x)<=.99f && Mathf.Abs(local.z)<=.99f,"The rendered rake head actually touches the soil at commit · local="+local.ToString("F4"));
                    Check(Vector3.Distance(c.Shore.Character.PalmPosition(false),rake.TransformPoint(new Vector3(0,.1f,0)))<.09f,"The real working palm still holds the rake at soil contact");
                    Directory.CreateDirectory("Logs/FeedbackToolsWorld");FishingV016Validation.Capture(c,"Logs/FeedbackToolsWorld","rake-soil-contact");touchedSoil=true;
                }
            }
            Check(c.Career.Farm.Plot(plot).tilled && touchedSoil,"The selected rake performs actual soil preparation with visible tool contact · "+c.VisibleMessage);
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(.4f,.2f,-1.5f),Quaternion.identity),"XR routing fixture approaches the new aquarium point");yield return null;
            Check(c.HomeEcology.NearbyHint!=null,"The new ecology point is the real nearby interaction");
            typeof(FishingXRNotebook).GetMethod("InteractTracked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.XRNotebook,null);
            Check(c.InventoryOpen,"The actual XR A-button routing reaches new world interactions before legacy hand-only fallback");c.ToggleInventory();
            Debug.Log("[FEEDBACK-TOOLS] LIMIT: bounded rule samples and direct XR routing are tested; physical controller calibration is not certified.");
        }
    }
}
