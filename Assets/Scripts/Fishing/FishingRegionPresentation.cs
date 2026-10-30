using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed class FishingRegionPresentation : MonoBehaviour
    {
        private FishingPrototype _c;
        private int _region = int.MinValue;
        private float _shownAt = -20, _candidateAt;
        private int _candidate = int.MinValue;
        private CanvasGroup _group;
        private Text _xrText, _xrSubtitle;
        private bool _xrPending;
        private static readonly Vector2 PageSize = new Vector2(840, 260);
        private AudioSource _audio;
        private readonly AudioClip[] _chimes = new AudioClip[3];
        public string Title { get; private set; } = "";
        public string Subtitle { get; private set; } = "";
        public int Theme { get; private set; }
        public float Age => Time.unscaledTime - _shownAt;
        public float Alpha => Mathf.SmoothStep(0, 1, Mathf.Clamp01(Age / 1.1f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((6.2f - Age) / 1.2f));
        public void Build(FishingPrototype c)
        {
            _c = c; _audio = gameObject.AddComponent<AudioSource>(); _audio.spatialBlend = 0; _audio.volume = .16f;
            _c.RegisterAudioSource(_audio,FishingAudioCategory.Effects,.16f);
            for (int n = 0; n < 3; n++)
            {
                var clip = AudioClip.Create("Tide chime " + n, 13230, 1, 44100, false); var samples = new float[13230];
                float frequency = n == 0 ? 523.25f : n == 1 ? 659.25f : 783.99f;
                for (int i = 0; i < samples.Length; i++) { float t = i / 44100f; samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2) * Mathf.Exp(-t * 14) * Mathf.Min(1, t * 90); }
                clip.SetData(samples, 0); _chimes[n] = clip;
            }
            var root = new GameObject("Region inscription XR", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(CanvasScaler)); root.layer=5;root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = c.viewCamera;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=4;
            var rt = root.GetComponent<RectTransform>(); rt.sizeDelta = PageSize; rt.localScale = Vector3.one * .0016f;
            _group = root.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0;
            Art("Windows region title glow",new Rect(28,0,784,260),FishingUITheme.TitleGlow,Color.white);
            Art("Windows region gilt rule",new Rect(180,117,480,12),FishingUITheme.GiltRule,FishingHUD.AmberInk);
            Art("Windows region centre star",new Rect(411,114,18,18),FishingUITheme.Star,FishingHUD.AmberInk);
            _xrText=Label("Region calligraphy",new Rect(24,12,792,104),56,FishingUITheme.Heading);
            _xrSubtitle=Label("Region description",new Rect(24,137,792,112),32,FishingUIFont.Font);
        }
        private RawImage Art(string name,Rect area,Texture texture,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.layer=5;go.transform.SetParent(_group.transform,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(area.x,-area.y);rect.sizeDelta=area.size;
            var image=go.GetComponent<RawImage>();image.texture=texture;image.color=color;image.material=FishingUITheme.WorldMaterial;image.raycastTarget=false;return image;
        }
        private Text Label(string name,Rect area,int size,Font font)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(_group.transform,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(area.x,-area.y);rect.sizeDelta=area.size;
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.UpperCenter;text.color=FishingHUD.CreamInk;text.material=FishingUITheme.WorldMaterial;text.raycastTarget=false;
            var shadow=go.AddComponent<Shadow>();shadow.effectColor=new Color(.015f,.035f,.045f,.95f);shadow.effectDistance=new Vector2(1,-1);return text;
        }
        public void Chime(int note)
        {
            if (_c.QuietMode || _c.Paused) return;
            _c.ApplyAudioSettings();_audio.PlayOneShot(_chimes[Mathf.Abs(note) % 3]);
        }
        public void Show(string title, string subtitle, int theme)
        {
            Title = title; Subtitle = subtitle; Theme = theme; _shownAt = Time.unscaledTime; Chime(theme);
            _xrText.text=title;_xrSubtitle.text=subtitle.Replace("，"," · ").TrimEnd('。');_xrPending=true;
        }
        public void Tick()
        {
            if (_c.Realms.Inside || _c.Realms.Transitioning || _c.DialogueOpen || _c.Paused) return;
            if(_c.Acceptance!=null && _c.Acceptance.FreezeScenario)return;
            int region = -1;
            if (_c.Home!=null && _c.Ashore && FishingPrototype.FlatDistance(_c.ExplorerPosition,_c.Home.Origin)<(_region==100?32:26)) region=100;
            else if (_c.TackleShop.Inside) region = 4;
            else
            {
                for (int i = 0; i < 4; i++)
                    if (FishingPrototype.FlatDistance(_c.ExplorerPosition, FishingIslandWorld.Center(i)) < (_region == i ? 105 : 80)) { region = i; break; }
                if (region < 0 && FishingPrototype.FlatDistance(_c.ExplorerPosition, _c.harbor) < 40) region = 5;
                if (region < 0 && _c.Ashore && _c.Shore.HasSurveySite) region = 6 + _c.Shore.CurrentSite;
                if (region < 0 && _c.Anchored) region = 10 + _c.Spot;
            }
            if (region != _candidate) { _candidate = region; _candidateAt = Time.unscaledTime; }
            if (region == _region || Time.unscaledTime - _candidateAt < .75f) return;
            _region = region;
            Show(region==100?"回潮渔屋":region < 0 ? "回潮海" : region < 4 ? FishingAdventure.Name(region) : region == 4 ? "潮汐钓具店" : region == 5 ? "潮汐渔港"
                : region < 10 ? FishingExploration.SiteName(region - 6) : FishingGrounds.Name(region - 10),
                region==100?"休息整备 · 再赴潮汐":region < 0 ? "自由航行 · 让风带你去新的岸边" : region < 4 ? FishingLore.Subtitle(region) : region == 4 ? "远航的准备，从一盏灯开始" : region == 5 ? "灯火常在 · 每一段航程都能归来" : "海岸札记 · 探索与发现", region);
        }
        private void LateUpdate()
        {
            if(_c==null || _group==null)return;
            if(!XRSettings.isDeviceActive){_group.alpha=0;return;}
            bool visible=_c.XRSeat.Ready && Application.isFocused && !_c.StartMenuOpen && !_c.Paused && !_c.StoryBlocksGameplay
                && !_c.XRNotebook.IsOpen && !_c.HasPendingCatch && (_c.Combat==null || _c.Combat.Status.Length==0);
            PresentXR(visible,Time.unscaledDeltaTime);
        }
        private void PresentXR(bool visible,float deltaTime)
        {
            // Arrival often fires inside a fade or before eye calibration; its reading time starts when it can be seen.
            if(!visible){if(Age<6.2f)_shownAt+=deltaTime;_group.alpha=0;return;}
            if(_xrPending){_xrPending=false;_shownAt=Time.unscaledTime;PlaceInView();}
            _group.alpha=Alpha;
            if(_group.alpha<=0)return;
            var eye=_c.viewCamera.transform;var toTitle=_group.transform.position-eye.position;
            if(Vector3.Angle(eye.forward,toTitle)>45 || toTitle.magnitude>3 || toTitle.magnitude<.7f)PlaceInView();
        }
        private void PlaceInView()
        {
            var camera=_c.viewCamera.transform;var direction=Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized;
            if(direction.sqrMagnitude<.1f)direction=Vector3.forward;
            float distance=2.1f;
            if(Physics.SphereCast(camera.position,.12f,direction,out var hit,distance,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore))distance=Mathf.Clamp(hit.distance-.15f,.8f,distance);
            _group.transform.SetPositionAndRotation(camera.position+direction*distance+Vector3.up*.18f,Quaternion.LookRotation(direction));
        }
        private void OnDestroy() { foreach (var clip in _chimes) if (clip != null) Destroy(clip); }
    }
}
