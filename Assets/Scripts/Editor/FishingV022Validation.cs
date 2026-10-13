using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV022Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V022");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V022] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v022-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            for(int i=0;i<35;i++)yield return null;
            Check(c.WaterRoute!=null && c.Letter!=null,"Presentation systems initialized in production scene");
            int oldWalls=0;
            foreach(var wall in Object.FindObjectsOfType<BoxCollider>())if(wall.transform.parent!=null && wall.transform.parent.name=="LevelBounds")
            {oldWalls++;Check(!wall.enabled,"Fishing mode opens the inherited racing boundary");}
            Check(oldWalls==3,"Only the three inherited racing walls were found");
            for(int target=0;target<c.NavigationCount;target++)
            {
                c.ReturnToHarbor();c.SelectNavigation(target);Physics.SyncTransforms();
                bool planned=c.WaterRoute.Plan();Check(planned,"Water route exists from harbor to "+c.NavigationName+" / "+c.WaterRoute.Failure);
                VerifyRoute(c);yield return null;
            }
            for(int island=0;island<4;island++)
            {
                c.ReturnToHarbor();Place(c,FishingIslandWorld.Berth(island)+Vector3.forward*10);
                c.SelectNavigation(c.IslandNavigationIndex((island+2)%4));
                Check(c.WaterRoute.Plan(),"Cross-island route detours around real coast "+island);VerifyRoute(c);yield return null;
            }
            c.ReturnToHarbor();c.SelectNavigation(1);for(int i=0;i<20;i++)yield return null;
            c.View.ResetLook();c.PresentCharacterFrame();
            var guide=c.transform.Find("V022 water guidance");
            Check(c.WaterRoute.Visible && guide.GetComponentsInChildren<LineRenderer>().Length==15,"Bounded water guide, arrow pool and arrival ring");
            foreach(var line in guide.GetComponentsInChildren<LineRenderer>())Check(line.sharedMaterial.shader.isSupported,"Guidance shader compiled: "+line.name);
            FishingV016Validation.Capture(c,evidence,"navigation-first-person");
            c.ToggleView();for(int i=0;i<8;i++)yield return null;FishingV016Validation.Capture(c,evidence,"navigation-third-person");c.ToggleView();
            Place(c,c.WaterRoute.Points[c.WaterRoute.Points.Count-1]);yield return null;
            Check(c.WaterRoute.Arrived,"Arrival recognized at clear water endpoint");
            c.ReturnToHarbor();Place(c,FishingIslandWorld.Berth(0));for(int i=0;i<25;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Island letter landing remains reachable");yield return null;
            var letter=c.Islands.WorldRoot.GetChild(0).Find("Voyage letter");
            Check(c.Shore.Ground(letter.position+Vector3.forward*.5f,out var feet) && c.Shore.Relocate(feet,Quaternion.Euler(0,180,0)),"Stand at actual letter with supported feet");
            c.PresentCharacterFrame();yield return null;int coins=c.Career.Coins;
            Check(c.Islands.InteractAt(0) && c.Letter.Open && c.Career.Adventure.Visited(0) && c.Career.Coins==coins+10,"Actual letter opens presentation and pays discovery once");
            var before=c.Shore.Position;c.Shore.Move(Vector2.up,true,true,false,.1f);c.Shore.RoomStep(Vector3.forward*.08f);
            Check(c.Shore.Position==before && !c.RequestWorldInteraction() && !c.Islands.InteractAt(0),"Reading blocks desktop/room-step movement and duplicate interactions");
            c.Letter.Tick(.45f,false,false);c.PresentCharacterFrame();
            Check(c.FieldWorld.PresentedItem==FieldItem.Empty && c.Shore.Character.FirstPersonSkin.forceRenderingOff,"Reading stows field book and clears near-eye body occlusion");
            FishingV016Validation.Capture(c,evidence,"letter-rise");
            float age=c.Letter.Age;c.Letter.Tick(float.NaN,false,false);Check(c.Letter.Age==age,"Nonfinite animation time ignored");
            c.Letter.Tick(1.35f,false,false);Check(Quaternion.Angle(c.Letter.Flap.localRotation,Quaternion.identity)>20,"Envelope flap rotates on a real hinge");
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,evidence,"letter-flap");
            c.Letter.Tick(1.3f,false,false);c.PresentCharacterFrame();FishingV016Validation.Capture(c,evidence,"letter-extract");
            float until=Time.unscaledTime+1;while(Time.unscaledTime<until)yield return null;
            Check(c.Letter.Ready && c.Letter.Paper.localScale.y>.001f,"Production Update completes extraction into readable paper");
            FishingV016Validation.Capture(c,evidence,"letter-read");
            var xrMenu=(FishingPressEdge)typeof(FishingXRNotebook).GetField("_menu",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(c.XRNotebook);
            xrMenu.Step(true,false);c.Letter.Close();
            Check(!xrMenu.Step(true,true),"Closing a letter clears the actual XR notebook edge so held B cannot reopen it");
            yield return null;yield return null;
            Check(letter.GetComponent<Renderer>().enabled,"Closing restores the original letter marker");
            Check(c.Islands.InteractAt(0) && c.Career.Coins==coins+10,"Rereading does not pay twice");c.Letter.Reveal();Check(c.Letter.Ready,"Skip reaches completed paper");
            c.ReturnToHarbor();yield return null;Check(!c.Letter.Open && !c.Ashore,"Rescue closes letter and restores boat input");
            Place(c,c.Shore.Sites[0].Berth);for(int i=0;i<25;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Maintenance record landing remains reachable");yield return null;
            Check(c.Shore.Relocate(c.Shore.Sites[0].Tasks[0],Quaternion.identity),"Stand beside maintenance record");
            c.PresentCharacterFrame();yield return null;
            Check(c.Expedition.InteractAt(0) && c.Letter.Open && c.Career.Expedition.Has(FishingExpedition.RecordRead),"Maintenance record uses shared reading presentation and preserves quest progress");
            c.Letter.Reveal();c.ReturnToHarbor();yield return null;
            int recordCoins=c.Career.Coins,recordFacts=c.Career.Expedition.Facts;
            c.ToggleJournal();
            Check(c.JournalOpen && c.Letter.CanRead && c.Expedition.ReadRecordFromJournal() && !c.JournalOpen && c.Letter.Open,"Journal rereads an already-read maintenance record from the boat");
            Check(c.Career.Coins==recordCoins && c.Career.Expedition.Facts==recordFacts,"Record reread does not mutate quest facts or rewards");
            c.Letter.Close();yield return null;yield return null;
            int pendingGrams=FishingSpecies.Grams(FishKind.Coral,.5f);
            Check(c.Career.BeginPending(new CatchRecord{id="v022-read-guard",kind=FishKind.Coral,grams=pendingGrams,value=FishingSpecies.Value(FishKind.Coral,false,pendingGrams)},0,c.spawnPosition.x,c.spawnPosition.z),"Create a valid pending catch through the production transaction");
            c.RestorePendingFishVisual();c.ToggleJournal();
            Check(c.JournalOpen && c.Paused && !c.Letter.CanRead,"All reading buttons are disabled while a pending catch needs settlement");
            Check(!c.Expedition.ReadRecordFromJournal() && c.JournalOpen && c.Paused && c.HasPendingCatch && !c.Letter.Open,"Blocked reading preserves the open journal and pending fish");
            c.ToggleJournal();Check(c.ResolveCatch(false) && c.Letter.CanRead,"Settling the pending catch restores reading eligibility");yield return null;
            foreach(int id in new[]{0,1,2,3,5})
            {
                var npc=c.Residents.Person(id);var group=npc.GetComponent<LODGroup>();
                Check(group!=null && group.lodCount==2,"Resident has authored near/far LODs "+id);
                foreach(var lod in group.GetLODs())foreach(var renderer in lod.renderers)
                    Check(renderer.sharedMaterial.GetTexture("_BaseMap")!=null && renderer.sharedMaterial.GetTexture("_BumpMap")!=null,"Resident retains skin, hair, eyes and fabric atlas "+id);
                var p=npc.position+new Vector3(0,1.2f,2.8f);c.viewCamera.transform.SetPositionAndRotation(p,Quaternion.LookRotation(npc.position+Vector3.up*.9f-p));
                FishingV016Validation.Capture(c,evidence,"resident-"+id);
            }
            var owned=new System.Collections.Generic.List<Object>();
            for(int i=0;i<FishingSpecies.Count;i++)
            {
                var kind=(FishKind)i;Check(Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail")!=null,"Model thumbnail exists for catalog species "+kind);
                var fish=FishingArt.Fish(c.transform,owned,c.PaletteShader,kind);Check(fish.GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"Actual fish mesh retained "+kind);Object.Destroy(fish);
            }
            foreach(var item in owned)Object.Destroy(item);
            Debug.Log("[V022] EVIDENCE: "+evidence);c.ReturnToHarbor();
        }
        private static void VerifyRoute(FishingPrototype c)
        {
            var points=c.WaterRoute.Points;Check(points.Count>=2,"Route contains a departure and destination");
            Check(FishingPrototype.FlatDistance(points[points.Count-1],c.NavigationPosition)<=8.1f,"Route terminates within waypoint approach range");
            for(int i=1;i<points.Count;i++)Check(c.WaterRoute.ClearSegment(points[i-1],points[i]),"Hull clearance on route segment "+i);
        }
        private static void Place(FishingPrototype c,Vector3 p)
        {
            var rb=c.Player.engine.RB;rb.position=p+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();c.View.ResetLook();
        }
    }
}
