using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Six original in-engine CG shots. XR watches a stationary cinema screen with tracked head freedom.</summary>
    public sealed class FishingPrologue : MonoBehaviour
    {
        public const int ShotCount=6;
        public const float ShotSeconds=6;
        private FishingPrototype _c;
        private Camera _camera;
        private RenderTexture _film;
        private Canvas _canvas;
        private Text _title,_subtitle,_hint;
        private Image _fade;
        private AudioSource _music;
        private AudioClip _score;
        private readonly FishingPressEdge _skip=new FishingPressEdge();
        private bool _screenXR,_xrIntroOffered;
        private int _headReadyFrames;
        public bool Open { get; private set; }
        public float Age { get; private set; }
        public int Shot => Mathf.Min(ShotCount-1,(int)(Age/ShotSeconds));
        public Camera CinemaCamera => _camera;
        public Canvas CinemaScreen => _canvas;
        public bool CanBegin => !Open && !_c.HasPendingCatch && !_c.WorldActionBusy && _c.CanChangeBait && !_c.DialogueOpen && !_c.Letter.Open
            && !_c.Home.Sleeping && !_c.ShopBlocksGameplay && (!_c.Paused || _c.JournalOpen);
        private static readonly string[] Titles={"回潮海","熄灭的航标","仍在呼吸的海湾","一间等待归来的家","祖父留下的信","潮汐回响"};
        private static readonly string[] Captions={
            "这里的人曾循着四座航标出海。潮汐带来鱼群，也把远方的来信送回旧港。",
            "三年前的风暴熄灭了群岛的灯。熟悉的航路散了，关于潮纹秘境的故事也渐渐沉入海底。",
            "礁石下有八腕的访客，浅湾里有缓慢的绿影。认识每一种生命，是重新认识这片海的开始。",
            "你离开很久了。旧港仍留着你的渔屋：一张床、一盏灯，和面朝大海的窗。",
            "“船旧了，可以修；灯灭了，可以再点。等你回来，替我听听海湾还有什么故事。” —— 祖父",
            "今天，你带着这封信回到港口。收集图鉴、结识居民，驶向第一座沉默的航标。"};
        public void Build(FishingPrototype c)
        {
            _c=c;var go=new GameObject("V023 CG camera",typeof(Camera));go.transform.SetParent(transform,false);
            _camera=go.GetComponent<Camera>();_camera.CopyFrom(c.viewCamera);_camera.enabled=false;_camera.cullingMask&=~(1<<5);_camera.fieldOfView=48;_camera.stereoTargetEye=StereoTargetEyeMask.None;
            bool mobile=Application.platform==RuntimePlatform.Android;
            _film=new RenderTexture(mobile?896:1280,mobile?504:720,24){name="Returning tide opening CG"};_film.Create();_camera.targetTexture=_film;
            var root=new GameObject("V023 cinema screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.layer=5;root.transform.SetParent(transform,false);
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            _canvas=root.GetComponent<Canvas>();_canvas.sortingOrder=200;_canvas.worldCamera=c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1280,720);
            var picture=new GameObject("CG film",typeof(RectTransform),typeof(RawImage));picture.layer=5;picture.transform.SetParent(root.transform,false);
            Stretch(picture.GetComponent<RectTransform>());picture.GetComponent<RawImage>().texture=_film;picture.GetComponent<RawImage>().raycastTarget=false;
            Band(root.transform,"Top letterbox",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-74),new Vector2(0,0));
            Band(root.transform,"Bottom letterbox",new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,152));
            _title=Label(root.transform,"Chapter",new Vector2(.08f,.80f),new Vector2(.92f,.90f),34);
            _subtitle=Label(root.transform,"World story",new Vector2(.10f,.05f),new Vector2(.90f,.19f),25);_subtitle.alignment=TextAnchor.MiddleCenter;
            _hint=Label(root.transform,"Skip hint",new Vector2(.08f,.012f),new Vector2(.92f,.047f),16);_hint.alignment=TextAnchor.MiddleCenter;
            var fade=new GameObject("Shot dissolve",typeof(RectTransform),typeof(Image));fade.layer=5;fade.transform.SetParent(root.transform,false);Stretch(fade.GetComponent<RectTransform>());
            _fade=fade.GetComponent<Image>();_fade.color=Color.black;_fade.raycastTarget=false;root.SetActive(false);
            _music=gameObject.AddComponent<AudioSource>();_music.spatialBlend=0;_music.volume=.20f;_music.playOnAwake=false;
            const int rate=22050;var samples=new float[rate*(int)(ShotCount*ShotSeconds)];float[] notes={146.83f,174.61f,220f,196f,164.81f,220f};
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;int shot=Mathf.Min(ShotCount-1,(int)(t/ShotSeconds));float envelope=Mathf.Sin(Mathf.PI*(t%ShotSeconds)/ShotSeconds);
                float n=notes[shot];samples[i]=envelope*(Mathf.Sin(t*n*Mathf.PI*2)*.14f+Mathf.Sin(t*n*1.5f*Mathf.PI*2)*.045f+Mathf.Sin(t*n*2*Mathf.PI*2)*.025f);
            }
            _score=AudioClip.Create("Original V023 returning tide score",samples.Length,1,rate,false);_score.SetData(samples,0);_music.clip=_score;
        }
        private void Update()
        {
            if(_c==null || _c.Automated || _c.HasIsolatedProfile || !Application.isFocused || !XRSettings.isDeviceActive || FishingXRNotebook.GuideCompleted || _xrIntroOffered)return;
            if(!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)){_headReadyFrames=0;return;}
            if(++_headReadyFrames<2)return;
            if(Open){_xrIntroOffered=true;return;}
            // Existing saves may have marked the old invisible opening as seen; the headset lesson has its own versioned receipt.
            if(!_c.Paused && !_c.XRNotebook.IsOpen && !_c.Expedition.Aligning && Begin())_xrIntroOffered=true;
        }
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void Band(Transform root,string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(root,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;
            var image=go.GetComponent<Image>();image.color=new Color(.01f,.025f,.04f,.98f);image.raycastTarget=false;
        }
        private static Text Label(Transform root,string name,Vector2 min,Vector2 max,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(root,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=size;text.color=new Color(.96f,.93f,.82f);text.raycastTarget=false;return text;
        }
        public bool Begin()
        {
            if(!CanBegin)return false;
            if(XRSettings.isDeviceActive && (!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)))return false;
            if(_c.JournalOpen)_c.ToggleJournal();
            if(_c.Paused)return false;
            _c.XRNotebook.Close();_c.CloseBoatPanels();Open=true;Age=0;_skip.Reset();_c.StoryModalChanged();
            ConfigureScreen(XRSettings.isDeviceActive);
            _canvas.gameObject.SetActive(true);_camera.enabled=true;_music.mute=_c.QuietMode;_music.Play();Present();return true;
        }
        public void Tick(float seconds,bool tracked)
        {
            if(!Open || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<0)return;
            if(tracked && (!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)))
            {_canvas.gameObject.SetActive(false);_music.Pause();_skip.Reset();return;}
            if(_screenXR!=tracked)ConfigureScreen(tracked);
            _canvas.gameObject.SetActive(true);_music.UnPause();
            Age=Mathf.Min(ShotCount*ShotSeconds,Age+Mathf.Min(seconds,.25f));
            if(tracked)
            {
                var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                right.TryGetFeatureValue(CommonUsages.primaryButton,out bool pressed);
                if(_skip.Step(right.isValid,pressed)){Finish();return;}
            }
            if(Age>=ShotCount*ShotSeconds){Finish();return;}Present();
        }
        public void Present()
        {
            if(!Open)return;int shot=Shot;float t=Age%ShotSeconds;float u=Mathf.SmoothStep(0,1,t/ShotSeconds);
            Vector3 focus,from,to;var harbor=_c.harbor;var home=_c.Home.Origin;
            switch(shot)
            {
                case 0:focus=harbor+Vector3.up*1.2f;from=harbor+new Vector3(-32,23,-28);to=harbor+new Vector3(-22,16,-18);break;
                case 1:focus=FishingIslandWorld.Berth(2)+new Vector3(0,3,-3);from=focus+new Vector3(22,12,27);to=focus+new Vector3(15,8,19);break;
                case 2:focus=_c.fishingSpots[0];from=focus+new Vector3(-8,3.6f,-12);to=focus+new Vector3(7,2.8f,-10);break;
                case 3:focus=home+new Vector3(-3.3f,1.05f,2.7f);from=home+new Vector3(-.9f,2.1f,-.1f);to=home+new Vector3(-1.2f,1.7f,.6f);break;
                case 4:focus=home+new Vector3(0,2,-1);from=home+new Vector3(12,7,-18);to=home+new Vector3(8,5,-14);break;
                default:focus=_c.Player.transform.position+Vector3.up*.8f;from=focus+new Vector3(-6,3,-7);to=focus+new Vector3(-3,1.4f,-4);break;
            }
            _camera.transform.position=Vector3.Lerp(from,to,u);_camera.transform.rotation=Quaternion.LookRotation(focus-_camera.transform.position);
            _title.text=Titles[shot];_subtitle.text=Captions[shot];_hint.text=(shot+1)+" / "+ShotCount+"     ·     "+(XRSettings.isDeviceActive?"右手 A 跳过并学习手柄":"E / 空格 / Esc 跳过")+"     ·     设置页可重看序章与教学";
            _fade.color=new Color(.01f,.02f,.03f,Mathf.Max(1-Mathf.Clamp01(t/.6f),Mathf.Clamp01((t-5.4f)/.6f)));
        }
        private void ConfigureScreen(bool xr)
        {
            _screenXR=xr;_canvas.renderMode=xr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            _canvas.transform.localScale=xr?Vector3.one*.0016f:Vector3.one;
            if(!xr)return;
            _xrIntroOffered=true;
            var eye=_c.viewCamera.transform;var forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=_c.Shore.Actor.forward;
            _canvas.transform.SetPositionAndRotation(eye.position+forward*2.1f,Quaternion.LookRotation(forward,Vector3.up));
        }
        public void Finish()
        {
            if(!Open)return;Open=false;_camera.enabled=false;_canvas.gameObject.SetActive(false);_music.Stop();
            _c.Career.FinishPrologue();_c.Save();_c.StoryModalChanged();
            if(XRSettings.isDeviceActive){_c.XRNotebook.BeginGuide();_c.Notice("欢迎回家。先安全练习手柄；B 可随时打开潮汐手册。",9);}
            else _c.Notice("欢迎回家。旧港西岸的木栈道通往渔屋；J 查看海湾图鉴与旅程。",9);
        }
        public void Suspend()
        {if(!Open)return;Open=false;_camera.enabled=false;_canvas.gameObject.SetActive(false);_music.Stop();_c.StoryModalChanged();}
        private void OnDisable()=>Suspend();
        private void OnDestroy(){if(_film!=null){_film.Release();Destroy(_film);}if(_score!=null)Destroy(_score);}
    }
}
