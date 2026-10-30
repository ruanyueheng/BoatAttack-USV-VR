using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using BoatAttack.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace BoatAttack.EditorTools
{
    [InitializeOnLoad]
    public static class FishingFieldValidation
    {
        private const string Key="Fishing.FieldValidation.";
        private static IEnumerator _checks;private static float _next;
        private static int _lastCheckFrame=-1;
        static FishingFieldValidation()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>{if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))SessionState.SetString(Key+"error",message);};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"active",false))return;
                SessionState.SetBool(Key+"active",false);string error=SessionState.GetString(Key+"error","");
                if(!SessionState.GetBool(Key+"done",false) && error=="")error="Stopped before all checks";
                if(error=="")Debug.Log("[FIELD] PASS: virtual keyboard locomotion, capsule clearance, bag/pickup transactions, real scene animal observation, v6 JSON and astern rigidbody thrust. Not physical input/device certification.");
                else Debug.LogError("[FIELD] FAIL: "+error);
                EditorApplication.Exit(error==""?0:1);
            };
        }
        public static void Capture(){SessionState.SetBool(Key+"capture",true);Run();}
        private static void Snapshot(FishingPrototype c,string name,Vector3 target,bool keepView=false)
        {
            if(!SessionState.GetBool(Key+"capture",false))return;
            var camera=c.viewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);Texture2D image=null;
            try
            {
                c.Shore.SyncRider();c.Hands.Apply();c.FieldWorld.ApplyHeldTool();
                if(!keepView){camera.transform.position=target+new Vector3(1.7f,1.1f,2.6f);camera.transform.LookAt(target);}
                camera.fieldOfView=52;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                string directory=SessionState.GetString(Key+"images","");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
                Debug.Log("[FIELD] IMAGE: "+Path.Combine(directory,name+".png")+" (camera render, no HUD)");
            }
            finally{camera.targetTexture=previous;camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
        }
        public static void Run()
        {
            SessionState.SetString(Key+"images",Path.GetFullPath(Path.Combine("Logs","field-preview-"+Guid.NewGuid().ToString("N"))));
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"done",false);SessionState.SetString(Key+"error","");
            SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+240);
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","Timeout");
            if(SessionState.GetString(Key+"error","")!=""){if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();return;}
            if(!EditorApplication.isPlaying || Time.realtimeSinceStartup<_next || Time.frameCount==_lastCheckFrame)return;
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();if(c==null || c.FieldWorld==null)return;
            try
            {
                if(_checks==null)_checks=Checks(c);_next=Time.realtimeSinceStartup+.02f;
                _lastCheckFrame=Time.frameCount;
                if(!_checks.MoveNext()){SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}
            }
            catch(Exception ex){SessionState.SetString(Key+"error",ex.ToString());EditorApplication.ExitPlaymode();}
        }
        private static void Check(bool success,string label){if(!success)throw new Exception(label);Debug.Log("[FIELD] CHECK: "+label);}
        private static void Step(FishingPrototype c,Keyboard keyboard,params UnityEngine.InputSystem.Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();c.Shore.Tick(true,.02f);
        }
        private static void Place(FishingPrototype c,Vector3 feet)
        {
            c.Shore.Body.enabled=false;c.Shore.Actor.position=feet+Vector3.up*.035f;c.Shore.Body.enabled=true;
            c.View.ResetLook();c.View.ApplyView(true,true);Physics.SyncTransforms();
        }
        private static IEnumerator Checks(FishingPrototype c)
        {
            yield return null;Check(c.HasIsolatedProfile && c.Automated,"test profile is isolated");
            Check(c.FieldWorld.Pickups.Count==FishingBackpack.PickupCount,"all sixteen stable-ID pickups, including island caches, are present");
            var pickupIds=new bool[FishingBackpack.PickupCount];
            foreach(var placedPickup in c.FieldWorld.Pickups)
            {
                Check(placedPickup.Id>=0 && placedPickup.Id<pickupIds.Length && !pickupIds[placedPickup.Id],"pickup ID is unique and save-compatible: "+placedPickup.Id);
                pickupIds[placedPickup.Id]=true;
                Check(placedPickup.Root!=null && c.Shore.Ground(placedPickup.Feet,out _),"pickup has a real model and supported ground: "+placedPickup.Id);
            }
            Check(c.FieldWorld.Animals.Count==8,"eight land animals spawned without changing terrain");
            foreach(var a in c.FieldWorld.Animals)Check(c.Shore.Ground(a.Feet,out _) && c.Shore.Clearance(a.Feet),"animal starts on clear dry ground: "+a.Kind);
            var rb=c.Player.engine.RB;var site=c.Shore.Sites[0];rb.position=site.Berth+Vector3.up*.5f;rb.rotation=Quaternion.identity;rb.velocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"real landing for locomotion tests");
            var previousKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                for(int i=0;i<8;i++)Step(c,keyboard);
                Check(c.Shore.Grounded,"character settles onto terrain");
                float baseY=c.Shore.Position.y,peak=baseY;Step(c,keyboard,UnityEngine.InputSystem.Key.Space);
                for(int i=0;i<100;i++){Step(c,keyboard,UnityEngine.InputSystem.Key.Space);peak=Mathf.Max(peak,c.Shore.Position.y);}
                Check(peak-baseY>.8f && peak-baseY<1.35f,"Space event produces one bounded jump arc: "+(peak-baseY));
                Check(c.Shore.Grounded && Mathf.Abs(c.Shore.Position.y-baseY)<.12f,"held Space lands without auto-jumping");
                if(!c.FirstPerson)c.ToggleView();
                c.PresentCharacterFrame();
                for(int frame=0;frame<120 && !c.Shore.Character.IsStanceSettled(true,false);frame++)
                {yield return null;Step(c,keyboard);c.PresentCharacterFrame();}
                float standingEye=c.viewCamera.transform.position.y-c.Shore.Position.y;
                Step(c,keyboard,UnityEngine.InputSystem.Key.LeftCtrl);
                for(int frame=0;frame<120 && !c.Shore.Character.IsStanceSettled(true,true);frame++)
                {yield return null;Step(c,keyboard,UnityEngine.InputSystem.Key.LeftCtrl);c.PresentCharacterFrame();}
                c.PresentCharacterFrame();
                // v0.11 镜头使用真实人物校准眼位，不再用旧程序化人的固定 0.88m 偏移。
                Check(c.Shore.Crouched && Mathf.Abs(c.Shore.Body.height-1.08f)<.01f && c.Shore.Character.IsStanceSettled(true,true)
                    && Vector3.Distance(c.viewCamera.transform.position,c.Shore.Character.EyePosition)<.03f
                    && standingEye-(c.viewCamera.transform.position.y-c.Shore.Position.y)>.35f,"Ctrl lowers real capsule and calibrated character eye");
                c.ToggleView();Snapshot(c,"crouch",c.Shore.Position+Vector3.up*.65f);c.ToggleView();
                var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.transform.position=c.Shore.Position+Vector3.up*1.5f;ceiling.transform.localScale=new Vector3(1.5f,.15f,1.5f);Physics.SyncTransforms();
                Step(c,keyboard);Check(c.Shore.Crouched,"low ceiling prevents standing into geometry");
                UnityEngine.Object.DestroyImmediate(ceiling);Physics.SyncTransforms();Step(c,keyboard);Check(!c.Shore.Crouched,"standing recovers once overhead clears");
                var feet=c.Shore.Position;Step(c,keyboard);c.Shore.Tick(false,.02f);Step(c,keyboard,UnityEngine.InputSystem.Key.Space);
                Check(c.Shore.Position.y<=feet.y+.03f,"focus/input recovery with Space held cannot jump");
                c.ToggleInventory();feet=c.Shore.Position;Step(c,keyboard,UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.Space);
                Check(c.InventoryOpen && c.Paused && c.Shore.Position==feet,"inventory modal blocks movement and jump");
                c.MoveFieldItem(0,9);Check(c.Career.Backpack.At(9).item==FieldItem.FieldGuide,"inventory UI command uses persistent production slots");
                c.MoveFieldItem(9,0);c.ToggleInventory();Check(!c.Paused,"inventory close restores simulation");
                // Real source mapping, not a synthetic USVInputState.
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.S));InputSystem.Update();var state=new USVDesktopInputSource().Read();
                Check(state.throttle==-1 && state.brake==0,"S keyboard maps to reverse with no brake");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.LeftShift));InputSystem.Update();state=new USVDesktopInputSource().Read();
                Check(state.brake==1 && state.throttle==0,"Shift is an independent brake");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.F));InputSystem.Update();
                Check(!new USVDesktopInputSource().Read().pause,"F tool binding no longer pauses");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            }
            finally{InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();}
            var pickup=c.FieldWorld.Pickups[0];Place(c,pickup.Feet+Vector3.back*.8f);c.viewCamera.transform.LookAt(pickup.Root.position);
            Check(c.FieldWorld.TryPickup() && c.Career.Backpack.Collected(pickup.Id) && !pickup.Root.gameObject.activeSelf,"nearby aimed pickup moves from actual world into backpack");
            int used=c.Career.Backpack.UsedSlots;c.FieldWorld.TryPickup();Check(c.Career.Backpack.UsedSlots==used,"repeated E cannot duplicate the same pickup");
            foreach(var a in c.FieldWorld.Animals)
            {
                if(c.Career.Backpack.Seen(a.Kind))continue;
                Place(c,a.Feet+Vector3.back*1.4f);c.viewCamera.transform.LookAt(a.Root.position+Vector3.up*.4f);
                if(a.Kind==LandAnimalKind.Rabbit)
                {
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Field sight occlusion fixture";
                    wall.transform.position=Vector3.Lerp(c.viewCamera.transform.position,a.Root.position+Vector3.up*.4f,.5f);
                    wall.transform.rotation=c.viewCamera.transform.rotation;wall.transform.localScale=new Vector3(2,2,.15f);Physics.SyncTransforms();
                    Check(!c.FieldWorld.Observe() && !c.Career.Backpack.Seen(a.Kind),"solid scene geometry blocks animal observation");
                    UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
                    // Allow renderer occlusion/culling to catch up after removing the temporary wall.
                    yield return null;c.viewCamera.transform.LookAt(a.Root.position+Vector3.up*.4f);
                }
                Check(c.FieldWorld.Observe() && c.Career.Backpack.Seen(a.Kind),"camera and scene line-of-sight observation: "+a.Kind);
                Snapshot(c,a.Kind.ToString(),a.Root.position+Vector3.up*.35f,true);
            }
            Check(c.Career.Backpack.AnimalMask==7,"all three species can enter field log");
            for(int i=0;i<200;i++)c.FieldWorld.TickAnimals(.1f);
            foreach(var a in c.FieldWorld.Animals)Check(Vector3.Distance(a.Feet,a.Home)<5.1f && c.Shore.Ground(a.Feet,out _) && c.Shore.Clearance(a.Feet),"roaming remains on dry clear ground near home: "+a.Kind);
            Check(FishingFieldWorld.AlertRadius(true,false)<FishingFieldWorld.AlertRadius(false,false) && FishingFieldWorld.AlertRadius(false,true)>FishingFieldWorld.AlertRadius(false,false),"crouch and sprint change wildlife alert distance");
            string path=Path.Combine(Application.temporaryCachePath,"field-v6-"+Guid.NewGuid().ToString("N")+".json");
            FishingSaveStore.Save(path,c.Career);var restored=FishingSaveStore.Load(path,out var warning);
            Check(warning=="" && restored.Backpack.Collected(pickup.Id) && restored.Backpack.AnimalMask==7,"Unity JSON preserves slots, claims and observations");
            var old=c.Career.Snapshot();old.version=5;old.backpack=null;old.selectedSlot=old.collectedPickupMask=old.landAnimalMask=0;old.coins=123;
            string legacy=path+".v5";string original=JsonUtility.ToJson(old);File.WriteAllText(legacy,original);
            var migrated=FishingSaveStore.LoadWithLegacyFallback(path+".new",legacy,out warning);FishingSaveStore.Save(path+".new",migrated);
            Check(migrated.Coins==123 && migrated.Backpack.Equipped==FieldItem.FieldGuide && File.ReadAllText(legacy)==original,"v5 migration is read-only and adds starter guide");
            while(c.Career.Backpack.TryAdd(FieldItem.Shell,1)){}
            var remaining=c.FieldWorld.Pickups[1];Place(c,remaining.Feet+Vector3.back*.8f);c.viewCamera.transform.LookAt(remaining.Root.position);
            Check(c.FieldWorld.TryPickup() && remaining.Root.gameObject.activeSelf && !c.Career.Backpack.Collected(remaining.Id),"full inventory leaves visible unclaimed world pickup");
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);
            for(int i=0;i<70;i++)yield return null;
            rb.velocity=Vector3.zero;
            for(int i=0;i<90;i++){c.Player.engine.Accelerate(-1);yield return null;}
            float speed=Vector3.Dot(rb.velocity,Vector3.ProjectOnPlane(rb.transform.forward,Vector3.up).normalized);
            Check(speed<-.4f,"real submerged engine/Rigidbody travels astern from rest: "+speed);
        }
    }
}
