using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV031Validation
    {
        public static void Run()
        {
            foreach(string path in Directory.GetFiles("Assets/Resources/FishingV031","*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.importAnimation=false;importer.bakeAxisConversion=true;importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/FishingV031","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.SaveAndReimport();
            }
            FishingV011Validation.Begin("V031");
        }
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V031] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var profile=c.Career.Snapshot();var hud=c.GetComponent<FishingHUD>();
            string output=Path.GetFullPath("Docs/Images/V031");Directory.CreateDirectory(output);
            try
            {
                Check(!c.StartMenuOpen && !c.Paused,"Batch validation bypasses the player title menu");
                foreach(string shader in new[]{"FishingNightSky","FishingLighthouseBeam"})
                    Check(File.ReadAllText("Assets/Resources/"+shader+".shader").Contains("#pragma multi_compile_instancing"),"Night shader retains instancing/stereo variants: "+shader);
                var career=c.Career;c.OpenStartMenu();
                Check(c.StartMenuOpen && c.Paused && Time.timeScale==0,"Opening title menu freezes gameplay");
                c.MenuControls=true;Check(c.Career==career,"Controls preserve the current profile");c.StartFromMenu();
                for(int i=0;i<600 && c.StartMenuOpen;i++)yield return null;
                Check(!c.StartMenuOpen && !c.Paused && c.Career==career,"Starting resumes the existing save without reset");
                Check(PlayerSettings.resizableWindow && PlayerSettings.allowFullscreenSwitch,"Desktop player permits resizing and fullscreen switching");
                foreach(string name in new[]{"FieldGuide","GuideCover","GuideLeaf"})
                {
                    var mesh=Resources.Load<GameObject>("FishingV031/"+name).GetComponentInChildren<MeshFilter>().sharedMesh;
                    Debug.Log("[V031] GUIDE BOUNDS: "+name+" "+mesh.bounds);
                    Check(mesh.bounds.size.y<.09f,"Book part retains the authored thin Y axis: "+name);
                }
                var coverMesh=Resources.Load<GameObject>("FishingV031/GuideCover").GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(coverMesh.bounds.min.x>-.015f && coverMesh.bounds.max.x>.40f,"Cover extends right from its hinge before opening");
                foreach(string name in new[]{"TitleSea","Chapters"})Check(Resources.Load<Texture2D>("FishingV031/"+name)!=null,"Illustration bundled: "+name);
                foreach(char glyph in "潮汐回响航海手札自然手册控制与航行沿岸调查循着灯火")
                    Check(FishingUITheme.Heading.HasCharacter(glyph),"Display font contains "+glyph);
                for(int i=0;i<7;i++){hud.JournalPage=i;Check(hud.JournalPage==i,"Chapter selection "+i);}
                hud.CatalogPage=3;Check(hud.CatalogPage==3,"Catalog reaches species 19 and 20");hud.CatalogPage=0;
                c.SetDayNight(false);c.Nightscape.Apply(0);Check(c.Nightscape.Visibility<.01f,"Moon and stars vanish in daylight");
                c.SetDayNight(true);c.Nightscape.Apply(0);Check(c.Nightscape.Visibility>.95f && c.Nightscape.StarCount==280,"Moon and stable star dome appear at night");
                var sweep=c.Nightscape.LighthouseSweep;var rotation=sweep.rotation;c.Nightscape.Apply(2);
                Check(Mathf.Abs(Quaternion.Angle(rotation,sweep.rotation)-18)<.1f,"Home lighthouse sweeps at nine degrees per second");
                Check(c.Nightscape.GetComponentsInChildren<Light>().Count(l=>l.enabled && l.type==LightType.Spot)==2,"Two opposing warm lighthouse lights illuminate the sea");
                var savedPosition=c.viewCamera.transform.position;var savedRotation=c.viewCamera.transform.rotation;
                var nightPosition=c.Home.Origin+new Vector3(26,8,-61);var nightTarget=c.Home.Origin+new Vector3(-9,17,10);
                c.viewCamera.transform.SetPositionAndRotation(nightPosition,Quaternion.LookRotation(nightTarget-nightPosition));
                c.Weather.ApplyEnvironment();c.Nightscape.Apply(0);FishingV016Validation.Capture(c,output,"nightscape-calibrated");
                c.viewCamera.transform.SetPositionAndRotation(savedPosition,savedRotation);c.Nightscape.Apply(0);
                c.TogglePause();rotation=sweep.rotation;yield return null;yield return null;
                Check(Quaternion.Angle(rotation,sweep.rotation)<.001f,"Pause stops lighthouse motion");c.TogglePause();c.SetDayNight(false);
                c.ReturnToHarbor();var point=c.Shore.Sites[0].Berth+Vector3.up;
                var rb=c.Player.engine.RB;rb.position=point;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(point,Quaternion.identity);Physics.SyncTransforms();yield return null;yield return null;
                Check(c.Shore.TryDisembark(),"Real shore landing for held-tool checks");
                var bag=c.Career.Backpack;Check(bag.TryAdd(FieldItem.Lantern,1),"Own a lantern");
                int slot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.Lantern);
                if(slot>=9){Check(bag.Move(slot,1),"Lamp moved into hotbar");slot=1;}
                c.SelectFieldSlot(slot);c.UseFieldItem();
                for(int i=0;i<8;i++){c.PresentCharacterFrame();yield return null;}
                Check(c.FieldWorld.LanternHeld && c.LanternOn,"Selected, used lantern is held and lit");
                Check(c.FieldWorld.HolsteredLantern==null || !c.FieldWorld.HolsteredLantern.gameObject.activeInHierarchy,"Handheld lamp has no duplicate on the waist");
                var lamp=c.FieldWorld.transform.Find("Field lantern light").GetComponent<Light>();
                Check(lamp.enabled && Vector3.Distance(lamp.transform.position,c.FieldWorld.HeldTool.TransformPoint(FishingFieldWorld.LanternLightPoint))<.002f,"Real warm light follows the held chamber");
                FishingV016Validation.Capture(c,output,"lantern-held");
                bool wasFirstPerson=c.FirstPerson;if(wasFirstPerson)c.ToggleView();c.PresentCharacterFrame();
                var tool=c.FieldWorld.HeldTool;var camera=c.viewCamera;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
                camera.transform.SetPositionAndRotation(tool.TransformPoint(new Vector3(.27f,.30f,.72f)),Quaternion.LookRotation(tool.TransformPoint(new Vector3(0,.23f,0))-tool.TransformPoint(new Vector3(.27f,.30f,.72f))));
                FishingV016Validation.Capture(c,output,"lantern-close-warm-contact");
                camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);if(wasFirstPerson)c.ToggleView();c.PresentCharacterFrame();
                c.SelectFieldSlot(0);c.PresentCharacterFrame();
                Check(!c.FieldWorld.LanternHeld && c.FieldWorld.HolsteredLantern.gameObject.activeInHierarchy,"Idle lamp returns to the belt when another item is selected");
                c.CoastWorld.ShowSeaFind(FieldItem.SeaTreasure);int treasures=c.Career.Backpack.Count(FieldItem.SeaTreasure);
                Check(c.OpenFieldGuide() && c.GuideOpening && !c.Paused,"F starts a physical book animation before opening the reader");
                Check(!c.CoastWorld.ShowingSeaFind && c.Career.Backpack.Count(FieldItem.SeaTreasure)==treasures,"Opening a book clears the old sea-find display without changing inventory");
                bool sawMovingCover=false,capturedGuide=false;
                for(int i=0;i<240 && c.GuideOpening;i++)
                {
                    c.PresentCharacterFrame();
                    if(c.GuideOpenAmount>.3f && c.GuideOpenAmount<.95f)
                    {
                        Check(c.FieldWorld.PresentedItem==FieldItem.FieldGuide && c.FieldWorld.GuideCover.gameObject.activeInHierarchy,"Moving cover stays attached to the visible book");
                        if(!capturedGuide && c.GuideOpenAmount>.78f){FishingV016Validation.Capture(c,output,"guide-open-calibrated");capturedGuide=true;}
                        sawMovingCover=true;
                    }
                    yield return null;
                }
                Check(sawMovingCover && c.JournalOpen && c.Paused && c.ReadingFieldGuide && hud.JournalPage==2,"Opening animation leads into illustrated nature reader");
                c.ToggleJournal();Check(!c.ReadingFieldGuide && !c.Paused,"Esc closes the book reader and restores play");yield return null;
                foreach(bool pick in new[]{false,true})
                {var harvest=CheckAutomaticHarvest(c,pick);try{while(harvest.MoveNext())yield return harvest.Current;}finally{(harvest as IDisposable)?.Dispose();}yield return null;}
            }
            finally
            {
                if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
                c.CancelWorldAction();c.ReturnToHarbor();c.RestoreShopTestProfile(profile);
            }
        }
        private static IEnumerator CheckAutomaticHarvest(FishingPrototype c,bool pick)
        {
            var node=c.WorkshopWorld.Nodes.First(n=>n.Item==(pick?FieldItem.IronOre:FieldItem.Stone));var feet=node.Feet;var position=node.Root.position;
            var floor=new GameObject("V031 isolated harvest floor");floor.transform.position=new Vector3(4000,1,4000);floor.AddComponent<BoxCollider>().size=new Vector3(8,.4f,8);
            try
            {
                c.Shore.Body.enabled=false;c.Shore.Actor.SetPositionAndRotation(new Vector3(4000,1.25f,4000),Quaternion.identity);c.Shore.Body.enabled=true;
                node.Feet=new Vector3(4000,1.2f,4000.48f);node.Root.position=node.Feet;Physics.SyncTransforms();c.Shore.SetCrouch(false,true);
                if(pick)
                {
                    Check(c.Career.Backpack.TryAdd(FieldItem.StonePickaxe,1),"Mining fixture owns a real pickaxe");
                    int slot=Enumerable.Range(0,36).First(i=>c.Career.Backpack.At(i).item==FieldItem.StonePickaxe);
                    if(slot>=9){Check(c.Career.Backpack.Move(slot,1),"Move mining tool into hotbar");slot=1;}c.SelectFieldSlot(slot);
                }
                else c.SelectFieldSlot(8);
                for(int i=0;i<120;i++)
                {
                    c.Shore.Move(Vector2.zero,false,false,false,1f/60);c.PresentCharacterFrame();yield return null;
                    if(c.Shore.Grounded && c.Shore.Character.IsStanceSettled(true,false))break;
                }
                c.WorkshopWorld.TryHarvestTarget(out _);var check=c.InspectWorldInteractionStart();
                Check(check.CanBegin && check.AutoCrouch,"Standing resource request plans a real automatic crouch: "+check);
                int before=c.Career.Backpack.Count(node.Item),attempts=c.ActionCommitAttempts;
                Check(c.RequestWorldInteraction() && c.Shore.Crouched,"E starts crouching without holding Ctrl");
                for(int i=0;i<240 && c.WorldActionBusy;i++){c.PresentCharacterFrame();yield return null;}
                Check(c.LastActionSucceeded && c.Career.Backpack.Count(node.Item)>before && c.ActionCommitAttempts==attempts+1,"Resource commits once after the actual crouch and hand contact");
                yield return null;c.Shore.Move(Vector2.zero,false,false,false,1f/60);
                Check(!c.Shore.Crouched,"Player can stand again after collection");
            }
            finally{c.CancelWorldAction();node.Feet=feet;node.Root.position=position;Object.Destroy(floor);Physics.SyncTransforms();}
        }
    }
}
