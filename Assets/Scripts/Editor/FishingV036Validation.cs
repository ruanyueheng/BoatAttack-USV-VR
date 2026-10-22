using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV036Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V036");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036] CHECK: "+label);}
        public static void Install()
        {
            try
            {
                const string path="Assets/Resources/FishingV036/televisionVintage.fbx";
                AssetDatabase.Refresh();var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;
                importer.animationType=ModelImporterAnimationType.None;importer.isReadable=true;importer.bakeAxisConversion=true;
                importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.None;importer.SaveAndReimport();
                Check(Resources.Load<GameObject>("FishingV036/televisionVintage")!=null,"CC0 vintage television imported");
                FishingV036LanternAssets.Install();
                FishingV036GardenAssets.Install();
                Debug.Log("[V036-ART] PASS: television and distinct Poly Haven hanging fixture imported");EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            string folder=Path.GetFullPath("Docs/Images/V036");Directory.CreateDirectory(folder);
            var original=c.Career.Snapshot();
            try
            {
                c.ReturnToHarbor();yield return null;c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(c,new object[]{FishingAcceptanceCase.HomeInterior});yield return null;
                Check(c.Shore.Relocate(c.Home.LoungeStance,Quaternion.Euler(0,180,0)),"Sofa approach has dry clear footing");
                if(!c.FirstPerson)c.ToggleView();yield return null;
                c.viewCamera.transform.LookAt(c.Home.InteractionPoint(4));yield return null;
                Check(c.Home.TryTarget(out int loungeTarget,out _) && loungeTarget==4,"Production proximity selects the sofa rather than the coffee-table radio");
                Check(c.Home.InteractAt(4) && c.Home.Lounging && c.StoryBlocksGameplay,"Production sofa interaction locks movement during a seated transition");
                c.Home.TickLeisureInput(null,false,true);c.Home.TickLeisureInput(null,true,true);
                Check(c.Home.Lounging && !c.Home.TelevisionOn,"Focus loss and absent tracked controller suspend seated input without forcing a camera move");
                for(int i=0;i<80;i++)yield return null;
                c.PresentCharacterFrame();Check(c.Shore.Character.Ready && c.Home.LoungeEyeOffset.sqrMagnitude>.01f,"Original character rig and camera follow the sofa seat");
                c.Home.ToggleTelevision();Check(c.Home.TelevisionOn,"Tide film plays from the television");
                var screen=c.Home.TelevisionScreen;var eye=c.Shore.Character.SleepPoseEye;var hip=c.Shore.Character.HipPosition;
                Debug.Log("[V036] LEISURE eye="+eye.ToString("F3")+" hip="+hip.ToString("F3")+" screen="+screen.bounds+" camera="+c.viewCamera.transform.position.ToString("F3"));
                Check(Vector3.Distance(c.viewCamera.transform.position,eye)<.01f,"Seated first-person camera follows the posed eyes");
                Check(eye.y>hip.y+.45f && hip.y>c.Home.Origin.y+.45f && hip.y<c.Home.Origin.y+1.5f,"Sofa supports the hips and preserves upright head height");
                var centre=c.viewCamera.WorldToViewportPoint(screen.bounds.center);
                Check(centre.z>.5f && centre.x>.2f && centre.x<.8f && centre.y>.2f && centre.y<.8f,"Actual CRT film is centred in the seated view");
                Check(screen.sharedMaterial.GetFloat("_Playing")==1 && screen.GetComponent<MeshFilter>().sharedMesh.vertexCount>12,"Playing material is attached to the source television CRT geometry");
                Check(screen.bounds.size.x<.8f,"Film occupies the rounded CRT without painting the right-side controls");
                FishingV016Validation.Capture(c,folder,"sofa-firstperson");
                for(int i=0;i<70;i++)yield return null;
                FishingV016Validation.Capture(c,folder,"sofa-firstperson-film-later");
                c.ToggleView();for(int i=0;i<3;i++)yield return null;c.PresentCharacterFrame();
                FishingV016Validation.Capture(c,folder,"sofa-thirdperson");
                foreach(var bone in c.Shore.Character.GetComponentsInChildren<Transform>())if(bone.name=="Foot.L" || bone.name=="Foot.R")
                {Debug.Log("[V036] SEATED FOOT "+bone.name+" above floor="+(bone.position.y-c.Home.Origin.y-.2f));Check(bone.position.y>c.Home.Origin.y+.15f && bone.position.y<c.Home.Origin.y+.55f,"Seated source feet reach the floor rather than floating at cushion height");}
                c.Home.StandUp();for(int i=0;i<60;i++)yield return null;
                Check(!c.Home.Lounging,"Standing transition restores normal gameplay");
                if(!c.FirstPerson)c.ToggleView();
                c.Home.ToggleTelevision();Check(!c.Home.TelevisionOn,"Television can be switched off");
                Check(c.Home.House.GetComponentsInChildren<Light>().Length>=11,"Interior and exterior practical lantern lights exist");
                var portableMeshes=new System.Collections.Generic.HashSet<Mesh>();
                foreach(var portableFilter in Resources.Load<GameObject>("FishingUserTools/UserLantern").GetComponentsInChildren<MeshFilter>())portableMeshes.Add(portableFilter.sharedMesh);
                int hangingCount=0;Transform closeLantern=null;
                foreach(Transform fixture in c.Home.House)if(fixture.name=="V036 hanging star-vent lantern")
                {
                    hangingCount++;int triangles=0;
                    foreach(var fixtureFilter in fixture.GetComponentsInChildren<MeshFilter>()){Check(!portableMeshes.Contains(fixtureFilter.sharedMesh),"Hanging fixture never reuses the portable hand lantern mesh");triangles+=fixtureFilter.sharedMesh.triangles.Length/3;}
                    Check(triangles>=10000 && triangles<=17000,"Distinct source hanging fixture retains its brass cage and chain");
                    if(fixture.localPosition.x>5)closeLantern=fixture;
                }
                Check(hangingCount==5 && closeLantern!=null,"Four roof-attached porch lanterns and one interior beam fixture exist");
                Check(c.Shore.Relocate(c.Home.Origin+new Vector3(4.5f,.2f,-9),Quaternion.Euler(0,35,0)),"Hanging lantern close approach has supported footing");
                c.SetDayNight(false);yield return null;c.PresentCharacterFrame();
                c.viewCamera.transform.LookAt(closeLantern.position+Vector3.up*.2f);FishingV016Validation.Capture(c,folder,"hanging-lantern-day-detail");
                c.SetDayNight(true);yield return null;c.PresentCharacterFrame();
                c.viewCamera.transform.LookAt(closeLantern.position+Vector3.up*.2f);FishingV016Validation.Capture(c,folder,"hanging-lantern-night-detail");
                c.SetDayNight(true);Check(c.Shore.Relocate(c.Home.Origin+new Vector3(7.6f,.2f,-9),Quaternion.Euler(0,-35,0)),"Night porch approach clear");c.View.ResetLook();yield return null;
                Check(RenderSettings.ambientSkyColor.r>.06f && RenderSettings.ambientSkyColor.r<.07f,"Home-local night fill preserves a bounded readable moonlit baseline");FishingV016Validation.Capture(c,folder,"home-lanterns-night");
                Check(c.Shore.Relocate(c.Home.Entry+Vector3.back*2,Quaternion.identity),"Pickaxe preview stance clear");
                Check(c.Career.Backpack.TryAdd(FieldItem.StonePickaxe,1),"Pickaxe fixture fits backpack");
                int slot=-1;for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==FieldItem.StonePickaxe){slot=i;break;}
                if(slot!=1)Check(c.Career.Backpack.Move(slot,1),"Move pick to hotbar");c.SelectFieldSlot(1);c.View.ResetLook();
                for(int i=0;i<30;i++)yield return null;c.PresentCharacterFrame();
                var tool=c.FieldWorld.HeldTool;Check(tool.gameObject.activeInHierarchy,"Pickaxe is presented at normal view");
                var tip=c.viewCamera.WorldToViewportPoint(tool.TransformPoint(FishingFieldWorld.PickTip));
                Debug.Log("[V036] PICK TIP: "+tip);
                Check(tip.z>.15f && tip.x>.03f && tip.x<.97f && tip.y>.04f && tip.y<.96f,"Pick head is visible without lowering the camera");
                var filter=tool.GetComponent<MeshFilter>();var min=Vector3.one*float.MaxValue;var max=Vector3.one*float.MinValue;
                foreach(var vertex in filter.sharedMesh.vertices)if(vertex.y>=FishingFieldWorld.PickTip.y*.8f){var projected=c.viewCamera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));min=Vector3.Min(min,projected);max=Vector3.Max(max,projected);}
                Debug.Log("[V036] PICK HEAD: "+min+" to "+max);
                Check(min.z>.10f && min.x>.02f && max.x<.98f && min.y>0 && max.y<.98f,"Complete pick head and top fit normal first-person view");
                FishingV016Validation.Capture(c,folder,"pickaxe-normal-view");
                c.ReturnToHarbor();yield return null;
                var safe=typeof(FishingPrototype).GetMethod("TickAutosave",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)safe.Invoke(c,null);long allocation=GC.GetAllocatedBytesForCurrentThread()-before;
                Debug.Log("[V036] 200 autosave idle polls including reflection invocation bytes: "+allocation);
                Check(allocation<30000,"Autosave polling no longer snapshots/resolves footing every rendered frame");
            }
            finally{c.Home.StandUp(true);c.Home.CancelSleep();c.CancelWorldAction();c.ReturnToHarbor();c.RestoreShopTestProfile(original);}
        }
    }
}
