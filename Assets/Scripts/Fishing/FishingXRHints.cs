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
    public sealed class FishingXRHints : MonoBehaviour
    {
        public enum Context { Shore, Boat, Fishing, Notebook, Story, Realm, Align, Climb, Camera, Craft }
        public static readonly string[] ContextNames={"岸上行走","船上驾驶","驻钓 / 搏鱼","手册 / 菜单","对话 / 信件 / 过场","秘境机关","信标校准","攀爬绳梯","相机取景","手工 / 锻造"};
        /// <summary>key: A B X Y Trigger Grip use PICO glyphs; Stick / Click / Menu are drawn as text.</summary>
        public static (string key,string text)[] Rows(Context context,bool left)
        {
            switch(context)
            {
                case Context.Shore:return left
                    ?new[]{("Stick",FishingXRComfort.Teleport?"前推瞄准 · 松开瞬移":"行走"),("X","上船（靠近泊船）"),("Y","切换装备"),("Menu","手册")}
                    :new[]{("Stick",FishingXRComfort.TurnName),("A","互动 / 对话 / 攀爬"),("Trigger","使用工具"),("Grip","握住：拾取物品 / 提灯 / 摇果树"),("B","手册")};
                case Context.Boat:return left
                    ?new[]{("Stick","前推油门 · 后拉倒车"),("Click","按下摇杆加速"),("Trigger","刹车"),("X","靠岸低速时上岸")}
                    :new[]{("Stick","左右掌舵"),("A","钓点驻钓 / 互动"),("B","手册 / 收起面板")};
                case Context.Fishing:return left
                    ?new[]{("Grip","握轮柄 · 转腕收线"),("Grip","鱼跃出水：松开"),("Grip","抄网 / 抓鱼")}
                    :new[]{("Grip","握竿，松开放下"),("Trigger","按住抖腕 · 松开抛竿 · 遛鱼按住收线"),("A","快捷抛竿 / 咬钩提竿"),("Trigger","事务面板：指向点选"),("B","收起面板 / 手册")};
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
                    ?new[]{("Stick","上推攀登 · 下拉下降")}
                    :new[]{("A","到端自动离开")};
                case Context.Camera:return left
                    ?new[]{("Stick","可继续行走")}
                    :new[]{("Trigger","拍照"),("B","收起 / 手册")};
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
            if(c.Life!=null && c.Life.Viewfinding)return Context.Camera;
            if(!c.Ashore)return c.Anchored || c.HasPendingCatch?Context.Fishing:Context.Boat;
            if(c.Shore.Climbing)return Context.Climb;
            return c.Realms!=null && c.Realms.Inside?Context.Realm:Context.Shore;
        }
        public static string KeyName(string key,bool left)=>key=="Stick"?(left?"左摇杆":"右摇杆"):key=="Click"?"按下左摇杆":key=="Menu"?"左菜单键":key=="Trigger"?(left?"左扳机":"右扳机"):key=="Grip"?(left?"左握把":"右握把"):key;
        /// <summary>Plain text for the notebook 操作说明 cards.</summary>
        public static string Card(Context context)
        {
            var text=ContextNames[(int)context];
            foreach(bool left in new[]{true,false})foreach(var (key,label) in Rows(context,left))text+="\n"+KeyName(key,left)+" · "+label;
            return text;
        }

        private const int Slots=7;private const float Scale=.0005f;
        private FishingPrototype _c;
        private readonly Tag[] _tags=new Tag[2];
        private Context _context=(Context)(-1);private string _nearby="";private float _teachUntil;
        private sealed class Tag{public Canvas Canvas;public RectTransform Rect;public Image[] Glyphs=new Image[Slots];public Text[] Labels=new Text[Slots];public float Alpha;public CanvasGroup Group;}
        public void Build(FishingPrototype c)
        {
            _c=c;
            for(int h=0;h<2;h++)
            {
                var root=new GameObject(h==0?"XR left controller hints":"XR right controller hints",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));root.layer=5;root.transform.SetParent(transform,false);
                var tag=_tags[h]=new Tag{Canvas=root.GetComponent<Canvas>(),Rect=root.GetComponent<RectTransform>(),Group=root.GetComponent<CanvasGroup>()};
                tag.Canvas.renderMode=RenderMode.WorldSpace;tag.Canvas.worldCamera=c.viewCamera;tag.Rect.localScale=Vector3.one*Scale;tag.Rect.pivot=new Vector2(.5f,0);
                var plate=root.AddComponent<Image>();plate.sprite=FishingUITheme.PaperSprite;plate.type=Image.Type.Sliced;plate.material=FishingUITheme.WorldMaterial;plate.raycastTarget=false;FishingUITheme.Bind(plate,0);
                for(int i=0;i<Slots;i++)
                {
                    var g=new GameObject("Glyph",typeof(RectTransform),typeof(Image));g.layer=5;g.transform.SetParent(root.transform,false);
                    var grt=g.GetComponent<RectTransform>();grt.anchorMin=grt.anchorMax=grt.pivot=new Vector2(0,1);grt.anchoredPosition=new Vector2(14,-12-i*40);grt.sizeDelta=new Vector2(36,36);
                    tag.Glyphs[i]=g.GetComponent<Image>();tag.Glyphs[i].material=FishingUITheme.WorldMaterial;tag.Glyphs[i].raycastTarget=false;FishingUITheme.Bind(tag.Glyphs[i],4);
                    var t=new GameObject("Label",typeof(RectTransform),typeof(Text));t.layer=5;t.transform.SetParent(root.transform,false);
                    var trt=t.GetComponent<RectTransform>();trt.anchorMin=trt.anchorMax=trt.pivot=new Vector2(0,1);trt.anchoredPosition=new Vector2(58,-12-i*40);trt.sizeDelta=new Vector2(300,36);
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
                if(h==1 && _nearby.Length>0){var list=new System.Collections.Generic.List<(string,string)>{("A",_nearby)};list.AddRange(rows);rows=list.ToArray();}var tag=_tags[h];float width=0;
                for(int i=0;i<Slots;i++)
                {
                    bool used=i<rows.Length;tag.Glyphs[i].gameObject.SetActive(false);tag.Labels[i].gameObject.SetActive(used);if(!used)continue;
                    var (key,text)=rows[i];bool glyph=key!="Stick" && key!="Click" && key!="Menu";
                    if(glyph){tag.Glyphs[i].sprite=FishingUITheme.ControllerGlyph(key);tag.Glyphs[i].gameObject.SetActive(true);}
                    tag.Labels[i].text=glyph?text:"【"+KeyName(key,h==0)+"】"+text;
                    tag.Labels[i].rectTransform.anchoredPosition=new Vector2(glyph?58:14,-12-i*40);
                    width=Mathf.Max(width,tag.Labels[i].rectTransform.anchoredPosition.x+tag.Labels[i].preferredWidth);
                }
                tag.Rect.sizeDelta=new Vector2(width+18,rows.Length*40+20);
            }
        }
        /// <summary>Validation capture: fills a tag for a context without needing a headset.</summary>
        public Canvas PreviewForValidation(Context context,bool left){Fill(context);_context=context;var tag=_tags[left?0:1];tag.Group.alpha=1;return tag.Canvas;}
        private void LateUpdate()
        {
            if(_c==null)return;
            bool xr=XRSettings.isDeviceActive && _c.XRSeat!=null && _c.XRSeat.Ready && Application.isFocused && !_c.Paused;
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
                if(PlayerPrefs.GetInt(key,0)==0)
                {
                    PlayerPrefs.SetInt(key,1);PlayerPrefs.Save();_teachUntil=Time.unscaledTime+12;
                    if(context!=Context.Notebook && context!=Context.Story)_c.Notice("首次"+ContextNames[(int)context]+" · 手柄上方显示按键；以后抬起手柄看向它即可再次查看。",6);
                }
            }
            var eye=_c.viewCamera.transform;bool teaching=Time.unscaledTime<_teachUntil && context!=Context.Story;
            for(int h=0;h<2;h++)
            {
                var tag=_tags[h];bool show=false;
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
                tag.Group.alpha=tag.Alpha;tag.Canvas.gameObject.SetActive(tag.Alpha>0);
            }
        }
    }
}
