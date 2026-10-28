using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
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

        // [Claude v0.44] 全部改为引擎内实时演出（不再混用 AI 插画），剧情见 Docs/STORY_V044.md。
        // Camera：镜头预设（PresentCamera）；Mood：0 原光照，1 黄昏金光，2 清晨冷金；Island：岛屿镜头用的岛序号。
        private struct Cut
        {
            public int Camera,Mood,Island,Alt,AltTarget;public bool Act;public string Title,Caption,Sting;
            // Alt：后半镜切到第二机位（景别变化，台词不断）；Act：主角在该机位做交互动作；Sting：开镜时的音乐短句。
            public Cut(int camera,int mood,string title,string caption,int island=0,int alt=-1,int altTarget=0,bool act=false,string sting=null)
            {Camera=camera;Mood=mood;Island=island;Title=title;Caption=caption;Alt=alt;AltTarget=altTarget;Act=act;Sting=sting;}
        }
        // 每段镜头数与 FishingV042/Story 配乐长度绑定（6/3/2/2/2/2/4 × 6.5s），改镜头数需同时重烘配乐。
        private static readonly Dictionary<Sequence,Cut[]> Films=new Dictionary<Sequence,Cut[]>
        {
            {Sequence.Prologue,new[]{
                new Cut(0,1,"回潮海","村里的老人说，海每涨一次潮，都会轻声问一句：“今晚，有人在等你回家吗？”",0,14,5),
                new Cut(8,1,"四盏灯","风、林、潮、星，四座灯塔替整片海回答。灯亮着，就是“有人在等”，渔船循着光一船船回港。",0,15),
                new Cut(9,1,"大寂潮","后来人一个个离开，灯一盏盏变暗。三十年前那一夜，海问了三遍，没有人回答——四盏灯同时熄灭。",3),
                new Cut(9,2,"守灯人的出航","祖父沈望潮是最后一位守灯人。他只留下一句“我去把灯问回来”，驾着小船驶进雾里，再没回来。",2),
                new Cut(4,2,"潮水印的来信","多年后，一封盖着潮水印子的信寄到你手里：“渔屋还在，灯塔还在，村子缺一个肯留下来的人。”",0,11,0,true),
                new Cut(12,2,"归来","先把日子过起来吧：钓鱼、种菜、修好渔屋。海会记得每一个愿意留下的人。——欢迎回家，村长。",0,11,0,true,"Tender")}},
            {Sequence.Chart,new[]{
                new Cut(1,1,"书桌里的旧海图","海图背面是祖父的字：“灯没有灭，只是在等人回答。我把答案拆成四段回响，藏进了四座岛。”",0,11,0,true,"Discover"),
                new Cut(9,1,"四段回响","风铃、潮林、沉船、星望——每座秘境里，都封着一位守灯人当年做过的选择。重走一遍，灯就会想起来。",0,9,1),
                new Cut(12,1,"新的航程","收齐四段回响，拼成远航罗盘。罗盘会指向祖父最后去的地方。")}},
            {Sequence.Wind,new[]{
                new Cut(10,1,"风之回响 · 摇铃的人","暴风夜，船听不见港，港也看不见船。守灯人阿铃摇了一整夜的铃——等待，要让对方听得见。",0,11,0,true),
                new Cut(9,1,"第一盏灯","共鸣台的钟声把风桥一座座托起。月牙风铃岛的灯塔亮了，海图上浮出第一段航线。",0,15,0,false,"Relic")}},
            {Sequence.Forest,new[]{
                new Cut(10,1,"林之回响 · 种树的人","林守阿根知道自己看不到树长成，还是在潮线上种下第一排红树。有些路，要很多年后才有人走。",0,11,0,true),
                new Cut(9,1,"第二盏灯","日井的光绕过三面镜子落进种池，根桥长了出来。翡翠潮林岛的灯塔亮了。",1,15,0,false,"Relic")}},
            {Sequence.Tide,new[]{
                new Cut(10,1,"潮之回响 · 卸货的船长","风暴那夜，老船长把满舱货物推进海里，空出位置给岛上的孩子。船沉了，人都回了家。",0,11,0,true),
                new Cut(9,1,"第三盏灯","浮桥升起，沉船沙洲的灯塔亮了。退潮的沙上露出一行刻字：“放下的，海会替你记着。”",2,15,0,false,"Relic")}},
            {Sequence.Star,new[]{
                new Cut(10,1,"星之回响 · 沈望潮","最后一段回响是祖父自己。他点亮星望塔，才发现灯要有人守着才不会再熄——于是他留了下来。",0,11,0,true,"Discover"),
                new Cut(9,2,"第四盏灯","望远镜里，海鸥、船锚之后，是那颗归航星。四盏灯都醒了——回工作台组装远航罗盘吧。",3,15,0,false,"Relic")}},
            {Sequence.Finale,new[]{
                new Cut(6,1,"白帆拱湾","罗盘嵌进古锁，指针转了三圈，最后停下——它指的不是远方，是回潮村。",0,7,0,true),
                new Cut(7,1,"祖父的宝箱","箱子里是一本航海日志、一枚旧灯芯，和一包写着“给下一位守灯人”的种子。",0,11,0,true,"Discover"),
                new Cut(8,1,"灯火重燃","日志最后一页写着：“海问的那句话，我替村子答了三十年。现在，换你来答。”那一夜，五座灯塔一起亮了。",0,15),
                new Cut(0,2,"守灯人","渔船一船船回到港里。村民笑着叫你“海贼王”，你只是把灯芯收进口袋——回潮村的日子，还长着呢。",0,14,5,false,"Tender")}},
        };

        private FishingPrototype _c;
        private Camera _camera;
        private RenderTexture _film;
        private Canvas _canvas;
        private RawImage _picture;
        private Texture2D _shade;
        private Text _title,_subtitle,_hint,_chapter,_card;
        private Image _fade;
        private AudioSource _music;
        private readonly Dictionary<Sequence,AudioClip> _scores=new Dictionary<Sequence,AudioClip>();
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
            var root=new GameObject("Story cinema screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.layer=5;root.transform.SetParent(transform,false);
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            _canvas=root.GetComponent<Canvas>();_canvas.sortingOrder=200;_canvas.worldCamera=c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1280,720);
            var backdrop=new GameObject("Backdrop",typeof(RectTransform),typeof(Image));backdrop.layer=5;backdrop.transform.SetParent(root.transform,false);
            Stretch(backdrop.GetComponent<RectTransform>());backdrop.GetComponent<Image>().color=Color.black;backdrop.GetComponent<Image>().raycastTarget=false;
            _picture=Picture(root.transform,"CG film");_picture.texture=_film;
            // 非 16:9 屏幕裁切而不是拉伸，保证角色比例。
            var fit=_picture.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=16f/9f;
            // V044 绘本字幕：细海军蓝遮幅 + 底部柔和暗晕托字 + 金线，样式取自 FishingUITheme（Docs/UI_STYLE_V044.md）。
            Band(root.transform,"Top letterbox",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-58),new Vector2(0,0));
            Band(root.transform,"Bottom letterbox",new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,46));
            // 底部纵向渐变托字（亮海面上也保证字幕对比度），上方再叠主题暗晕让过渡更柔。
            _shade=new Texture2D(1,64,TextureFormat.RGBA32,false){name="Caption shade",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)_shade.SetPixel(0,y,new Color(.02f,.035f,.07f,Mathf.Lerp(.86f,0,Mathf.SmoothStep(0,1,y/63f))));_shade.Apply(false,true);
            var shade=Picture(root.transform,"Caption shade");shade.texture=_shade;var shadeRect=shade.rectTransform;shadeRect.anchorMin=Vector2.zero;shadeRect.anchorMax=new Vector2(1,.46f);
            var glow=Picture(root.transform,"Caption glow");glow.texture=FishingUITheme.TitleGlow;glow.color=new Color(0,0,0,.55f);
            var glowRect=glow.rectTransform;glowRect.anchorMin=new Vector2(.18f,.04f);glowRect.anchorMax=new Vector2(.82f,.30f);
            var rule=Picture(root.transform,"Title gilt rule");rule.texture=FishingUITheme.GiltRule;rule.color=new Color(.93f,.78f,.51f,.9f);
            var ruleRect=rule.rectTransform;ruleRect.anchorMin=ruleRect.anchorMax=new Vector2(.5f,.206f);ruleRect.sizeDelta=new Vector2(340,8);
            var star=Picture(root.transform,"Chapter star");star.texture=FishingUITheme.Star;star.color=new Color(.93f,.78f,.51f);
            var starRect=star.rectTransform;starRect.anchorMin=starRect.anchorMax=new Vector2(.045f,.96f);starRect.sizeDelta=new Vector2(28,28);
            _chapter=Label(root.transform,"Chapter",new Vector2(.066f,.925f),new Vector2(.94f,.995f),21);_chapter.font=FishingUITheme.Heading;_chapter.color=new Color(.88f,.73f,.47f,.95f);
            _title=Label(root.transform,"Title",new Vector2(.06f,.215f),new Vector2(.94f,.29f),36);_title.font=FishingUITheme.Heading;_title.alignment=TextAnchor.MiddleCenter;
            _subtitle=Label(root.transform,"Subtitle",new Vector2(.08f,.07f),new Vector2(.92f,.2f),26);_subtitle.alignment=TextAnchor.UpperCenter;
            _card=Label(root.transform,"Chapter card",new Vector2(.1f,.42f),new Vector2(.9f,.62f),46);_card.font=FishingUITheme.Heading;_card.alignment=TextAnchor.MiddleCenter;
            _hint=Label(root.transform,"Skip hint",new Vector2(.06f,.005f),new Vector2(.94f,.06f),16);_hint.alignment=TextAnchor.MiddleRight;_hint.color=new Color(.72f,.76f,.80f,.75f);
            var fade=new GameObject("Shot dissolve",typeof(RectTransform),typeof(Image));fade.layer=5;fade.transform.SetParent(root.transform,false);Stretch(fade.GetComponent<RectTransform>());
            _fade=fade.GetComponent<Image>();_fade.color=Color.black;_fade.raycastTarget=false;_card.transform.SetAsLastSibling();root.SetActive(false);
            _music=gameObject.AddComponent<AudioSource>();_music.spatialBlend=0;_music.volume=.20f;_music.playOnAwake=false;
            _c.RegisterAudioSource(_music,FishingAudioCategory.Music,.20f);
            RenderPipelineManager.beginCameraRendering+=BeginFilmRender;RenderPipelineManager.endCameraRendering+=EndFilmRender;
        }
        private AudioClip Score(Sequence sequence)
        {
            if(_scores.TryGetValue(sequence,out var clip))return clip;
            clip=Resources.Load<AudioClip>("FishingV042/Story/"+sequence);
            if(clip==null)throw new System.InvalidOperationException("Missing baked story score: "+sequence);
            _scores[sequence]=clip;return clip;
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
            var image=go.GetComponent<Image>();image.color=new Color(.035f,.06f,.10f,1);image.raycastTarget=false;
        }
        private static Text Label(Transform root,string name,Vector2 min,Vector2 max,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text),typeof(Shadow));go.layer=5;go.transform.SetParent(root,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=size;text.color=new Color(.97f,.92f,.82f);text.raycastTarget=false;text.lineSpacing=1.15f;
            go.GetComponent<Shadow>().effectColor=new Color(0,0,0,.75f);go.GetComponent<Shadow>().effectDistance=new Vector2(1.5f,-1.5f);return text;
        }
        public bool BeginForCapture(Sequence sequence)
        {
            Current=sequence;_shots=Films[sequence];Open=true;Age=0;_performed=-1;Capturing=true;CollectSigns();ConfigureScreen(false);
            _canvas.gameObject.SetActive(true);_music.clip=Score(sequence);Present();return true;
        }
        public void SeekForCapture(float age){Age=Mathf.Clamp(age,0,Duration);Present();}
        public bool Begin()=>Begin(Sequence.Prologue);
        public bool Begin(Sequence sequence)
        {
            if(!CanBegin)return false;
            if(XRSettings.isDeviceActive && (!_c.XRSeat.Ready || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.Head,out _,out _)))return false;
            if(_c.JournalOpen)_c.ToggleJournal();
            if(_c.Paused)return false;
            Current=sequence;_shots=Films[sequence];CollectSigns();
            _c.XRNotebook.Close();_c.CloseBoatPanels();Open=true;Age=0;_performed=-1;Capturing=false;_ending=false;_skip.Reset();_c.StoryModalChanged();
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
            if(Age>=Duration)
            {
                // 回响章与终章自然播完时，用水彩墨迹盖满后再收场（桌面与 VR 同一入口，VR 版为头前固定遮罩，不移动视角）。
                if(Current>=Sequence.Wind && !Automated){if(!_ending){_ending=true;FishingInkTransition.Play(Finish);}Present();return;}
                Finish();return;
            }
            Present();
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
            _picture.enabled=true;_camera.enabled=true;_mood=shot.Mood;
            // 有第二机位时后半镜切换（越肩/特写/人物），字幕连续；切点用短暂压暗过渡。
            const float Half=ShotSeconds*.5f;int part=shot.Alt>=0 && t>=Half?1:0;
            float local=part==1?(t-Half)/(ShotSeconds-Half):shot.Alt>=0?t/Half:t/ShotSeconds;
            Perform(shot,index*2+part);
            // VR 里屏幕是世界中静止的“舞台”，镜头内也不运镜，避免大面积视差运动引起晕动；切镜只靠渐黑。
            PresentCamera(part==1?shot.Alt:shot.Camera,part==1?shot.AltTarget:shot.Island,_screenXR?.5f:Mathf.SmoothStep(0,1,local));
            _chapter.text=Chapter(Current)+"   ·   "+(index+1)+" / "+_shots.Length;
            _title.text=shot.Title;
            int typed=Mathf.Clamp(Mathf.FloorToInt((t-Fade*.5f)*TypeSpeed),0,shot.Caption.Length);
            var caption=Balance(shot.Caption);typed=Mathf.Min(typed+(caption.Length-shot.Caption.Length),caption.Length);_subtitle.text=caption.Substring(0,typed);
            _hint.text=XRSettings.isDeviceActive?"右手 A 跳过":"E / 空格 继续    Esc 跳过";
            float titleIn=Mathf.Clamp01((t-.2f)/.6f);_title.color=new Color(.97f,.92f,.82f,titleIn);
            float dip=shot.Alt>=0?Mathf.Clamp01(1-Mathf.Abs(t-Half)/.22f)*.9f:0;
            // 章节卡：每段开头 2 秒在压暗画面上显示章节名，再淡出进入第一镜。
            float card=Mathf.Clamp01(Mathf.Min(Age/.4f,(2.2f-Age)/.5f));_card.text=Chapter(Current).Replace(" · ","\n");_card.color=new Color(.97f,.88f,.66f,card);
            _fade.color=new Color(.02f,.035f,.06f,Mathf.Max(card*.8f,dip,Mathf.Max(1-Mathf.Clamp01(t/Fade),Mathf.Clamp01((t-(ShotSeconds-Fade))/Fade))));
        }
        private static string Chapter(Sequence sequence)=>sequence==Sequence.Prologue?"序章 · 潮汐之约":sequence==Sequence.Chart?"第一章 · 被拆开的答案"
            :sequence==Sequence.Finale?"终章 · 换你来答":"第"+"二三四五"[sequence-Sequence.Wind]+"章 · "+FishingLore.ShrineName(sequence-Sequence.Wind);
        // 超过一行的字幕在最靠近中间的标点后断行，避免第二行只剩一两个字。
        private static string Balance(string caption)
        {
            if(caption.Length<=40)return caption;int best=-1;
            for(int i=8;i<caption.Length-6;i++)if("，。：；？！”".IndexOf(caption[i])>=0 && (best<0 || Mathf.Abs(i-caption.Length/2)<Mathf.Abs(best-caption.Length/2)))best=i;
            return best<0?caption:caption.Substring(0,best+1)+"\n"+caption.Substring(best+1);
        }
        private int _performed=-1;public bool Capturing{get;private set;}private bool _ending;private bool Automated=>_c.Automated;
        private void Perform(Cut shot,int key)
        {
            if(_performed==key)return;_performed=key;
            if(key%2==0 && shot.Sting!=null && !Capturing)_c.WorldAudio?.PlayStinger(shot.Sting);
            // 主角在本机位做“伸手”交互动作（读信、触碰回响、开箱）；只用已有片段，避免混合造成肢体扭曲。
            bool actorShot=(key%2==0?shot.Camera:shot.Alt)==11 || (key%2==0?shot.Camera:shot.Alt)==10 || (key%2==0?shot.Camera:shot.Alt)==7;
            if(shot.Act && actorShot && _c.Ashore)_c.Shore.Character.PlayInteraction();
        }
        private void PresentCamera(int camera,int target,float u)
        {
            Vector3 focus,from,to;var harbor=_c.harbor;var home=_c.Home.Origin;
            var actor=_c.Ashore?_c.Shore.Position:_c.Player.transform.position;
            var facing=_c.Ashore?Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0):Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);
            switch(camera)
            {
                case 0:focus=harbor+Vector3.up*4;from=harbor+new Vector3(-44,7,-34);to=harbor+new Vector3(-33,5.5f,-27);break;
                case 1:// 从屋内中央看向书桌，镜头始终在墙内
                    focus=_c.Home.InteractionPoint(2)+Vector3.up*.15f;var room=home-focus;room.y=0;room=room.normalized;var aside=Vector3.Cross(Vector3.up,room);
                    from=focus+room*1.35f+aside*.45f+Vector3.up*1.05f;to=focus+room*1.0f+aside*.25f+Vector3.up*.85f;break;
                case 2:focus=_c.fishingSpots[0];from=focus+new Vector3(-8,3.6f,-12);to=focus+new Vector3(7,2.8f,-10);break;
                case 3:focus=actor+Vector3.up*1.1f;from=focus+facing*new Vector3(-3.2f,1.6f,3.6f);to=focus+facing*new Vector3(2.2f,.9f,2.6f);break;
                case 4:focus=home+new Vector3(0,2,-1);from=home+new Vector3(12,7,-18);to=home+new Vector3(8,5,-14);break;
                case 6:case 7:
                    var chest=_c.Voyage!=null && _c.Voyage.TreasureChest!=null?_c.Voyage.TreasureChest.position:actor;
                    focus=_c.Voyage!=null?_c.Voyage.TreasureFocus:chest+Vector3.up;
                    var toChest=chest-actor;toChest.y=0;toChest=toChest.sqrMagnitude>.01f?toChest.normalized:facing*Vector3.forward;var flank=Vector3.Cross(Vector3.up,toChest);
                    // 6：主角身后高处看向宝箱；7：侧面中景，主角与打开的宝箱同框
                    if(camera==6){from=focus-toChest*6.5f+flank*2.5f+Vector3.up*3.2f;to=focus-toChest*4.4f+flank*1.6f+Vector3.up*2.0f;}
                    else{focus=Vector3.Lerp(focus,actor+Vector3.up*1.35f,.5f);from=focus+flank*3.6f-toChest*.6f+Vector3.up*.5f;to=focus+flank*2.8f-toChest*.3f+Vector3.up*.35f;}
                    break;
                case 8:// 灯塔低机位仰拍，缓慢横移
                    focus=_c.Home.LighthouseLens+Vector3.down*3;from=focus+new Vector3(-30,-7,-26);to=focus+new Vector3(-22,-8,-31);break;
                case 9:// 远景岛屿，高处缓慢推近
                    focus=FishingIslandWorld.Center(target)+Vector3.up*6;var side=(focus-harbor);side.y=0;side=side.normalized;
                    from=focus-side*95+Vector3.Cross(Vector3.up,side)*30+Vector3.up*26;to=focus-side*72+Vector3.Cross(Vector3.up,side)*18+Vector3.up*18;break;
                case 10:// 秘境回响所在；不在秘境时退回主角
                    var echo=_c.Realms!=null && _c.Realms.Inside?_c.Realms.Device(3):null;
                    focus=(echo!=null?echo.position:actor)+Vector3.up*1.2f;from=focus+facing*new Vector3(-6.8f,3.8f,-6.6f);to=focus+facing*new Vector3(-5.2f,3.0f,-5.4f);break;
                case 14:// 居民中景：从四个方向里选第一个不被遮挡的机位
                    var person=_c.Residents!=null?_c.Residents.Person(target):null;var at=person!=null?person.position:actor;focus=at+Vector3.up*1.35f;
                    var front=person!=null?person.forward:facing*Vector3.forward;
                    for(int i=0;i<4;i++){var d=Quaternion.Euler(0,i*90,0)*front;if(!Physics.Raycast(focus+d*.6f,d,5,~(1<<5),QueryTriggerInteraction.Ignore)){front=d;break;}}
                    var right=Vector3.Cross(Vector3.up,front);
                    from=focus+front*4.2f+right*1.2f+Vector3.up*.35f;to=focus+front*3.3f+right*.8f+Vector3.up*.2f;break;
                case 15:// 灯塔灯室特写
                    focus=_c.Home.LighthouseLens;from=focus+new Vector3(-10,-1.5f,-8);to=focus+new Vector3(-7.5f,-2.2f,-6.5f);break;
                case 13:// 港口贴水望向星望岛（祖父出航的方向）
                    var away=FishingIslandWorld.Center(3)-harbor;away.y=0;away=away.normalized;var across=Vector3.Cross(Vector3.up,away);
                    focus=harbor+away*120+Vector3.up*6;from=harbor+away*8-across*6+Vector3.up*2.6f;to=harbor+away*14-across*3+Vector3.up*2.2f;break;
                case 11:// 主角正面近景
                    focus=actor+Vector3.up*1.45f;from=focus+facing*new Vector3(.9f,.12f,2.9f);to=focus+facing*new Vector3(.55f,.05f,2.1f);break;
                case 12:// 越肩望海
                    focus=actor+facing*Vector3.forward*40+Vector3.up*3;from=actor+facing*new Vector3(.75f,1.75f,-1.9f);to=actor+facing*new Vector3(.6f,1.7f,-1.4f);break;
                default:focus=actor+Vector3.up*.9f;from=focus+facing*new Vector3(-6,3,-7);to=focus+facing*new Vector3(-3,1.4f,-4);break;
            }
            var eye=Vector3.Lerp(from,to,u);
            // 近景镜头若被墙、船体等挡住，就沿视线拉到遮挡物前方。
            if(camera==7 || camera==10 || camera==11){var ray=eye-focus;float reach=ray.magnitude;if(reach>.5f && Physics.Raycast(focus,ray/reach,out var hit,reach,~(1<<5),QueryTriggerInteraction.Ignore))eye=hit.point-ray/reach*.3f;}
            _camera.transform.position=eye;_camera.transform.rotation=Quaternion.LookRotation(focus-_camera.transform.position);
        }
        // 只在胶片相机渲染期间临时改主光（不加后处理、不改游戏时间），渲染结束立刻还原。
        private int _mood;private bool _lit;private Quaternion _sunRotation;private Color _sunColor,_fogColor;private float _sunIntensity;private Vector4 _skySun;private SphericalHarmonicsL2 _ambient;private Color _skyTint;private bool _tinted;
        private void ApplyMood()
        {
            var sun=RenderSettings.sun;if(_mood==0 || sun==null)return;
            _lit=true;_sunRotation=sun.transform.rotation;_sunColor=sun.color;_sunIntensity=sun.intensity;_fogColor=RenderSettings.fogColor;
            bool dusk=_mood==1;
            sun.transform.rotation=Quaternion.Euler(dusk?9:20,_camera.transform.eulerAngles.y+(dusk?118:-125),0);
            sun.color=dusk?new Color(1,.60f,.34f):new Color(1,.84f,.64f);// 秘境（如星轨圣所）会把世界压成夜色；过场里的岛屿镜头仍需看得清，所以给主光设下限。
            sun.intensity=Mathf.Max(_sunIntensity,1.1f)*(dusk?1.25f:1.05f);
            _ambient=RenderSettings.ambientProbe;var ambient=_ambient;ambient*=dusk?.62f:.8f;RenderSettings.ambientProbe=ambient;
            RenderSettings.fogColor=Color.Lerp(_fogColor,dusk?new Color(.98f,.80f,.66f):new Color(.86f,.88f,.92f),.3f);
            var sky=RenderSettings.skybox;if(sky!=null && sky.HasProperty("_SunDirection")){_skySun=sky.GetVector("_SunDirection");sky.SetVector("_SunDirection",-sun.transform.forward);}
            if(sky!=null && sky.HasProperty("_SkyTint")){_skyTint=sky.GetColor("_SkyTint");_tinted=true;sky.SetColor("_SkyTint",_skyTint*(dusk?new Color(1.12f,.93f,.82f):new Color(1.04f,1,.96f)));}
        }
        private void RestoreMood()
        {
            if(!_lit)return;_lit=false;var sun=RenderSettings.sun;
            if(sun!=null){sun.transform.rotation=_sunRotation;sun.color=_sunColor;sun.intensity=_sunIntensity;}RenderSettings.fogColor=_fogColor;RenderSettings.ambientProbe=_ambient;
            var sky=RenderSettings.skybox;if(sky!=null && sky.HasProperty("_SunDirection"))sky.SetVector("_SunDirection",_skySun);
            if(_tinted && sky!=null){sky.SetColor("_SkyTint",_skyTint);_tinted=false;}
        }
        /// <summary>日志重看：已解锁的剧情段（序章总可看）。</summary>
        public bool Unlocked(Sequence sequence)
        {
            var lore=_c.Career.Lore;
            return sequence==Sequence.Prologue || (sequence==Sequence.Chart && lore.ChartFound)
                || (sequence==Sequence.Finale && lore.GrandTreasureFound) || (sequence>=Sequence.Wind && sequence<=Sequence.Star && lore.Cleared(sequence-Sequence.Wind));
        }
        public Sequence Latest{get{var latest=Sequence.Prologue;foreach(Sequence s in System.Enum.GetValues(typeof(Sequence)))if(Unlocked(s))latest=s;return latest;}}
        public static string Name(Sequence sequence)=>Chapter(sequence);
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
        private void BeginFilmRender(ScriptableRenderContext context,Camera camera){if(camera==_camera && _c!=null && _c.Shore!=null){_c.Shore.Character.SetFilmPresentation(true);ApplyMood();Signs(true);}}
        private void EndFilmRender(ScriptableRenderContext context,Camera camera){if(camera==_camera && _c!=null && _c.Shore!=null){_c.Shore.Character.SetFilmPresentation(false);RestoreMood();Signs(false);}}
        // 世界里的浮空提示字（TextMesh）在电影画面里只会穿帮，渲染胶片时暂时隐藏。
        private readonly List<Renderer> _signs=new List<Renderer>();private readonly List<bool> _signHidden=new List<bool>();
        private void CollectSigns(){_signs.Clear();foreach(var sign in FindObjectsOfType<TextMesh>())_signs.Add(sign.GetComponent<Renderer>());}
        private void Signs(bool hide)
        {
            if(hide)_signHidden.Clear();
            for(int i=0;i<_signs.Count;i++){var r=_signs[i];if(r==null){if(hide)_signHidden.Add(false);continue;}if(hide){_signHidden.Add(r.forceRenderingOff);r.forceRenderingOff=true;}else if(i<_signHidden.Count)r.forceRenderingOff=_signHidden[i];}
        }
        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering-=BeginFilmRender;RenderPipelineManager.endCameraRendering-=EndFilmRender;
            if(_c!=null && _c.Shore!=null)_c.Shore.Character.SetFilmPresentation(false);
            if(_film!=null){_film.Release();Destroy(_film);}if(_shade!=null)Destroy(_shade);_scores.Clear();
        }
    }
}
