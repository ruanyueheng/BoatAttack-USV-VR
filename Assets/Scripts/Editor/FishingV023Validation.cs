using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV023Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V023");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V023] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v023-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            for(int i=0;i<35;i++)yield return null;
            Check(c.Home!=null && c.Home.House!=null && c.Home.Bed!=null && c.Prologue!=null,"Furnished home and opening cinema exist in production scene");
            Check(c.Home.House.GetComponentsInChildren<MeshFilter>().Length>=29,"At least 29 actual furniture and shell instances");
            Check(c.Home.House.GetComponentsInChildren<BoxCollider>().Length>=25,"Home has structural and furniture collisions");
            foreach(var mesh in c.Home.House.GetComponentsInChildren<MeshFilter>())
                Check(mesh.GetComponent<Renderer>().sharedMaterial.shader==c.PaletteShader,"Furnished prefab preserves actual vertex palette: "+mesh.name);
            var pick=FishingV023Art.ItemMesh(FieldItem.IronPickaxe);
            var sourcePick=Resources.Load<GameObject>("FishingUserTools/Pickaxe");var sourceFilter=sourcePick.GetComponentInChildren<MeshFilter>();
            Debug.Log("[V023] AXES: "+pick.bounds+" min="+pick.bounds.min+" max="+pick.bounds.max+" scale="+sourceFilter.transform.lossyScale+" rotation="+sourceFilter.transform.eulerAngles);
            Debug.Log("[V023] SHELL AXES: "+Resources.Load<GameObject>("FishingV023/Home/HouseShellView").GetComponentInChildren<MeshFilter>().sharedMesh.bounds);
            Debug.Log("[V023] SPEAR AXES: "+FishingV023Art.ItemMesh(FieldItem.HuntingSpear).bounds);
            Check(FishingV023Art.ItemMesh(FieldItem.HuntingSpear).bounds.max.z>.91f,"Spear's visible tip shares the positive-Z gameplay contact axis");
            Check(Resources.Load<GameObject>("FishingV023/Home/HouseShellView").GetComponentInChildren<MeshFilter>().sharedMesh.bounds.min.z<-7.9f,"Visible porch shares the negative-Z entrance and floor colliders");
            Check(pick!=null && Mathf.Abs(pick.bounds.size.y-.58f)<.01f && pick.bounds.Contains(FishingFieldWorld.PickTip) && pick.bounds.Contains(FishingFieldWorld.PickPalm),"Supplied pick retains real metre grip and contact axes");
            foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))
            {
                // Appended hunting/forge items use the supplied textured rifle and
                // code-native pictograms, not the v0.23 photographed asset catalog.
                if((int)item>=(int)FieldItem.HuntingRifle)
                {
                    Check(FishingItemIcons.Get(item)!=null,"Pictogram for appended hunting/forge item "+item);
                    if(item==FieldItem.HuntingRifle)
                        Check(c.RifleHunting!=null && c.RifleHunting.Rifle!=null,"Supplied textured rifle resolves outside vertex-colour catalog");
                    if(item==FieldItem.Forge)
                    {
                        var forgeMesh=FishingWorkshopArt.Make(item);
                        Check(forgeMesh.vertexCount>20 && forgeMesh.colors32.Length==forgeMesh.vertexCount,"Code-native forge geometry and palette");
                        Object.Destroy(forgeMesh);
                    }
                    continue;
                }
                if(item==FieldItem.Empty)continue;var mesh=FishingV023Art.ItemMesh(item);var photo=FishingV023Art.Icon(item);
                Check(mesh!=null && mesh.vertexCount>20 && mesh.colors32.Length==mesh.vertexCount,"Real vertex-colour item geometry "+item);
                Check(photo!=null && photo.width==256 && photo.height==256,"Matching actual model photograph "+item);
            }
            for(int i=0;i<FishingSpecies.Count;i++)
            {
                var kind=(FishKind)i;var source=Resources.Load<GameObject>("FishingFish/"+kind);
                Check(source!=null && Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail")!=null,"Every catalog species has a complete imported model and photograph "+kind);
                var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(mesh.triangles.Length/3<=5000,"Marine runtime triangle budget "+kind);
                if(i>=12)Check(Mathf.Abs(Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z)-FishingSpecies.LengthMeters(kind))<.002f,"New fauna true metre dimensions "+kind);
            }
            c.SelectNavigation(c.HomeNavigationIndex);Check(c.NavigationName=="回潮渔屋" && c.WaterRoute.Plan(),"Home is a reachable water waypoint");
            c.ReturnToHarbor();c.SelectNavigation(c.HomeNavigationIndex);PlaceBoat(c,c.Home.Berth);for(int i=0;i<25;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Actual home coast landing is usable");yield return null;
            foreach(var approach in new[]{c.Home.BoardwalkStart,c.Home.Entry})
            {
                Debug.Log("[V023] WALK START: feet="+c.Shore.Position+" target="+approach+" free="+c.Shore.FreeLanding);
                for(int frame=0;frame<650 && FishingPrototype.FlatDistance(c.Shore.Position,approach)>.65f;frame++)
                {
                    var heading=Vector3.ProjectOnPlane(approach-c.Shore.Position,Vector3.up);
                    c.viewCamera.transform.rotation=Quaternion.LookRotation(heading);
                    c.Shore.Move(Vector2.up,false,false,false,.05f);
                    if(frame%100==0)Debug.Log("[V023] WALK: "+frame+" feet="+c.Shore.Position+" message="+c.VisibleMessage+" swimming="+c.Shore.Swimming);
                    yield return null;
                }
                Check(!c.Shore.Swimming && FishingPrototype.FlatDistance(c.Shore.Position,approach)<.75f,"Production character walks the real coast/boardwalk leg / "+c.Shore.Position);
            }
            Check(c.Shore.Relocate(c.Home.Entry,Quaternion.identity),"Walkable entrance with character clearance");
            c.View.ResetLook();
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,evidence,"home-entry");
            var points=new[]{c.Home.Origin+new Vector3(-1.6f,.20f,-3),c.Home.Origin+new Vector3(0,.20f,0),c.Home.WakePoint,c.Home.Origin+new Vector3(1,.20f,2)};
            foreach(var point in points)Check(c.Shore.Ground(point,out var ground) && c.Shore.Clearance(ground),"Room has supported unobstructed walking point "+point);
            Check(c.Shore.Relocate(c.Home.WakePoint,Quaternion.Euler(0,-90,0)),"Reach the actual bedside");c.View.ResetLook();yield return null;yield return null;
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,evidence,"home-bedroom");
            double before=c.Career.Calendar.TotalMinutes;int catches=c.Career.Catches,coins=c.Career.Coins;
            Check(c.Home.CanSleep && c.RequestWorldInteraction() && c.Home.Sleeping,"E interaction at bed begins sleep through production input route");
            var feet=c.Shore.Position;c.Shore.Move(Vector2.up,true,true,false,.1f);Check(c.Shore.Position==feet && !c.RequestWorldInteraction(),"Sleep blocks walking and duplicate E");
            c.Home.Tick(.25f);c.Home.CancelSleep();yield return null;yield return null;
            Check(c.Career.Calendar.TotalMinutes==before,"Sleep cancelled before black-screen commitment keeps calendar unchanged");
            Check(c.Home.Sleep(),"Sleep can be restarted after cancellation");
            for(int i=0;i<11;i++)c.Home.Tick(.25f);
            Check(!c.Home.Sleeping && c.Career.Calendar.Hour==6 && c.Career.Calendar.TotalMinutes>before,"Sleep completes to the next morning once");
            double morning=c.Career.Calendar.TotalMinutes;c.Home.Tick(1);
            Check(c.Career.Calendar.TotalMinutes==morning && c.Career.Catches==catches && c.Career.Coins==coins,"Completed sleep neither repeats time advance nor grants rewards");
            yield return null;yield return null;
            for(int i=12;i<FishingSpecies.Count;i++)
            {
                var kind=(FishKind)i;int grams=FishingSpecies.Grams(kind,.5f);
                Check(c.Career.BeginPending(new CatchRecord{id="v023-live-"+i,kind=kind,rare=true,grams=grams,value=FishingSpecies.Value(kind,true,grams)},0,0,0),"Stage a genuine new creature catch "+kind);
                c.RestorePendingFishVisual();
                Check(!c.Home.CanSleep && !c.Prologue.CanBegin,"Pending creature guards bed and opening "+kind);
                Check(c.MeasureCatch() && c.ResolveCatch(false) && c.Career.Discovered(kind,true),"Actual measure/release records new creature "+kind);yield return null;
            }
            for(int i=0;i<3;i++)yield return null;
            c.SetDayNight(false);yield return null;
            Check(c.Prologue.Begin() && c.StoryBlocksGameplay,"Replay starts the real six-shot CG");
            before=c.Career.Calendar.TotalMinutes;feet=c.Shore.Position;
            c.Shore.Move(Vector2.up,true,true,false,.1f);c.Shore.RoomStep(Vector3.forward*.08f);c.ToggleInventory();c.ToggleJournal();
            Check(c.Shore.Position==feet && !c.InventoryOpen && !c.JournalOpen && !c.RequestWorldInteraction() && !c.CanSetTime,"CG blocks desktop, room step, tools, menus and clock changes");
            Check(c.Prologue.CinemaCamera.stereoTargetEye==StereoTargetEyeMask.None,"Film camera cannot take over tracked stereo views");
            for(int shot=0;shot<FishingPrologue.ShotCount;shot++)
            {
                float target=shot*FishingPrologue.ShotSeconds+1.5f;
                while(c.Prologue.Age<target)c.Prologue.Tick(.25f,false);
                c.Prologue.Present();Check(c.Prologue.Shot==shot,"Opening shot "+shot+" reached with bounded animation steps");
                CaptureCamera(c.Prologue.CinemaCamera,Path.Combine(evidence,"cg-film-"+shot+".png"));
                CaptureCinemaScreen(c,Path.Combine(evidence,"cg-screen-"+shot+".png"));
                for(int f=0;f<4;f++)yield return null;
            }
            c.Prologue.Finish();c.Prologue.Finish();yield return null;yield return null;
            Check(!c.Prologue.Open && c.Career.PrologueSeen && c.Career.Calendar.TotalMinutes==before,"Skipping/finishing CG restores play, records it once and consumes no game time");
            var restored=FishingSaveStore.Load(c.SavePath,out var warning);
            Check(warning=="" && restored.DiscoveryMask==c.Career.DiscoveryMask && restored.DiscoveryMask>int.MaxValue && restored.PrologueSeen,"Unity JSON round trip preserves high discovery bits and the opening flag");
            foreach(var item in new[]{FieldItem.FieldGuide,FieldItem.Binoculars,FieldItem.Lantern,FieldItem.StonePickaxe,FieldItem.IronPickaxe,FieldItem.HuntingSpear,FieldItem.Workbench,FieldItem.Furnace})
            {
                var bag=c.Career.Backpack;int found=-1;
                for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==item){found=i;break;}
                if(found<0){Check(bag.TryAdd(item,1),"Equip fixture adds actual inventory item "+item);for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==item){found=i;break;}}
                if(found!=1)Check(bag.Move(found,1),"Existing inventory transaction equips "+item);
                c.SelectFieldSlot(1);for(int f=0;f<5;f++)yield return null;c.PresentCharacterFrame();
                Check(c.FieldWorld.PresentedItem==item && c.FieldWorld.HeldToolRenderer.enabled,"Upgraded item is actually held "+item);
                Check(Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)<.035f,"Actual right palm reaches its calibrated item socket "+item);
                if(item==FieldItem.FieldGuide || item==FieldItem.Binoculars || item==FieldItem.Workbench || item==FieldItem.Furnace)
                    Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.035f,"Actual left palm supports two-handed item "+item);
                FishingV016Validation.Capture(c,evidence,"held-"+item);
            }
            c.ToggleInventory();ScreenCapture.CaptureScreenshot(Path.Combine(evidence,"backpack-photos.png"));for(int f=0;f<5;f++)yield return null;c.ToggleInventory();
            c.ReturnToHarbor();Debug.Log("[V023] EVIDENCE: "+evidence);
        }
        private static void PlaceBoat(FishingPrototype c,Vector3 p)
        {var rb=c.Player.engine.RB;rb.position=p+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();c.View.ResetLook();}
        private static void CaptureCamera(Camera camera,string path)
        {
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var photo=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;photo.ReadPixels(new Rect(0,0,1280,720),0,0);photo.Apply();File.WriteAllBytes(path,photo.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(photo);}
        }
        private static void CaptureCinemaScreen(FishingPrototype c,string path)
        {
            var screen=c.Prologue.CinemaScreen;var mode=screen.renderMode;var camera=screen.worldCamera;float plane=screen.planeDistance;
            int mask=c.viewCamera.cullingMask;
            try
            {
                c.Prologue.CinemaCamera.Render();screen.renderMode=RenderMode.ScreenSpaceCamera;screen.worldCamera=c.viewCamera;
                screen.planeDistance=c.viewCamera.nearClipPlane+.10f;c.viewCamera.cullingMask|=1<<5;Canvas.ForceUpdateCanvases();
                CaptureCamera(c.viewCamera,path);
            }
            finally{screen.renderMode=mode;screen.worldCamera=camera;screen.planeDistance=plane;c.viewCamera.cullingMask=mask;Canvas.ForceUpdateCanvases();}
        }
    }
}
