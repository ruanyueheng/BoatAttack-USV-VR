using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    /// <summary>
    /// VR controller hints: a small paper tag floats above each controller with the buttons of the current context.
    /// Tags show while the player looks at a controller, and once (about 12 s) the first time each context is entered.
    /// The same table feeds the notebook's 操作说明 page and Docs/VR_CONTROLS_V044.md.
    /// </summary>
    [DefaultExecutionOrder(1600)]
    public sealed class FishingXRHints : MonoBehaviour
    {
        public const string NotebookHelp="B 手册：地图 / 背包 / 设置与救援";
        public const string CameraHelp="相机无需装备。右手举到眼前 22 厘米内，右扳机拍照；放下手收起。\n先收竿起航，用 Y 换下猎枪 / 望远镜；照片见 B → 航海手札 → 相册。";
        public const string BinocularHelp="双手握住望远镜举到眼前，通过镜筒观察；移开或松握退出。\n右摇杆上下调倍，看向 22 米内陆地动物，A 记录；B → 航海手札 → 自然观察查看记录。";
        public enum Context { Shore, Boat, Fishing, Notebook, Story, Realm, Align, Climb, Camera, Craft, Binocular }
        public static readonly string[] ContextNames={"岸上行走","船上驾驶","驻钓 / 搏鱼","手册 / 菜单","对话 / 信件 / 过场","秘境机关","信标校准","攀爬绳梯","相机取景","手工 / 锻造","望远镜观察"};
        /// <summary>key: A B X Y Trigger Grip use PICO glyphs; Stick / Click / Menu are drawn as text.</summary>
        public static (string key,string text)[] Rows(Context context,bool left)
        {
            switch(context)
            {
                case Context.Shore:return left
                    ?new[]{("Stick",FishingXRComfort.Teleport?"前推瞄准 · 松开瞬移":"行走"),("Grip","移动时按住疾跑"),("Trigger","移动时按住疾跑"),("X","上船（靠近泊船）"),("Y","换装 / 收灯"),("Menu","手册")}
                    :new[]{("Stick",FishingXRComfort.TurnName+" · 按下跳跃"),("A","互动 / 对话 / 攀爬"),("Trigger","工具 / 提灯开关 / 信标"),("Grip","拾取 / 提灯 / 摇树 / 掀箱盖"),("Trigger","右手举到眼前 22 厘米：拍照"),("B","手册")};
                case Context.Boat:return left
                    ?new[]{("Stick","前推油门 · 后拉倒车"),("Grip","按住加速"),("Click","按下左摇杆亦可加速"),("Trigger","刹车"),("X","靠岸低速时上岸")}
                    :new[]{("Stick","左右掌舵"),("A","钓点驻钓 / 互动"),("Trigger","右手举到眼前 22 厘米：拍照"),("B","手册 / 收起面板")};
                case Context.Fishing:return left
                    ?new[]{("Grip","握轮柄 · 转腕收线"),("Grip","鱼跃出水：松开"),("Grip","抄网 / 抓鱼")}
                    :new[]{("Grip","握竿，松开放下"),("Trigger","按住蓄力 · 松开抛竿 · 遛鱼按住收线"),("A","快捷抛竿 / 咬钩提竿"),("Click","收竿起航"),("B","手册 / 收起面板")};
                case Context.Notebook:return left
                    ?new[]{("Menu","也可开关手册")}
                    :new[]{("Trigger","射线点选"),("B","关闭手册")};
                case Context.Story:return left
                    ?new[]{("Stick","过场中可自由转头")}
                    :new[]{("A","继续 / 展开"),("Trigger","点选对话选项"),("B","关闭 / 跳过")};
                case Context.Realm:return left
                    ?new[]{("Stick",FishingXRComfort.Teleport?"前推瞄准 · 松开瞬移":"行走"),("Menu","手册 · 求助在设置")}
                    :new[]{("Grip","握住曲柄 · 转腕拨动"),("A","互动 / 求助石"),("Grip","阵风时按住稳住"),("Stick","星象镜：转动"),("Trigger","星象镜：对焦"),("B","退出观测 / 手册")};
                case Context.Align:return left
                    ?new[]{("Stick","（校准中不移动）")}
                    :new[]{("Stick","左右转动转盘"),("A","确认"),("B","取消")};
                case Context.Climb:return left
                    ?new[]{("Grip","握住横档下拉 · 双手交替"),("Stick","上推攀登 · 下拉下降")}
                    :new[]{("Grip","握住下一横档 · 下拉攀登"),("A","到端自动离开")};
                case Context.Camera:return left
                    ?new[]{("Stick","可继续行走")}
                    :new[]{("Trigger","拍照 · 放下右手收起相机"),("B","手册 → 航海手札 → 相册")};
                case Context.Binocular:return left
                    ?new[]{("Grip","握住镜筒，双手举到眼前"),("Y","切换装备"),("Stick","可缓慢接近动物")}
                    :new[]{("Grip","握住镜筒 · 移开或松握退出"),("Stick","上下调倍"),("A","举镜后，看向 22 米内陆地动物并记录"),("B","手册 → 航海手札 → 自然观察")};
                default:return left
                    ?new[]{("Grip","握住工具")}
                    :new[]{("Grip","握住工具 / 曲柄"),("A","切换配方"),("B","退出")};
            }
        }
        public static Context Current(FishingPrototype c)
        {
            if(c.XRNotebook!=null && c.XRNotebook.IsOpen)return Context.Notebook;
            if(c.HandGames!=null && c.HandGames.Active)return Context.Craft;
            if(c.StoryBlocksGameplay)return Context.Story;
            if(c.Expedition!=null && c.Expedition.Aligning)return Context.Align;
            if(c.Life!=null && (c.Life.Viewfinding || c.Life.VrRaised))return Context.Camera;
            if(!c.Ashore)return c.Anchored || c.HasPendingCatch?Context.Fishing:Context.Boat;
            if(c.Shore.Climbing)return Context.Climb;
            if(c.Career.Backpack.Equipped==FieldItem.Binoculars && !c.Shore.Swimming && !(c.Realms!=null && c.Realms.Inside))return Context.Binocular;
            return c.Realms!=null && c.Realms.Inside?Context.Realm:Context.Shore;
        }
        public static string KeyName(string key,bool left)=>key=="Stick"?(left?"左摇杆":"右摇杆"):key=="Click"?(left?"按下左摇杆":"按下右摇杆"):key=="Menu"?"左菜单键":key=="Trigger"?(left?"左扳机":"右扳机"):key=="Grip"?(left?"左握把":"右握把"):key;
        /// <summary>Plain text for the notebook 操作说明 cards.</summary>
        public static string Card(Context context)
        {
            var text=ContextNames[(int)context];
            foreach(bool left in new[]{true,false})foreach(var (key,label) in Rows(context,left))text+="\n"+KeyName(key,left)+" · "+label;
            return text;
        }

        /// <summary>The desktop's next-action and objective cues remain visible in VR without raising a controller.</summary>
        public static string Guidance(FishingPrototype c)
        {
            const string help=NotebookHelp;
            if(c.Combat!=null && c.Combat.Status.Length>0)return c.Combat.Status+"\n红圈出现后侧移离开落点；右扳机使用武器\n"+help;
            if(c.HasPendingCatch)return "钓到了 · 按 A 放入鱼箱，或在收获卡选择放生\n传说鱼：A 测量，再确认放生\n"+help;
            if(c.Expedition.Aligning)return "校准信标 · 右摇杆左右调转盘，A 确认，B 取消\n"+c.Expedition.AlignmentHint;
            if(!c.Ashore && c.Anchored)return FishingAction(c.Session,c.Hands.HoldingRod)+"\n按下右摇杆收竿起航 · B 手册换饵 / 返家";
            if(c.Life!=null && c.Life.VrRaised)return "相机取景 · 右扳机拍照\n放下右手收起 · B → 航海手札 → 相册";
            string action;
            if(c.Ashore)
            {
                string nearby=c.WorldInteractionTitle;
                action=FishingXRText.Adapt(nearby ?? c.Shore.ActionTitle,true);
                if(c.Shore.Climbing)return action+"\n双手握住横档交替下拉，或左摇杆上下；到端自动离开\n"+help;
                if(c.Shore.Swimming)return "游泳 · 左摇杆朝浅岸移动，左握把加速\n靠近船边按 X 上船，右摇杆按下攀低台\n"+help;
                if(c.Career.Backpack.Equipped==FieldItem.Binoculars)return (c.Life!=null && c.Life.Binoculars!=null && c.Life.Binoculars.Active?"望远镜已举起 · 看向动物，A 记录\n":"望远镜已选中 · 双手握住举到眼前\n")+BinocularHelp;
                if(nearby==null)action+=" · "+(FishingXRComfort.Teleport?"左摇杆前推，松开瞬移":"左摇杆行走，左握把疾跑")+" · 右摇杆按下跳跃 · Y 换装备";
            }
            else if(c.AtHarbor)action="潮汐渔港 · A 交付 / 出售，X 上岸\n左摇杆前进 / 倒车，右摇杆掌舵";
            else if(c.SpotDistance<=22)action=c.Speed>3?"接近钓点 · 按住左扳机减速至停船，再按 A 驻钓":"A 驻钓 · 右握把拿竿后，A 抛竿 / 提竿";
            else action="左摇杆前进 / 倒车 · 右摇杆掌舵 · 靠岸 X 下船";
            string goal=FishingXRText.Adapt(FishingVoyageWorld.CompactObjective(c.Career),true);
            if(!c.Ashore)
            {
                action+="\n按住左握把加速 · 左扳机制动"+(c.Boost.Overheated?" · 过热冷却至 25%":c.Boost.Active?" · 加速中":"");
                var delta=c.NavigationPosition-c.ExplorerPosition;
                float bearing=Mathf.DeltaAngle(c.viewCamera.transform.eulerAngles.y,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
                string direction=Mathf.Abs(bearing)<12?"前方":(bearing<0?"左侧 ":"右侧 ")+Mathf.RoundToInt(Mathf.Abs(bearing))+"°";
                goal=c.NavigationName+" · "+Mathf.RoundToInt(FishingPrototype.FlatDistance(c.ExplorerPosition,c.NavigationPosition))+" 米 · "+direction+"\n"+goal;
            }
            return action+"\n"+goal+"\n"+help;
        }
        public static string FishingAction(CatchSession session,bool holdingRod)
        {
            if(session.Phase==CatchPhase.Landing)return "鱼已靠近 · 左手握抄网，套住鱼后举起\n不要继续硬拉；鱼线张力 "+Mathf.RoundToInt(session.Tension*100)+"%";
            if(!holdingRod)return "按 A 快捷拿竿并抛出，或右握把握住竿柄\n拿竿后：右扳机按住蓄力、松开抛竿";
            switch(session.Phase)
            {
                case CatchPhase.Waiting:return session.BitePreview>0?"浮标轻啄 · 先别提竿，等明显下沉再按 A":"等待咬钩 · 看浮标、听声音，咬钩后按 A 提竿";
                case CatchPhase.Bite:return "咬钩了！按 A 或右扳机提竿 · 剩余 "+session.Remaining.ToString("0.0")+" 秒";
                case CatchPhase.Fighting:
                    string action=session.Beat==FishBeat.Leap?"跃出水面！松开右扳机和左握把，停止收线"
                        :session.Tension>.78f?"张力过高！松开右扳机和左握把，停止收线"
                        :session.Beat==FishBeat.Rest?"鱼在回气 · 按住右扳机收线，或左握把转轮"
                        :session.Beat==FishBeat.Windup?"鱼在蓄力 · 准备松开右扳机和左握把":"鱼在冲刺 · 松开右扳机和左握把放线";
                    string steer=session.FishDirection<-.2f?"持竿手向右横移":session.FishDirection>.2f?"持竿手向左横移":"保持收线节奏";
                    return action+"\n"+steer+" · 张力 "+Mathf.RoundToInt(session.Tension*100)+"% · 余线 "+session.Distance.ToString("0.0")+" 米";
                case CatchPhase.Caught:return "鱼获已处理 · 右扳机蓄力松开或 A 再抛一竿";
                case CatchPhase.Escaped:return "鱼逃走了 · A 重试；鱼冲刺、跃水或张力高时放线";
                default:return "右扳机按住蓄力，松开抛竿；向前挥手可增加抛距\nA 快捷抛竿；咬钩后 A 或右扳机提竿";
            }
        }

        private const int Slots=7;private const float Scale=.0005f;
        private FishingPrototype _c;
        private readonly Tag[] _tags=new Tag[2];
        private Context _context=(Context)(-1);private string _nearby="";private float _teachUntil;private int _taughtContexts;
        private sealed class Tag{public Canvas Canvas;public RectTransform Rect;public Text Title;public Image[] Glyphs=new Image[Slots];public Text[] Labels=new Text[Slots];public float Alpha;public CanvasGroup Group;}
        public void Build(FishingPrototype c)
        {
            _c=c;
            for(int h=0;h<2;h++)
            {
                var root=new GameObject(h==0?"XR left controller hints":"XR right controller hints",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(CanvasScaler));root.layer=5;root.transform.SetParent(transform,false);
                var tag=_tags[h]=new Tag{Canvas=root.GetComponent<Canvas>(),Rect=root.GetComponent<RectTransform>(),Group=root.GetComponent<CanvasGroup>()};
                root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=4;
                tag.Canvas.renderMode=RenderMode.WorldSpace;tag.Canvas.worldCamera=c.viewCamera;tag.Rect.localScale=Vector3.one*Scale;tag.Rect.pivot=new Vector2(.5f,0);
                var plate=root.AddComponent<Image>();plate.sprite=FishingUITheme.StorybookPaperSprite;plate.type=Image.Type.Sliced;plate.material=FishingUITheme.WorldMaterial;plate.raycastTarget=false;FishingUITheme.Bind(plate,0);
                var edge=new GameObject("Gilt star edge",typeof(RectTransform),typeof(Image));edge.layer=5;edge.transform.SetParent(root.transform,false);
                var er=edge.GetComponent<RectTransform>();er.anchorMin=Vector2.zero;er.anchorMax=Vector2.one;er.offsetMin=er.offsetMax=Vector2.zero;
                var frame=edge.GetComponent<Image>();frame.sprite=FishingUITheme.GiltFrameSprite;frame.type=Image.Type.Sliced;frame.material=FishingUITheme.WorldMaterial;frame.raycastTarget=false;FishingUITheme.Bind(frame,4);
                var title=new GameObject("Controller context heading",typeof(RectTransform),typeof(Text));title.layer=5;title.transform.SetParent(root.transform,false);
                tag.Title=title.GetComponent<Text>();tag.Title.fontSize=27;tag.Title.material=FishingUITheme.WorldMaterial;tag.Title.raycastTarget=false;FishingUITheme.StyleHeading(tag.Title);
                var titleRect=tag.Title.rectTransform;titleRect.anchorMin=titleRect.anchorMax=titleRect.pivot=new Vector2(0,1);titleRect.anchoredPosition=new Vector2(14,-6);titleRect.sizeDelta=new Vector2(400,44);
                for(int i=0;i<Slots;i++)
                {
                    var g=new GameObject("Glyph",typeof(RectTransform),typeof(Image));g.layer=5;g.transform.SetParent(root.transform,false);
                    var grt=g.GetComponent<RectTransform>();grt.anchorMin=grt.anchorMax=grt.pivot=new Vector2(0,1);grt.anchoredPosition=new Vector2(14,-52-i*40);grt.sizeDelta=new Vector2(36,36);
                    tag.Glyphs[i]=g.GetComponent<Image>();tag.Glyphs[i].material=FishingUITheme.WorldMaterial;tag.Glyphs[i].raycastTarget=false;FishingUITheme.Bind(tag.Glyphs[i],4);
                    var t=new GameObject("Label",typeof(RectTransform),typeof(Text));t.layer=5;t.transform.SetParent(root.transform,false);
                    var trt=t.GetComponent<RectTransform>();trt.anchorMin=trt.anchorMax=trt.pivot=new Vector2(0,1);trt.anchoredPosition=new Vector2(58,-52-i*40);trt.sizeDelta=new Vector2(300,36);
                    var text=tag.Labels[i]=t.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=24;text.alignment=TextAnchor.MiddleLeft;text.material=FishingUITheme.WorldMaterial;text.raycastTarget=false;
                    text.horizontalOverflow=HorizontalWrapMode.Overflow;FishingUITheme.Bind(text,2);
                }
                root.SetActive(false);
            }
        }
        private void Fill(Context context)
        {
            for(int h=0;h<2;h++)
            {
                var rows=Rows(context,h==0);
                // The right tag leads with what A does right here (the desktop "E ..." prompt, in PICO words).
                if(h==1 && _nearby.Length>0){var list=new System.Collections.Generic.List<(string,string)>{("A",_nearby)};list.AddRange(rows);rows=list.ToArray();}var tag=_tags[h];tag.Title.text=ContextNames[(int)context]+(h==0?" · 左手":" · 右手");float width=tag.Title.preferredWidth+14;
                for(int i=0;i<Slots;i++)
                {
                    bool used=i<rows.Length;tag.Glyphs[i].gameObject.SetActive(false);tag.Labels[i].gameObject.SetActive(used);if(!used)continue;
                    var (key,text)=rows[i];bool glyph=key!="Stick" && key!="Click" && key!="Menu";
                    if(glyph){tag.Glyphs[i].sprite=FishingUITheme.ControllerGlyph(key);tag.Glyphs[i].gameObject.SetActive(true);}
                    tag.Labels[i].text=glyph?text:"【"+KeyName(key,h==0)+"】"+text;
                    tag.Labels[i].rectTransform.anchoredPosition=new Vector2(glyph?58:14,-52-i*40);
                    width=Mathf.Max(width,tag.Labels[i].rectTransform.anchoredPosition.x+tag.Labels[i].preferredWidth);
                }
                tag.Rect.sizeDelta=new Vector2(width+18,rows.Length*40+60);tag.Title.rectTransform.sizeDelta=new Vector2(width-14,44);
            }
        }
        /// <summary>Validation capture: fills a tag for a context without needing a headset.</summary>
        public Canvas PreviewForValidation(Context context,bool left){Fill(context);_context=context;var tag=_tags[left?0:1];tag.Group.alpha=1;return tag.Canvas;}
        public bool IsVisible(bool left){var tag=_tags[left?0:1];return tag!=null && tag.Canvas.gameObject.activeInHierarchy && tag.Group.alpha>0;}
        private void LateUpdate()
        {
            if(_c==null)return;
            bool xr=XRSettings.isDeviceActive && _c.XRSeat!=null && _c.XRSeat.Ready && Application.isFocused && !_c.Paused && !_c.HandGames.Active;
            if(!xr){foreach(var tag in _tags)tag.Canvas.gameObject.SetActive(false);return;}
            var context=Current(_c);
            string nearby=context==Context.Shore || context==Context.Realm?FishingXRText.Adapt(_c.WorldInteractionTitle??"",true).TrimStart():"";
            if(nearby.StartsWith("A "))nearby=nearby.Substring(2).TrimStart('·',' '); // the A glyph already says it
            if(nearby.Length>18)nearby=nearby.Substring(0,17)+"…";
            if(nearby!=_nearby){_nearby=nearby;if(context==_context)Fill(context);}
            if(context!=_context)
            {
                _context=context;Fill(context);
                string key="FishingXR.Taught."+context;
                if(_c.HasIsolatedProfile?(_taughtContexts&(1<<(int)context))==0:PlayerPrefs.GetInt(key,0)==0)
                {
                    _taughtContexts|=1<<(int)context;
                    if(!_c.HasIsolatedProfile){PlayerPrefs.SetInt(key,1);PlayerPrefs.Save();}_teachUntil=Time.unscaledTime+12;
                    if(context!=Context.Notebook && context!=Context.Story)_c.Notice("首次"+ContextNames[(int)context]+" · 手柄上方显示按键；以后抬起手柄看向它即可再次查看。",6);
                }
            }
            var eye=_c.viewCamera.transform;bool teaching=Time.unscaledTime<_teachUntil && context!=Context.Story;
            for(int h=0;h<2;h++)
            {
                var tag=_tags[h];bool show=false;
                var life=_c.Life;
                bool optical=life!=null && (life.CameraVisible || life.OwnsVrTrigger || (life.Binoculars!=null && life.Binoculars.Active));
                bool chart=h==0 && life!=null && life.PalmMap!=null && life.PalmMap.gameObject.activeInHierarchy;
                // The chart and tags share the same hand anchor; optical views must clear immediately, including fade-out frames.
                if(optical || chart){tag.Alpha=0;tag.Group.alpha=0;tag.Canvas.gameObject.SetActive(false);continue;}
                if(FishingXRTracking.TryPose(_c.viewCamera,h==0?XRNode.LeftHand:XRNode.RightHand,out var hand,out _))
                {
                    var toHand=hand-eye.position;
                    bool looked=toHand.magnitude<.8f && Vector3.Angle(eye.forward,toHand)<24;
                    show=looked || teaching || (h==1 && _nearby.Length>0);
                    // Above the controller, facing the eye, at hand distance (≥1° text: 24 px × 0.5 mm at ≤0.7 m).
                    var position=hand+Vector3.up*.07f;
                    tag.Rect.SetPositionAndRotation(position,Quaternion.LookRotation(position-eye.position,Vector3.up));
                }
                tag.Alpha=Mathf.MoveTowards(tag.Alpha,show?1:0,Time.unscaledDeltaTime*6);
                tag.Group.alpha=tag.Alpha;tag.Canvas.gameObject.SetActive(tag.Alpha>0);if(tag.Alpha>0)FishingUITheme.StyleHeading(tag.Title);
            }
        }
    }
}
