using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV028CoastValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V028-COAST");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V028-COAST] CHECK: "+label);}
        private static CatchRecord Fish(string id,FishKind kind)=>new CatchRecord{id=id,kind=kind,grams=FishingSpecies.Grams(kind,.5f),value=FishingSpecies.Value(kind,false,FishingSpecies.Grams(kind,.5f))};
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Coast validation uses an isolated profile");
            var original=c.Career.Snapshot();var coast=c.CoastWorld;
            string evidence=Path.GetFullPath("Logs/v028-coast-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            try
            {
                c.ReturnToHarbor();yield return null;yield return null;
                var fixture=new FishingCareer();fixture.Store(Fish("v028-bbq-bream",FishKind.SeaBream));fixture.Store(Fish("v028-bbq-mackerel",FishKind.Mackerel));
                c.RestoreShopTestProfile(fixture.Snapshot());
                var empty=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning=="" && empty.Count==2 && empty.Barbecue.fish==null,"An empty grill survives the actual JsonUtility disk save/load");
                foreach(string name in new[]{"Barbecue","FishCooler","GrilledFish","OldBoot","RustyCan","SeaTreasure"})
                {
                    var source=Resources.Load<GameObject>("FishingV028/Coast/"+name);var mesh=source==null?null:source.GetComponentInChildren<MeshFilter>();
                    Check(mesh!=null && mesh.sharedMesh.vertexCount>30 && mesh.sharedMesh.colors.Length==mesh.sharedMesh.vertexCount,"Refined metre-scale vertex-colour coast mesh imports: "+name);
                    Check(Resources.Load<Texture2D>("FishingV028/Coast/"+name+"Thumbnail")!=null,"Matching real model photograph imports: "+name);
                }
                Physics.SyncTransforms();
                CheckFeet(coast.Grill,new Vector2(.625f,.39f));CheckFeet(coast.Cooler,new Vector2(.48f,.325f));
                c.SelectNavigation(c.HomeNavigationIndex);PlaceBoat(c,c.Home.Berth);for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Home berth still reaches the V027 island");
                var apron=coast.Grill.position+Vector3.back*1.6f;
                Check(c.Shore.Relocate(apron,Quaternion.identity),"Barbecue front apron has dry ground and capsule clearance");
                c.View.ResetLook();yield return null;yield return null;
                Check(coast.NearGrill,"The actual front apron offers the new grill interaction");
                Check(!coast.StartRoasting("missing-id") && c.Career.Count==2,"An unavailable fish ID cannot consume a fish");
                var blocker=new GameObject("V028 validation sight blocker",typeof(BoxCollider));
                blocker.transform.position=Vector3.Lerp(c.Shore.Position+Vector3.up*c.Shore.EyeHeight,coast.Grill.position+Vector3.up*.75f,.5f);
                blocker.GetComponent<BoxCollider>().size=new Vector3(1.6f,2,.12f);Physics.SyncTransforms();
                Check(!coast.NearGrill && !coast.StartRoasting("v028-bbq-mackerel"),"A solid blocker prevents reaching and starting a barbecue through it");
                UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();
                Check(c.RequestWorldInteraction() && coast.MenuOpen,"Desktop E opens the selected barbecue fishbox menu");
                coast.CloseMenu();yield return null;
                typeof(FishingXRNotebook).GetMethod("InteractTracked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.XRNotebook,null);
                Check(coast.MenuOpen && (bool)typeof(FishingPrototype).GetProperty("HomeInteractionTargetSelected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c),"Tracked A uses the shared immediate home/barbecue target route");
                coast.CloseMenu();c.XRNotebook.Close();yield return null;
                Check(coast.StartRoasting("v028-bbq-mackerel") && c.Career.Count==1 && c.Career.Barbecue.fish.id=="v028-bbq-mackerel","Selected mackerel moves out of the real fishbox into the saved grill");
                var cookingEffects=FishingV039Validation.InspectBarbecue(c);while(cookingEffects.MoveNext())yield return cookingEffects.Current;
                yield return null;yield return null;
                Capture(c,coast.Grill.position+new Vector3(3.8f,2.2f,-4),coast.Grill.position+Vector3.up*.7f,Path.Combine(evidence,"barbecue-raw-fish.png"));
                var saved=FishingSaveStore.Load(c.SavePath,out warning);
                Check(warning=="" && saved.Count==1 && saved.Barbecue.fish.id=="v028-bbq-mackerel","Reserved fish and progress survive an actual disk reload");
                c.TogglePause();float remaining=c.Career.Barbecue.remaining;yield return null;yield return null;
                Check(c.Career.Barbecue.remaining==remaining,"Pause never advances the grill");c.TogglePause();
                coast.OpenMenu();remaining=c.Career.Barbecue.remaining;yield return null;yield return null;
                Check(c.Career.Barbecue.remaining==remaining,"The open fishbox menu never advances cooking");coast.CloseMenu();yield return null;
                for(int i=0;i<72;i++)c.Career.TickBarbecue(.25f);yield return null;
                Capture(c,coast.Grill.position+new Vector3(1.8f,1.8f,-1.9f),coast.CookingPoint,Path.Combine(evidence,"barbecue-cooked-fish.png"));
                Check(c.Career.BarbecueReady && coast.Collect() && !coast.Collect() && c.Career.Backpack.Count(FieldItem.GrilledFish)==1,"Cooked fish is collected once into the shared backpack");
                saved=FishingSaveStore.Load(c.SavePath,out warning);
                Check(warning=="" && saved.Barbecue.fish==null && saved.Backpack.Count(FieldItem.GrilledFish)==1,"A collected empty grill and meal survive the real disk roundtrip");
                Check(c.Shore.Relocate(coast.Grill.position+Vector3.back*4,Quaternion.identity),"The island keeps a clear route away from the grill");yield return null;
                Check(!coast.NearGrill && !coast.StartRoasting("v028-bbq-bream"),"A distant grill cannot remove fish remotely");
                foreach(var item in new[]{FieldItem.OldBoot,FieldItem.RustyCan,FieldItem.SeaTreasure})
                {
                    var visual=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("FishingV028/Coast/"+item));
                    foreach(var renderer in visual.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=coast.Grill.GetComponentInChildren<Renderer>().sharedMaterial;
                    var at=coast.Grill.position+Vector3.up*1.05f;visual.transform.position=at;
                    Capture(c,at+new Vector3(.9f,.7f,-1),at+Vector3.up*.16f,Path.Combine(evidence,"sea-find-"+item+".png"));UnityEngine.Object.DestroyImmediate(visual);
                }
                var jump=c.Ecology.PreviewSeaBeat(false,.5f);Check(jump!=null && jump.position.y>.8f,"A pooled sea fish reaches its visible airborne jump apex");
                Capture(c,jump.position+new Vector3(3,1.5f,-4),jump.position,Path.Combine(evidence,"fish-jump.png"));
                var gull=c.Ecology.PreviewSeaBeat(true,.40f);Check(gull!=null && gull.position.y<.3f,"An existing sky bird completes the fishing dive near the water");
                Capture(c,gull.position+new Vector3(3,1.5f,-4),gull.position,Path.Combine(evidence,"gull-dive.png"));
                gull=c.Ecology.PreviewSeaBeat(true,.60f);
                var prey=gull.Find("Seabird's visible catch");Check(prey!=null && prey.gameObject.activeInHierarchy,"The climbing bird actually carries its visible fish");
                var preySize=prey.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var size=Vector3.Scale(preySize,prey.lossyScale);float length=Mathf.Max(size.x,size.y,size.z);
                Check(length>=.12f && length<=.17f && Vector3.Distance(prey.position,gull.TransformPoint(FishingEcology.GullBeakPoint))<.02f,"The carried fish is 12–17 cm in world space and touches the calibrated supplied gull's beak");
                Capture(c,gull.TransformPoint(new Vector3(1.8f,.6f,2.0f)),gull.TransformPoint(new Vector3(0,0,.1f)),Path.Combine(evidence,"gull-with-fish.png"));
                SeaFindChecks(c);
                Debug.Log("[V028-COAST] EVIDENCE: "+evidence);
            }
            finally
            {
                coast.CloseMenu();c.XRNotebook.Close();if(c.Paused)c.TogglePause();c.ReturnToHarbor();c.RestoreShopTestProfile(original);
            }
        }
        private static void CheckFeet(Transform root,Vector2 half)
        {
            foreach(float x in new[]{-half.x,half.x})foreach(float z in new[]{-half.y,half.y})
            {
                bool supported=false;
                foreach(var hit in Physics.RaycastAll(root.position+new Vector3(x,.25f,z),Vector3.down,.5f,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(root) && !FishingShore.NonSolidSurface(hit.collider) && hit.normal.y>=.69f
                        && Mathf.Abs(hit.point.y-(root.position.y-.015f))<=.18f)supported=true;
                Check(supported,"Dry geometry supports the actual footprint corner of "+root.name+": "+x+", "+z);
            }
        }
        internal static void SeaFindChecks(FishingPrototype c,Action<FieldItem> landed=null)
        {
            c.ReturnToHarbor();
            var fixture=new FishingCareer();fixture.Store(Fish("v028-sea-discovery",FishKind.Coral));fixture.SellAll();
            c.RestoreShopTestProfile(fixture.Snapshot());c.MoveToSpotForAutomation(0);c.Interact();
            foreach(var item in new[]{FieldItem.OldBoot,FieldItem.RustyCan,FieldItem.SeaTreasure})
            {
                CatchRecord receipt=null;
                for(int i=0;i<10000;i++)
                {
                    var candidate=Fish("v028-net-"+item+"-"+i,FishKind.Coral);
                    if(FishingCoastRules.SeaFind(candidate,true)==item){receipt=candidate;break;}
                }
                Check(receipt!=null && c.Cast(),"A real production cast starts the sea-find fixture: "+item);
                var s=c.Session;s.Cancel();
                Check(s.Cast(FishKind.Coral,false,c.Career.LineLevel,receipt.id),"A deterministic ordinary catch receipt selects "+item);
                for(int i=0;i<1500 && s.Phase==CatchPhase.Waiting;i++)s.Tick(.02f,false);
                Check(c.Hook(),"The sea-find fixture hooks through production input");
                for(int i=0;i<6000 && s.Phase==CatchPhase.Fighting;i++)s.Tick(.02f,s.Tension<.6f);
                for(int i=0;i<15;i++)s.Tick(.1f,false);
                Check(c.CatchRig.CaptureAndStoreForAutomation() && !c.HasPendingCatch && c.Career.Count==0
                    && c.Career.Backpack.Count(item)==1,"Actual assisted net capture reports a committed sea find without inventing a pending fish: "+item);
                landed?.Invoke(item);
                Check(!c.CatchRig.CaptureAndStoreForAutomation() && !c.Career.Store(receipt)
                    && c.Career.Backpack.Count(item)==1,"Repeating the scoop or receipt cannot duplicate the sea find: "+item);
                var saved=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning=="" && saved.Backpack.Count(item)==1 && !saved.CollectSeaFind(receipt,item),"The sea-find receipt survives the real disk roundtrip: "+item);
            }
        }
        private static void PlaceBoat(FishingPrototype c,Vector3 point)
        {
            var rb=c.Player.engine.RB;rb.position=point+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();c.View.ResetLook();
        }
        private static void Capture(FishingPrototype c,Vector3 position,Vector3 target,string path)
        {
            var camera=c.viewCamera;var p=camera.transform.position;var q=camera.transform.rotation;float fov=camera.fieldOfView,aspect=camera.aspect;
            var oldTarget=camera.targetTexture;var old=RenderTexture.active;
            var rt=new RenderTexture(1400,1000,24);var photo=new Texture2D(1400,1000,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=48;camera.aspect=1.4f;camera.targetTexture=rt;
                camera.Render();camera.Render();RenderTexture.active=rt;photo.ReadPixels(new Rect(0,0,1400,1000),0,0);photo.Apply();File.WriteAllBytes(path,photo.EncodeToPNG());
            }
            finally{camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=fov;camera.aspect=aspect;camera.targetTexture=oldTarget;RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(photo);}
        }
    }
}
