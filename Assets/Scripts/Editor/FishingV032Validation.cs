using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV032Validation
    {
        public static void Run()
        {
            var importer=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Resources/FishingV032/CoastalArch.fbx");
            if(importer==null)throw new InvalidOperationException("V032 Tripo arch has not been imported");
            importer.isReadable=true;importer.bakeAxisConversion=true;importer.importAnimation=false;importer.materialImportMode=UnityEditor.ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            FishingV011Validation.Begin("V032");
        }
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V032] CHECK: "+label);}
        private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        private static IEnumerator Capture(string output,string name)
        {
            string path=Path.Combine(output,name+".png");if(File.Exists(path))File.Delete(path);
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();FishingV016Validation.Capture(c,output,name+"-scene");
            ScreenCapture.CaptureScreenshot(path);for(int i=0;i<5 && !File.Exists(path);i++)yield return null;
            Check(File.Exists(Path.Combine(output,name+"-scene.png")),"Scene camera capture: "+name);
            if(!File.Exists(path))Debug.Log("[V032] Desktop HUD capture requires the standalone acceptance player: "+name);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            string output=Path.GetFullPath("Docs/Images/V032");Directory.CreateDirectory(output);
            var colliders=UnityEngine.Object.FindObjectsOfType<Collider>().Where(x=>x.enabled && !x.isTrigger).Select(x=>PathOf(x.transform)+" | "+x.bounds).ToArray();
            File.WriteAllLines(Path.Combine(output,"scene-colliders.txt"),colliders);
            File.WriteAllLines(Path.Combine(output,"scene-roots.txt"),c.gameObject.scene.GetRootGameObjects().Select(x=>x.name+" children="+x.GetComponentsInChildren<Renderer>(true).Length));
            var career=c.Career;var position=c.Player.transform.position;
            Check(c.CoastPresentation.RemovedCards>=21,"All legacy collidable bush cards are removed");
            foreach(var center in FishingCoastalPresentation.Islets)Check(c.Shore.Ground(center,out var ground) && ground.y>.6f && c.Shore.SafeCoastalFooting(ground),"New islet has real dry, supporting ground: "+center);
            c.OpenStartMenu();yield return null;
            Check(c.StartMenuOpen && c.Paused && Time.timeScale==0,"Title freezes gameplay");
            Check(c.TitleVoyage.Boat!=null && c.TitleVoyage.Boat.GetComponentsInChildren<Collider>().Length==0,"Title captain and boat are visual actors without colliders");
            var take=Capture(output,"title-live");while(take.MoveNext())yield return take.Current;
            c.MenuControls=true;var hud=c.GetComponent<FishingHUD>();
            for(int tab=0;tab<3;tab++){hud.SettingsTab=tab;yield return null;take=Capture(output,"settings-"+tab);while(take.MoveNext())yield return take.Current;}
            c.SetSetting("Master",.35f);Check(Mathf.Abs(AudioListener.volume-.35f)<.001f,"Master slider reaches the real audio listener");
            c.SetSetting("Sensitivity",999);Check(c.MouseSensitivity==2.5f,"Sensitivity clamps at input boundary");c.SetSetting("Sensitivity",1);
            c.SetSetting("FOV",float.NaN);Check(c.FieldOfView==FishingViewRig.DesktopFieldOfView,"Nonfinite setting rejected");
            c.SetSetting("Master",1);c.MenuControls=false;c.StartFromMenu();var boat=c.TitleVoyage.Boat.position;
            Check(c.StartMenuOpen && c.TitleVoyage.Sailing && c.Paused,"Start begins departure before gameplay resumes");
            for(int i=0;i<90 && c.StartMenuOpen;i++)yield return null;
            Check(Vector3.Distance(boat,c.TitleVoyage.Boat.position)>.1f,"Departure boat actually moves while game is paused");
            take=Capture(output,"departure");while(take.MoveNext())yield return take.Current;
            for(int i=0;i<600 && c.StartMenuOpen;i++)yield return null;
            Check(!c.StartMenuOpen && !c.Paused && c.Career==career,"Departure resumes the same isolated save");
            c.SetSetting("Quality",0);Check(((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).shadowDistance==0,"Low quality actually disables distant shadow rendering");c.SetSetting("Quality",2);
            Check(FishingPrototype.FlatDistance(position,c.Player.transform.position)<1,"Menu never teleports the playable boat");
            Check(c.Prologue.Begin() && c.Prologue.Open,"Departure can hand control to the real game CG");c.Prologue.Suspend();
            if(c.FirstPerson)c.ToggleView();yield return null;take=Capture(output,"harbor-hud");while(take.MoveNext())yield return take.Current;
            c.OpenStartMenu();yield return null;c.FinishTitleVoyage();
            c.Effects.Splash(c.Player.transform.position+Vector3.forward*4,1.5f);yield return null;
            Check(c.Effects.SplashCount>0 && c.Effects.SprayBudget<=192,"Real splash emitter is bounded and receives gameplay events");
            foreach(int id in new[]{0,1})
            {
                c.SelectNavigation(c.HomeNavigationIndex+1+id);Check(c.NavigationName==FishingCoastalPresentation.IsletName(id),"New islet navigation resolves by name");
                var p=FishingCoastalPresentation.Islets[id];c.viewCamera.transform.SetPositionAndRotation(p+new Vector3(40,24,45),Quaternion.LookRotation(p+Vector3.up*2-(p+new Vector3(40,24,45))));
                FishingV016Validation.Capture(c,output,"islet-"+id);
                c.ReturnToHarbor();var rb=c.Player.engine.RB;var berth=FishingCoastalPresentation.Berth(id)+Vector3.up;
                rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
                for(int i=0;i<12;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Real boat-to-islet landing: "+id);Check(c.Shore.FreeLanding,"New islet uses actual physics coastline");
                c.SetSetting("FOV",82);if(!c.FirstPerson)c.ToggleView();c.View.ApplyView(true,true);Check(Mathf.Abs(c.viewCamera.fieldOfView-82)<.01f,"Field of view changes the actual first-person camera");c.SetSetting("FOV",FishingViewRig.DesktopFieldOfView);
                c.ReturnToHarbor();
            }
            var arch=Resources.Load<GameObject>("FishingV032/CoastalArch");int tris=arch.GetComponentsInChildren<MeshFilter>().Sum(x=>x.sharedMesh.triangles.Length/3);
            Check(tris>15000 && tris<22000,"Tripo arch reduced from 467496 to a bounded game mesh");
            var map=BoatAttack.Editor.FishingMapBaker.BakeInitializedScene();Check(map.sources.Any(x=>x.Contains("白帆拱湾")) && map.sources.Any(x=>x.Contains("萤火沙洲")),"Both new islets are baked into the real terrain map");
        }
    }
}
