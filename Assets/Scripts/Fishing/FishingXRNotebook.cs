using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public sealed class FishingNotebookButton : MonoBehaviour { public FishingXRNotebook Owner; public int Index; public Action Command; }
    /// <summary>World-locked, ray-selectable notebook; all mutations use the existing inventory/workshop transactions.</summary>
    public sealed partial class FishingXRNotebook : MonoBehaviour
    {
        private FishingPrototype _c;
        private Canvas _canvas;
        private Text _status;
        private RawImage _map;
        private RectTransform _mapPlayer,_mapTarget;
        private FishingMapDefinition _mapDefinition;
        private Material _mapPalette;
        private bool _craftBasics;
        private readonly Text[] _labels=new Text[9];
        private readonly Image[] _plates=new Image[9];
        private readonly RawImage[] _icons=new RawImage[9];
        private readonly Action[] _actions=new Action[9];
        private readonly FishingPressEdge _menu=new FishingPressEdge(),_primary=new FishingPressEdge(),_board=new FishingPressEdge(),_slot=new FishingPressEdge(),_trigger=new FishingPressEdge();
        private LineRenderer _ray;
        private int _page,_offset;
        private int _presentedPage=-1;
        private float _pageOpenedAt;
        private int _guideLesson,_guidePracticed;
        private Canvas _feedback;
        private Text _feedbackText,_feedbackTitle;
        private string _feedbackMessage;
        private Quaternion _feedbackYaw;
        private bool _toastFollowing,_feedbackWasVisible;
        private bool _guideLeftReady,_guideRightReady;
        private readonly FishingPressEdge _guideLeftTrigger=new FishingPressEdge(),_guideLeftGrip=new FishingPressEdge(),_guideRightGrip=new FishingPressEdge(),_guideBoost=new FishingPressEdge();
        public const string GuidePreference="FishingXRGuideV025";
        public static bool GuideCompleted=>PlayerPrefs.GetInt(GuidePreference,0)==1;
        public bool StorePage => IsOpen && (_page==4 || _page==5);
        private bool _turnReady;
        private float _nextText,_nextStrike;
        private Vector3 _lastTip;
        private bool _tipSample;
        private bool _leftPointer;
        public bool IsOpen { get; private set; }
        public bool MotionReady { get; private set; }
        public bool BlocksGameplay=>IsOpen || (XRSettings.isDeviceActive && !MotionReady);
        public void Build(FishingPrototype c)
        {
            _c=c;
            var root=new GameObject("Coastal XR notebook",typeof(RectTransform),typeof(Canvas));root.layer=5;root.transform.SetParent(transform,false);
            _canvas=root.GetComponent<Canvas>();_canvas.renderMode=RenderMode.WorldSpace;_canvas.worldCamera=c.viewCamera;
            var rt=root.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(580,660);rt.localScale=Vector3.one*.0016f;
            var panel=root.AddComponent<Image>();panel.color=new Color(.025f,.048f,.082f,.97f);panel.raycastTarget=false;
            _status=Label(root.transform,new Vector2(24,-20),new Vector2(532,224),23);
            var map=new GameObject("Shared bay terrain map",typeof(RectTransform),typeof(RawImage));map.layer=5;map.transform.SetParent(root.transform,false);
            var mapRect=map.GetComponent<RectTransform>();mapRect.anchorMin=mapRect.anchorMax=mapRect.pivot=new Vector2(0,1);mapRect.anchoredPosition=new Vector2(170,-78);mapRect.sizeDelta=new Vector2(240,166);
            _map=map.GetComponent<RawImage>();_map.raycastTarget=false;_mapDefinition=Resources.Load<FishingMapDefinition>("FishingMap");
            if(_mapDefinition!=null && _mapDefinition.IsUsable)
            {_map.texture=_mapDefinition.texture;float ratio=_map.texture.width/(float)_map.texture.height;mapRect.sizeDelta=new Vector2(Mathf.Min(532,166*ratio),166);mapRect.anchoredPosition=new Vector2((580-mapRect.sizeDelta.x)*.5f,-78);}
            _mapPlayer=MapMarker(map.transform,"你",new Color(.94f,.96f,.94f));_mapTarget=MapMarker(map.transform,"★",new Color(1,.77f,.42f));map.SetActive(false);
            for(int i=0;i<9;i++)
            {
                var go=new GameObject("Notebook action "+i,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(root.transform,false);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
                rect.anchoredPosition=new Vector2(24+(i%3)*181,-258-(i/3)*128);rect.sizeDelta=new Vector2(170,114);
                _plates[i]=go.GetComponent<Image>();_plates[i].raycastTarget=false;
                _labels[i]=Label(go.transform,new Vector2(10,-10),new Vector2(150,94),21);
                var icon=new GameObject("Object photo",typeof(RectTransform),typeof(RawImage));icon.layer=5;icon.transform.SetParent(go.transform,false);
                var imageRect=icon.GetComponent<RectTransform>();imageRect.anchorMin=imageRect.anchorMax=imageRect.pivot=new Vector2(0,1);
                imageRect.anchoredPosition=new Vector2(7,-16);imageRect.sizeDelta=new Vector2(56,56);
                _icons[i]=icon.GetComponent<RawImage>();_icons[i].raycastTarget=false;icon.SetActive(false);
                var collider=go.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(170,114,3);collider.center=new Vector3(85,-57,0);
                var button=go.AddComponent<FishingNotebookButton>();button.Owner=this;button.Index=i;
            }
            BuildNotebookHome();
            FishingUITheme.Bind(_status,2);
            var mapShader=Resources.Load<Shader>("FishingTerrainMap");
            if(mapShader!=null && mapShader.isSupported)
            {
                _mapPalette=new Material(mapShader){name="Notebook daylight chart",renderQueue=3200};
                _mapPalette.SetFloat("_Circular",0);_mapPalette.SetFloat("_ZTest",4);_mapPalette.SetFloat("_HasMap",1);_mapPalette.SetVector("_ViewUV",new Vector4(0,0,1,1));
            }
            _ray=FishingArt.Line("Notebook controller ray",transform,c.lineMaterial,.003f);_ray.positionCount=2;Close();
            var toast=new GameObject("Coastal action reading card",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));toast.layer=5;toast.transform.SetParent(transform,false);
            _feedback=toast.GetComponent<Canvas>();_feedback.renderMode=RenderMode.WorldSpace;_feedback.worldCamera=c.viewCamera;
            toast.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=4;
            var toastRect=toast.GetComponent<RectTransform>();toastRect.sizeDelta=new Vector2(840,196);toastRect.localScale=Vector3.one*.00125f;
            var toastPlate=toast.AddComponent<Image>();toastPlate.color=Color.white;toastPlate.sprite=FishingUITheme.PanelVeilSprite;toastPlate.type=Image.Type.Sliced;toastPlate.material=FishingUITheme.WorldMaterial;toastPlate.raycastTarget=false;
            HomeImage(toast.transform,"Desktop action accent",new Rect(12,16,3,164),FishingUITheme.Sage);
            _feedbackTitle=Label(toast.transform,new Vector2(28,-12),new Vector2(784,64),42);_feedbackTitle.lineSpacing=1;_feedbackTitle.color=FishingHUD.CreamInk;
            _feedbackText=Label(toast.transform,new Vector2(28,-80),new Vector2(784,108),30);_feedbackText.color=FishingHUD.CreamInk;toast.SetActive(false);
            foreach(var text in new[]{_feedbackTitle,_feedbackText})
            {var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.015f,.035f,.045f,.95f);shadow.effectDistance=new Vector2(1,-1);}
        }
        private void LateUpdate()
        {
            if(IsOpen)_canvas.GetComponent<Image>().color=FishingPaperDesign.PaperTint;
            if(_mapPalette!=null){FishingUITheme.StyleMap(_mapPalette);_map.material=_mapPalette;}
            if(_c==null || _feedback==null)return;
            if(IsOpen && !_c.Automated && !_notebookHeld)FollowHead();
            if(IsOpen && !_c.Automated)_canvas.transform.localScale=Vector3.one*.00105f*Mathf.Lerp(.985f,1,Mathf.SmoothStep(0,1,(Time.unscaledTime-_pageOpenedAt)/.12f));
            bool visible=XRSettings.isDeviceActive && _c.XRSeat.Ready && Application.isFocused && !IsOpen
                && !_c.StartMenuOpen && !_c.Paused && !_c.StoryBlocksGameplay && !_c.DockConsole.Visible;
            _feedback.gameObject.SetActive(visible);if(!visible){_feedbackWasVisible=false;return;}
            var message=_c.SaveWarning.Length>0?_c.SaveWarning:_c.VisibleMessage;
            bool fighting=_c.Anchored && (_c.Session.Phase==CatchPhase.Bite || _c.Session.Phase==CatchPhase.Fighting || _c.Session.Phase==CatchPhase.Landing);
            if(string.IsNullOrEmpty(message) || (fighting && _c.SaveWarning.Length==0))message=FishingXRHints.Guidance(_c);
            else message+="\n"+FishingXRHints.NotebookHelp;
            var forward=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,Vector3.up);
            var gaze=Quaternion.LookRotation(forward.sqrMagnitude>.1f?forward:_c.XRSeat.Origin.transform.forward,Vector3.up);
            if(!_feedbackWasVisible){_feedbackYaw=gaze;_toastFollowing=false;}_feedbackWasVisible=true;
            if(message!=_feedbackMessage){_feedbackMessage=message;ApplyGuidanceText(message);}
            _feedbackTitle.color=_c.SaveWarning.Length>0?FishingUITheme.Coral:FishingHUD.CreamInk;
            // Lazy follow: the toast stays put until the gaze leaves it by 35°, then eases back in front.
            if(Quaternion.Angle(_feedbackYaw,gaze)>35)_toastFollowing=true;
            if(_toastFollowing){_feedbackYaw=Quaternion.Slerp(_feedbackYaw,gaze,1-Mathf.Exp(-3*Time.unscaledDeltaTime));if(Quaternion.Angle(_feedbackYaw,gaze)<3)_toastFollowing=false;}
            _feedback.transform.SetPositionAndRotation(_c.viewCamera.transform.position-Vector3.up*.34f
                +_feedbackYaw*Vector3.forward*ClearDistance(_c.viewCamera.transform.position,_feedbackYaw*Vector3.forward,1.4f),_feedbackYaw);
        }
        private void ApplyGuidanceText(string message)
        {
            var lines=message.Split(new[]{'\n'},2);_feedbackTitle.text=lines[0];_feedbackText.text=lines.Length>1?lines[1]:string.Empty;
            _feedbackTitle.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,768);_feedbackText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,768);
            float heading=Mathf.Max(32,_feedbackTitle.preferredHeight),body=Mathf.Max(24,_feedbackText.preferredHeight);
            HomeRect(_feedbackTitle.rectTransform,new Rect(36,22,768,heading));HomeRect(_feedbackText.rectTransform,new Rect(36,32+heading,768,body));
            float height=heading+body+54;((RectTransform)_feedback.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            HomeRect((RectTransform)_feedback.transform.Find("Desktop action accent"),new Rect(20,24,3,height-48));
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject("Notebook label",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=fontSize;text.fontStyle=FontStyle.Normal;text.lineSpacing=1.12f;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.color=FishingUITheme.Ink;text.material=FishingUITheme.WorldMaterial;text.raycastTarget=false;return text;
        }
        private static RectTransform MapMarker(Transform parent,string mark,Color color)
        {var text=Label(parent,Vector2.zero,new Vector2(36,36),18);text.text=mark;text.color=color;text.alignment=TextAnchor.MiddleCenter;var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(1,-1);return text.rectTransform;}
        public void BeginGuide(){Open(11);}
        private bool _following;
        private readonly Collider[] _pokeHits=new Collider[8];
        private int _pokedIndex=-1;private float _pokeReady;
        /// <summary>1.1 m ahead, pulled in (never under 0.8 m) when scenery is closer so the page never sinks into walls.</summary>
        private static Vector3 NotebookPose(Transform eye,Vector3 forward)
        {
            var dir=(forward-Vector3.up*.09f).normalized;float distance=1.1f; // 1.1 m keeps 18–22px text at ≥1° visual angle
            if(Physics.SphereCast(eye.position,.12f,dir,out var hit,1.25f,~0,QueryTriggerInteraction.Ignore))distance=Mathf.Clamp(hit.distance-.12f,.8f,1.1f);
            return eye.position+dir*distance;
        }
        /// <summary>Lazy follow: only once the page leaves a 40° cone, then ease back in front over ~0.4 s (no per-frame head lock).</summary>
        private void FollowHead()
        {
            var eye=_c.viewCamera.transform;var forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up);if(forward.sqrMagnitude<.1f)return;forward.Normalize();
            var toPage=Vector3.ProjectOnPlane(_canvas.transform.position-eye.position,Vector3.up);
            float angle=Vector3.Angle(forward,toPage),distance=toPage.magnitude;
            if(!_following && (angle>40 || distance>1.7f || distance<.6f))_following=true;
            if(!_following)return;
            var target=NotebookPose(eye,forward);var rotation=Quaternion.LookRotation(forward,Vector3.up);
            float k=1-Mathf.Exp(-Time.unscaledDeltaTime*7);
            _canvas.transform.SetPositionAndRotation(Vector3.Lerp(_canvas.transform.position,target,k),Quaternion.Slerp(_canvas.transform.rotation,rotation,k));
            if(Vector3.Distance(_canvas.transform.position,target)<.02f && angle<3)_following=false;
        }
        public void Open(int page=0)
        {
            if(!XRSettings.isDeviceActive)return;
            if(page!=SavePage)CloseSaveSlots();
            if(page!=8 && (!IsOpen || _page!=page))_c.WorldAudio?.Play("UIConfirm",_c.ExplorerPosition,.24f);
            if(!IsOpen)
            {
                var eye=_c.viewCamera.transform;var forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.1f)forward=_c.Shore.Actor.forward;
                _canvas.transform.SetPositionAndRotation(NotebookPose(eye,forward),Quaternion.LookRotation(forward,Vector3.up));_following=false;
                _c.Hands.ResetInteraction();_c.FieldWorld.ClearToolPresentation();_trigger.Reset();
            }
            IsOpen=true;_page=page;_offset=0;
            if(page==21)_ecologyMode=-1;
            if(page==11){_guideLesson=0;ResetGuidePractice();}
            _canvas.gameObject.SetActive(true);_c.WorkshopWorld.SelectNearby();Refresh();
        }
        public void Close(){CloseSaveSlots();bool wasOpen=IsOpen;IsOpen=false;_discardQuote="";_bagSelection=-1;if(_canvas!=null)_canvas.gameObject.SetActive(false);if(_ray!=null)_ray.enabled=false;ResetInput();if(wasOpen)_c?.Hands?.ResetInteraction();}
        public void ResetInput(){_menu.Reset();_primary.Reset();_board.Reset();_slot.Reset();_trigger.Reset();_turnReady=false;_tipSample=false;MotionReady=false;ResetPageGestures();_guideLeftTrigger.Reset();_guideLeftGrip.Reset();_guideRightGrip.Reset();_guideBoost.Reset();if(_homeDecor!=null)UpdateHomePointer(-1,false);}
        private bool ReArmMotion(Vector2 leftAxis,Vector2 rightAxis,bool boost)
        {
            if(!MotionReady && leftAxis.sqrMagnitude<.18f*.18f && rightAxis.sqrMagnitude<.18f*.18f && !boost)MotionReady=true;
            return MotionReady;
        }
        private void Set(int index,string label,Action action)
        {
            _icons[index].gameObject.SetActive(false);_labels[index].rectTransform.anchoredPosition=new Vector2(10,-10);_labels[index].rectTransform.sizeDelta=new Vector2(150,94);_labels[index].fontSize=21;
            label=FishingXRText.Adapt(label);_labels[index].text=label;_pageContent[index]=label;_actions[index]=action;
            if(_homeDecor==null)_plates[index].color=action==null?new Color(.09f,.13f,.14f):new Color(.12f,.27f,.25f);
        }
        private void Photo(int index,Texture2D texture)
        {
            if(texture==null)return;_icons[index].texture=texture;_icons[index].color=Color.white;_icons[index].gameObject.SetActive(true);
            _labels[index].rectTransform.anchoredPosition=new Vector2(67,-10);_labels[index].rectTransform.sizeDelta=new Vector2(98,94);_labels[index].fontSize=17;
        }
        private void Refresh()
        {
            _pageItemCount=-1;
            _status.fontSize=23;
            _map.gameObject.SetActive(_page==12 && _map.texture!=null);
            var day=_c.Career.Calendar;var bag=_c.Career.Backpack;
            _status.text=BayClimate.Name(_c.Weather.Climate.Current)+"\n金币 "+_c.Career.Coins+"\n行囊 "+bag.UsedSlots+" / 36\n装备："+FishingBackpack.Name(bag.Equipped)+"\n"+_c.VisibleMessage;
            for(int i=0;i<9;i++)Set(i,"—",null);
            if(_page==21)RefreshHomeEcology();
            else if(_page==0)
            {
                Set(0,"背包 / 装备",()=>Open(1));Set(1,"制作 / 熔炉 / 锻造",()=>Open(2));Set(2,"航海图 / 航点",()=>Open(12));
                Set(3,"日历 / 图鉴 / 观察",()=>Open(10));Set(4,"港口事务 / 委托",()=>Open(7));Set(5,"百货 / 装备升级",()=>Open(4));
                Set(6,"潮汐回响 / 群岛",()=>Open(9));Set(7,"设置 / 手柄教学",()=>Open(14));Set(8,"快速回家",()=>_c.ReturnHome());
            }
            else if(_page==1)
            {
                _status.text+="\n选中物品：装备 / 检视 / 移动 / 拆分 / 销毁";
                for(int i=0;i<6;i++)
                {
                    int slot=_offset+i;if(slot>=FishingBackpack.SlotCount)break;var item=bag.At(slot);
                    Set(i,(slot+1)+"  "+FishingBackpack.Name(item.item)+"\n×"+item.count,item.count==0?(Action)null:()=>SelectInventoryItem(slot));
                    Photo(i,FishingV023Art.Icon(item.item) ?? FishingItemIcons.Get(item.item));
                }
                Pager(FishingBackpack.SlotCount);
                Set(7,"家园生态 / 饲养",()=>{_ecologyMode=-1;Open(21);});
            }
            else if(_page==2)
            {
                _status.text="当前设施："+FishingRecipes.StationName(_c.WorkshopWorld.Context)+"\n左侧选择配方，右侧核对材料后制作；熔炉可选择批量与燃料。";
                Set(7,"返回行囊",()=>Open(1));
            }
            else if(_page==3)
            {
                _status.text=day.DateLabel+"   "+day.TimeLabel+" · "+day.TideName+"\n每季 28 天 · 每日 15 分钟\n今日："+BayClimate.Name(_c.Weather.Climate.Current)+
                    "\n明日："+BayClimate.Name(FishingCalendar.Forecast(day.Day+1))+"\n打开手册时日历和熔炉暂停"+TideStatus();
                Set(0,"休息至清晨",_c.CanRest?(Action)(()=>{Close();_c.RestUntilMorning();}):null);
                Set(1,"观察附近动物",()=>{Close();if(_c.Ashore)_c.FieldWorld.Observe();else _c.Ecology.Observe();});
                Set(2,"生物 / 相册图鉴\n实物照片 / 栖息条件",()=>Open(10));Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
                Set(6,"自然观察 / 海岸调查",()=>Open(15));
                Set(3,"推进至白昼\n08:00",_c.CanSetTime?(Action)(()=>_c.SetDayNight(false)):null);
                Set(4,"推进至夜晚\n21:00",_c.CanSetTime?(Action)(()=>_c.SetDayNight(true)):null);
                Set(5,day.Automatic?"自动流逝\n点击固定":"时间固定\n点击自动",_c.CanSetTime?(Action)_c.ToggleClock:null);
            }
            else if(_page==10)
            {
                RefreshCatalog();
            }
            else if(_page==8 && _c.DialogueOpen)
            {
                _status.text=FishingLore.ResidentName(_c.Residents.ActiveId)+" · "+_c.Residents.TopicTitle+"\n"+_c.Residents.VisibleSpeech;
                _status.fontSize=18;
                for(int i=0;i<_c.Residents.ChoiceCount;i++){int choice=i;Set(i,_c.Residents.Choice(i),()=>{_c.Residents.Choose(choice);Refresh();});}
                if(_c.Residents.ActiveId==4)Set(6,"查看商品",()=>_c.Residents.BrowseCashierGoods());
                Set(8,"结束交谈",()=>_c.Residents.Close());
            }
            else if(_page==9)
            {
                _status.fontSize=21;
                _status.text=_c.Career.PlayerTitle+" · 四境图页 "+_c.Career.Lore.ClearCount+" / 4\n"+_c.Career.MainObjective+"\n"+(_c.Realms.Inside?FishingLore.ShrineName(_c.Realms.Island):"守望者保存遗物、材料与永久设施蓝图");
                for(int i=0;i<4;i++){int island=i;Set(i,FishingAdventure.Name(i)+"\n"+(_c.Career.Lore.RewardClaimed(i)?"遗物与蓝图已领":_c.Career.Lore.Cleared(i)?"遗物与蓝图待领":"秘境尚待探索"),()=>{Close();_c.SelectNavigation(_c.IslandNavigationIndex(island));});}
                if(_c.Career.Backpack.Count(FieldItem.VoyageCompass)>0 && !_c.Career.PirateKing)Set(4,"白帆拱湾\n前往宝藏航点",()=>{Close();_c.SelectNavigation(_c.HomeNavigationIndex+1);});
                else Set(4,"群岛来信 / 航标",()=>Open(6));
                Set(5,"附近互动 / 补领遗物",()=>{Close();_c.RequestWorldInteraction();});
                Set(6,"信件 / 漂流瓶",()=>Open(LettersPage));Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
            }
            else if(_page==5)
            {
                _status.text="海湾百货 · "+FishingSupplies.CategoryName(_offset/FishingSupplies.CategorySize)+" · 金币 "+_c.Career.Coins+"\n港口或店内柜台可购买\n用品进入背包；船舶升级立即生效\n"+_c.Message;
                for(int i=0;i<6;i++)
                {
                    int product=_offset+i;if(product>=FishingSupplies.Items.Length)break;
                    Set(i,FishingBackpack.Name(FishingSupplies.Items[product])+" ×"+FishingSupplies.Quantity(product)+"\n"+FishingSupplies.Price(product)+" 金币",()=>{_c.BuySupply(product);Refresh();});
                    Photo(i,FishingV023Art.Icon(FishingSupplies.Items[product]));
                }
                Set(6,_offset+FishingSupplies.CategorySize<FishingSupplies.Items.Length?"下一类物资":"返回探索工具",()=>{_offset=(_offset+FishingSupplies.CategorySize)%FishingSupplies.Items.Length;Refresh();});Set(7,"船舶 / 钓具升级",()=>Open(4));Set(8,"关闭",Close);
            }
            else if(_page==6)
            {
                int island=_c.Islands.NearIsland(out int nearby)?nearby:0;
                _status.text="群岛来信 · "+FishingAdventure.Name(island)+"\n"+FishingAdventure.Clue(island)+"\n读信 → 地标寻宝 / 校准信标\n四灯已亮后返港提交 +150 金币\n"+_c.Message;
                for(int i=0;i<4;i++){int dest=i;Set(i,FishingAdventure.Name(i)+"\n"+(_c.Career.Adventure.Restored(i)?"信标已亮":"设为航点"),()=>{_c.SelectNavigation(_c.IslandNavigationIndex(dest));Refresh();});}
                Set(4,"附近互动\n读信 / 开箱 / 转动",()=>{Close();_c.RequestWorldInteraction();});
                Set(5,"确认灯头方向",()=>{Close();_c.Islands.ConfirmNearby();});
                Set(6,"返港提交群岛航记",()=>{Close();_c.Islands.CompleteAtHarbor();});Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
            }
            else if(_page==7)
            {
                _status.text="港口事务 · 鱼箱 "+_c.Career.Count+" / "+_c.Career.Capacity+"\n委托「"+_c.Career.CurrentOrder.Name+"」"+(_c.Career.OrderReady?" · 可交付":" · 待集齐")+"\n"+FishingSurvey.Goal(_c.Career)+"\n"+FishingSurvey.Checklist(_c.Career)+"\n"+_c.VisibleMessage;
                Set(0,"船上事务 / 起锚\n交易 / 存放 / 放生",!_c.Ashore?(Action)(()=>{Close();_c.DockConsole.Show();}):null);
                Set(1,"鱼箱 / 委托需求",()=>Open(13));Set(2,"领取炉内成品",()=>_c.WorkshopWorld.Collect());
                Set(3,"旧港信标任务",()=>{Close();_c.Expedition.PortAction();});
                Set(4,"切换鱼饵",()=>{Close();_c.CycleBait();});
                Set(5,"提交群岛航记",()=>{Close();_c.Islands.CompleteAtHarbor();});
                Set(6,"收回选中设施",()=>_c.WorkshopWorld.Remove());
                Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
            }
            else if(_page==11)RefreshGuide();
            else if(_page==12)
            {
                _status.fontSize=20;
                _status.text="航海图 · 北↑ · 你：当前位置 · ★：目标\n"+_c.NavigationName+" · "+FishingPrototype.FlatDistance(_c.ExplorerPosition,_c.NavigationPosition).ToString("0")+" 米 · 地形不代表水深 / 可通航";
                if(_map.texture==null)_status.text+="\n地形图未烘焙，下方仍可选择航点。";
                PlaceMapMarker(_mapPlayer,_c.ExplorerPosition);PlaceMapMarker(_mapTarget,_c.NavigationPosition);
                for(int i=0;i<6;i++)
                {
                    int target=_offset+i;if(target>=_c.NavigationCount)break;
                    string name=NavigationLabel(target);
                    Set(i,(_c.NavigationTarget==target?"★ ":"")+name,()=>{_c.SelectNavigation(target);Refresh();});
                }
                Pager(_c.NavigationCount);
            }
            else if(_page==13)
            {
                var order=_c.Career.CurrentOrder;
                _status.fontSize=20;_status.text="鱼箱 "+_c.Career.Count+" / "+_c.Career.Capacity+" · 货值 "+_c.Career.HoldValue+" 金币\n委托「"+order.Name+"」 · 奖金 "+order.Bonus+"\n";
                for(int i=0;i<order.LineCount;i++){var line=order.Line(i);_status.text+=FishingSpecies.Name(line.Kind)+" ×"+line.Count+(line.Rare?" 珍珠":"")+(line.MinGrams>0?" ≥"+FishingText.Weight(line.MinGrams):"")+"\n";}
                _status.text+="普通出售保留委托鱼货 · 港口事务页交易";
                for(int i=0;i<6;i++)
                {int index=_offset+i;if(index>=_c.Career.Count)break;var fish=_c.Career.Inventory[index];Set(i,FishingSpecies.Name(fish.kind)+(fish.rare?" · 珍珠":"")+"\n"+FishingText.Weight(fish.grams)+" · "+fish.value+" 金币",null);Photo(i,Resources.Load<Texture2D>("FishingFish/"+fish.kind+"Thumbnail"));}
                Pager(_c.Career.Count);
            }
            else if(_page==14)
            {
                _status.text="海湾设置 / 手柄教学\n"+(_c.Paused?"世界已暂停 · A 或下方按钮继续":"手册打开时停止操作和世界计时")+"\nVR 使用世界空间中文界面\n与电脑版共用背包、任务、图鉴和制作\n"+_c.VisibleMessage;
                Set(0,_c.Paused?"继续游戏":"暂停游戏",()=>{_c.TogglePause();Refresh();});
                Set(1,"VR 舒适\n转向 / 移动 / 视野收窄",()=>Open(22));
                Set(2,"快速回家\n传送回潮渔屋",()=>_c.ReturnHome());
                Set(3,"存档 / 读取 / 新航程",OpenSaveSlots);
                Set(4,"声音混音\nBGM / 音效 / 环境",()=>Open(20));
                Set(5,"完整手柄教学\n安全练习全部按键",BeginGuide);
                Set(6,"校准眼高",()=>{_c.XRSeat.Recenter();Close();});
                Set(7,"序章 / 环境特效 / 救援",()=>Open(16));Set(8,"返回首页",()=>Open());
            }
            else if(_page==15)
            {
                RefreshNature();
            }
            else if(_page==16)
            {
                _status.text="序章 / 航行帮助\n序章使用固定影院屏，保留自由转头\n救援保留金币、背包与鱼箱\nB 随时返回手册\n"+_c.VisibleMessage;
                Set(0,"重看剧情 · "+FishingPrologue.Name(_c.Prologue.Latest),_c.Prologue.CanBegin?(Action)(()=>{Close();_c.Prologue.Begin(_c.Prologue.Latest);}):null);
                Set(1,"操作说明\n各情境按键表",()=>Open(23));
                Set(2,"救援返回港口",()=>{Close();if(_c.Paused)_c.TogglePause();_c.ReturnToHarbor();});
                Set(3,"快速回家\n保留金币、背包与鱼获",()=>_c.ReturnHome());
                Set(4,"雨量特效\n"+(_c.Weather.ReducedEffects?"精简":"标准"),()=>{_c.Weather.ToggleReducedEffects();Refresh();});
                Set(5,"收起船上事务面板",()=>_c.DockConsole.Hide());
                Set(6,"图标署名与授权\nDelapouite / game-icons.net\nTwo Coins / Wooden Crate\nCC BY 3.0\ncreativecommons.org/licenses/by/3.0\n原图未修改，运行时依昼夜主题着色。",null);
                Set(7,"返回设置",()=>Open(14));Set(8,"返回海湾",Close);
            }
            else if(_page==17)RefreshBarbecuePage();
            else if(_page==18 || _page==19)RefreshHomeStorage();
            else if(_page==20)RefreshAudioPage();
            else if(_page==22)RefreshComfortPage();
            else if(_page==23)RefreshControlsPage();
            else if(_page==LettersPage)RefreshLettersPage();
            else if(_page==AlbumPage)RefreshAlbumPage();
            else if(_page==PhotoCodexPage)RefreshPhotoCodexPage();
            else if(_page==SavePage)RefreshSaveSlotsPage();
            else if(_page==ItemPage)RefreshInventoryItem();
            else if(_page==MovePage)RefreshInventoryMove();
            else if(_page==LegendPage)RefreshLegend();
            else if(_page==JournalPage)RefreshJournalIndex();
            else if(_page==LorePage)RefreshLore();
            else if(_page==SurveyPage)RefreshSurvey();
            else
            {
                _status.text="二级载重 / 动力 / 操控专精三选一\n其他两项可升至一级；旧升级保留\n金币 "+_c.Career.Coins;
                for(int i=0;i<6;i++){var track=(UpgradeTrack)i;bool locked=_c.Career.SpecializationBlocked(track);Set(i,FishingText.UpgradeName(track)+" Lv."+_c.Career.Level(track)+"\n"+(locked?"专精互斥":_c.Career.NextUpgradeCost(track)+" 金币"),locked?null:(Action)(()=>{_c.BuyUpgrade(track);Refresh();}));}
                Set(6,"购买探索用品",()=>Open(5));
                Set(7,_c.TackleShop.NearCounter?"店员介绍 / 故事":"返回首页",_c.TackleShop.NearCounter?(Action)(()=>_c.Residents.Talk(4)):(Action)(()=>Open()));Set(8,"关闭",Close);
            }
            PresentNotebookHome();PresentDesktopSurfaces();
            if(_presentedPage!=_page){_presentedPage=_page;_pageOpenedAt=Time.unscaledTime;}
        }
        private string NavigationLabel(int target)=>target==_c.HomeNavigationIndex?"回潮渔屋":target>_c.ShopNavigationIndex?FishingAdventure.Name(target-_c.ShopNavigationIndex-1):target==_c.ShopNavigationIndex?_c.ShopName:target==0?"潮汐渔港":target<=_c.fishingSpots.Length?FishingGrounds.Name(target-1):FishingExploration.SiteName(target-_c.fishingSpots.Length-1);
        private void PlaceMapMarker(RectTransform marker,Vector3 world)
        {
            bool valid=_mapDefinition!=null && _mapDefinition.IsUsable && FishingMapMath.TryWorldToUV(_mapDefinition.Bounds,world.x,world.z,out _,out _);
            if(!valid){marker.gameObject.SetActive(false);return;}
            FishingMapMath.TryWorldToUV(_mapDefinition.Bounds,world.x,world.z,out var u,out var v);
            marker.gameObject.SetActive(FishingMapMath.KnownUV(u,v));
            marker.anchoredPosition=new Vector2((float)u*_map.rectTransform.rect.width-marker.rect.width*.5f,-(1-(float)v)*_map.rectTransform.rect.height+marker.rect.height*.5f);
        }
        private static readonly string[][] GuideCards={
            new[]{"右 A · 互动\n靠近目标 / 对话\n持竿：抛竿 / 提竿","右 B · 手册\n打开 / 关闭\n也可收起事务面板","左 X · 上下船\n靠近岸边且低速\n岸上返回泊船","左 Y · 换装备\n依次切换 1–9 格\n背包可换入物品","左摇杆\n船上前进 / 倒车\n岸上各方向行走","右摇杆\n船上转向\n岸上默认平滑转身\n机关中转动转盘"},
            new[]{"右扳机\n射线点击界面\n岸上使用工具\n钓鱼：按住蓄力，松开抛竿","左扳机\n船上刹车\n岸上无额外功能","右握把\n靠近鱼竿握住\n松开后放下\n靠近鱼获抓取\n靠近腰间提灯拿起","左握把\n靠近轮柄握住\n转动手腕收线\n靠近鱼获抓取","按下左摇杆\n向前航行时加速\n留意过热冷却","按下右摇杆\n钓鱼：收竿起航\n岸上：跳跃 / 攀低台"},
            new[]{"钓鱼\n右扳机按住蓄力，松开抛竿\n咬钩按右扳机 / A 提竿\n按下右摇杆收竿起航","搏鱼\n按住右扳机或左握把转轮收线\n持竿手反向横移控鱼\n跃水松开扳机和左握把","抄网 / 鱼获\n左手抄网套住举起\n握把拿鱼慢放鱼箱\n事务面板存放 / 放生","岸上探索\nA 与居民 / 机关互动\nY 换镐、猎枪或灯\n右扳机使用工具","制作 / 锻造\n右扳机放置设施\n靠近按 A 打开\n手册制作并领取成品","随时求助\nB → 设置重看教学\n校准眼高 / 救援返港\n序章可随时重播"}};
        private void ResetGuidePractice(){_guidePracticed=0;_guideLeftReady=_guideRightReady=false;ResetInput();}
        private bool GuideReady=>_guideLesson==2 || _guidePracticed==31;
        private void AdvanceGuide()
        {
            if(!GuideReady)return;
            if(_guideLesson<2){_guideLesson++;ResetGuidePractice();Refresh();return;}
            if(!_c.HasIsolatedProfile){PlayerPrefs.SetInt(GuidePreference,1);PlayerPrefs.Save();}Close();
            _c.Notice("手柄教学完成。B 打开手册，地图选目的地；前方提示会持续说明下一步操作。",8);
        }
        private void RefreshGuide()
        {
            _status.fontSize=21;_status.text="手柄教学 · "+(_guideLesson+1)+" / 3\n右手 A/B · 左手 X/Y · 食指扳机，中指握把\n"+(_guideLesson<2?"安全练习五项操作，此时不操作游戏\n已完成 "+PracticeCount(_guidePracticed)+" / 5":"读完操作说明，返回海湾")+"\n"+(GuideReady?"按 A 进入下一页 / 完成，无需瞄准":"指向右侧卡片可看完整说明")+"\nB 关闭；设置页可重新练习";
            for(int i=0;i<6;i++){Set(i,GuideCards[_guideLesson][i],null);_labels[i].fontSize=18;}
            if(_guideLesson<2)
            {
                int[] cards=_guideLesson==0?new[]{0,2,3,4,5}:new[]{0,1,2,3,4};
                for(int bit=0;bit<cards.Length;bit++)if((_guidePracticed&(1<<bit))!=0)_plates[cards[bit]].color=new Color(.12f,.35f,.27f);
            }
            Set(6,"重新练习本页",()=>{ResetGuidePractice();Refresh();});
            Set(7,_guideLesson==2?"A 完成教学":GuideReady?"A 下一页":"先完成练习",GuideReady?(Action)AdvanceGuide:null);
            Set(8,"关闭教学\n以后可以重看",Close);
        }
        private static int PracticeCount(int bits){int count=0;for(int i=0;i<5;i++)if((bits&(1<<i))!=0)count++;return count;}
        private void PracticeGuide(InputDevice left,InputDevice right,Vector2 rightAxis,bool a,bool x,bool y,bool trigger)
        {
            if(_guideLesson==0)
            {
                if(a)_guidePracticed|=1;if(x)_guidePracticed|=2;if(y)_guidePracticed|=4;
                left.TryGetFeatureValue(CommonUsages.primary2DAxis,out var axis);
                if(axis.magnitude<.25f)_guideLeftReady=true;if(rightAxis.magnitude<.25f)_guideRightReady=true;
                if(_guideLeftReady && axis.magnitude>.5f)_guidePracticed|=8;if(_guideRightReady && rightAxis.magnitude>.5f)_guidePracticed|=16;
            }
            else if(_guideLesson==1)
            {
                bool lt=FishingXRInput.Trigger(left);bool lg=FishingXRInput.Grip(left);bool rg=FishingXRInput.Grip(right);left.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool boost);
                if(trigger)_guidePracticed|=1;if(_guideLeftTrigger.Step(true,lt))_guidePracticed|=2;if(_guideRightGrip.Step(true,rg))_guidePracticed|=4;if(_guideLeftGrip.Step(true,lg))_guidePracticed|=8;if(_guideBoost.Step(true,boost))_guidePracticed|=16;
            }
        }
        private void Pager(int count)
        {
            _pageItemCount=count;
            Set(6,"上一页",_offset>0?(Action)(()=>{_offset-=6;Refresh();}):null);Set(7,"返回首页",()=>Open());
            Set(8,"下一页",_offset+6<count?(Action)(()=>{_offset+=6;Refresh();}):null);
        }
        private void EquipSlot(int slot)
        {
            var bag=_c.Career.Backpack;
            if(slot>=FishingBackpack.HotbarCount)
            {
                // Swap through the existing transaction, including when every quick slot is occupied.
                if(!bag.Move(slot,bag.Selected))return;
                slot=bag.Selected;
            }

            _c.SelectFieldSlot(slot);Close();_c.InspectFieldItem(slot);
        }
        public bool Tick(bool allow)
        {
            // A closed XR notebook must not reset the desktop cast/hand interaction every frame.
            if(!XRSettings.isDeviceActive){if(IsOpen)Close();return false;}
            bool rightTracked=allow && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out _,out _);
            bool leftTracked=allow && FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out _,out _);
            if(!rightTracked && !leftTracked){ResetInput();_ray.enabled=false;if(!allow)Close();return true;}
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if(!rightTracked || !leftTracked){MotionReady=false;_turnReady=false;_tipSample=false;ResetPageGestures();}
            right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool menu);right.TryGetFeatureValue(CommonUsages.primaryButton,out bool primary);
            // Left menu key mirrors right B so the notebook is reachable with either hand.
            menu&=rightTracked;
            if(leftTracked)menu|=FishingXRInput.Menu(left);
            var pointer=rightTracked?right:left;
            if(_leftPointer!=!rightTracked){_leftPointer=!rightTracked;_trigger.Reset();_pokedIndex=-1;}
            bool trigger=FishingXRInput.Trigger(pointer);left.TryGetFeatureValue(CommonUsages.primaryButton,out bool board);left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool slot);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis,out var axis);
            bool action=_primary.Step(rightTracked,primary),boardEdge=_board.Step(leftTracked,board),slotEdge=_slot.Step(leftTracked,slot),click=_trigger.Step(true,trigger);
            if(_menu.Step(true,menu)){if(_c.Expedition.Aligning)_c.Expedition.Cancel();else if(IsOpen)Close();else if(_c.DockConsole.Visible)_c.DockConsole.Hide();else Open();return true;}
            if(IsOpen)
            {
                if(_page==8 && action){_c.Residents.Reveal();Refresh();return true;}
                if(_page==11){if(rightTracked && leftTracked)PracticeGuide(left,right,axis,action,boardEdge,slotEdge,click);if(action && GuideReady){AdvanceGuide();return true;}}
                FishingXRTracking.TryPose(_c.viewCamera,rightTracked?XRNode.RightHand:XRNode.LeftHand,out var origin,out var rotation,true);var direction=rotation*Vector3.forward;
                FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out var leftOrigin,out var leftRotation,true);
                bool leftGrip=FishingXRInput.Grip(left);bool rightGrip=FishingXRInput.Grip(right);
                if(rightTracked && leftTracked && TickPageGestures(leftGrip,rightGrip,new Pose(leftOrigin,leftRotation),new Pose(origin,rotation),Time.unscaledTime))
                {_ray.enabled=false;_pokedIndex=-1;UpdateHomePointer(-1,false);return true;}
                _ray.enabled=true;_ray.SetPosition(0,origin);_ray.SetPosition(1,origin+direction*2);
                if(Time.unscaledTime>=_nextText){Refresh();_nextText=Time.unscaledTime+.2f;}
                int homeHover=-1;
                // Direct poke: the right controller tip touching a button presses it once (no ray needed).
                var tip=origin+direction*.06f;int poked=-1;FishingNotebookButton pokedButton=null;
                int touching=Physics.OverlapSphereNonAlloc(tip,.02f,_pokeHits,1<<5,QueryTriggerInteraction.Collide);
                for(int i=0;i<touching;i++){var b=_pokeHits[i].GetComponent<FishingNotebookButton>();if(b!=null && b.Owner==this){poked=b.Index;pokedButton=b;break;}}
                if(poked>=0 && poked!=_pokedIndex && Time.unscaledTime>=_pokeReady)
                {
                    _pokeReady=Time.unscaledTime+.35f;pointer.SendHapticImpulse(0,.25f,.04f);
                    PressNotebookButton(pokedButton);
                }
                _pokedIndex=poked;if(!IsOpen)return true;
                if(Physics.Raycast(origin,direction,out var hit,3,1<<5,QueryTriggerInteraction.Collide))
                {
                    var button=hit.collider.GetComponent<FishingNotebookButton>();
                    if(button!=null && button.Owner==this && _c.Shore.ClearSight(origin,hit.point))
                    {
                        _ray.SetPosition(1,hit.point);
                        homeHover=button.Index;
                        if(click)PressNotebookButton(button);
                    }
                }
                UpdateHomePointer(homeHover,trigger);
                return true;
            }
            if(action && _c.HasPendingCatch){_c.DockConsole.ResolvePendingFromPrimary();return true;}
            // Menus and rescue stay reachable with one hand; gameplay still stops on tracking loss.
            if(!rightTracked || !leftTracked){_ray.enabled=false;return true;}
            left.TryGetFeatureValue(CommonUsages.primary2DAxis,out var leftAxis);
            bool boost=FishingXRInput.StickClick(left)||FishingXRInput.Grip(left);
            if(!ReArmMotion(leftAxis,axis,boost)){_c.Notice("请松开双摇杆和左摇杆按压，再继续移动或使用工具。",2);return true;}
            if(_c.Paused){if(action)_c.TogglePause();return true;}
            if(_c.Expedition.Aligning){_c.Expedition.Dial.Rotate(axis.x*Time.unscaledDeltaTime*45);if(action)_c.Expedition.Confirm();return true;}
            if(Mathf.Abs(axis.x)<.25f)_turnReady=true;
            if(_c.Ashore && FishingXRComfort.TurnMode==FishingXRComfort.Turn.Smooth){_c.XRSeat.SnapTurn(FishingXRComfort.SmoothTurnDegrees(axis.x,Time.deltaTime));}
            else if(_c.Ashore && _turnReady && Mathf.Abs(axis.x)>.7f){_c.XRSeat.SnapTurn(Mathf.Sign(axis.x)*FishingXRComfort.SnapDegrees);_turnReady=false;}
            if(boardEdge){if(_c.Ashore)_c.Shore.TryBoard();else _c.Shore.TryDisembark();return true;}
            if(slotEdge){_c.SelectFieldSlot((_c.Career.Backpack.Selected+1)%9);return true;}
            if(action && !_c.XRSeat.HeadBlocked)
            {
                if(!_c.Ashore && _c.Anchored)
                {if(_c.Hands.HoldRodForButtons()){if(_c.Session.Phase==CatchPhase.Bite)_c.Hook();else if(_c.CanChangeBait)_c.Cast();}}
                else if(_c.Hands.HoldingRod){if(_c.Session.Phase==CatchPhase.Bite)_c.Hook();else _c.Cast();}
                else InteractTracked();return true;
            }
            if(_c.Ashore && click && !_c.XRSeat.HeadBlocked){UseTrackedTool();return true;}
            return false;
        }
        private void UseTrackedTool()
        {
            if(_c.InspectingItem){_c.EndItemInspection();return;}
            if(_c.Islands.TryTarget(out int islandTarget,out _) && islandTarget%3==2){_c.Islands.ConfirmNearby();return;}
            var item=_c.Career.Backpack.Equipped;
            if(item==FieldItem.Rake)
            {
                var tool=_c.FieldWorld.HeldTool;
                if(tool==null || !_c.Farm.TryTarget(out int plot,out var soil) || plot>=12 || _c.Farm.Work(plot)!=FarmWork.Till
                    || Vector3.Distance(tool.TransformPoint(FishingFieldWorld.WorkToolTip(item)),soil.position+Vector3.up*.2f)>.3f)
                {_c.Notice("把耙齿放到未整地的菜畦上，再按右扳机。",4);return;}
            }
            if(FishingRecipes.IsStation(item)){_c.WorkshopWorld.PlaceEquipped();return;}
            if(FishingBackpack.IsGatherTool(item)){TryStrike(true);return;}
            _c.UseFieldItem();
        }
        private void PressNotebookButton(FishingNotebookButton button)
        {
            if(button.Command!=null)button.Command();
            else if(button.Index>=0 && button.Index<_actions.Length)
            {if(_page==0 && _actions[button.Index]!=null)_homeSelected=button.Index;_actions[button.Index]?.Invoke();}
            if(IsOpen)Refresh();
        }
        public void SampleTool()
        {
            if(!XRSettings.isDeviceActive || BlocksGameplay || _c.Paused || _c.StoryBlocksGameplay || _c.XRSeat.HeadBlocked || _c.FieldWorld.HeldTool==null){_tipSample=false;return;}
            var item=_c.FieldWorld.PresentedItem;
            if(!FishingBackpack.IsGatherTool(item)){_tipSample=false;return;}
            var tip=_c.XRSeat.Origin.transform.InverseTransformPoint(_c.FieldWorld.HeldTool.TransformPoint(FishingFieldWorld.WorkToolTip(item)));float dt=Time.deltaTime;
            if(_tipSample && dt>.001f && dt<.1f && (tip-_lastTip).magnitude<.4f && (tip-_lastTip).magnitude/dt>.85f)TryStrike();
            _lastTip=tip;_tipSample=true;
        }
        private bool TryStrike(bool manual=false)
        {
            if(Time.time<_nextStrike)return false;
            if(!_c.WorkshopWorld.TryHarvestTarget(out var node))
            {if(manual)_c.Notice("先靠近可采资源，再把镐尖贴到目标后按 A 或右扳机。",4);return false;}
            var tool=_c.FieldWorld.HeldTool;if(tool==null || !tool.gameObject.activeSelf)
            {if(manual)_c.Notice("请恢复手柄追踪并用 Y 选中石镐或铁镐，再靠近资源。",4);return false;}
            var tip=tool.TransformPoint(FishingFieldWorld.WorkToolTip(_c.Career.Backpack.Equipped));
            if(Vector3.Distance(tip,FishingContactGeometry.ClosestPoint(node.Root,tip))>.10f || !_c.Shore.ClearSight(tool.position,tip))
            {if(manual)_c.Notice("把镐尖贴近资源后按 A / 右扳机，或挥动采集；未接触不会消耗材料。",4);return false;}
            _nextStrike=Time.time+.65f;return _c.WorkshopWorld.HarvestNode(node.Id,node.Item,_c.Career.Backpack.Equipped);
        }
        private void InteractTracked()
        {
            if(!_c.Ashore){_c.Interact();return;}
            if(_c.Combat?.NearbyHint!=null || _c.Voyage?.NearbyHint!=null || _c.HomeEcology?.NearbyHint!=null || _c.Keeper?.NearbyHint!=null || _c.TidalFlat?.NearbyHint!=null){_c.RequestWorldInteraction();return;}
            if(_c.HomeInteractionTargetSelected){_c.RequestWorldInteraction();return;}
            if(_c.Realms.TryTarget(out _,out _)){_c.Realms.Interact();return;}
            if(_c.Residents.TryNearby(out int npc)){_c.Residents.Talk(npc);return;}
            if(_c.Shore.TryClimb())return;
            if(_c.TackleShop!=null && _c.TackleShop.NearCounter){Open(4);return;}
            if(_c.WorkshopWorld.TryInteractionTarget(out int station,out var resource) && station>=0){_c.WorkshopWorld.OpenStation(station);return;}
            if(_c.Islands.TryTarget(out int islandTarget,out _)){_c.Islands.InteractAt(islandTarget);return;}
            if(!_c.Hands.TryControllerPalm(false,out var palm,out _))return;
            if(_c.RifleHunting.TryLootTarget(out var carcass))
            {
                if(!CollectCarcass(palm))_c.Notice("请靠近猎物，将右手伸到猎物旁再按 A；背包满时猎物会保留。",5);
                return;
            }
            if(_c.FieldWorld.TryPickupTarget(out var pickup))
            {
                if(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(pickup.Root,palm))<.24f)_c.FieldWorld.PickupById(pickup.Id,pickup.Item,pickup.Count);
                else _c.Notice("把右手伸到物品旁，再按 A 或握把拾取；低处物品可蹲下触及。",5);
                return;
            }
            if(resource!=null)
            {
                if(FishingBackpack.IsGatherTool(_c.Career.Backpack.Equipped)){TryStrike(true);return;}
                if(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(resource.Root,palm))<.24f)_c.WorkshopWorld.HarvestNode(resource.Id,resource.Item,_c.Career.Backpack.Equipped);
                else _c.Notice("把右手伸到资源旁再按 A；需要工具的资源请用 Y 换上对应工具。",5);
                return;
            }
            int target=_c.Expedition.Resolve();var root=_c.Expedition.InteractionRoot(target);
            if(root!=null)
            {
                if(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(root,palm))<.24f)_c.Expedition.InteractAt(target);
                else _c.Notice("靠近目标，把右手伸到它旁边再按 A；B 打开手册可查看任务和救援。",5);
                return;
            }
            if(_c.Shore.NearbyTask>=0){_c.Shore.SurveyTask(_c.Shore.NearbyTask);return;}
            if(_c.Career.Backpack.Equipped==FieldItem.FieldGuide || _c.Career.Backpack.Equipped==FieldItem.Binoculars){_c.FieldWorld.Observe();return;}
            _c.Notice("右手接近目标后按 A；镐尖触碰矿石可挥动采集。B 打开手册，X 上下船，Y 换装备。",5);
        }
        private bool CollectCarcass(Vector3 palm)
        {
            if(_c.Paused || _c.StoryBlocksGameplay || BlocksGameplay || !_c.RifleHunting.TryLootTarget(out var carcass))return false;
            var contact=FishingContactGeometry.ClosestPoint(carcass.Root,palm);
            return Vector3.Distance(palm,contact)<.24f && _c.Shore.ClearSight(palm,contact) && _c.RifleHunting.CollectAnimal(carcass.Id);
        }
        private void OnApplicationFocus(bool focus){if(!focus)Close();}
        private void OnApplicationPause(bool pause){if(pause)Close();}
        private void OnDestroy(){if(_mapPalette!=null)Destroy(_mapPalette);}
    }
}
