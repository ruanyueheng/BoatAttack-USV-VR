using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// V044 starry watercolour ink-spread transition (chapter end, realm clear, portals).
    /// Desktop: overlay canvas + one thresholded mask quad. XR: same shader on a camera-parented quad 0.3 m ahead; the view never moves during it.
    /// </summary>
    public sealed class FishingInkTransition : MonoBehaviour
    {
        private static FishingInkTransition _instance;
        private RawImage _image;
        private Material _material;
        public static bool Playing { get; private set; }

        /// <summary>Covers the screen, calls <paramref name="onCovered"/>, holds briefly, then reveals.</summary>
        public static void Play(Action onCovered,float cover=.9f,float reveal=.9f)
        {
            if(_instance==null)
            {
                var go=new GameObject("V044 ink transition");DontDestroyOnLoad(go);
                _instance=go.AddComponent<FishingInkTransition>();_instance.Build();
            }
            _instance.StopAllCoroutines();_instance.StartCoroutine(_instance.Run(onCovered,Mathf.Max(.05f,cover),Mathf.Max(.05f,reveal)));
        }
        private void Build()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var go=new GameObject("Ink",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(transform,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            _image=go.GetComponent<RawImage>();_image.raycastTarget=false;
            var shader=Resources.Load<Shader>("FishingV044/InkWipe");
            if(shader!=null)
            {
                _material=new Material(shader){name="V044 ink wipe"};
                _material.SetTexture("_Mask",Resources.Load<Texture2D>("FishingV044/UI/InkMask"));
                _image.material=_material;_image.texture=FishingUITheme.StarrySea;
            }
            _image.enabled=false;
        }
        private MeshRenderer _veil;
        /// <summary>XR: same shader on a camera-parented quad 0.3 m ahead (covers ~150° FOV), identical in both eyes.</summary>
        private void XRVeil(bool on)
        {
            var cam=Camera.main;
            if(on && _veil==null && cam!=null && _material!=null)
            {
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="V044 XR ink veil";Destroy(quad.GetComponent<Collider>());
                _veil=quad.GetComponent<MeshRenderer>();_veil.sharedMaterial=_material;_veil.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_veil.receiveShadows=false;
            }
            if(_veil==null)return;
            if(on && cam!=null && _veil.transform.parent!=cam.transform)
            {_veil.transform.SetParent(cam.transform,false);_veil.transform.localPosition=new Vector3(0,0,.3f);_veil.transform.localRotation=Quaternion.identity;_veil.transform.localScale=new Vector3(2.2f,2.2f,1);}
            _veil.enabled=on;
        }
        private void Apply(float progress)
        {
            bool xr=XRSettings.isDeviceActive;
            _image.enabled=progress>0 && !xr;
            if(_material==null){_image.material=null;_image.texture=null;_image.color=new Color(.03f,.06f,.12f,progress);return;}
            _material.SetFloat("_Progress",progress);_material.SetTexture("_MainTex",FishingUITheme.StarrySea);
            _image.material=_material;_image.texture=FishingUITheme.StarrySea;_image.color=Color.white;
            XRVeil(xr && progress>0);
        }
        private IEnumerator Run(Action onCovered,float cover,float reveal)
        {
            Playing=true;
            try
            {
                for(float t=0;t<1;t+=Time.unscaledDeltaTime/cover){Apply(Mathf.SmoothStep(0,1,t));yield return null;}
                Apply(1);
                try{onCovered?.Invoke();}catch(Exception e){Debug.LogException(e);}
                yield return new WaitForSecondsRealtime(.15f);
                for(float t=1;t>0;t-=Time.unscaledDeltaTime/reveal){Apply(Mathf.SmoothStep(0,1,t));yield return null;}
                Apply(0);
            }
            finally{Playing=false;}
        }
        private void OnDestroy(){if(_veil!=null)Destroy(_veil.gameObject);if(_material!=null)Destroy(_material);if(_instance==this)_instance=null;}
    }
}
