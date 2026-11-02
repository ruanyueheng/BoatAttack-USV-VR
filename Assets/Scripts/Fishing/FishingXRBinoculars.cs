using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Physical, tool-mounted eyepieces. The binocular body and both hands stay visible; HMD projection is never changed.</summary>
    [DefaultExecutionOrder(1500)]
    public sealed class FishingXRBinoculars : MonoBehaviour
    {
        public const int Width=768,Height=768;
        public const float EyepieceRadius=.065f,RearLensZ=-.152f;
        public const float DefaultEyeSeparation=.064f,ComfortEyeDistance=.065f;
        public const float MinimumEyeDistance=.05f,MaximumEyeDistance=.20f;
        private FishingPrototype _c;
        private GameObject _window;
        private readonly Transform[] _lenses=new Transform[2];
        private Camera _camera;
        private RenderTexture _texture;
        private Material _material;
        private Mesh _quad;
        private float _zoomAt;
        private int _zoomDirection;
        public bool Active {get;private set;}
        public Camera OpticalCamera=>_camera;
        public RenderTexture OpticalTexture=>_texture;
        public Transform OpticalWindow=>_window!=null?_window.transform:null;
        public Transform LeftEyepiece=>_lenses[0];
        public Transform RightEyepiece=>_lenses[1];
        public float LensSeparation {get;private set;}=DefaultEyeSeparation;
        public static Vector3 EyepieceLocal(bool left,float eyeSeparation=DefaultEyeSeparation)
            =>new Vector3((left?-1:1)*eyeSeparation/(2*FishingFieldWorld.TrackedBinocularScale),0,RearLensZ);
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        private static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        private static bool Finite(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Quaternion.Dot(q,q)>.5f;
        public static float CalibratedEyeSeparation(Vector3 left,Vector3 right)
        {
            float distance=Vector3.Distance(left,right);
            return Finite(left) && Finite(right) && distance>=.05f && distance<=.08f?distance:DefaultEyeSeparation;
        }
        private static bool TryEyes(Camera camera,out Vector3 left,out Vector3 right)
        {
            left=right=Vector3.zero;if(camera==null)return false;
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(head.isValid && head.TryGetFeatureValue(CommonUsages.leftEyePosition,out var lp)
                && head.TryGetFeatureValue(CommonUsages.rightEyePosition,out var rp)
                && head.TryGetFeatureValue(CommonUsages.devicePosition,out var hp)
                && head.TryGetFeatureValue(CommonUsages.deviceRotation,out var hq)
                && Finite(lp) && Finite(rp) && Finite(hp) && Finite(hq)
                && Vector3.Distance(lp,rp)>=.05f && Vector3.Distance(lp,rp)<=.08f)
            {
                var basis=camera.transform.rotation*Quaternion.Inverse(hq);
                left=camera.transform.position+basis*(lp-hp);right=camera.transform.position+basis*(rp-hp);return true;
            }
            if(!camera.stereoEnabled)return false;
            left=camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.MultiplyPoint(Vector3.zero);
            right=camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.MultiplyPoint(Vector3.zero);
            float distance=Vector3.Distance(left,right);
            return Finite(left) && Finite(right) && distance>=.05f && distance<=.08f;
        }
        public static float EyeSeparation(Camera camera)=>TryEyes(camera,out var left,out var right)?CalibratedEyeSeparation(left,right):DefaultEyeSeparation;
        public static Vector3 EyeCentre(Camera camera)=>TryEyes(camera,out var left,out var right)?(left+right)*.5f:camera!=null?camera.transform.position:Vector3.zero;
        public static bool CanAlignAtEyes(Pose head,Vector3 leftPalm,Vector3 rightPalm)
        {
            if(!Finite(head.position) || !Finite(head.rotation) || !Finite(leftPalm) || !Finite(rightPalm))return false;
            var separation=rightPalm-leftPalm;
            var local=Quaternion.Inverse(head.rotation)*((leftPalm+rightPalm)*.5f-head.position);
            if(!(separation.magnitude>.10f && separation.magnitude<.32f
                && Vector3.Dot(separation.normalized,head.rotation*Vector3.right)>.65f
                && Mathf.Abs(local.x)<.10f && Mathf.Abs(local.y)<.10f && local.z>.07f && local.z<.27f))return false;
            var root=head.position+head.rotation*Vector3.forward*(ComfortEyeDistance-RearLensZ*FishingFieldWorld.TrackedBinocularScale);
            return Vector3.Distance(leftPalm,root+head.rotation*(FishingFieldWorld.TrackedBinocularLeftPalm*FishingFieldWorld.TrackedBinocularScale))<=.12f
                && Vector3.Distance(rightPalm,root+head.rotation*(FishingFieldWorld.TrackedBinocularRightPalm*FishingFieldWorld.TrackedBinocularScale))<=.12f;
        }
        public static bool CanObserve(Pose head,Pose tool,float scale,Vector3 leftPalm,Vector3 rightPalm,bool leftGrip,bool rightGrip)
        {
            if(!leftGrip || !rightGrip || !Finite(head.position) || !Finite(head.rotation) || !Finite(tool.position)
                || !Finite(tool.rotation) || !Finite(scale) || scale<=.01f || !Finite(leftPalm) || !Finite(rightPalm))return false;
            var leftMount=tool.position+tool.rotation*(FishingFieldWorld.TrackedBinocularLeftPalm*scale);
            var rightMount=tool.position+tool.rotation*(FishingFieldWorld.TrackedBinocularRightPalm*scale);
            if(Vector3.Distance(leftPalm,leftMount)>.12f || Vector3.Distance(rightPalm,rightMount)>.12f)return false;
            var rear=tool.position+tool.rotation*(Vector3.forward*RearLensZ*scale);
            var delta=rear-head.position;
            float ahead=Vector3.Dot(delta,head.rotation*Vector3.forward);
            return ahead>=MinimumEyeDistance && ahead<=MaximumEyeDistance
                && Mathf.Abs(Vector3.Dot(delta,head.rotation*Vector3.right))<=.07f
                && Mathf.Abs(Vector3.Dot(delta,head.rotation*Vector3.up))<=.07f
                && Vector3.Dot(tool.rotation*Vector3.forward,head.rotation*Vector3.forward)>=.9f
                && Vector3.Dot(tool.rotation*Vector3.right,head.rotation*Vector3.right)>=.85f;
        }
        public static float OpticalFieldOfView(float magnification,float eyeDistance=ComfortEyeDistance,float scale=FishingFieldWorld.TrackedBinocularScale)
        {
            if(!Finite(magnification))magnification=2;
            if(!Finite(eyeDistance))eyeDistance=ComfortEyeDistance;
            if(!Finite(scale) || scale<=0)scale=FishingFieldWorld.TrackedBinocularScale;
            return 2*Mathf.Atan(EyepieceRadius*scale/(Mathf.Clamp(eyeDistance,MinimumEyeDistance,MaximumEyeDistance)*Mathf.Clamp(magnification,2,8)))*Mathf.Rad2Deg;
        }
        public void Build(FishingPrototype controller)=>_c=controller;
        private void EnsureOptics(Transform tool)
        {
            if(_window!=null){if(_window.transform.parent!=tool)_window.transform.SetParent(tool,false);return;}
            var shader=Resources.Load<Shader>("FishingBinocularLens");
            if(shader==null)throw new System.InvalidOperationException("Missing binocular eyepiece shader");
            _texture=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){name="Binocular objective distant view",antiAliasing=1,filterMode=FilterMode.Bilinear};
            _texture.Create();
            _material=new Material(shader){name="Physical binocular eyepiece glass"};_material.SetTexture("_MainTex",_texture);
            _quad=new Mesh{name="Binocular rear-lens glass"};
            _quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};
            _quad.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};_quad.triangles=new[]{0,1,2,0,2,3};_quad.RecalculateBounds();
            _window=new GameObject("Physical binocular rear eyepieces"){layer=5};_window.transform.SetParent(tool,false);
            for(int i=0;i<2;i++)
            {
                var lens=new GameObject(i==0?"Left rear optical glass":"Right rear optical glass",typeof(MeshFilter),typeof(MeshRenderer)){layer=5};
                var t=lens.transform;t.SetParent(_window.transform,false);t.localPosition=EyepieceLocal(i==0);t.localScale=Vector3.one*EyepieceRadius*2;
                lens.GetComponent<MeshFilter>().sharedMesh=_quad;
                var renderer=lens.GetComponent<MeshRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                _lenses[i]=t;
            }
            var go=new GameObject("Binocular objective camera",typeof(Camera));go.transform.SetParent(transform,false);
            _camera=go.GetComponent<Camera>();_camera.CopyFrom(_c.viewCamera);
            _camera.enabled=false;_camera.stereoTargetEye=StereoTargetEyeMask.None;_camera.cameraType=CameraType.Reflection;
            _camera.ResetProjectionMatrix();_camera.ResetCullingMatrix();_camera.clearFlags=CameraClearFlags.Skybox;
            _camera.cullingMask&=~(1<<5);_camera.nearClipPlane=.1f;_camera.depth=_c.viewCamera.depth-10;
            _camera.allowHDR=false;_camera.allowMSAA=false;_camera.aspect=1;_camera.targetTexture=_texture;
            var data=_camera.GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderPostProcessing=false;data.renderShadows=false;
            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            _window.SetActive(false);
        }
        private bool Available=>_c!=null && _c.Career!=null && _c.Ashore
            && _c.Career.Backpack.Equipped==FieldItem.Binoculars && _c.FieldWorld.PresentedItem==FieldItem.Binoculars
            && !_c.InspectingItem && !_c.Paused && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay
            && !_c.InventoryOpen && !_c.JournalOpen && !_c.WorldActionBusy && !_c.Expedition.Aligning
            && !_c.Shore.Swimming && !_c.Shore.Climbing && !_c.XRNotebook.IsOpen && !_c.XRSeat.HeadBlocked;
        private void LateUpdate()
        {
            Vector3 left=Vector3.zero,right=Vector3.zero;
            bool tracked=_c!=null && _c.Hands.TryControllerPalm(true,out left,out _) && _c.Hands.TryControllerPalm(false,out right,out _);
            bool lg=FishingXRInput.Grip(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand));
            bool rg=FishingXRInput.Grip(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));
            Present(XRSettings.isDeviceActive && Application.isFocused && tracked,left,right,lg,rg);
            if(Active)ReadZoomAxis(FishingXRInput.Stick(InputDevices.GetDeviceAtXRNode(XRNode.RightHand)),Time.unscaledTime);
        }
        public void PresentForValidation(bool tracked,Vector3 leftPalm,Vector3 rightPalm,bool leftHeld=true,bool rightHeld=true)
        {
            if(_c==null || !_c.Automated)throw new System.InvalidOperationException("Isolated binocular validation only");
            Present(tracked,leftPalm,rightPalm,leftHeld,rightHeld);
        }
        private void Present(bool tracked,Vector3 leftPalm,Vector3 rightPalm,bool leftHeld,bool rightHeld)
        {
            var tool=_c!=null && _c.FieldWorld!=null?_c.FieldWorld.HeldTool:null;
            var eye=_c!=null?_c.viewCamera:null;
            Active=tracked && Available && tool!=null && tool.gameObject.activeInHierarchy && eye!=null
                && CanObserve(new Pose(EyeCentre(eye),eye.transform.rotation),new Pose(tool.position,tool.rotation),
                    Mathf.Abs(tool.lossyScale.x),leftPalm,rightPalm,leftHeld,rightHeld);
            if(!Active){Hide();return;}
            EnsureOptics(tool);_window.SetActive(true);_camera.enabled=true;
            LensSeparation=EyeSeparation(eye);
            for(int i=0;i<2;i++)_lenses[i].localPosition=EyepieceLocal(i==0,LensSeparation);
            _camera.transform.SetPositionAndRotation(tool.TransformPoint(new Vector3(0,0,.27f)),tool.rotation);
            UpdateOpticalFieldOfView();_camera.farClipPlane=eye.farClipPlane;
        }
        private void UpdateOpticalFieldOfView()
        {
            var tool=_c.FieldWorld.HeldTool;
            float distance=Vector3.Dot(tool.TransformPoint(Vector3.forward*RearLensZ)-EyeCentre(_c.viewCamera),_c.viewCamera.transform.forward);
            _camera.fieldOfView=OpticalFieldOfView(_c.View.BinocularMagnification,distance,Mathf.Abs(tool.lossyScale.x));
        }
        public void ReadZoomAxis(Vector2 axis,float now)
        {
            if(!Active || !Finite(axis.y) || !Finite(now))return;
            if(Mathf.Abs(axis.y)<.35f){_zoomDirection=0;return;}
            if(Mathf.Abs(axis.y)<.65f)return;
            int direction=axis.y>0?1:-1;
            if(direction!=_zoomDirection || now>=_zoomAt)
            {
                _zoomDirection=direction;_zoomAt=now+.3f;_c.View.AdjustMagnification(direction);UpdateOpticalFieldOfView();
            }
        }
        private void Hide(){if(_window!=null)_window.SetActive(false);if(_camera!=null)_camera.enabled=false;_zoomDirection=0;}
        private void OnDisable(){Active=false;Hide();}
        private void OnDestroy()
        {
            if(_camera!=null){_camera.enabled=false;_camera.targetTexture=null;Destroy(_camera.gameObject);}if(_window!=null)Destroy(_window);
            if(_texture!=null){_texture.Release();Destroy(_texture);}
            if(_material!=null)Destroy(_material);if(_quad!=null)Destroy(_quad);
        }
    }
}
