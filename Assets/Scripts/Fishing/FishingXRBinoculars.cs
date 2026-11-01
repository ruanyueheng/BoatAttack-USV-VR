using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>A local optical window. Magnification never rewrites either HMD eye projection or head pose.</summary>
    [DefaultExecutionOrder(1500)]
    public sealed class FishingXRBinoculars : MonoBehaviour
    {
        public const int Width=768,Height=432;
        public const float WindowDistance=.8f,WindowHeight=.5f;
        private FishingPrototype _c;
        private GameObject _window;
        private Camera _camera;
        private RenderTexture _texture;
        private Material _material;
        private Mesh _quad;
        private Text _label;
        private float _zoomAt;
        private int _zoomDirection;
        private Renderer _heldRenderer;
        private bool _hidTool;
        public bool Active {get;private set;}
        public Camera OpticalCamera=>_camera;
        public RenderTexture OpticalTexture=>_texture;
        public Transform OpticalWindow=>_window!=null?_window.transform:null;
        public static float OpticalFieldOfView(float magnification)
        {
            if(float.IsNaN(magnification)||float.IsInfinity(magnification))magnification=2;
            return 2*Mathf.Atan(WindowHeight/(2*WindowDistance*Mathf.Clamp(magnification,2,8)))*Mathf.Rad2Deg;
        }
        public void Build(FishingPrototype controller)=>_c=controller;
        private void EnsureOptics()
        {
            if(_window!=null)return;
            var shader=Resources.Load<Shader>("FishingBinocularLens");
            if(shader==null)throw new System.InvalidOperationException("Missing binocular optical-window shader");
            _texture=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){name="XR binocular distant view",antiAliasing=1,filterMode=FilterMode.Bilinear};
            _texture.Create();
            _material=new Material(shader){name="XR binocular eyepiece"};_material.SetTexture("_MainTex",_texture);
            _quad=new Mesh{name="XR binocular local optical window"};
            _quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};
            _quad.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};_quad.triangles=new[]{0,1,2,0,2,3};_quad.RecalculateBounds();
            _window=new GameObject("Binocular local magnified eyepiece",typeof(MeshFilter),typeof(MeshRenderer)){layer=5};
            _window.transform.SetParent(_c.viewCamera.transform,false);
            _window.transform.localPosition=Vector3.forward*WindowDistance;
            _window.transform.localScale=new Vector3(WindowHeight*Width/Height,WindowHeight,1);
            _window.GetComponent<MeshFilter>().sharedMesh=_quad;
            var renderer=_window.GetComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var labelRoot=new GameObject("Binocular magnification and controls",typeof(RectTransform),typeof(Canvas)){layer=5};
            labelRoot.transform.SetParent(_window.transform,false);
            var canvas=labelRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_c.viewCamera;
            var rect=labelRoot.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(768,42);rect.localPosition=new Vector3(0,-.57f,-.005f);rect.localScale=new Vector3(1/768f,1/432f,1);
            var text=new GameObject("Binocular label",typeof(RectTransform),typeof(Text)){layer=5};text.transform.SetParent(rect,false);
            var textRect=text.GetComponent<RectTransform>();textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.sizeDelta=Vector2.zero;
            _label=text.GetComponent<Text>();_label.font=FishingUIFont.Font;_label.fontSize=24;_label.alignment=TextAnchor.MiddleCenter;_label.color=new Color(1,.94f,.77f);_label.raycastTarget=false;
            var go=new GameObject("Binocular optical camera",typeof(Camera));go.transform.SetParent(transform,false);
            _camera=go.GetComponent<Camera>();_camera.CopyFrom(_c.viewCamera);
            _camera.enabled=false;_camera.stereoTargetEye=StereoTargetEyeMask.None;_camera.cameraType=CameraType.Reflection;
            _camera.ResetProjectionMatrix();_camera.ResetCullingMatrix();_camera.clearFlags=CameraClearFlags.Skybox;
            _camera.cullingMask&=~(1<<5);_camera.nearClipPlane=.75f;_camera.depth=_c.viewCamera.depth-10;
            _camera.allowHDR=false;_camera.allowMSAA=false;_camera.aspect=Width/(float)Height;_camera.targetTexture=_texture;
            var data=_camera.GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderPostProcessing=false;data.renderShadows=false;
            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            _window.SetActive(false);
        }
        private bool Available=>_c!=null && _c.Career!=null && _c.Ashore && _c.BinocularsRaised
            && _c.Career.Backpack.Equipped==FieldItem.Binoculars && !_c.InspectingItem && !_c.Paused
            && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay && !_c.InventoryOpen && !_c.JournalOpen
            && !_c.WorldActionBusy && !_c.Expedition.Aligning && !_c.Shore.Swimming && !_c.Shore.Climbing
            && !_c.XRNotebook.IsOpen && !_c.XRSeat.HeadBlocked;
        private void LateUpdate()
        {
            bool tracked=_c!=null && _c.Hands.TryControllerPalm(false,out _,out _);
            Present(XRSettings.isDeviceActive && Application.isFocused && tracked);
            if(Active)ReadZoomAxis(FishingXRInput.Stick(InputDevices.GetDeviceAtXRNode(XRNode.RightHand)),Time.unscaledTime);
        }
        public void PresentForValidation(bool tracked)
        {
            if(_c==null || !_c.Automated)throw new System.InvalidOperationException("Isolated binocular validation only");
            Present(tracked);
        }
        private void Present(bool tracked)
        {
            Active=tracked && Available;
            if(!Active){Hide();return;}
            EnsureOptics();_window.SetActive(true);_camera.enabled=true;
            _camera.transform.SetPositionAndRotation(_c.viewCamera.transform.position,_c.viewCamera.transform.rotation);
            _camera.fieldOfView=OpticalFieldOfView(_c.View.BinocularMagnification);
            _camera.farClipPlane=_c.viewCamera.farClipPlane;
            _label.text="望远镜 "+_c.View.BinocularMagnification.ToString("0.0")+"×  ·  右摇杆上下调倍  ·  A 记录  ·  右扳机放下";
            // Once observing, the optical window replaces the near-eye barrels, not the HMD camera.
            _heldRenderer=_c.FieldWorld.HeldToolRenderer;
            if(_heldRenderer!=null && _c.FieldWorld.PresentedItem==FieldItem.Binoculars){_heldRenderer.enabled=false;_hidTool=true;}
        }
        public void ReadZoomAxis(Vector2 axis,float now)
        {
            if(!Active || float.IsNaN(axis.y)||float.IsInfinity(axis.y)||float.IsNaN(now)||float.IsInfinity(now))return;
            if(Mathf.Abs(axis.y)<.35f){_zoomDirection=0;return;}
            if(Mathf.Abs(axis.y)<.65f)return;
            int direction=axis.y>0?1:-1;
            if(direction!=_zoomDirection || now>=_zoomAt)
            {
                _zoomDirection=direction;_zoomAt=now+.3f;_c.View.AdjustMagnification(direction);
                _camera.fieldOfView=OpticalFieldOfView(_c.View.BinocularMagnification);
                _label.text="望远镜 "+_c.View.BinocularMagnification.ToString("0.0")+"×  ·  右摇杆上下调倍  ·  A 记录  ·  右扳机放下";
            }
        }
        private void Hide()
        {
            if(_window!=null)_window.SetActive(false);if(_camera!=null)_camera.enabled=false;
            if(_hidTool && _heldRenderer!=null && _c!=null && _c.FieldWorld.PresentedItem==FieldItem.Binoculars)_heldRenderer.enabled=true;
            _hidTool=false;_zoomDirection=0;
        }
        private void OnDisable(){Active=false;Hide();}
        private void OnDestroy()
        {
            if(_camera!=null){_camera.enabled=false;_camera.targetTexture=null;Destroy(_camera.gameObject);}if(_window!=null)Destroy(_window);
            if(_texture!=null){_texture.Release();Destroy(_texture);}
            if(_material!=null)Destroy(_material);if(_quad!=null)Destroy(_quad);
        }
    }
}
