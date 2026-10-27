using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BoatAttack.EditorTools
{
    public static class FishingPortableChartValidation
    {
        private const string Evidence="Logs/PortableChart";
        private static readonly MethodInfo Update=typeof(FishingPrototype).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        public static void Run()=>FishingV011Validation.Begin("PORTABLE-CHART");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("[PORTABLE-CHART] "+label);Debug.Log("[PORTABLE-CHART] CHECK: "+label);}

        private static void Key(Keyboard keyboard,params UnityEngine.InputSystem.Key[] keys)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}

        private static void Press(FishingPrototype c,Keyboard keyboard,UnityEngine.InputSystem.Key key)
        {
            Key(keyboard);Key(keyboard,key);
            keyboard.MakeCurrent();
            Debug.Log("[PORTABLE-CHART] INPUT: "+key+" current="+(Keyboard.current==keyboard)+" enabled="+keyboard.enabled
                +" held="+keyboard[key].isPressed+" pressed="+keyboard[key].wasPressedThisFrame+" update="+InputState.currentUpdateType
                +" pause="+c.Paused+" start="+c.StartMenuOpen+" inventory="+c.InventoryOpen+" journal="+c.JournalOpen
                +" guide="+c.GuideOpening+" story="+c.StoryBlocksGameplay+" shop="+c.ShopBlocksGameplay
                +" action="+c.WorldActionBlocksGameplay+" catch="+c.HasPendingCatch+" inspect="+c.InspectingItem
                +" chartBlocked="+c.HomeChartBlocksGameplay+" sleep="+c.Home.Sleeping+" swim="+c.Shore.Swimming+" climb="+c.Shore.Climbing);
            Check(keyboard[key].wasPressedThisFrame,"simulated "+key+" produces an actual InputSystem press edge");
            Update.Invoke(c,null);
            Key(keyboard);
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.DesktopDisplay,"simulated desktop input uses the existing isolated profile");
            Directory.CreateDirectory(Evidence);
            var previousSettings=InputSystem.settings;
            var settings=UnityEngine.Object.Instantiate(previousSettings);
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=settings;
            var previousKeyboard=Keyboard.current;
            var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();
            var career=c.Career;int selected=c.Career.Backpack.Selected;int navigation=c.NavigationTarget;
            bool firstPerson=c.FirstPerson;
            var lanternProperty=typeof(FishingPrototype).GetProperty("LanternOn");bool lanternBefore=c.LanternOn;
            var canProperty=typeof(FishingFarmWorld).GetProperty("CarryingCan");bool canBefore=c.Farm.CarryingCan;
            try
            {
                if(c.StartMenuOpen)c.FinishTitleVoyage();
                c.CloseHomeChart(true);
                if(c.InventoryOpen)c.ToggleInventory();
                if(c.JournalOpen)c.ToggleJournal();
                if(c.Paused)c.TogglePause();
                c.ReturnToHarbor();yield return null;

                foreach(bool ashore in new[]{false,true})
                {
                    if(ashore)
                    {
                        var rb=c.Player.engine.RB;var berth=c.Home.Berth+Vector3.up*.7f;
                        rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                        c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
                        for(int i=0;i<20;i++)yield return null;
                        Check(c.Shore.TryDisembark(),"production home berth permits shore map fixture");
                        Check(c.Shore.Relocate(c.Home.WakePoint,Quaternion.identity),"map fixture has real supported feet");
                    }
                    foreach(bool first in new[]{true,false})
                    {
                        if(c.FirstPerson!=first)c.ToggleView();
                        for(int i=0;i<8;i++)yield return null;
                        string label=(ashore?"shore":"boat")+"-"+(first?"first":"third");
                        var feet=c.ExplorerPosition;var boat=c.Player.transform.position;
                        var cameraPosition=c.viewCamera.transform.position;var cameraRotation=c.viewCamera.transform.rotation;
                        bool smallChart=c.ChartExpanded;int smallRange=c.ChartRangeMeters;
                        float priorScale=Time.timeScale;
                        Press(c,keyboard,UnityEngine.InputSystem.Key.M);
                        Check(c.HomeChartOpen && c.PortableChart!=null && c.PortableChart.Active,label+": InputSystem M reaches production Update and starts taking out map");
                        Check(c.Paused && Time.timeScale==0 && c.HomeChartBlocksGameplay && !c.HomeChartReady,label+": opening blocks gameplay before the full map appears");
                        Check(c.ChartExpanded==smallChart && c.ChartRangeMeters==smallRange,label+": global map does not change the corner map preference");
                        float progress=c.PortableChart.Progress;
                        c.PortableChart.Tick(.25f);c.PresentCharacterFrame();
                        Check(c.PortableChart.Progress>progress,label+": draw animation advances while scaled time is zero");
                        FishingV016Validation.Capture(c,Evidence,label+"-draw-025");
                        c.PortableChart.Tick(.25f);c.PresentCharacterFrame();
                        FishingV016Validation.Capture(c,Evidence,label+"-draw-050");
                        c.PortableChart.Tick(.21f);c.PresentCharacterFrame();
                        Check(c.HomeChartReady && c.PortableChart.Ready,label+": unfolded map becomes interactive");
                        FishingV016Validation.Capture(c,Evidence,label+"-draw-070");
                        CheckPresentation(c,label,first);
                        var paperMatrix=c.PortableChart.Paper.localToWorldMatrix;
                        var left=c.Shore.Character.PalmPosition(true);var right=c.Shore.Character.PalmPosition(false);
                        float sameProgress=c.PortableChart.Progress;
                        c.PresentCharacterFrame();
                        Check(c.PortableChart.Paper.localToWorldMatrix==paperMatrix && c.PortableChart.Progress==sameProgress
                            && Vector3.Distance(left,c.Shore.Character.PalmPosition(true))<.0001f
                            && Vector3.Distance(right,c.Shore.Character.PalmPosition(false))<.0001f,label+": repeated presentation does not advance or drift the map and hands");

                        float range=c.HomeChartRangeMeters;c.ZoomHomeChart(1);
                        Check(c.HomeChartRangeMeters<range,label+": full map zoom stays functional");
                        c.PanHomeChart(new Vector2(25,-40));c.CenterHomeChartOnPlayer();
                        Check(Vector3.Distance(c.HomeChartCenter,c.ChartPlayerPosition)<.001f,label+": locate follows chart player position");
                        Check(c.SelectHomeChartNavigation(0) && c.NavigationTarget==0,label+": destination selection works during modal pause");
                        c.ResetHomeChartView();Check(c.HomeChartRangeMeters>320,label+": all-world view is available");
                        Check(Vector3.Distance(feet,c.ExplorerPosition)<.001f && Vector3.Distance(boat,c.Player.transform.position)<.001f,label+": map does not move character or boat");
                        if(ashore && first)
                        {
                            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Evidence,"global-map-full-hud.png")));
                            for(int i=0;i<4;i++)yield return null;
                        }
                        yield return null;
                        Press(c,keyboard,first?UnityEngine.InputSystem.Key.M:UnityEngine.InputSystem.Key.Escape);
                        Check(c.HomeChartOpen && c.PortableChart.Closing && !c.HomeChartReady && c.Paused,label+": M or Escape folds the map before releasing the modal");
                        float deadline=Time.realtimeSinceStartup+3;
                        while(c.HomeChartOpen && Time.realtimeSinceStartup<deadline)yield return null;
                        Check(!c.HomeChartOpen && !c.PortableChart.Active && !c.Paused && Time.timeScale==priorScale,
                            label+": production unscaled ticks complete the stow and restore the original time scale");
                        Check(Vector3.Distance(cameraPosition,c.viewCamera.transform.position)<.03f && Quaternion.Angle(cameraRotation,c.viewCamera.transform.rotation)<.25f,
                            label+": stowing restores the original playable camera pose");
                        yield return null;
                        Check(!c.HomeChartBlocksGameplay,label+": movement and tools are released next frame");
                    }
                }

                if(!c.FirstPerson)c.ToggleView();yield return null;
                foreach(float yaw in new[]{120f,180f})
                {
                    c.View.ResetLook();c.View.ApplyView(true,true);
                    c.View.ApplyShoreLookDelta(new Vector2(yaw/(.12f*c.MouseSensitivity),0));c.View.ApplyView(true,true);
                    var position=c.viewCamera.transform.position;var rotation=c.viewCamera.transform.rotation;
                    var lookYaw=typeof(FishingViewRig).GetField("_lookYaw",BindingFlags.Instance|BindingFlags.NonPublic);
                    float beforeYaw=(float)lookYaw.GetValue(c.View);
                    Check(c.OpenPortableChart(),"look-back "+yaw+": map opens while looking behind the character");
                    c.PortableChart.Tick(.71f);c.PresentCharacterFrame();
                    CheckPresentation(c,"look-back "+yaw,true);
                    Check(Mathf.Abs(Mathf.DeltaAngle(beforeYaw,(float)lookYaw.GetValue(c.View)))<.001f,"look-back "+yaw+": reading composition preserves the private look yaw");
                    FishingV016Validation.Capture(c,Evidence,"look-back-"+yaw);
                    c.CloseHomeChart(true);
                    Check(Vector3.Distance(position,c.viewCamera.transform.position)<.001f && Quaternion.Angle(rotation,c.viewCamera.transform.rotation)<.01f,
                        "look-back "+yaw+": forced cleanup restores the exact original view");
                    yield return null;
                }
                c.View.ResetLook();c.View.ApplyView(true,true);yield return null;

                lanternProperty.SetValue(c,true);
                Check(c.OpenPortableChart(),"lit-lantern fixture opens the map");
                c.PortableChart.Tick(.71f);c.PresentCharacterFrame();
                c.CloseHomeChart();
                float closeDeadline=Time.realtimeSinceStartup+3;
                while(c.HomeChartOpen && Time.realtimeSinceStartup<closeDeadline)yield return null;
                Check(!c.HomeChartOpen && c.LanternOn,"normal map stow restores the previously lit lantern");
                lanternProperty.SetValue(c,lanternBefore);yield return null;

                canProperty.SetValue(c.Farm,true);c.PresentCharacterFrame();
                Check(c.Farm.CarryingCan && c.Farm.WateringCan.gameObject.activeInHierarchy,"carried-can fixture has a visible physical can");
                Check(c.OpenPortableChart(),"carried-can fixture opens the map");
                c.PortableChart.Tick(.71f);c.PresentCharacterFrame();
                Check(c.Farm.CarryingCan && !c.Farm.WateringCan.gameObject.activeInHierarchy,"map hides the carried can without clearing ownership");
                c.CloseHomeChart();closeDeadline=Time.realtimeSinceStartup+3;
                while(c.HomeChartOpen && Time.realtimeSinceStartup<closeDeadline)yield return null;
                c.PresentCharacterFrame();
                Check(!c.HomeChartOpen && c.Farm.CarryingCan && c.Farm.WateringCan.gameObject.activeInHierarchy,"normal map stow restores the carried can");
                canProperty.SetValue(c.Farm,canBefore);c.Farm.ApplyPresentation();yield return null;

                Check(c.OpenPortableChart(),"rapid-close fixture opens");
                c.CloseHomeChart();Check(!c.OpenPortableChart(),"rapid reopen cannot override an active stow");
                c.CloseHomeChart(true);Check(c.HomeChartBlocksGameplay,"forced close consumes its entire input frame");yield return null;
                Check(c.OpenPortableChart(),"map can reopen after forced cleanup");
                c.CloseHomeChart(true);yield return null;
                c.TogglePause();
                Check(!c.OpenPortableChart(),"portable shortcut cannot open over the pause menu");
                Check(c.OpenHomeChart() && c.HomeChartReady && (c.PortableChart==null || !c.PortableChart.Active),"original desk window remains immediately available without the portable animation");
                c.CloseHomeChart();Check(!c.HomeChartOpen && c.Paused && Time.timeScale==0,"desk close immediately preserves a pre-existing pause");
                c.TogglePause();yield return null;
                c.ToggleInventory();
                Check(!c.OpenPortableChart(),"inventory prevents a second modal and hand owner");
                c.ToggleInventory();yield return null;

                c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();
                for(int i=0;i<15;i++)yield return null;
                c.PresentCharacterFrame();Check(c.Hands.Rod.gameObject.activeInHierarchy,"fishing rod fixture is actually equipped");
                Check(c.OpenPortableChart(),"equipped fishing rod can be temporarily stowed for the map");
                c.PortableChart.Tick(.71f);c.PresentCharacterFrame();
                Check(!c.Hands.Rod.gameObject.activeInHierarchy,"paper map exclusively owns the hands during reading");
                c.CloseHomeChart(true);yield return null;c.PresentCharacterFrame();
                Check(c.Hands.Rod.gameObject.activeInHierarchy,"closing restores the original fishing tool");
                Check(ReferenceEquals(career,c.Career) && c.Career.Backpack.Selected==selected,"map never replaces career or selected inventory slot");
                Debug.Log("[PORTABLE-CHART] EVIDENCE: "+Path.GetFullPath(Evidence)+"; camera renders cover draw stages; full-HUD backbuffer capture requires image inspection. InputSystem injection is not physical keyboard or headset certification.");
            }
            finally
            {
                Key(keyboard);InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
                InputSystem.settings=previousSettings;UnityEngine.Object.Destroy(settings);
                c.CloseHomeChart(true);
                lanternProperty.SetValue(c,lanternBefore);canProperty.SetValue(c.Farm,canBefore);c.Farm.ApplyPresentation();
                if(c.InventoryOpen)c.ToggleInventory();
                if(c.Paused)c.TogglePause();
                c.ReturnToHarbor();c.SelectNavigation(navigation);
                if(c.FirstPerson!=firstPerson)c.ToggleView();
            }
        }

        private static void CheckPresentation(FishingPrototype c,string label,bool firstPerson)
        {
            var paper=c.PortableChart.Paper;var character=c.Shore.Character;
            Check(paper!=null && paper.gameObject.activeInHierarchy,label+": physical paper is visible");
            Check(paper.GetComponentsInChildren<Collider>(true).Length==0
                && paper.GetComponentsInChildren<Camera>(true).Length==0,label+": paper adds no collider or second camera");
            int triangles=0;bool texture=false;var map=Resources.Load<FishingMapDefinition>("FishingMap");
            foreach(var mesh in paper.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh!=null)triangles+=mesh.sharedMesh.triangles.Length/3;
            foreach(var renderer in paper.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)if(material!=null && material.mainTexture==map.texture)texture=true;
            Check(triangles>0 && triangles<=16 && texture,label+": low-poly paper reuses the existing full-world texture");
            foreach(bool left in new[]{true,false})
            {
                var goal=left?c.Hands.Left:c.Hands.Right;
                Check(goal.Visible && Vector3.Distance(character.PalmPosition(left),goal.Palm.position)<.035f,label+": source-skinned "+(left?"left":"right")+" palm reaches the independent paper grip");
                Check(Vector3.Distance(character.ShoulderPosition(left),character.PalmPosition(left))<=character.ArmReach(left)+.005f,label+": paper does not stretch the arm");
            }
            if(firstPerson)
            {
                var p=c.viewCamera.WorldToViewportPoint(paper.position);
                Check(p.z>c.viewCamera.nearClipPlane && p.x>.05f && p.x<.95f && p.y>.03f && p.y<.95f,label+": paper centre lies inside the first-person view");
                CheckVisibleSkin(c,label);
            }
        }

        private static void CheckVisibleSkin(FishingPrototype c,string label)
        {
            var skin=c.Shore.Character.FirstPersonSkin;
            Check(skin!=null && skin.enabled && skin.gameObject.activeInHierarchy && !skin.forceRenderingOff,
                label+": actual first-person source skin is enabled for rendering");
            var shown=RenderPixels(c.viewCamera);
            Color32[] hidden;
            try{skin.enabled=false;hidden=RenderPixels(c.viewCamera);}
            finally{skin.enabled=true;}
            foreach(bool left in new[]{true,false})
            {
                float aspect=c.viewCamera.aspect;c.viewCamera.aspect=1280f/720;
                var palm=c.viewCamera.WorldToViewportPoint(c.Shore.Character.PalmPosition(left));
                c.viewCamera.aspect=aspect;
                int x=Mathf.RoundToInt(palm.x*1280),y=Mathf.RoundToInt(palm.y*720),count=0;
                for(int py=Mathf.Max(0,y-40);py<Mathf.Min(720,y+40);py++)
                    for(int px=Mathf.Max(0,x-40);px<Mathf.Min(1280,x+40);px++)
                    {
                        int i=py*1280+px;
                        int difference=Math.Abs(shown[i].r-hidden[i].r)+Math.Abs(shown[i].g-hidden[i].g)+Math.Abs(shown[i].b-hidden[i].b);
                        if(difference>18)count++;
                    }
                if(count<20)
                {
                    SavePixels(shown,label+"-skin-visible");SavePixels(hidden,label+"-skin-hidden");
                }
                Check(palm.z>c.viewCamera.nearClipPlane && count>=20,label+": rendered "+(left?"left":"right")+" hand contributes visible pixels near its palm; pixels="+count+" viewport="+palm);
            }
        }

        private static Color32[] RenderPixels(Camera camera)
        {
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float aspect=camera.aspect;
            var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.aspect=1280f/720;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture=oldTarget;camera.aspect=aspect;RenderTexture.active=oldActive;
                RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(image);
            }
        }

        private static void SavePixels(Color32[] pixels,string label)
        {
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{image.SetPixels32(pixels);image.Apply();File.WriteAllBytes(Path.Combine(Evidence,label.Replace(' ','-')+".png"),image.EncodeToPNG());}
            finally{UnityEngine.Object.Destroy(image);}
        }
    }
}
