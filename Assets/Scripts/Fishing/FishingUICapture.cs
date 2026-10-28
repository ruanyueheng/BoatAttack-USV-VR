using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>V044 native full-HUD captures of the modal storybook screens (pause, settings, saves, bag, journal, dialogue).</summary>
    public sealed class FishingUICapture : MonoBehaviour
    {
        public const string EnableFlag="-fishing-ui-capture";
        private FishingPrototype _c;
        [Serializable] private sealed class Entry {public string stage,directory;}
        [Serializable] private sealed class Manifest {public string version=FishingPrototype.Version;public bool physicalInputTested=false;public List<Entry> captures=new List<Entry>();}
        private readonly Manifest _manifest=new Manifest();
        public void Begin(FishingPrototype c)
        {
            if(!c.Automated || !c.HasIsolatedProfile || c.AcceptanceOptions==null || c.AcceptanceOptions.Automatic || string.IsNullOrEmpty(c.AcceptanceOptions.OutputDirectory) || !FishingPrototype.HasArgument(EnableFlag))
                throw new InvalidOperationException("UI capture needs an explicit isolated manual acceptance session.");
            _c=c;Application.logMessageReceived+=Error;StartCoroutine(Run());
        }
        private void Error(string message,string stack,LogType type)
        {if(type==LogType.Exception || type==LogType.Error){Debug.LogWarning("[UI-CAPTURE] Abort after runtime error: "+message);Application.Quit(1);}}
        private static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException("[UI-CAPTURE] "+text);}
        private IEnumerator Run()
        {
            yield return null;yield return null;
            _c.Acceptance.SelectCase(FishingAcceptanceCase.HomeInterior);yield return null;
            var hud=_c.GetComponent<FishingHUD>();Require(hud!=null,"Desktop HUD exists");
            _c.TogglePause();Require(_c.Paused,"Pause opens");
            var shot=Shot("pause");while(shot.MoveNext())yield return shot.Current;
            hud.SettingsTab=0;_c.MenuControls=true;shot=Shot("settings-controls");while(shot.MoveNext())yield return shot.Current;
            hud.SettingsTab=2;shot=Shot("settings-audio");while(shot.MoveNext())yield return shot.Current;
            _c.MenuControls=false;_c.OpenSaveMenu("load");shot=Shot("saves");while(shot.MoveNext())yield return shot.Current;
            _c.CloseSaveMenu();_c.TogglePause();yield return null;Require(!_c.Paused,"Pause closes");
            _c.ToggleInventory();Require(_c.InventoryOpen,"Backpack opens");shot=Shot("backpack");while(shot.MoveNext())yield return shot.Current;
            _c.ToggleInventory();yield return null;
            _c.ToggleJournal();Require(_c.JournalOpen,"Journal opens");
            foreach(int page in new[]{0,4,5,8})
            {hud.JournalPage=page;shot=Shot("journal-"+page);while(shot.MoveNext())yield return shot.Current;}
            _c.ToggleJournal();yield return null;
            bool talked=false;
            foreach(int id in new[]{5,0,1,2,3})
            {
                var person=_c.Residents.Person(id);if(person==null)continue;
                var seed=person.position+person.forward*2.2f;
                if(!_c.Shore.Ground(seed,out var feet) || !_c.Shore.Relocate(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(person.position-seed,Vector3.up))))continue;
                for(int i=0;i<8;i++)_c.Shore.Move(Vector2.zero,false,false,false,.02f);
                // Face the resident's head so the shot shows them above the dialogue panel.
                _c.View.ResetLook();_c.PresentCharacterFrame();var delta=person.position+Vector3.up*1.55f-_c.viewCamera.transform.position;
                float pitch=Mathf.Atan2(-delta.y,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude)*Mathf.Rad2Deg;
                _c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));_c.PresentCharacterFrame();
                for(int i=0;i<6;i++)yield return null;
                if(_c.Residents.Talk(id)){talked=true;break;}
            }
            Require(talked,"A resident conversation opens");
            // Frame the resident directly: 2.2 m ahead of them, 1.6 m high, looking at the head (after they turn to talk).
            for(int i=0;i<30;i++)yield return null;
            var talker=_c.Residents.Person(_c.Residents.ActiveId);var animator=talker.GetComponentInChildren<Animator>();
            var headBone=animator!=null && animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            var head=headBone!=null?headBone.position:talker.position+Vector3.up*1.55f;
            var forward=Vector3.ProjectOnPlane(talker.forward,Vector3.up).normalized;
            Vector3 eye=default;bool clear=false;
            // Prefer straight ahead; swing around the resident until the eye is in open air with a clear line to the head.
            foreach(float yaw in new[]{0f,25,-25,50,-50,80,-80})
            {
                eye=new Vector3(talker.position.x,talker.position.y+1.6f,talker.position.z)+Quaternion.Euler(0,yaw,0)*forward*2.2f;
                if(!Physics.CheckSphere(eye,.2f,~0,QueryTriggerInteraction.Ignore) && !Physics.Linecast(eye,head-(head-eye).normalized*.35f,~0,QueryTriggerInteraction.Ignore)){clear=true;break;}
            }
            Debug.Log("[UI-CAPTURE] Dialogue camera resident="+_c.Residents.ActiveId+" head="+head+" eye="+eye+" clear="+clear+" humanHead="+(headBone!=null));
            var pin=_c.viewCamera.gameObject.AddComponent<CameraPin>();pin.Set(eye,Quaternion.LookRotation(head-eye,Vector3.up));
            shot=Shot("dialogue");while(shot.MoveNext())yield return shot.Current;
            // Deterministic states: a fresh line is mid-reveal ("正在说"); the second story beat is the resident's question.
            var residents=_c.Residents;residents.Choose(0);
            shot=Shot("dialogue-typing",.05f);while(shot.MoveNext())yield return shot.Current;
            Require(residents.Typing,"Resident is still speaking when the typing frame is taken");
            residents.Reveal();residents.Choose(0);
            if(residents.Asking){residents.Reveal();shot=Shot("dialogue-asking");while(shot.MoveNext())yield return shot.Current;}
            else Debug.LogWarning("[UI-CAPTURE] This resident has no question line; asking card not captured.");
            Destroy(pin);
            File.WriteAllText(Path.Combine(_c.AcceptanceOptions.OutputDirectory,"ui-manifest.json"),JsonUtility.ToJson(_manifest,true));
            Debug.Log("[UI-CAPTURE] PASS: "+_manifest.captures.Count+" native full-HUD captures; physical input was not tested.");Application.Quit(0);
        }
        private IEnumerator Shot(string stage,float settle=.6f)
        {
            yield return new WaitForSecondsRealtime(settle);
            var capture=_c.Acceptance.CaptureFrame();while(capture.MoveNext())yield return capture.Current;
            Require(FishingAcceptance.Failure==null && _c.Acceptance.CaptureDirectory!=null,"Native backbuffer and opaque HUD proof are present");
            _manifest.captures.Add(new Entry{stage=stage,directory=_c.Acceptance.CaptureDirectory});
            Debug.Log("[UI-CAPTURE] Captured "+stage);
        }
        private void OnDestroy(){Application.logMessageReceived-=Error;}
        /// <summary>Holds the camera pose after the view rig's LateUpdate so the capture frame is exact.</summary>
        [DefaultExecutionOrder(32000)]
        private sealed class CameraPin : MonoBehaviour
        {
            private Vector3 _p;private Quaternion _r;
            public void Set(Vector3 p,Quaternion r){_p=p;_r=r;transform.SetPositionAndRotation(p,r);}
            private void LateUpdate()=>transform.SetPositionAndRotation(_p,_r);
        }
    }
}
