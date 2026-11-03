using System;
using System.Collections;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV062SawValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("[V062-SAW] "+label);Debug.Log("[V062-SAW] CHECK: "+label);}
        private static bool Move(FishingHandGames games,Vector3 palm,Quaternion rotation,bool held=true)
        {
            var args=new object[]{palm,rotation,held,Vector2.zero};
            bool contact=(bool)typeof(FishingHandGames).GetMethod("TrackedHandMotion",Private).Invoke(games,args);
            var point=(Vector2)args[3];games.Task.SampleContact(point.x,point.y,.02f,contact);return contact;
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.HasIsolatedProfile && c.Automated && !c.CreativeMode,"Saw checks use an isolated ordinary career");
            var games=c.HandGames;if(games.Active)games.Cancel();
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.Paused)c.TogglePause();
            if(!c.Ashore)
            {
                c.ReturnToHarbor();yield return null;
                var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Saw fixture reaches a supported landing");yield return null;
            }
            foreach(float calibration in new[]{0f,30f})
            {
                if(c.Career.Backpack.Count(FieldItem.Driftwood)<2)Check(c.Career.Backpack.TryAdd(FieldItem.Driftwood,2),"Saw fixture supplies real wood");
                var station=games.Station(2);Check(c.Shore.Relocate(games.Approach(2),Quaternion.LookRotation(-station.forward)),"Saw table is reachable");
                c.View.ResetLook();yield return null;FishingV036FarmValidation.Aim(c,station.TransformPoint(new Vector3(0,1.1f,0)));yield return null;
                Check(games.Begin(2),"Saw opens at palm roll "+calibration);
                try
                {
                    var task=games.Task;var rotation=Quaternion.Euler(0,0,calibration);
                    var palm=games.HandlePoint-rotation*new Vector3(0,-.026f,0);
                    Move(games,palm,rotation,false);
                    Check(Move(games,palm,rotation) && games.XRToolHeld && task.Strokes==0,"A calibrated palm grabs once without a false cut");
                    Check(Vector3.Distance(games.HandlePoint,palm+rotation*new Vector3(0,-.026f,0))<.002f,"Saw handle stays at the calibrated palm");
                    palm+=station.right*.12f;
                    Check(Move(games,palm,rotation) && Mathf.Abs(station.InverseTransformPoint(games.WorkPoint).x)<.002f,"Visible timber centre accepts real contact and chooses its cut");
                    for(int frame=0;frame<40;frame++)Move(games,palm,rotation);
                    Check(task.Strokes==0,"A stationary grip does not cut wood");
                    var lifted=palm+Vector3.up*.20f;
                    for(int frame=0;frame<24;frame++)Move(games,lifted+station.forward*(frame%2==0?.05f:-.05f),rotation);
                    Check(task.Strokes==0 && games.XRToolHeld,"Air motion cannot cut or drop the held saw");
                    Check(Move(games,palm,rotation) && task.Strokes==0,"Re-entry starts a fresh observed stroke");
                    var inverted=Quaternion.AngleAxis(180,games.ActiveTool.up)*rotation;
                    Check(!Move(games,palm,inverted) && task.Strokes==0,"The back of the saw cannot substitute for its teeth");
                    Move(games,palm,rotation);
                    rotation=Quaternion.AngleAxis(35,Vector3.up)*rotation;
                    Move(games,palm,rotation);
                    palm+=station.TransformPoint(new Vector3(0,1.025f,0))-games.ToolContact;
                    Check(Move(games,palm,rotation),"A naturally angled blade still touches the visible wood");
                    Move(games,palm+Vector3.up*.20f,rotation);int initialStrokes=task.Strokes;
                    Check(Move(games,palm,rotation) && task.Strokes==initialStrokes,"Real contact re-entry initializes the short stroke at its centre without priming progress");
                    var axis=games.ActiveTool.up;int beforeWood=c.Career.Backpack.Count(FieldItem.Driftwood),beforePlanks=c.Career.Backpack.Count(FieldItem.Plank);
                    float previous=0;bool touching=true;
                    for(int stroke=0;stroke<16 && !task.Complete;stroke++)
                    {
                        float next=stroke%2==0?.045f:-.045f;
                        for(int frame=1;frame<=20;frame++)
                            touching&=Move(games,palm+axis*Mathf.Lerp(previous,next,frame/20f),rotation);
                        previous=next;
                        if(stroke==0)Check(task.Strokes==initialStrokes,"An initial 4.5 cm half-stroke does not lower the 7.5 cm threshold");
                        if(stroke==1)Check(task.Strokes==initialStrokes+1,"The first full 9 cm return stroke starts at the real turning point");
                    }
                    Check(touching,"Continuous nine-centimetre blade strokes remain in the wood");
                    Check(task.Complete && games.XRToolHeld,"Real short push/pull strokes complete an angled cut");
                    typeof(FishingHandGames).GetMethod("PresentWorkpieces",Private).Invoke(games,new object[]{0f});
                    var mesh=games.Workpiece(2).GetComponent<MeshFilter>().sharedMesh;bool atChosenCut=false;var vertices=mesh.vertices;var colors=mesh.colors;
                    for(int i=0;i<vertices.Length;i++)if(colors[i].r>.85f && colors[i].g>.6f && Mathf.Abs(vertices[i].x)<.05f)atChosenCut=true;
                    Check(atChosenCut,"The visible kerf follows the player's chosen cut rather than the old hidden point");
                    Check(task.Claim(c.Career.Backpack) && c.Career.Backpack.Count(FieldItem.Driftwood)==beforeWood-1
                        && c.Career.Backpack.Count(FieldItem.Plank)==beforePlanks+4 && !task.Claim(c.Career.Backpack),"A completed ordinary saw round exchanges one log for four planks exactly once");
                    Move(games,palm,rotation,false);Check(!games.XRToolHeld,"Release opens the calibrated grip");
                }
                finally{games.Cancel();}
                yield return null;
            }
        }
    }
}
