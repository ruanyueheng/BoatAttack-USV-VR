using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingHomeEcologyValidation
    {
        public static void Run()=>FishingV011Validation.Begin("HOME-ECOLOGY");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("[HOME-ECOLOGY] "+label);Debug.Log("[HOME-ECOLOGY] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.HomeEcology!=null,"Ecology uses the isolated production world and career");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;var berth=c.Home.Berth+Vector3.up*.7f;rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"The real berth allows home access");yield return null;
            var world=c.HomeEcology;
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(.4f,.2f,-1.5f),Quaternion.identity),"The aquarium has supported standing access");
            c.View.ResetLook();yield return null;
            Check(world.NearHome && c.Career.Backpack.TryAdd(FieldItem.Aquarium,1),"A real aquarium kit is present near the home");
            world.Choices(0)[0].Run();Check(c.Career.AquariumLevel==1,"The actual facility action consumes its kit and builds the aquarium");
            Check(c.Career.Backpack.Count(FieldItem.Aquarium)==0 && world.transform.Find("家园生态展示/家园水族箱")!=null,"The permanent aquarium world display exists after the transaction");
            int grams=FishingSpecies.Grams(FishKind.Coral,.45f);var fish=new CatchRecord{id="ecology-world-fish",kind=FishKind.Coral,grams=grams,value=FishingSpecies.Value(FishKind.Coral,false,grams)};
            Check(c.Career.Store(fish),"A caught fish fixture is in the actual hold");world.Choices(1)[0].Run();
            Check(c.Career.AquariumCount==1 && c.Career.Count==0,"The shared display action moves the unique fish out of the sale hold");
            var displayed=(IList)typeof(FishingHomeEcologyWorld).GetField("_fish",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
            var fishBounds=((Transform)displayed[0]).GetComponentInChildren<Renderer>().bounds;
            float displayLength=Mathf.Max(fishBounds.size.x,fishBounds.size.y,fishBounds.size.z);
            Check(displayLength>=.14f && displayLength<=.39f,"Aquarium fish are readable at tank scale without altering catch records");
            Check(c.Career.Backpack.TryAdd(FieldItem.FishFeed,3),"Feed fixture added");world.Choices(2)[0].Run();
            Check(c.Career.Backpack.Count(FieldItem.FishFeed)==2,"The first daily feeding consumes exactly one feed");world.Choices(2)[0].Run();Check(c.Career.Backpack.Count(FieldItem.FishFeed)==2,"Repeated daily feeding consumes nothing");
            Check(c.RequestWorldInteraction() && c.InventoryOpen,"The real E interaction opens home ecology management");yield return null;
            Directory.CreateDirectory("Logs/HomeEcology");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/HomeEcology/home-ecology-management-full-hud.png"));for(int i=0;i<5;i++)yield return null;
            if(!File.Exists("Logs/HomeEcology/home-ecology-management-full-hud.png"))Debug.Log("[HOME-ECOLOGY] LIMIT: batch Editor did not export the IMGUI screen; native Player menu capture remains pending.");
            c.ToggleInventory();yield return null;
            Check(c.Career.Backpack.TryAdd(FieldItem.CaptureNet,1),"Capture net exists in the actual backpack");
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==FieldItem.CaptureNet){if(i>=9)c.Career.Backpack.QuickMove(i);break;}
            for(int i=0;i<9;i++)if(c.Career.Backpack.At(i).item==FieldItem.CaptureNet){c.SelectFieldSlot(i);break;}
            Check(c.Career.Backpack.Equipped==FieldItem.CaptureNet,"The net can be selected through the existing hotbar");
            for(int i=0;i<60;i++){c.Shore.Move(Vector2.zero,false,false,true,.02f);c.PresentCharacterFrame();yield return null;}
            FishingFieldWorld.Animal rabbit=null;foreach(var a in c.RifleHunting.Targets)if(a.Kind==LandAnimalKind.Rabbit && !a.Dead && a.Root.gameObject.activeInHierarchy){rabbit=a;break;}
            Check(rabbit!=null,"A real wild rabbit source is available");
            // Settle before approaching: waiting inside the .85m alert radius makes a live rabbit flee beyond the net.
            bool placed=false;for(int i=0;i<8;i++){var offset=Quaternion.Euler(0,i*45,0)*Vector3.back;if(c.Shore.Ground(rabbit.Feet+offset*1.0f,out var point) && c.Shore.Relocate(point,Quaternion.LookRotation(-offset))){placed=true;break;}}
            Check(placed,"A supported nearby rabbit approach exists");c.View.ResetLook();
            var rabbitStart=rabbit.Feet;
            c.Shore.Move(Vector2.zero,false,false,true,.02f);c.PresentCharacterFrame();FishingV036FarmValidation.Aim(c,rabbit.Root.position+Vector3.up*.2f);yield return null;
            var staleNet=(Transform)typeof(FishingHomeEcologyWorld).GetField("_net",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
            Check(staleNet!=null && staleNet.gameObject.activeInHierarchy,"The equipped capture net is visible before input");
            staleNet.position+=Vector3.up*10;
            bool captured=world.UseNet();
            Check(Vector3.Distance(staleNet.position,c.ExplorerPosition)<3,"Capture refreshes the current net pose before testing contact");
            if(!captured)
            {
                var net=(Transform)typeof(FishingHomeEcologyWorld).GetField("_net",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
                var point=rabbit.Root.position+Vector3.up*.2f;var eye=c.Shore.Character.EyePosition;
                var mouth=net==null?Vector3.zero:net.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,0,0));
                Debug.Log("[HOME-ECOLOGY] NET DIAGNOSTIC: actor="+c.ExplorerPosition+" rabbitStart="+rabbitStart+" rabbitFeet="+rabbit.Feet+" point="+point+" eye="+eye+" palm="+c.Shore.Character.PalmPosition(false)+" mouth="+mouth+" actorDistance="+Vector3.Distance(c.ExplorerPosition,point)+" mouthDistance="+Vector3.Distance(mouth,point)+" dot="+Vector3.Dot(c.viewCamera.transform.forward,(point-c.viewCamera.transform.position).normalized)+" sight="+c.Shore.ClearSight(eye,point)+" crouched="+c.Shore.Crouched+" fleeing="+rabbit.Fleeing+" netActive="+(net!=null && net.gameObject.activeInHierarchy)+" tools="+c.Hands.CanPresentTools+" paused="+c.Paused+" inventory="+c.InventoryOpen+" grounded="+c.Shore.Grounded+" notice="+c.Message);
                FishingV016Validation.Capture(c,"Logs/HomeEcology","net-capture-failure");
            }
            Check(captured && c.Career.HomeAnimalCount==1 && c.Career.IsWildAnimalCaptured(rabbit.Id),"The real net validates distance and sight before live capture");
            Check(!rabbit.Root.gameObject.activeSelf && !c.Career.WildAnimalAvailable(rabbit.Id,rabbit.Kind),"Live capture hides and reserves the real wild source");
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(.4f,.2f,-1.5f),Quaternion.identity),"The carried animal can return to supported home ground");
            c.Career.Backpack.TryAdd(FieldItem.RabbitHutch,1);world.Choices(0)[1].Run();world.Choices(3)[0].Run();
            Check(c.Career.HousedAnimalCount(LandAnimalKind.Rabbit)==1,"The actual habitat and housing actions place the captured rabbit");
            FishingSaveStore.Save(c.SavePath,c.Career);var loaded=FishingSaveStore.Load(c.SavePath,out _);
            Check(loaded.AquariumCount==1 && loaded.HousedAnimalCount(LandAnimalKind.Rabbit)==1 && loaded.IsWildAnimalCaptured(rabbit.Id),"Ecology ownership and source receipts survive the real save store");
            c.Shore.SetCrouch(false);for(int f=0;f<35;f++)yield return null;
            for(int i=0;i<9;i++)if(c.Career.Backpack.At(i).count==0){c.SelectFieldSlot(i);break;}
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(2,.2f,-3.1f),Quaternion.identity),"The aquarium has a clear front viewing position");
            c.View.ResetLook();FishingV036FarmValidation.Aim(c,world.AquariumPosition+Vector3.up*.9f);yield return null;
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/HomeEcology","home-ecology-aquarium");
            var habitat=world.HabitatPosition(LandAnimalKind.Rabbit);
            Check(c.Shore.Ground(habitat+Vector3.forward*3,out var habitatFloor) && c.Shore.Relocate(habitatFloor,Quaternion.Euler(0,180,0)),"The rabbit habitat has supported viewing space");
            c.View.ResetLook();FishingV036FarmValidation.Aim(c,habitat+Vector3.up*.4f);yield return null;
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/HomeEcology","home-ecology-rabbit");
            Debug.Log("[HOME-ECOLOGY] EVIDENCE: "+Path.GetFullPath("Logs/HomeEcology")+"; physical input and headset comfort remain unverified.");
        }

        public static IEnumerator QuickPurchaseAndDeploymentChecks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Quick purchase/deployment checks require an isolated profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();
            typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                .Invoke(c,new object[]{FishingAcceptanceCase.HomeInterior});
            yield return null;yield return null;
            var original=c.Career.Snapshot();bool creative=c.CreativeMode;GameObject obstruction=null;
            try
            {
                var funded=new FishingCareer().Snapshot();funded.coins=2000;c.RestoreShopTestProfile(funded);c.SetCreativeMode(false);
                Check(c.Ashore && !c.TackleShop.NearCounter && !c.AtHarbor,"Quick delivery is tested at home, away from every purchase counter");
                c.ToggleInventory();
                int before=c.Career.Coins,quantity=c.Career.Backpack.Count(FieldItem.Rope);
                Check(c.InventoryOpen && c.Paused && c.BuySupplyFromMenu(5),"An open backpack permits the real menu purchase without changing its modal flags");
                Check(c.InventoryOpen && c.Paused && c.Career.Coins==before-FishingSupplies.Price(5)
                    && c.Career.Backpack.Count(FieldItem.Rope)==quantity+FishingSupplies.Quantity(5),"Menu purchase charges the ordinary wallet once and adds the exact real quantity");
                var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning.Length==0 && loaded.Coins==c.Career.Coins && loaded.Backpack.Count(FieldItem.Rope)==c.Career.Backpack.Count(FieldItem.Rope),"Menu purchase persists coins and backpack through the actual save store");
                var poor=new FishingCareer().Snapshot();c.RestoreShopTestProfile(poor);c.SetCreativeMode(false);
                Check(!c.BuySupplyFromMenu(5) && c.Career.Coins==0 && c.Career.Backpack.Count(FieldItem.Rope)==0 && c.Message.Contains("金币不足"),"Insufficient money rejects visibly and cannot create items");
                var full=new FishingCareer().Snapshot();full.coins=2000;
                for(int i=0;i<full.backpack.Count;i++)full.backpack[i]=new FieldStack{item=FieldItem.FieldGuide,count=1};
                c.RestoreShopTestProfile(full);c.SetCreativeMode(false);
                Check(!c.BuySupplyFromMenu(5) && c.Career.Coins==2000 && c.Career.Backpack.Count(FieldItem.Rope)==0 && c.Message.Contains("背包空间不足"),"A full backpack rejects visibly without charging or deleting any item");
                c.SetCreativeMode(true);
                Check(!c.BuySupplyFromMenu(5) && c.Career.Coins==2000 && c.Career.Backpack.Count(FieldItem.Rope)==0,"Creative quick delivery still respects full backpack capacity");
                c.RestoreShopTestProfile(poor);
                Check(c.CreativeMode && c.BuySupplyFromMenu(5) && c.Career.Coins==0 && c.Career.Backpack.Count(FieldItem.Rope)==FishingSupplies.Quantity(5),"Creative quick delivery creates real items while retaining its existing free-purchase policy");
                c.RestoreShopTestProfile(funded);c.SetCreativeMode(false);
                Check(!c.BuySupplyFromMenu(-1) && c.Career.Coins==2000,"An invalid menu product cannot spend or index the product list");
                c.ToggleInventory();yield return null;
                var world=c.HomeEcology;world.Refresh();yield return null;Physics.SyncTransforms();
                Check(world.NearHome && world.Choices(0).Count==6,"The first facility page exposes aquarium and all five animal deployments");
                var display=world.transform.Find("家园生态展示");
                for(int i=0;i<5;i++)Check(display.Find(FishingCareer.HabitatName((LandAnimalKind)i)+"/Marked habitat deployment plot")!=null,"The empty deployment plot is visible for "+(LandAnimalKind)i);
                Check(c.Career.Backpack.TryAdd(FieldItem.Aquarium,1),"The aquarium fixture has its actual kit");
                world.Choices(0)[0].Run();yield return null;
                Check(c.Career.AquariumLevel==1 && c.Career.Backpack.Count(FieldItem.Aquarium)==0,"The shared menu deploys and consumes exactly one aquarium kit");
                var land=c.Home.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();var heights=new float[5];
                for(int i=0;i<5;i++)
                {
                    var kind=(LandAnimalKind)i;var kit=FishingCareer.HabitatKit(kind);
                    Check(c.Career.Backpack.TryAdd(kit,1),"The real deployment kit is present for "+kind);
                    var choice=world.Choices(0)[i+1];Check(choice.Enabled,"The facility has its own enabled deployment action: "+kind);choice.Run();yield return null;Physics.SyncTransforms();
                    var root=world.transform.Find("家园生态展示/"+FishingCareer.HabitatName(kind));heights[i]=root.position.y;
                    Check(c.Career.HabitatLevel(kind)==1 && c.Career.Backpack.Count(kit)==0 && root.Find("Shelter, exercise run and feeding station")!=null,"The menu consumes the correct kit and creates the visible habitat: "+kind);
                    foreach(var offset in new[]{Vector3.zero,new Vector3(-1.5f,0,-.75f),new Vector3(1.5f,0,-.75f),new Vector3(-1.5f,0,2.55f),new Vector3(1.5f,0,2.55f)})
                    {
                        Check(land.Raycast(new Ray(root.position+offset+Vector3.up*30,Vector3.down),out var hit,60)
                            && Mathf.Abs(root.position.y-hit.point.y)<.025f,"The complete habitat footprint rests on authored home land: "+kind+" "+offset);
                    }
                }
                loaded=FishingSaveStore.Load(c.SavePath,out warning);
                Check(warning.Length==0 && loaded.AquariumLevel==1,"All permanent deployments use the real save store");
                for(int i=0;i<5;i++)Check(loaded.HabitatLevel((LandAnimalKind)i)==1,"Deployment survives save/reload: "+(LandAnimalKind)i);
                obstruction=new GameObject("Temporary overlapping legacy support fixture");
                for(int i=0;i<5;i++)
                {
                    var block=new GameObject("Higher legacy collider");block.transform.SetParent(obstruction.transform,false);block.transform.position=world.HabitatPosition((LandAnimalKind)i)+Vector3.up*.7f;
                    block.AddComponent<BoxCollider>().size=new Vector3(3.4f,.2f,3.7f);
                }
                Physics.SyncTransforms();world.Refresh();yield return null;Physics.SyncTransforms();
                for(int i=0;i<5;i++)Check(Mathf.Abs(world.transform.Find("家园生态展示/"+FishingCareer.HabitatName((LandAnimalKind)i)).position.y-heights[i])<.005f,"Overlapping legacy colliders cannot lift a refreshed habitat: "+(LandAnimalKind)i);
                world.Open();
                var hud=typeof(FishingPrototype).GetField("_hud",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(c);
                Check(c.InventoryOpen && (int)typeof(FishingHUD).GetField("_ecologyMode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(hud)==0,"Opening ecology always starts with all six construction actions");
                Debug.Log("[HOME-ECOLOGY] CHECK: real quick delivery, visible transaction failures, save round-trip, six deployments and stable authored land grounding passed.");
            }
            finally
            {
                if(obstruction!=null){obstruction.SetActive(false);UnityEngine.Object.Destroy(obstruction);}
                if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
                c.RestoreShopTestProfile(original);c.SetCreativeMode(creative);c.HomeEcology.Refresh();
            }
        }
    }
}
