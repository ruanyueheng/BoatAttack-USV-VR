using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// [Claude v0.41] 剧情过场系统：序章、发现旧海图、四境回响重现、宝藏终章。
    /// 每个镜头可以是手绘插画 CG（缓慢推拉）或引擎内实时运镜；宽银幕、逐字字幕、渐隐转场。
    /// XR 仍观看固定在世界中的影院屏幕，保留头部自由度。
    /// </summary>
    public sealed class FishingPrologue : MonoBehaviour
    {
        public enum Sequence { Prologue, Chart, Wind, Forest, Tide, Star, Finale }
        public const int ShotCount=6;
        public const float ShotSeconds=6.5f;
        private const float Fade=.7f,TypeSpeed=16;

        // Still: -1 引擎实时镜头；0-7 章节图集单元；8 渔村明信片；9 章节图集全景。
        private struct Cut
        {
            public int Still,Camera;public string Title,Caption;
            public Cut(int still,int camera,string title,string caption){Still=still;Camera=camera;Title=title;Caption=caption;}
        }
        private static readonly Dictionary<Sequence,Cut[]> Films=new Dictionary<Sequence,Cut[]>
        {
            {Sequence.Prologue,new[]{
                new Cut(8,-1,"回潮海","很久以前，回潮海上有四座灯塔。风、林、潮、星——它们轮流唱歌，为夜里的渔船指一条回家的路。"),
                new Cut(1,-1,"大寂潮","三十年前的一个夜晚，一场无声的大潮漫过群岛。四盏灯同时熄灭，歌声停了，鱼群也散了。"),
                new Cut(-1,0,"守灯人的出航","老村长沈望潮——你的祖父——说要去找回灯火。他驾着小船驶出潮汐渔港，再也没有回来。"),
                new Cut(-1,4,"海风邮戳的来信","多年后，一封盖着海风邮戳的信把你叫回回潮村：“渔屋还在，村子需要一位新的村长。”"),
                new Cut(-1,2,"先把日子过起来","钓鱼、种菜、修好渔屋，和留下来的人一起生活。海会记得每一个愿意留下的人。"),
                new Cut(-1,5,"归来","回潮渔屋的书桌里，还压着祖父没讲完的故事。——欢迎回家，村长。")}},
            {Sequence.Chart,new[]{
                new Cut(-1,1,"书桌里的旧海图","任命函下面压着一张泛黄的海图，边角写着祖父的字：“灯没有灭，只是睡着了。”"),
                new Cut(9,-1,"四段失落的图页","海图被撕成四份，分别指向月牙风铃岛、翡翠潮林岛、沉船沙洲和星望遗迹岛。"),
                new Cut(-1,5,"新的航程","找回四境的回响与遗物，拼出完整航路。祖父最后去了哪里，答案就在图的尽头。")}},
            {Sequence.Wind,new[]{
                new Cut(4,-1,"风之回响","巨钟再次鸣响。风把一句老话送回你耳边：航路不是画在海上的线，而是人们愿意等待彼此的约定。"),
                new Cut(-1,3,"第一盏灯","月牙风铃岛的灯塔亮了一瞬。风之遗物落入你的掌心——还有三段回响在等你。")}},
            {Sequence.Forest,new[]{
                new Cut(5,-1,"林之回响","两束光穿过潮林，活根架成了桥。守灯人留下一片树荫，让幼苗和归来的渔人都有地方歇脚。"),
                new Cut(-1,3,"第二盏灯","翡翠潮林岛的灯塔亮了。祖父的笔迹在海图上又清晰了一段。")}},
            {Sequence.Tide,new[]{
                new Cut(6,-1,"潮之回响","沉船的舱门重新升起。风暴那夜，老船长卸下货物，把最后的舱位留给了岛上的孩子。"),
                new Cut(-1,3,"第三盏灯","沉船沙洲的灯塔亮了。潮水退去的地方，露出一行刻字：“继续往星星的方向走。”")}},
            {Sequence.Star,new[]{
                new Cut(7,-1,"星之回响","三幅星图重新连成一线。四座灯塔从来不是为了守护财宝，而是为了回答一句话：回家的人，走哪条路？"),
                new Cut(-1,3,"第四盏灯","星望遗迹岛的灯塔亮了。四段回响已经齐全——回到工作台，把遗物组装成远航罗盘。")}},
            {Sequence.Finale,new[]{
                new Cut(-1,6,"白帆拱湾","罗盘嵌进古锁，指针转了三圈，停在回潮村的方向。"),
                new Cut(-1,7,"祖父的宝藏","箱子里没有成堆的金币，只有一本航海日志、一枚旧灯芯，和一包给“下一位村长”的种子。"),
                new Cut(1,-1,"灯火重燃","日志最后一页写着：“灯芯交给你了。”那天夜里，四座灯塔同时亮起，鱼群回到了回潮海。"),
                new Cut(8,-1,"海贼王","村民们说，真正的海贼王敢于远航，也记得回家。从今天起，这个称号属于你——而回潮村的日子，还长着呢。")}},
        };

        private FishingPrototype _c;
        private Camera _camera;
        private RenderTexture _film;
        private Canvas _canvas;
        private RawImage _picture,_still;
        private Text _title,_subtitle,_hint,_chapter;
        private Image _fade;
        private AudioSource _music;
        private readonly Dictionary<Sequence,AudioClip> _scores=new Dictionary<Sequence,AudioClip>();
        private Texture2D _atlas,_postcard;
        private readonly FishingPressEdge _skip=new FishingPressEdge();
        private readonly Queue<Sequence> _pending=new Queue<Sequence>();
        private bool _screenXR,_xrIntroOffered;
        private int _headReadyFrames;
        private Cut[] _shots=Films[Sequence.Prologue];
        public bool Open { get; private set; }
        public Sequence Current { get; private set; }
        public float Age { get; private set; }
        public int Shot => Mathf.Min(_shots.Length-1,(int)(Age/ShotSeconds));
        public float Duration => _shots.Length*ShotSeconds;
        public Camera CinemaCamera => _camera;
        public Canvas CinemaScreen => _canvas;
        public bool CanBegin => !Open && !_c.HasPendingCatch && !_c.WorldActionBusy && _c.CanChangeBait && !_c.DialogueOpen && !_c.Letter.Open
            && !_c.Home.Sleeping && !_c.ShopBlocksGameplay && (!_c.Paused || _c.JournalOpen);
        public void Build(FishingPrototype c)
        {
            _c=c;var go=new GameObject("Story CG camera",typeof(Camera));go.transform.SetParent(transform,false);
            _camera=go.GetComponent<Camera>();_camera.CopyFrom(c.viewCamera);_camera.enabled=false;_camera.cullingMask&=~(1<<5);_camera.fieldOfView=42;_camera.stereoTargetEye=StereoTargetEyeMask.None;
            bool mobile=Application.platform==RuntimePlatform.Android;
            _film=new RenderTexture(mobile?896:1280,mobile?504:720,24){name="Tidebound story film"};_film.Create();_camera.targetTexture=_film;
            _atlas=Resources.Load<Texture2D>("FishingV031/Chapters");_postcard=Resources.Load<Texture2D>("FishingUIPrototype/IslandPostcard");
            var root=new GameObject("Story cinema screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.layer=5;root.transform.SetParent(transform,false);
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            _canvas=root.GetComponent<Canvas>();_canvas.sortingOrder=200;_canvas.worldCamera=c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1280,720);
            var backdrop=new GameObject("Backdrop",typeof(RectTransform),typeof(Image));backdrop.layer=5;backdrop.transform.SetParent(root.transform,false);
            Stretch(backdrop.GetComponent<RectTransform>());backdrop.GetComponent<Image>().color=Color.black;backdrop.GetComponent<Image>().raycastTarget=false;
            _picture=Picture(root.transform,"CG film");_picture.texture=_film;
            _still=Picture(root.transform,"CG illustration");
            Band(root.transform,"Top letterbox",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-78),new Vector2(0,0));
            Band(root.transform,"Bottom letterbox",new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,150));
            _chapter=Label(root.transform,"Chapter",new Vector2(.06f,.905f),new Vector2(.94f,.95f),15);_chapter.color=new Color(.88f,.73f,.46f,.9f);
            _title=Label(root.transform,"Title",new Vector2(.06f,.165f),new Vector2(.94f,.215f),30);_title.font=FishingUITheme.Heading;_title.alignment=TextAnchor.MiddleCenter;_title.color=new Color(.97f,.86f,.62f);
            _subtitle=Label(root.transform,"Subtitle",new Vector2(.12f,.05f),new Vector2(.88f,.16f),22);_subtitle.alignment=TextAnchor.UpperCenter;
            _hint=Label(root.transform,"Skip hint",new Vector2(.06f,.012f),new Vector2(.94f,.045f),13);_hint.alignment=TextAnchor.MiddleRight;_hint.color=new Color(.75f,.75f,.72f,.7f);
            var fade=new GameObject("Shot dissolve",typeof(RectTransform),typeof(Image));fade.layer=5;fade.transform.SetParent(root.transform,false);Stretch(fade.GetComponent<RectTransform>());
            _fade=fade.GetComponent<Image>();_fade.color=Color.black;_fade.raycastTarget=false;root.SetActive(false);
            _music=gameObject.AddComponent<AudioSource>();_music.spatialBlend=0;_music.volume=.20f;_music.playOnAwake=false;
            _c.RegisterAudioSource(_music,FishingAudioCategory.Music,.20f);
        }
        private AudioClip Score(Sequence sequence)
        {
            if(_scores.TryGetValue(sequence,out var clip))return clip;
            // 原创合成配乐：每个镜头一组和弦，柔和包络；终章转入大调上行。
            float[][] chords=sequence==Sequence.Finale?new[]{new[]{196f,246.94f,293.66f},new[]{220f,261.63f,329.63f},new[]{174.61f,220f,261.63f},new[]{196f,246.94f,293.66f,392f}}
                :sequence==Sequence.Prologue?new[]{new[]{146.83f,220f,293.66f},new[]{130.81f,196f,261.63f},new[]{116.54f,174.61f,233.08f},new[]{146.83f,220f,277.18f},new[]{164.81f,246.94f,329.63f},new[]{146.83f,220f,293.66f}}
                :new[]{new[]{164.81f,207.65f,246.94f},new[]{196f,246.94f,293.66f},new[]{220f,277.18f,329.63f}};
            int shots=Films[sequence].Length;const int rate=22050;var samples=new float[(int)(rate*shots*ShotSeconds)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;int shot=Mathf.Min(shots-1,(int)(t/ShotSeconds));float local=t-shot*ShotSeconds;
                float envelope=Mathf.Clamp01(local/1.4f)*Mathf.Clamp01((ShotSeconds-local)/1.6f);
                var chord=chords[shot%chords.Length];float v=0;
                for(int n=0;n<chord.Length;n++)v+=Mathf.Sin(t*chord[n]*Mathf.PI*2+n)*(n==0?.09f:.05f)*(1+.25f*Mathf.Sin(t*.7f+n));
                // 轻柔分解和弦的拨弦点缀。
                float pluckT=local%.75f;int pluck=(int)(local/.75f)%chord.Length;
                v+=Mathf.Sin(t*chord[pluck]*2*Mathf.PI*2)*Mathf.Exp(-pluckT*5)*.04f;
                samples[i]=v*envelope;
            }
            clip=AudioClip.Create("Tidebound story score "+sequence,samples.Length,1,rate,false);clip.SetData(samples,0);_scores[sequence]=clip;return clip;
        }
        private void Update()
        {
            if(_c==null)return;
            // 剧情节点产生的过场在玩家空闲时依次播放，避免打断钓鱼、对话或菜单。
            if(_pending.Count>0 && !Open && !_c.Automated && !_c.Paused && Application.isFocused && CanBegin && !(XRSettings.isDeviceActive && !_c.XRSeat.Ready))
            {if(Begin(_pending.Peek()))_pending.Dequeue();}
            if(_c.Automated || _c.HasIsolatedProfile || !Application.isFocused || !XRSettings.isDeviceActive || FishingXRNotebook.GuideCompleted || _xrIntroOffered)return;
            if(!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)){_headReadyFrames=0;return;}
            if(++_headReadyFrames<2)return;
            if(Open){_xrIntroOffered=true;return;}
            // Existing saves may have marked the old invisible opening as seen; the headset lesson has its own versioned receipt.
            if(!_c.Paused && !_c.XRNotebook.IsOpen && !_c.Expedition.Aligning && Begin())_xrIntroOffered=true;
        }
        /// <summary>剧情节点调用：排队在下一个空闲时刻播放。</summary>
        public void Queue(Sequence sequence){if(!_pending.Contains(sequence))_pending.Enqueue(sequence);}
        public static Sequence Relic(int island)=>island==0?Sequence.Wind:island==1?Sequence.Forest:island==2?Sequence.Tide:Sequence.Star;
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static RawImage Picture(Transform root,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.layer=5;go.transform.SetParent(root,false);
            Stretch(go.GetComponent<RectTransform>());var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;
        }
        private static void Band(Transform root,string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(root,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;
            var image=go.GetComponent<Image>();image.color=new Color(.005f,.012f,.02f,1);image.raycastTarget=false;
        }
        private static Text Label(Transform root,string name,Vector2 min,Vector2 max,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text),typeof(Shadow));go.layer=5;go.transform.SetParent(root,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=size;text.color=new Color(.96f,.93f,.86f);text.raycastTarget=false;text.lineSpacing=1.15f;
            go.GetComponent<Shadow>().effectColor=new Color(0,0,0,.6f);return text;
        }
        internal bool BeginForCapture(Sequence sequence)
        {
            Current=sequence;_shots=Films[sequence];Open=true;Age=0;ConfigureScreen(false);
            _canvas.gameObject.SetActive(true);_music.clip=Score(sequence);Present();return true;
        }
        internal void SeekForCapture(float age){Age=Mathf.Clamp(age,0,Duration);Present();}
        public bool Begin()=>Begin(Sequence.Prologue);
        public bool Begin(Sequence sequence)
        {
            if(!CanBegin)return false;
            if(XRSettings.isDeviceActive && (!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)))return false;
            if(_c.JournalOpen)_c.ToggleJournal();
            if(_c.Paused)return false;
            Current=sequence;_shots=Films[sequence];
            _c.XRNotebook.Close();_c.CloseBoatPanels();Open=true;Age=0;_skip.Reset();_c.StoryModalChanged();
            ConfigureScreen(XRSettings.isDeviceActive);
            _canvas.gameObject.SetActive(true);_c.ApplyAudioSettings();_music.clip=Score(sequence);_music.Play();Present();return true;
        }
        public void Tick(float seconds,bool tracked)
        {
            if(!Open || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<0)return;
            if(tracked && (!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)))
            {_canvas.gameObject.SetActive(false);_music.Pause();_skip.Reset();return;}
            if(_screenXR!=tracked)ConfigureScreen(tracked);
            _canvas.gameObject.SetActive(true);_music.UnPause();
            Age=Mathf.Min(Duration,Age+Mathf.Min(seconds,.25f));
            if(tracked)
            {
                var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                right.TryGetFeatureValue(CommonUsages.primaryButton,out bool pressed);
                if(_skip.Step(right.isValid,pressed)){Finish();return;}
            }
            if(Age>=Duration){Finish();return;}Present();
        }
        /// <summary>桌面按键：字幕未打完时先补全本句，再次按下进入下一镜；Esc 直接跳过整段。</summary>
        public void Advance(bool skipAll)
        {
            if(!Open)return;
            if(skipAll){Finish();return;}
            float local=Age-Shot*ShotSeconds;float typed=(local-Fade*.5f)*TypeSpeed;
            if(typed<_shots[Shot].Caption.Length){Age=Shot*ShotSeconds+Fade*.5f+_shots[Shot].Caption.Length/TypeSpeed+.01f;Present();return;}
            if(Shot>=_shots.Length-1){Finish();return;}
            Age=(Shot+1)*ShotSeconds+.01f;Present();
        }
        public void Present()
        {
            if(!Open)return;int index=Shot;var shot=_shots[index];float t=Age-index*ShotSeconds;float u=Mathf.SmoothStep(0,1,t/ShotSeconds);
            bool still=shot.Still>=0 && StillTexture(shot.Still)!=null;
            _camera.enabled=!still;_picture.enabled=!still;_still.enabled=still;
            if(still)PresentStill(shot.Still,u,index);else PresentCamera(shot.Camera,u);
            _chapter.text=Chapter(Current)+"   ·   "+(index+1)+" / "+_shots.Length;
            _title.text=shot.Title;
            int typed=Mathf.Clamp(Mathf.FloorToInt((t-Fade*.5f)*TypeSpeed),0,shot.Caption.Length);
            _subtitle.text=shot.Caption.Substring(0,typed);
            _hint.text=XRSettings.isDeviceActive?"右手 A 跳过":"E / 空格 继续    Esc 跳过";
            float titleIn=Mathf.Clamp01((t-.2f)/.6f);_title.color=new Color(.97f,.86f,.62f,titleIn);
            _fade.color=new Color(.005f,.01f,.02f,Mathf.Max(1-Mathf.Clamp01(t/Fade),Mathf.Clamp01((t-(ShotSeconds-Fade))/Fade)));
        }
        private static string Chapter(Sequence sequence)=>sequence==Sequence.Prologue?"序章 · 潮汐回响":sequence==Sequence.Chart?"第一章 · 祖父的旧海图"
            :sequence==Sequence.Finale?"终章 · 回家的航路":"回响 · "+FishingLore.ShrineName(sequence-Sequence.Wind);
        private Texture2D StillTexture(int still)=>still==8?_postcard:_atlas;
        private void PresentStill(int still,float u,int index)
        {
            var texture=StillTexture(still);_still.texture=texture;
            // 16:9 取景，缓慢推近并平移（Ken Burns），奇偶镜头方向相反。
            Rect cell=still==8?new Rect(0,0,1,1):still==9?new Rect(0,0,1,1):new Rect((still%4)*.25f,still<4?.5f:0,.25f,.5f);
            float aspect=(cell.width*texture.width)/(cell.height*texture.height);
            float h=cell.height,w=cell.width;
            if(aspect>16f/9f)w=cell.height*texture.height*16f/9f/texture.width;else h=cell.width*texture.width*9f/16f/texture.height;
            float zoom=Mathf.Lerp(1f,.86f,u);w*=zoom;h*=zoom;
            float dir=index%2==0?1:-1;
            float px=Mathf.Lerp(.5f-.5f*dir,.5f+.5f*dir,u),py=Mathf.Lerp(.65f,.45f,u);
            _still.uvRect=new Rect(cell.x+(cell.width-w)*px,cell.y+(cell.height-h)*py,w,h);
            _still.color=Color.white;
        }
        private void PresentCamera(int camera,float u)
        {
            Vector3 focus,from,to;var harbor=_c.harbor;var home=_c.Home.Origin;
            var actor=_c.Ashore?_c.Shore.Position:_c.Player.transform.position;
            var facing=_c.Ashore?Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0):Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);
            switch(camera)
            {
                case 0:focus=harbor+Vector3.up*1.2f;from=harbor+new Vector3(-32,23,-28);to=harbor+new Vector3(-22,12,-18);break;
                case 1:focus=_c.Home.InteractionPoint(2)+Vector3.up*.05f;from=focus+new Vector3(2.6f,2.0f,-2.4f);to=focus+new Vector3(1.6f,1.3f,-1.5f);break;
                case 2:focus=_c.fishingSpots[0];from=focus+new Vector3(-8,3.6f,-12);to=focus+new Vector3(7,2.8f,-10);break;
                case 3:focus=actor+Vector3.up*1.1f;from=focus+facing*new Vector3(-3.2f,1.6f,3.6f);to=focus+facing*new Vector3(2.2f,.9f,2.6f);break;
                case 4:focus=home+new Vector3(0,2,-1);from=home+new Vector3(12,7,-18);to=home+new Vector3(8,5,-14);break;
                case 6:case 7:
                    var chest=_c.Voyage!=null && _c.Voyage.TreasureChest!=null?_c.Voyage.TreasureChest.position:actor;
                    focus=chest+Vector3.up*.3f;
                    if(camera==6){from=focus+new Vector3(-6,4,-6);to=focus+new Vector3(-2.4f,1.6f,-2.6f);}
                    else{from=focus+new Vector3(2.6f,2.2f,-2.4f);to=focus+new Vector3(1.7f,1.5f,-1.6f);}
                    break;
                default:focus=actor+Vector3.up*.9f;from=focus+facing*new Vector3(-6,3,-7);to=focus+facing*new Vector3(-3,1.4f,-4);break;
            }
            _camera.transform.position=Vector3.Lerp(from,to,u);_camera.transform.rotation=Quaternion.LookRotation(focus-_camera.transform.position);
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
            var finished=Current;
            if(finished==Sequence.Prologue)
            {
                _c.Career.FinishPrologue();_c.Career.AcceptMayorship();_c.Save();_c.StoryModalChanged();
                if(XRSettings.isDeviceActive){_c.XRNotebook.BeginGuide();_c.Notice("欢迎回家。先安全练习手柄；B 可随时打开潮汐手册。",9);}
                else _c.Notice("村长，欢迎回家。回潮渔屋的航线书桌上留着祖父的旧海图；J 查看主线，M 掏出随身海图。",10);
                return;
            }
            _c.StoryModalChanged();
            if(finished!=Sequence.Finale)_c.Notice(_c.Career.MainObjective,9);
        }
        public void Suspend()
        {if(!Open)return;Open=false;_camera.enabled=false;_canvas.gameObject.SetActive(false);_music.Stop();if(Current!=Sequence.Prologue && !_pending.Contains(Current))_pending.Enqueue(Current);_c.StoryModalChanged();}
        private void OnDisable()=>Suspend();
        private void OnDestroy(){if(_film!=null){_film.Release();Destroy(_film);}foreach(var clip in _scores.Values)if(clip!=null)Destroy(clip);}
    }
}
