using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV039Validation
    {
        public static void Run(){FishingV039Art.Prepare();FishingV011Validation.Begin("V039");}
        public static void RunHome()=>FishingV011Validation.Begin("V039-HOME");
        public static void RunRealm()=>FishingV011Validation.Begin("V039-REALM");
        public static IEnumerator Realm(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V039");
            var run=FishingV035Levels.Island(c,2,Path.GetFullPath("Logs/V039"));while(run.MoveNext())yield return run.Current;
        }
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[V039] CHECK: "+text);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V039");
            c.ReturnToHarbor();yield return null;
            var berth=FishingIslandWorld.Berth(2)+Vector3.up;var rb=c.Player.engine.RB;
            rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=berth;rb.rotation=Quaternion.identity;
            c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            float until=Time.time+.5f;while(Time.time<until)yield return null;
            Check(c.Shore.TryDisembark(),"Production shipwreck island landing");yield return null;
            c.Career.CompleteShrine(2);
            Check(c.Shore.Relocate(c.Realms.PortalApproach(2),Quaternion.identity),"Completed-save entrance footing");
            Check(c.Realms.Enter(2),"Re-enter completed shipwreck using production transition");
            while(c.Realms.Transitioning)yield return null;yield return null;yield return null;
            var r=c.Realms;
            var completedBridge=r.Room.Find("FloatGangway");var completedDeck=completedBridge.GetComponent<BoxCollider>();
            Check(c.Career.Lore.Cleared(2) && !r.Trial.Ready && completedDeck.enabled
                && r.TryWater(completedBridge.position,out float completedWater,out _)
                && completedBridge.TransformPoint(completedDeck.center+Vector3.up*completedDeck.size.y*.5f).y>=completedWater,
                "A previously completed save intentionally reopens the bridge above water before repeating the trial");
            for(float x=-.9f;x<1;x+=.9f)for(float z=6.6f;z<15;z+=.1f)
            {
                var seed=r.Room.TransformPoint(new Vector3(x,1.8f,z));
                if(!c.Shore.Ground(seed,out var floor) || Mathf.Abs(floor.y-seed.y)>.25f)
                    Debug.Log("[V039] SUPPORT GAP local="+new Vector3(x,1.8f,z)+" floor="+r.Room.InverseTransformPoint(floor));
            }
            Check(c.Shore.Relocate(r.Room.TransformPoint(new Vector3(-4.2f,1.8f,4)),Quaternion.identity),"Upper side-deck fixture footing");
            foreach(var p in new[]{new Vector3(-4.2f,1.8f,7),new Vector3(0,1.8f,7)})
            {var walk=FishingV035Levels.Walk(c,r.Room.TransformPoint(p));while(walk.MoveNext())yield return walk.Current;}
            foreach(float x in new[]{0f,-.85f,.85f,-1.12f,1.12f})foreach(float dt in new[]{.016f,.05f})
            {
                foreach(var p in new[]{new Vector3(x,1.8f,7),new Vector3(-x,1.8f,15.6f),new Vector3(x,1.8f,7)})
                {
                    var target=r.Room.TransformPoint(p);
                    for(int i=0;i<900 && FishingPrototype.FlatDistance(c.Shore.Position,target)>.09f;i++)
                    {
                        var delta=Vector3.ProjectOnPlane(target-c.Shore.Position,Vector3.up);
                        c.viewCamera.transform.rotation=Quaternion.LookRotation(delta);
                        c.Shore.Move(Vector2.up,false,false,false,Mathf.Min(dt,delta.magnitude/2.6f));
                        if(i%5==0)yield return null;
                    }
                    Check(!c.Shore.Swimming && FishingPrototype.FlatDistance(c.Shore.Position,target)<.15f,"Bridge both joins, diagonal lane="+x+" dt="+dt+" target="+p+" actual="+r.Room.InverseTransformPoint(c.Shore.Position));
                }
            }
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/V039","bridge-crossing");
            Check(c.Shore.Relocate(r.Room.TransformPoint(new Vector3(0,1.88f,11)),Quaternion.identity),"Actual floating deck supports railing fixture");
            c.viewCamera.transform.rotation=Quaternion.LookRotation(r.Room.TransformDirection(Vector3.right));
            for(int i=0;i<65;i++){c.Shore.Move(Vector2.up,false,false,false,.05f);if(i%5==0)yield return null;}
            Check(r.Room.InverseTransformPoint(c.Shore.Position).x<1.3f && !c.Shore.Swimming,"Actual capsule cannot walk through the floating bridge side rope");
            var homeChecks=Home(c);while(homeChecks.MoveNext())yield return homeChecks.Current;
        }
        public static IEnumerator Home(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V039");
            c.ReturnToHarbor();yield return null;
            var home=c.Home;var boat=c.Player.engine.RB;var berth=home.Berth+Vector3.up*.7f;
            boat.position=berth;boat.rotation=Quaternion.identity;boat.velocity=boat.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Production home island landing");
            CheckGrounding(c);
            var fence=home.Origin+new Vector3(18.4f,0,-10.4f);
            Check(c.Shore.Relocate(fence,Quaternion.identity),"Garden fence approach has real footing");
            c.viewCamera.transform.rotation=Quaternion.identity;
            for(int i=0;i<65;i++){c.Shore.Move(Vector2.up,false,false,false,.05f);if(i%5==0)yield return null;}
            Check(c.Shore.Position.z<home.Origin.z-9.55f,"Garden fence stops the capsule between its main posts");
            Check(c.Shore.Relocate(home.Origin+new Vector3(9.8f,0,4.5f),Quaternion.identity),"Garden entrance approach remains open");
            var gate=FishingV035Levels.Walk(c,home.Origin+new Vector3(12.2f,0,4.5f));while(gate.MoveNext())yield return gate.Current;
            c.PresentCharacterFrame();c.viewCamera.transform.rotation=Quaternion.LookRotation(home.Origin+new Vector3(23,.4f,-7)-c.viewCamera.transform.position);
            FishingV016Validation.Capture(c,"Logs/V039","garden-grounding");
            CaptureAt(c,home.Origin+new Vector3(32,2.6f,-14),home.Origin+new Vector3(27,.2f,-8),"garden-feet-close");
            var coast=NaturalShore(c);while(coast.MoveNext())yield return coast.Current;
            c.ReturnToHarbor();yield return null;
            var craft=FishingV035Validation.Checks(c);while(craft.MoveNext())yield return craft.Current;
            var barbecue=FishingV028CoastValidation.Checks(c);while(barbecue.MoveNext())yield return barbecue.Current;
        }
        private static IEnumerator NaturalShore(FishingPrototype c)
        {
            Vector3 bank=Vector3.zero;bool found=false;
            foreach(float z in new[]{5f,10f,15f,0f,-10f,-15f,-5f,-1f,6f,8f,12f})
            {
                for(float x=34;x<48;x+=.2f)
                {
                    var seed=c.Home.Origin+new Vector3(x,0,z);seed.y=0;
                    if(!c.Shore.Ground(seed,out var floor) || floor.y<.65f || floor.y>.85f || !c.Shore.SafeCoastalFooting(floor) || !c.Shore.Clearance(floor))continue;
                    var blockers=Physics.SphereCastAll(floor+Vector3.up*.36f,c.Shore.Body.radius,Vector3.right,6.5f,~(1<<4),QueryTriggerInteraction.Ignore);
                    if(blockers.Any(h=>h.collider!=c.Shore.Body && !h.transform.IsChildOf(c.Shore.Actor) && !FishingShore.NonSolidSurface(h.collider)))continue;
                    bank=floor;found=true;break;
                }
                if(found)break;
            }
            Check(found,"Locate a real low sandy home beach with capsule clearance");
            Check(c.Shore.Relocate(bank,Quaternion.identity),"Natural shoreline fixture starts on authored terrain");
            for(int i=0;i<75;i++)
            {c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.right);c.Shore.Move(Vector2.up,false,false,false,.05f);yield return null;}
            Check(c.Shore.Swimming,"Walking out from the actual beach enters swimming");
            for(int i=0;i<260 && c.Shore.Swimming;i++)
            {c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(bank-c.Shore.Position,Vector3.up));c.Shore.Move(Vector2.up,false,false,false,.05f);yield return null;}
            if(c.Shore.Swimming)
            {
                float water=(float)typeof(FishingShore).GetMethod("WaterHeight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(c.Shore,null);
                var grab=c.Shore.Position+Vector3.ProjectOnPlane(bank-c.Shore.Position,Vector3.up).normalized*.45f;
                Debug.Log("[V039] SHORE DIAGNOSTIC water="+water+" feet="+c.Shore.Position+" grab="+grab);
                foreach(var hit in Physics.RaycastAll(new Vector3(grab.x,water+1.7f,grab.z),Vector3.down,2.6f,~(1<<4),QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider==c.Shore.Body)continue;
                    Debug.Log("[V039] SHORE HIT "+hit.collider.name+" point="+hit.point+" normal="+hit.normal+" four="+c.Shore.SafeCoastalFooting(hit.point,water-.65f)+" clearance="+c.Shore.Clearance(hit.point));
                    var start=c.Shore.Position+Vector3.up*.85f;start.y=Mathf.Max(start.y,water+.25f);var delta=hit.point+Vector3.up*.85f-start;
                    foreach(var wall in Physics.SphereCastAll(start,c.Shore.Body.radius,delta.normalized,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore))
                        if(wall.collider!=c.Shore.Body)Debug.Log("[V039] SHORE EXIT BLOCKER "+wall.collider.name+" "+wall.point+" "+wall.normal);
                }
            }
            Check(!c.Shore.Swimming && c.Shore.Clearance(c.Shore.Position),"Swimming onto the real beach becomes walking without jump input; bank="+bank+" actual="+c.Shore.Position);
            for(int i=0;i<90;i++){c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.left);c.Shore.Move(Vector2.up,false,false,false,.05f);yield return null;}
            bool dry=c.Shore.Ground(c.Shore.Position,out var dryFloor);
            if(!dry)
            {
                foreach(var hit in Physics.RaycastAll(c.Shore.Position+Vector3.up*80,Vector3.down,100,~(1<<4),QueryTriggerInteraction.Ignore))
                    if(hit.collider!=c.Shore.Body)Debug.Log("[V039] DRY DIAGNOSTIC "+hit.collider.name+" parent="+hit.transform.parent?.name+" point="+hit.point+" normal="+hit.normal);
                c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/V039","shore-diagnostic");
            }
            Check(!c.Shore.Swimming && dry && Mathf.Abs(c.Shore.Position.y-dryFloor.y)<.2f,"After the shallow transition, ordinary walking reaches the dry beach; actual="+c.Shore.Position+" ground="+dryFloor+" grounded="+c.Shore.Grounded+" swimming="+c.Shore.Swimming);
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/V039","natural-swim-exit");
        }
        private static void CheckGrounding(FishingPrototype c)
        {
            var land=c.Home.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();
            var scenery=c.Home.transform.Find("V035 Home HomeLandscaping").GetComponentInChildren<MeshFilter>();
            foreach(var local in new[]{new Vector3(29,0,-9.4f),new Vector3(29,0,10)})
            {
                var seed=c.Home.Origin+local;
                Check(land.Raycast(new Ray(seed+Vector3.up*30,Vector3.down),out var floor,60),"Grounding fixture hits actual home terrain: "+local);
                float bottom=float.MaxValue;int vertices=0;
                foreach(var vertex in scenery.sharedMesh.vertices)
                {
                    var p=scenery.transform.TransformPoint(vertex);
                    if(Mathf.Abs(p.x-seed.x)>.13f || Mathf.Abs(p.z-seed.z)>.13f)continue;
                    vertices++;bottom=Mathf.Min(bottom,p.y);
                }
                Check(vertices>3 && bottom<=floor.point.y+.035f && bottom>floor.point.y-.24f,"Actual fence mesh intersects its ground: "+local+" bottom="+bottom+" terrain="+floor.point.y);
            }
            foreach(Transform plant in c.Home.transform.Find("V035 Home flowers and orchard"))
                Check(land.Raycast(new Ray(plant.position+Vector3.up*30,Vector3.down),out var hit,60) && Mathf.Abs(plant.position.y-hit.point.y)<.025f,"Plant root follows actual terrain: "+plant.name);
        }
        public static IEnumerator InspectBarbecue(FishingPrototype c)
        {
            float until=Time.time+1.3f;while(Time.time<until)yield return null;
            var effect=c.CoastWorld.Grill.GetComponent<FishingBarbecueEffects>();
            var audio=effect.GetComponent<AudioSource>();
            var smoke=effect.GetComponentsInChildren<ParticleSystem>().Single(x=>x.name=="Soft curling grill smoke");
            Check(audio.clip!=null && audio.clip.length>3.8f && audio.isPlaying && audio.spatialBlend==1,"Recorded sizzle loops at the cooking grill in 3D");
            Check(c.WorldAudio!=null && c.WorldAudio.LoopSource("barbecue")==null,"V039 grill owns its cooking sound without a duplicate world-audio loop");
            Check(smoke.isPlaying && smoke.particleCount>0 && smoke.particleCount<=64,"Real cooking produces bounded smoke particles; playing="+smoke.isPlaying+" count="+smoke.particleCount);
            var fish=c.CoastWorld.Grill.Find(FishingV028CoastWorld.CookedFishName);
            Check(fish!=null && fish.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length/3<=6100 && fish.GetComponentInChildren<Renderer>().sharedMaterial.mainTexture!=null,"Production grill uses textured Textured fish within the mobile triangle budget");
            Check(FishingV023Art.ItemMesh(FieldItem.GrilledFish)==fish.GetComponentInChildren<MeshFilter>().sharedMesh && FishingUserTools.Material(FieldItem.GrilledFish).mainTexture!=null && FishingV023Art.Icon(FieldItem.GrilledFish)==Resources.Load<Texture2D>("FishingV039/GrilledFishThumbnail"),"Backpack, held fish and icon all use the same Textured asset");
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/V039","textured-fish-smoke");
            CaptureAt(c,c.CoastWorld.Grill.position+new Vector3(1.5f,1.8f,-2.3f),c.CoastWorld.CookingPoint+Vector3.up*.4f,"textured-grill-overview");
            CaptureAt(c,c.CoastWorld.Grill.position+new Vector3(.65f,1.45f,-.85f),c.CoastWorld.CookingPoint+Vector3.up*.1f,"textured-grill-close");
            c.TogglePause();yield return null;yield return null;
            Check(!audio.isPlaying && !smoke.isPlaying,"Pause stops the grill loop and smoke emission");
            c.TogglePause();yield return null;yield return null;
        }
        private static void CaptureAt(FishingPrototype c,Vector3 eye,Vector3 target,string name)
        {
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;
            try{camera.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));FishingV016Validation.Capture(c,"Logs/V039",name);}
            finally{camera.SetPositionAndRotation(position,rotation);}
        }
    }
}
