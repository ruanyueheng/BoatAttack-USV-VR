using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using BoatAttack.VR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BoatAttack.EditorTools
{
    public static class FishingV020Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V020");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[V020] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v020-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            for(int frame=0;frame<30;frame++)yield return null;
            for(int island=0;island<4;island++)
            {
                var t=c.Islands.WorldRoot.GetChild(island);
                Check(Vector3.Distance(t.position,FishingIslandWorld.Center(island))<.01f,"Saved island geometry agrees with navigation and landing coordinates "+island);
                var ground=t.Find("Walkable beach and interior").GetComponent<MeshCollider>().sharedMesh;
                Check(ground==Resources.Load<Mesh>("FishingV020/Terrain"+island) && ground.triangles.Length/3==6656 && ground.bounds.size.y<6,"Imported shore axes, metre scale and topology "+island);
                if(island==0)Check(ground.bounds.center.x<0 && ground.bounds.center.z<0,"FBX handedness preserves both authored crescent and footpath directions");
                Check(t.Find(FishingV020WorldBuilder.ArtRoot)!=null && !t.Find("Distinct natural landmark").gameObject.activeSelf,"Original v20 art replaces old island blockout "+island);
                c.viewCamera.transform.SetPositionAndRotation(FishingIslandWorld.Center(island)+new Vector3(38,27,48),Quaternion.LookRotation(new Vector3(-38,-24,-48)));
                if(island>=2)foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                    if(renderer.name.Contains("Hero_") && renderer.name.Contains("LOD"))Check(!renderer.bounds.Contains(c.viewCamera.transform.position)
                        && !renderer.bounds.Contains(FishingIslandWorld.Berth(island)+Vector3.up*1.7f),"Southern island arrival and overview stay outside old cliff volumes "+island);
                FishingV016Validation.Capture(c,evidence,"island-overview-"+island);
                c.viewCamera.transform.SetPositionAndRotation(FishingIslandWorld.Center(island)+new Vector3(0,3.5f,30),Quaternion.LookRotation(new Vector3(0,1,-28)));
                FishingV016Validation.Capture(c,evidence,"island-arrival-"+island);
            }
            // Batch mode has no focused Game View; route only this isolated fixture's synthetic devices.
            var originalSettings=InputSystem.settings;var fixtureSettings=UnityEngine.Object.Instantiate(originalSettings);
            fixtureSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            fixtureSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=fixtureSettings;
            var rb=c.Player.engine.RB;var keyboard=InputSystem.AddDevice<Keyboard>();
            var input=USVInputRouter.Instance;
            Check(input.Settings.Mode!=USVInputMode.XR,"Desktop validation uses desktop input settings");
            bool comfort=c.ComfortMode;if(comfort)c.ToggleComfort();
            try
            {
                float cruise=0,boost=0;
                for(int pass=0;pass<2;pass++)
                {
                    c.ReturnToHarbor();rb.position=new Vector3(-350,.35f,100);rb.rotation=Quaternion.Euler(0,180,0);rb.velocity=rb.angularVelocity=Vector3.zero;
                    Physics.SyncTransforms();float until=Time.time+1;while(Time.time<until)yield return null;
                    InputSystem.QueueStateEvent(keyboard,pass==0?new KeyboardState(Key.W):new KeyboardState(Key.W,Key.LeftCtrl));
                    until=Time.time+6;while(Time.time<until)yield return null;
                    Check(c.Boosting==(pass==1) && c.Speed>4,"Real keyboard W/Ctrl reaches production driving "+pass+" throttle="+input.Current.throttle+" boost="+input.Current.boost+" speed="+c.Speed+" source="+input.ActiveSource);
                    if(pass==0)cruise=c.Speed;else boost=c.Speed;
                    Check(c.Speed<(pass==0?1f:1.35f)*c.Player.engine.forwardSpeedLimit+.5f,"Water speed respects bounded engine envelope "+pass);
                }
                Check(boost>cruise*1.20f,"Held boost increases real cruise speed by at least 20%; cruise="+cruise+" boost="+boost);
                float overheatAt=Time.time+3;while(Time.time<overheatAt)yield return null;
                Check(c.Boost.Overheated && !c.Boosting && input.Current.boost,"V21 held Ctrl overheats and disables actual boost");
                c.DockConsole.PreviewForValidation();
                Check(Array.Exists(c.DockConsole.GetComponentsInChildren<UnityEngine.UI.Text>(),label=>label.text.Contains("HEAT ") && label.text.Contains("COOL TO 25%")),"V21 actual XR console explains the active thermal lock");
                float coolingAt=Time.time+4;while(Time.time<coolingAt)yield return null;
                Check(c.Boost.Overheated && !c.Boosting && c.Speed<boost*.95f,"V21 thermal lock returns actual hull to cruise speed");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftCtrl,Key.LeftShift));yield return null;yield return null;
                Check(!c.Boosting && !input.Current.boost && input.Current.brake==1,"Actual Shift brake overrides held Ctrl");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.LeftCtrl));yield return null;yield return null;
                Check(!c.Boosting && input.Current.throttle==-1,"Reverse never boosts");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftCtrl));yield return null;yield return null;
                float heat=c.Boost.Heat;c.ToggleJournal();for(int i=0;i<20;i++)yield return null;Check(!c.Boosting && c.Boost.Heat==heat,"Journal blocks propulsion and preserves heat");c.ToggleJournal();
                c.ToggleInventory();yield return null;Check(!c.Boosting,"Inventory blocks propulsion and boost feedback");c.ToggleInventory();
                c.TogglePause();Check(!c.Boosting,"Pause clears boost feedback even with no physics ticks");c.TogglePause();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
                Check(!c.Boosting && !input.Current.boost,"Release ends boost through the real input router");
                coolingAt=Time.time+12;while(Time.time<coolingAt)yield return null;
                Check(!c.Boost.Overheated && c.Boost.Heat<.25f,"V21 cooling unlocks another burst");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftCtrl));for(int i=0;i<10;i++)yield return null;
                Check(c.Boosting,"V21 production thrust can boost again after cooling");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                c.ReturnToHarbor();rb.position=new Vector3(-350,.35f,100);rb.velocity=rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();
                for(int i=0;i<60;i++)yield return null;
                foreach(Key turn in new[]{Key.D,Key.A})
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,turn));
                    float yaw=0,tilt=0,until=Time.time+5;int wet=0,total=0;
                    while(Time.time<until){yaw=Mathf.Max(yaw,Mathf.Abs(rb.angularVelocity.y));tilt=Mathf.Max(tilt,Vector3.Angle(c.Player.transform.up,Vector3.up));if(c.Player.engine.InWater)wet++;total++;yield return null;}
                    Check(yaw>.9f && yaw<=2.5f && tilt<1 && wet>total*.95f,"Faster left/right turning remains upright and powered: "+turn+" yaw="+yaw+" tilt="+tilt);
                }
            }
            finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings=originalSettings;UnityEngine.Object.Destroy(fixtureSettings);if(comfort!=c.ComfortMode)c.ToggleComfort();c.ReturnToHarbor();}
            Debug.Log("[V020] EVIDENCE: "+evidence);
        }
    }
}
