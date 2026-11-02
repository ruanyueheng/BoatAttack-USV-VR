using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV059WorkpieceValidation
    {
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("[V059-WORKPIECE] "+label);Debug.Log("[V059-WORKPIECE] CHECK: "+label);}
        private static Mesh Shape(FishingHandGames games,int station)=>games.Workpiece(station).GetComponent<MeshFilter>().sharedMesh;
        private static Mesh Debris(FishingHandGames games,int station)=>games.Station(station).Find(station==2?"Saw chips at the cut":"Sparks thrown from both sides of the billet").GetComponent<MeshFilter>().sharedMesh;
        private static void Present(FishingHandGames games,float seconds=0)
        {
            typeof(FishingHandGames).GetMethod("PresentWorkpieces",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(games,new object[]{seconds});
            games.Present();
        }
        private static void Stroke(FishingHandGames games,int station)
        {
            var task=games.Task;int before=task.Strokes;
            if(station==2)
            {
                if(before==0)task.SampleContact(-.1f,0,.02f,true);
                for(int sample=0;sample<12;sample++)task.SampleContact(before%2==0?.1f:-.1f,0,.02f,true);
            }
            else
            {
                for(int sample=0;sample<12;sample++)task.SampleContact(0,.1f,.02f,true);
                for(int sample=0;sample<12;sample++)task.SampleContact(0,0,.02f,true);
            }
            Check(task.Strokes==before+1,"One accepted production stroke advances exactly once: "+station+" / "+task.Strokes);
            Present(games);
        }
        private static float KerfBottom(Mesh mesh)
        {
            var vertices=mesh.vertices;var colors=mesh.colors;float bottom=float.PositiveInfinity;
            for(int i=0;i<vertices.Length;i++)if(colors[i].r>.85f && colors[i].g>.6f)bottom=Mathf.Min(bottom,vertices[i].y);
            return bottom;
        }
        private static bool SpansCut(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                for(int j=0;j<3;j++){float x=vertices[triangles[i+j]].x;minimum=Mathf.Min(minimum,x);maximum=Mathf.Max(maximum,x);}
                if(minimum<-.12f && maximum>-.12f)return true;
            }
            return false;
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Workpiece checks use an isolated production career");
            var games=c.HandGames;if(games.Active)games.Cancel();
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.Paused)c.TogglePause();
            if(!c.Ashore)
            {
                c.ReturnToHarbor();yield return null;
                var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Workpiece fixture reaches a supported landing");yield return null;
            }
            Directory.CreateDirectory("Logs/V059");
            foreach(int station in new[]{2,3})
            {
                var input=station==2?FieldItem.Driftwood:FieldItem.IronIngot;int supply=station==2?2:12;
                int missing=supply-c.Career.Backpack.Count(input);
                if(missing>0)Check(c.Career.Backpack.TryAdd(input,missing),"Fixture supplies real workpiece input: "+input);
                Check(c.Shore.Relocate(games.Approach(station),Quaternion.LookRotation(-games.Station(station).forward)),"Workpiece table has a reachable porch approach: "+station);
                c.View.ResetLook();yield return null;
                FishingV036FarmValidation.Aim(c,games.Station(station).TransformPoint(new Vector3(-.10f,1.02f,0)));yield return null;
                Check(games.Begin(station),"Production workpiece round opens: "+station);
                try
                {
                    var mesh=Shape(games,station);var original=mesh.bounds;var originalVertices=mesh.vertices;
                    Check(mesh.vertexCount>0 && Debris(games,station).vertexCount==0,"The resting material exists with no idle impact particles: "+station);
                    for(int sample=0;sample<40;sample++)games.Task.SampleContact(sample%2==0?-.1f:.1f,sample%2==0?.1f:0,.02f,false);
                    for(int sample=0;sample<40;sample++)games.Task.SampleContact(0,0,.02f,true);
                    Present(games);
                    bool unchanged=mesh.vertexCount==originalVertices.Length;var stationary=mesh.vertices;
                    for(int i=0;unchanged && i<stationary.Length;i++)unchanged&=stationary[i]==originalVertices[i];
                    Check(games.Task.Strokes==0 && unchanged && Debris(games,station).vertexCount==0,"Air movement and a stationary tool leave both material and particles unchanged: "+station);
                    // A stationary contact above initialized the gesture sample. Restart from a released tool.
                    games.Task.Release();
                    if(station==2)Check(float.IsPositiveInfinity(KerfBottom(mesh)) && SpansCut(mesh),"An untouched plank has a connected lower web and no fresh cut face");
                    if(station==3)
                    {
                        bool recipe=games.ForgeGunParts;Stroke(games,station);
                        Check(mesh.bounds.size.y<original.size.y && Debris(games,station).vertexCount>0,"The first actual hammer blow flattens metal and emits visible sparks");
                        games.SwitchForgeOutput();Present(games);
                        Check(games.ForgeGunParts!=recipe && games.Task.Strokes==0 && Mathf.Abs(mesh.bounds.size.y-original.size.y)<.0001f
                            && Debris(games,station).vertexCount==0,"Changing the forge recipe starts a fresh unworked billet and clears old sparks");
                        games.SwitchForgeOutput();Present(games);
                        Check(games.ForgeGunParts==recipe && games.Task.Strokes==0,"Switching back preserves the original recipe with fresh visual stroke tracking");
                    }
                    var task=games.Task;int beforeInput=c.Career.Backpack.Count(input),beforeOutput=c.Career.Backpack.Count(task.Output);
                    for(int stroke=0;stroke<task.StrokeTarget/2;stroke++)Stroke(games,station);
                    if(station==2)
                    {
                        float kerf=KerfBottom(mesh);
                        Check(kerf>original.min.y+.001f && kerf<1.024f && SpansCut(mesh),"Halfway sawing exposes deeper fresh faces while the lower wood web still joins both halves");
                        Check(Debris(games,station).vertexCount>0,"Accepted saw strokes release visible chips from the kerf");
                    }
                    else
                    {
                        Check(mesh.bounds.size.x>original.size.x && mesh.bounds.size.y<original.size.y,"Halfway forging visibly spreads and flattens the actual billet mesh");
                        Present(games,.04f);var sparks=Debris(games,station).bounds;
                        Check(sparks.min.x<-.12f && sparks.max.x>-.08f,"Impact sparks physically travel out from both sides of the billet");
                    }
                    FishingV016Validation.Capture(c,"Logs/V059",station==2?"wood-half-cut":"iron-half-forged");
                    while(!task.Complete)Stroke(games,station);
                    if(station==2)
                        Check(!SpansCut(mesh) && mesh.bounds.size.x>.66f && Mathf.Abs(KerfBottom(mesh)-original.min.y)<.0001f,"The final saw stroke cuts the lower web completely and visibly separates two wood pieces");
                    else
                    {
                        Check(mesh.bounds.size.x>.329f && mesh.bounds.size.y<.036f,"The completed metal has a longer tapered shape and half its original thickness");
                        float surface=games.Station(station).InverseTransformPoint(games.WorkPoint).y;
                        Check(Mathf.Abs(surface-.995f)<.0001f && Vector3.Distance(games.ToolContact,games.WorkPoint)<.012f,"Physical strike contact and the desktop hammer follow the lowered worked-metal surface");
                    }
                    Check(c.Career.Backpack.Count(input)==beforeInput && c.Career.Backpack.Count(task.Output)==beforeOutput,"Visible processing alone never charges materials or invents output: "+station);
                    Check(task.Claim(c.Career.Backpack) && c.Career.Backpack.Count(input)==beforeInput-(station==2?1:games.ForgeGunParts?6:3)
                        && c.Career.Backpack.Count(task.Output)==beforeOutput+task.OutputCount,"Completed visible work commits exactly one real inventory exchange: "+station);
                    Check(!task.Claim(c.Career.Backpack),"The completed visible round cannot pay twice: "+station);
                    var completed=mesh.bounds;games.Cancel();Present(games);
                    Check(mesh.bounds==completed,"The completed cut or forged shape remains on the table after its reward is claimed: "+station);
                    FishingV016Validation.Capture(c,"Logs/V059",station==2?"wood-cut-through":"iron-forged-sparks");
                }
                finally{games.Cancel();}
                yield return null;
            }
        }
    }
}
