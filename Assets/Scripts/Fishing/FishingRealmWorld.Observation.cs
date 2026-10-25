using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingRealmWorld
    {
        private bool _observing,_focusReady,_observationDesktop,_exitHeld;
        private float _observationFov;
        private Transform _scopeYaw,_scopePitch,_eyepiece,_aimMarker;
        private Mesh _starSphereMesh;
        private Renderer[] _scopeSurfaces;
        public bool ObservingStars=>_observing;
        private static readonly Vector2[][] StarPatterns={
            new[]{new Vector2(-3,0),new Vector2(-1,.4f),Vector2.zero,new Vector2(1,.4f),new Vector2(3,0),Vector2.zero,new Vector2(0,2),Vector2.zero,new Vector2(0,-2)},
            new[]{new Vector2(0,2),new Vector2(0,-1),new Vector2(-1,-2),new Vector2(-2,-1),new Vector2(-1,-2),new Vector2(0,-1),new Vector2(1,-2),new Vector2(2,-1),new Vector2(1,-2),new Vector2(0,-1),new Vector2(0,2),new Vector2(-.6f,2.3f),new Vector2(.6f,2.3f),new Vector2(0,2)},
            new[]{new Vector2(0,3),new Vector2(-2,-1),new Vector2(2,-1),new Vector2(0,3),new Vector2(0,-2),new Vector2(-2,-2),new Vector2(2,-2)}
        };
        public static IReadOnlyList<Vector2> StarPattern(int stage)=>StarPatterns[Mathf.Clamp(stage,0,2)];
        public static string StarPatternName(int stage)=>stage==0?"海鸥":stage==1?"船锚":"归航星";
        private void BuildObservation(Transform room)
        {
            var station=room.Find("Device 0");var stand=Model("TelescopeBase",station,Vector3.zero);
            var body=stand.gameObject.AddComponent<BoxCollider>();body.center=Vector3.up*.8f;body.size=new Vector3(1.35f,1.6f,1.35f);
            _scopeYaw=new GameObject("Telescope azimuth").transform;_scopeYaw.SetParent(station,false);_scopeYaw.localPosition=Vector3.up*2.1f;
            Model("TelescopeMount",_scopeYaw,Vector3.zero);
            _scopePitch=new GameObject("Telescope altitude").transform;_scopePitch.SetParent(_scopeYaw,false);
            var tube=Model("TelescopeTube",_scopePitch,Vector3.zero);var capsule=tube.gameObject.AddComponent<CapsuleCollider>();capsule.direction=2;capsule.radius=.28f;capsule.height=3.3f;capsule.center=new Vector3(0,0,.7f);
            _eyepiece=Marker(tube,"Eyepiece");
            _scopeSurfaces=station.GetComponentsInChildren<Renderer>();
            var b=new FishingMeshBuilder();b.Ellipsoid(Vector3.zero,Vector3.one,Color.white,8,4);_starSphereMesh=b.Finish("Shared constellation stars");
            for(int stage=0;stage<3;stage++)
            {
                var direction=Quaternion.Euler(-FishingShrineTrial.StarTargetPitch(stage),FishingShrineTrial.StarTargetYaw(stage),0)*Vector3.forward;
                var facing=Quaternion.LookRotation(direction,Vector3.up);var centre=station.localPosition+Vector3.up*2.1f+direction*70;
                var pattern=StarPatterns[stage];var line=FishingArt.Line("Constellation "+StarPatternName(stage),room,_lightMaterial,.035f,false);line.useWorldSpace=false;line.positionCount=pattern.Length;
                for(int i=0;i<pattern.Length;i++)
                {
                    var p=centre+facing*new Vector3(pattern[i].x*1.8f,pattern[i].y*1.8f,0);line.SetPosition(i,p);
                    var star=new GameObject("Star "+stage+" "+i,typeof(MeshFilter),typeof(MeshRenderer));star.transform.SetParent(room,false);star.transform.localPosition=p;star.transform.localScale=Vector3.one*.10f;
                    star.GetComponent<MeshFilter>().sharedMesh=_starSphereMesh;star.GetComponent<MeshRenderer>().sharedMaterial=_lightMaterial;
                }
            }
            _aimMarker=new GameObject("Tracked telescope reticle").transform;_aimMarker.SetParent(room,false);
            var reticle=FishingArt.Line("Telescope direction",_aimMarker,_lightMaterial,.009f,true);reticle.useWorldSpace=false;reticle.positionCount=32;
            for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32;reticle.SetPosition(i,new Vector3(Mathf.Cos(a)*.18f,Mathf.Sin(a)*.18f,0));}
            _aimMarker.gameObject.SetActive(false);
        }
        private bool BeginObservation()
        {
            if(!Inside || Island!=3 || _observing || _c.Paused)return false;
            _observationDesktop=_c.DesktopDisplay;_observationFov=_c.viewCamera.fieldOfView;_observing=true;_focusReady=false;_exitHeld=true;
            _c.StoryModalChanged();_c.FieldWorld.ClearToolPresentation();
            if(_observationDesktop)
            {
                foreach(var renderer in _scopeSurfaces)renderer.forceRenderingOff=true;
                _c.Shore.Character.SetTabletopView(true);_c.View.Pointer.LockState=CursorLockMode.Locked;_c.View.Pointer.Visible=false;
            }
            _c.Notice("寻找「"+StarPatternName(Trial.StarStage)+"」轮廓，保持对焦。",6);return true;
        }
        public void CancelObservation()
        {
            if(!_observing)return;
            _observing=false;Trial?.ReleaseFocus();_aimMarker.gameObject.SetActive(false);
            if(_observationDesktop)
            {
                foreach(var renderer in _scopeSurfaces)renderer.forceRenderingOff=false;
                if(_c.DesktopDisplay)_c.viewCamera.fieldOfView=_observationFov;
                _c.Shore.Character.SetTabletopView(false);_c.Shore.Character.SetOpticalView(false);
                _c.View.ResetInput();_c.View.Pointer.LockState=CursorLockMode.None;_c.View.Pointer.Visible=true;
            }
            _c.View.ResetLook();_c.Shore.ResetMoveInput();_c.StoryModalChanged();
        }
        public bool AimTelescope(Vector2 degrees,bool focusing,float seconds)
        {
            if(!_observing || !Inside || Island!=3 || _c.Paused || _c.StartMenuOpen)return false;
            bool completed=Trial.Observe(Trial.StarYaw+degrees.x,Trial.StarPitch+degrees.y,seconds,focusing);
            if(completed)
            {
                _c.WorldAudio?.Play("MechanismClick",_devices[0].position,.4f);_c.Regions.Chime(Trial.StarStage-1);Refresh();
                _c.Notice(Trial.Ready?"三幅星图已记录。收起望远镜，阅读观测台上的回响。":"已记录 · 下一幅寻找「"+StarPatternName(Trial.StarStage)+"」。",6);
            }
            return completed;
        }
        public bool TickObservationInput(Keyboard keyboard,Mouse mouse,bool focused,bool xr)
        {
            if(!_observing)return false;
            if(!Inside || _c.Paused || _c.StartMenuOpen || !focused || xr==_observationDesktop || Vector3.Distance(_c.Shore.Position,_devices[0].position)>3.2f)
            {CancelObservation();return true;}
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame){CancelObservation();return true;}
            if(_c.Automated)return true;
            Vector2 degrees=Vector2.zero;bool focus=false;
            if(xr)
            {
                var d=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);d.TryGetFeatureValue(CommonUsages.secondaryButton,out bool exit);
                if(exit && !_exitHeld){CancelObservation();return true;}_exitHeld=exit;
                bool tracked=_c.Hands.TryControllerPalm(false,out _,out _) && !_c.XRSeat.HeadBlocked;
                if(tracked)
                {
                    d.TryGetFeatureValue(CommonUsages.primary2DAxis,out var stick);degrees=stick*Time.deltaTime*35;
                    d.TryGetFeatureValue(CommonUsages.trigger,out float trigger);focus=trigger>.55f;
                }
                else {_focusReady=false;Trial.ReleaseFocus();return true;}
            }
            else
            {
                if(mouse!=null){var look=FishingViewRig.ScaleLookDelta(mouse.delta.ReadValue(),_c.MouseSensitivity,_c.InvertLook);degrees=new Vector2(look.x,-look.y);focus=mouse.leftButton.isPressed;}
                if(keyboard!=null)degrees+=new Vector2((keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0),(keyboard.upArrowKey.isPressed?1:0)-(keyboard.downArrowKey.isPressed?1:0))*Time.deltaTime*35;
            }
            if(!focus)_focusReady=true;AimTelescope(degrees,focus && _focusReady,Time.deltaTime);return true;
        }
        public void PresentObservation()
        {
            if(!Inside || Island!=3 || Trial==null)return;
            _scopeYaw.localRotation=Quaternion.Euler(0,Trial.StarYaw,0);_scopePitch.localRotation=Quaternion.Euler(-Trial.StarPitch,0,0);
            if(!_observing)return;
            if(_c.Paused || _c.StartMenuOpen){CancelObservation();return;}
            if(_c.DesktopDisplay)
            {
                _c.viewCamera.transform.SetPositionAndRotation(_eyepiece.position,Quaternion.LookRotation(_scopePitch.forward,Vector3.up));_c.viewCamera.fieldOfView=32;
            }
            else
            {
                _aimMarker.gameObject.SetActive(true);_aimMarker.SetPositionAndRotation(_scopePitch.position+_scopePitch.forward*20,_scopePitch.rotation);
            }
        }
    }
}
