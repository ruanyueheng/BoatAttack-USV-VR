using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public sealed class FishingNotebookButton : MonoBehaviour { public FishingXRNotebook Owner; public int Index; }
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
        private Text _feedbackText;
        private string _feedbackMessage;
        private Quaternion _feedbackYaw;
        private bool _guideLeftReady,_guideRightReady;
        private readonly FishingPressEdge _guideLeftTrigger=new FishingPressEdge(),_guideLeftGrip=new FishingPressEdge(),_guideRightGrip=new FishingPressEdge(),_guideBoost=new FishingPressEdge();
        public const string GuidePreference="FishingXRGuideV025";
        public static bool GuideCompleted=>PlayerPrefs.GetInt(GuidePreference,0)==1;
        public bool StorePage => IsOpen && (_page==4 || _page==5);
        private bool _turnReady;
        private float _nextText,_nextStrike;
        private Vector3 _lastTip;
        private bool _tipSample;
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
            FishingUITheme.Bind(panel,0);FishingUITheme.Bind(_status,2);
            var mapShader=Resources.Load<Shader>("FishingTerrainMap");
            if(mapShader!=null && mapShader.isSupported)
            {
                _mapPalette=new Material(mapShader){name="Notebook daylight chart",renderQueue=3200};
                _mapPalette.SetFloat("_Circular",0);_mapPalette.SetFloat("_ZTest",4);_mapPalette.SetFloat("_HasMap",1);_mapPalette.SetVector("_ViewUV",new Vector4(0,0,1,1));
            }
            _ray=FishingArt.Line("Notebook controller ray",transform,c.lineMaterial,.003f);_ray.positionCount=2;Close();
            var toast=new GameObject("Transient Chinese XR feedback",typeof(RectTransform),typeof(Canvas));toast.layer=5;toast.transform.SetParent(transform,false);
            _feedback=toast.GetComponent<Canvas>();_feedback.renderMode=RenderMode.WorldSpace;_feedback.worldCamera=c.viewCamera;
            var toastRect=toast.GetComponent<RectTransform>();toastRect.sizeDelta=new Vector2(560,144);toastRect.localScale=Vector3.one*.0016f;
            var toastPlate=toast.AddComponent<Image>();toastPlate.color=FishingUITheme.Paper;toastPlate.sprite=FishingUITheme.PaperSprite;toastPlate.type=Image.Type.Sliced;toastPlate.material=FishingUITheme.WorldMaterial;toastPlate.raycastTarget=false;
            FishingUITheme.Bind(toastPlate,0);
            _feedbackText=Label(toast.transform,new Vector2(18,-14),new Vector2(524,116),21);toast.SetActive(false);
            FishingUITheme.Bind(_feedbackText,2);
        }
        private void LateUpdate()
        {
            if(_mapPalette!=null){FishingUITheme.StyleMap(_mapPalette);_map.material=_mapPalette;}
            if(_c==null || _feedback==null)return;
            if(IsOpen && !_c.Automated)_canvas.transform.localScale=Vector3.one*.00105f*Mathf.Lerp(.985f,1,Mathf.SmoothStep(0,1,(Time.unscaledTime-_pageOpenedAt)/.12f));
            var message=_c.VisibleMessage;
            bool visible=XRSettings.isDeviceActive && _c.XRSeat.Ready && Application.isFocused && !IsOpen
                && !_c.Paused && !_c.StoryBlocksGameplay && !_c.DockConsole.Visible && !string.IsNullOrEmpty(message);
            _feedback.gameObject.SetActive(visible);if(!visible)return;
            if(message!=_feedbackMessage)
            {
                _feedbackMessage=message;
                var forward=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,Vector3.up);
                _feedbackYaw=Quaternion.LookRotation(forward.sqrMagnitude>.1f?forward:_c.XRSeat.Origin.transform.forward,Vector3.up);
                _feedbackText.text=message;
            }
            _feedback.transform.SetPositionAndRotation(_c.XRSeat.Origin.transform.position+Vector3.up*(FishingXRSeat.EyeHeight-.45f)
                +_feedbackYaw*Vector3.forward*1.8f,_feedbackYaw);
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject("Notebook label",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=fontSize;text.color=FishingUITheme.Ink;text.material=FishingUITheme.WorldMaterial;text.raycastTarget=false;return text;
        }
        private static RectTransform MapMarker(Transform parent,string mark,Color color)
        {var text=Label(parent,Vector2.zero,new Vector2(36,36),18);text.text=mark;text.color=color;text.alignment=TextAnchor.MiddleCenter;var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(1,-1);return text.rectTransform;}
        public void BeginGuide(){Open(11);}
        public void Open(int page=0)
        {
            if(!XRSettings.isDeviceActive)return;
            if(!IsOpen)
            {
                var eye=_c.viewCamera.transform;var forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.1f)forward=_c.Shore.Actor.forward;
                _canvas.transform.SetPositionAndRotation(eye.position+forward*1.3f-Vector3.up*.12f,Quaternion.LookRotation(forward,Vector3.up));
                _c.Hands.ResetInteraction();_c.FieldWorld.ClearToolPresentation();_trigger.Reset();
            }
            IsOpen=true;_page=page;_offset=0;
            if(page==11){_guideLesson=0;ResetGuidePractice();}
            _canvas.gameObject.SetActive(true);_c.WorkshopWorld.SelectNearby();Refresh();
        }
        public void Close(){IsOpen=false;if(_canvas!=null)_canvas.gameObject.SetActive(false);if(_ray!=null)_ray.enabled=false;ResetInput();_c?.Hands?.ResetInteraction();}
        public void ResetInput(){_menu.Reset();_primary.Reset();_board.Reset();_slot.Reset();_trigger.Reset();_turnReady=false;_tipSample=false;MotionReady=false;_guideLeftTrigger.Reset();_guideLeftGrip.Reset();_guideRightGrip.Reset();_guideBoost.Reset();if(_homeDecor!=null)UpdateHomePointer(-1,false);}
        private bool ReArmMotion(Vector2 leftAxis,Vector2 rightAxis,bool boost)
        {
            if(!MotionReady && leftAxis.sqrMagnitude<.18f*.18f && rightAxis.sqrMagnitude<.18f*.18f && !boost)MotionReady=true;
            return MotionReady;
        }
        private void Set(int index,string label,Action action)
        {
            _icons[index].gameObject.SetActive(false);_labels[index].rectTransform.anchoredPosition=new Vector2(10,-10);_labels[index].rectTransform.sizeDelta=new Vector2(150,94);_labels[index].fontSize=21;
            _labels[index].text=label;_pageContent[index]=label;_actions[index]=action;
            if(_homeDecor==null)_plates[index].color=action==null?new Color(.09f,.13f,.14f):new Color(.12f,.27f,.25f);
        }
        private void Photo(int index,Texture2D texture)
        {
            if(texture==null)return;_icons[index].texture=texture;_icons[index].gameObject.SetActive(true);
            _labels[index].rectTransform.anchoredPosition=new Vector2(67,-10);_labels[index].rectTransform.sizeDelta=new Vector2(98,94);_labels[index].fontSize=17;
        }
        private void Refresh()
        {
            _status.fontSize=23;
            _map.gameObject.SetActive(_page==12 && _map.texture!=null);
            var day=_c.Career.Calendar;var bag=_c.Career.Backpack;
            _status.text=BayClimate.Name(_c.Weather.Climate.Current)+"\n金币 "+_c.Career.Coins+"\n行囊 "+bag.UsedSlots+" / 36\n装备："+FishingBackpack.Name(bag.Equipped)+"\n"+_c.VisibleMessage;
            for(int i=0;i<9;i++)Set(i,"—",null);
            if(_page==0)
            {
                Set(0,"背包 / 装备",()=>Open(1));Set(1,"制作 / 熔炉 / 锻造",()=>Open(2));Set(2,"航海图 / 航点",()=>Open(12));
                Set(3,"日历 / 图鉴 / 观察",()=>Open(3));Set(4,"港口事务 / 委托",()=>Open(7));Set(5,"百货 / 装备升级",()=>Open(4));
                Set(6,"潮汐回响 / 群岛",()=>Open(9));Set(7,"设置 / 手柄教学",()=>Open(14));Set(8,"返回海湾",Close);
            }
            else if(_page==1)
            {
                _status.text+="\n背包格点选：换入当前快捷位";
                for(int i=0;i<6;i++)
                {
                    int slot=_offset+i;if(slot>=FishingBackpack.SlotCount)break;var item=bag.At(slot);
                    Set(i,(slot+1)+"  "+FishingBackpack.Name(item.item)+"\n×"+item.count,item.count==0?(Action)null:()=>EquipSlot(slot));
                    Photo(i,FishingV023Art.Icon(item.item) ?? FishingItemIcons.Get(item.item));
                }
                Pager(FishingBackpack.SlotCount);
            }
            else if(_page==2)
            {
                var context=_c.WorkshopWorld.Context;var recipes=FishingRecipes.ForStation(context,_craftBasics);
                _status.text="制作 · "+FishingRecipes.StationName(context)+"\n"+FishingRecipes.Purpose(context)+"\n"+(_craftBasics?"随身加工":"设施配方")+" · 点卡片制作 1 份\n燃料：煤、木炭、漂流木、木板\n港口事务页领取成品 / 收回设施\n关手册后炉火继续 · "+_c.VisibleMessage;
                for(int i=0;i<6;i++)
                {
                    int recipe=_offset+i;if(recipe>=recipes.Length)break;var r=recipes[recipe];string costs="";
                    foreach(var cost in r.Costs)costs+=FishingBackpack.Name(cost.item)+" "+bag.Count(cost.item)+"/"+cost.count+" ";
                    Set(i,FishingBackpack.Name(r.Output)+" ×"+r.Count+"\n"+costs,()=>Craft(r));
                    Photo(i,FishingV023Art.Icon(r.Output) ?? FishingItemIcons.Get(r.Output));
                }
                Pager(recipes.Length);
                Set(7,_craftBasics?"切回设施配方":"查看随身加工",()=>{_craftBasics=!_craftBasics;_offset=0;Refresh();});
            }
            else if(_page==3)
            {
                _status.text=day.DateLabel+"   "+day.TimeLabel+" · "+day.TideName+"\n每季 28 天 · 每日 15 分钟\n今日："+BayClimate.Name(_c.Weather.Climate.Current)+
                    "\n明日："+BayClimate.Name(FishingCalendar.Forecast(day.Day+1))+"\n夜间提灯照路 · 星灯鱼仅在夜间出现\n打开手册时日历和熔炉暂停";
                Set(0,"休息至清晨",_c.CanRest?(Action)(()=>{Close();_c.RestUntilMorning();}):null);
                Set(1,"观察附近动物",()=>{Close();if(_c.Ashore)_c.FieldWorld.Observe();else _c.Ecology.Observe();});
                Set(2,"图鉴："+FishingSpecies.Count+" 种生物\n实物照片 / 栖息条件",()=>Open(10));Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
                Set(6,"自然观察 / 海岸调查",()=>Open(15));
                Set(3,"推进至白昼\n08:00",_c.CanSetTime?(Action)(()=>_c.SetDayNight(false)):null);
                Set(4,"推进至夜晚\n21:00",_c.CanSetTime?(Action)(()=>_c.SetDayNight(true)):null);
                Set(5,day.Automatic?"自动流逝\n点击固定":"时间固定\n点击自动",_c.CanSetTime?(Action)_c.ToggleClock:null);
            }
            else if(_page==10)
            {
                _status.text="海湾生物图鉴 · "+FishingSpecies.Count+" 种\n点选实物照片查看栖息条件与纪录\n普通与珍珠发现、体重纪录继续保存";
                for(int i=0;i<6;i++)
                {
                    int kind=_offset+i;if(kind>=FishingSpecies.Count)break;var species=(FishKind)kind;
                    Set(i,FishingSpecies.Name(species)+"\n"+(_c.Career.Discovered(species)?"已记录":"未发现"),()=>
                    {_status.text=FishingSpecies.Name(species)+"\n"+FishingSpecies.Of(species).Habitat+"\n纪录："+FishingText.Weight(_c.Career.BestGrams(species));_nextText=Time.unscaledTime+6;});
                    Photo(i,Resources.Load<Texture2D>("FishingFish/"+species+"Thumbnail"));
                }
                Pager(FishingSpecies.Count);
            }
            else if(_page==8 && _c.DialogueOpen)
            {
                _c.Residents.Reveal();
                _status.text=FishingLore.ResidentName(_c.Residents.ActiveId)+" · "+_c.Residents.TopicTitle+"\n"+_c.Residents.Speech;
                _status.fontSize=18;
                for(int i=0;i<_c.Residents.ChoiceCount;i++){int choice=i;Set(i,_c.Residents.Choice(i),()=>{_c.Residents.Choose(choice);Refresh();});}
                if(_c.Residents.ActiveId==4)Set(6,"查看商品",()=>_c.Residents.BrowseCashierGoods());
                Set(8,"结束交谈",()=>_c.Residents.Close());
            }
            else if(_page==9)
            {
                _status.fontSize=21;
                _status.text="潮汐回响 · "+_c.Career.Lore.ClearCount+" / 4\n"+(_c.Realms.Inside?FishingLore.ShrineName(_c.Realms.Island)+"\n"+FishingLore.TrialHint(_c.Realms.Island):"向岛上居民询问故事与机关线索\n刻着潮纹的石门通向独立秘境")+"\n完成四处后向港口林舟汇报。";
                for(int i=0;i<4;i++){int island=i;Set(i,FishingAdventure.Name(i)+"\n"+(_c.Career.Lore.Cleared(i)?"回响已取回":"秘境尚待探索"),()=>{Close();_c.SelectNavigation(_c.IslandNavigationIndex(island));});}
                Set(4,"群岛来信 / 航标",()=>Open(6));
                Set(5,"附近互动",()=>{Close();if(_c.Realms.TryTarget(out _,out _))_c.Realms.Interact();else if(_c.Residents.TryNearby(out int npc))_c.Residents.Talk(npc);else _c.Shore.TryClimb();});
                Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
            }
            else if(_page==5)
            {
                _status.text="海湾百货 · 金币 "+_c.Career.Coins+"\n港口或店内柜台可购买\n用品进入背包；船舶升级立即生效\n"+_c.Message;
                for(int i=0;i<6;i++)
                {
                    int product=_offset+i;if(product>=FishingSupplies.Items.Length)break;
                    Set(i,FishingBackpack.Name(FishingSupplies.Items[product])+" ×"+FishingSupplies.Quantity(product)+"\n"+FishingSupplies.Price(product)+" 金币",()=>{_c.BuySupply(product);Refresh();});
                    Photo(i,FishingV023Art.Icon(FishingSupplies.Items[product]));
                }
                Set(6,_offset==0?"下一页":"上一页",()=>{_offset=_offset==0?6:0;Refresh();});Set(7,"船舶 / 钓具升级",()=>Open(4));Set(8,"关闭",Close);
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
                Set(0,"交易 / 存放 / 放生\nB 可收起面板",!_c.Ashore?(Action)(()=>{Close();_c.DockConsole.Show();}):null);
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
                Set(1,"舒适航行\n"+(_c.ComfortMode?"开启":"标准动力"),()=>{_c.ToggleComfort();Refresh();});
                Set(2,"天气："+BayClimate.Name(_c.Weather.Climate.Current)+"\n点击切换",()=>{_c.Weather.CyclePreview();Refresh();});
                Set(3,_c.Weather.Climate.Automatic?"天气循环：自动\n点击固定":"天气循环：固定\n点击自动",()=>{if(_c.Weather.Climate.Automatic)_c.Weather.Climate.Hold();else _c.Weather.Climate.ResumeCycle();Refresh();});
                Set(4,"环境音 / 音乐\n"+(_c.QuietMode?"静音":"开启"),()=>{_c.ToggleQuiet();Refresh();});
                Set(5,"完整手柄教学\n安全练习全部按键",BeginGuide);
                Set(6,"校准眼高",()=>{_c.XRSeat.Recenter();Close();});
                Set(7,"序章 / 环境特效 / 救援",()=>Open(16));Set(8,"返回首页",()=>Open());
            }
            else if(_page==15)
            {
                _status.fontSize=20;_status.text="自然观察 / 海岸调查\n自然手册或望远镜：看向动物按 A\n海洋生物保持距离，陆地动物缓慢接近\n各区域两项调查回港提交 +25 金币\n"+_c.VisibleMessage;
                for(int i=0;i<6;i++)
                {
                    int index=_offset+i;
                    if(index<5){var kind=(MarineKind)index;Set(i,BayClimate.MarineName(kind)+"\n"+(_c.Career.Seen(kind)?"已记录":"未记录"),()=>{_status.text=BayClimate.MarineName(kind)+"\n"+BayClimate.MarineHint(kind);_nextText=Time.unscaledTime+5;});}
                    else if(index<10){var kind=(LandAnimalKind)(index-5);Set(i,FishingBackpack.AnimalName(kind)+"\n"+(bag.Seen(kind)?"已记录":"未记录"),null);}
                    else if(index<14){int site=index-10;Set(i,FishingExploration.SiteName(site)+"\n"+(FishingExploration.SiteDone(_c.Career.ShoreTaskMask,site)?"调查完成":"待调查"),()=>{_c.SelectNavigation(_c.fishingSpots.Length+1+site);Refresh();});}
                }
                Pager(14);
            }
            else if(_page==16)
            {
                _status.text="序章 / 航行帮助\n序章使用固定影院屏，保留自由转头\n救援保留金币、背包与鱼箱\nB 随时返回手册\n"+_c.VisibleMessage;
                Set(0,"重看序章 CG",_c.Prologue.CanBegin?(Action)(()=>{Close();_c.Prologue.Begin();}):null);
                Set(1,"完整手柄教学",BeginGuide);
                Set(2,"救援返回港口",()=>{Close();if(_c.Paused)_c.TogglePause();_c.ReturnToHarbor();});
                Set(3,"导航回潮渔屋",()=>{_c.SelectNavigation(_c.HomeNavigationIndex);Close();});
                Set(4,"雨量特效\n"+(_c.Weather.ReducedEffects?"精简":"标准"),()=>{_c.Weather.ToggleReducedEffects();Refresh();});
                Set(5,"收起船上事务面板",()=>_c.DockConsole.Hide());
                Set(6,"图标署名与授权\nDelapouite / game-icons.net\nTwo Coins / Wooden Crate\nCC BY 3.0\ncreativecommons.org/licenses/by/3.0\n原图未修改，运行时依昼夜主题着色。",null);
                Set(7,"返回设置",()=>Open(14));Set(8,"返回海湾",Close);
            }
            else if(_page==17)RefreshBarbecuePage();
            else
            {
                _status.text="二级载重 / 动力 / 操控专精三选一\n其他两项可升至一级；旧升级保留\n金币 "+_c.Career.Coins;
                for(int i=0;i<6;i++){var track=(UpgradeTrack)i;bool locked=_c.Career.SpecializationBlocked(track);Set(i,FishingText.UpgradeName(track)+" Lv."+_c.Career.Level(track)+"\n"+(locked?"专精互斥":_c.Career.NextUpgradeCost(track)+" 金币"),locked?null:(Action)(()=>{_c.BuyUpgrade(track);Refresh();}));}
                Set(6,"购买探索用品",()=>Open(5));
                Set(7,_c.TackleShop.NearCounter?"店员介绍 / 故事":"返回首页",_c.TackleShop.NearCounter?(Action)(()=>_c.Residents.Talk(4)):(Action)(()=>Open()));Set(8,"关闭",Close);
            }
            PresentNotebookHome();
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
            new[]{"右 A · 互动\n靠近目标 / 对话\n持竿：抛竿 / 提竿","右 B · 手册\n打开 / 关闭\n也可收起事务面板","左 X · 上下船\n靠近岸边且低速\n岸上返回泊船","左 Y · 换装备\n依次切换 1–9 格\n背包可换入物品","左摇杆\n船上前进 / 倒车\n岸上各方向行走","右摇杆\n船上转向\n岸上 30° 转身\n机关中转动转盘"},
            new[]{"右扳机\n射线点击界面\n岸上使用工具\n猎枪：单次开火","左扳机\n船上刹车\n岸上无额外功能","右握把\n靠近鱼竿握住\n松开后放下\n靠近鱼获抓取\n靠近腰间提灯拿起","左握把\n靠近轮柄握住\n转动手腕收线\n靠近鱼获抓取","按下左摇杆\n向前航行时加速\n留意过热冷却","按下右摇杆\n当前未绑定\n系统 / 菜单键\n由 PICO 系统使用"},
            new[]{"钓鱼\n驻钓后右握把拿竿\n甩动并松右扳机\nA 也可快捷抛竿","搏鱼\n咬钩时扳机 / A\n左握把转轮收线\n跃水松开左握把","抄网 / 鱼获\n左手抄网套住举起\n握把拿鱼慢放鱼箱\n事务面板存放 / 放生","岸上探索\nA 与居民 / 机关互动\nY 换镐、猎枪或灯\n右扳机使用工具","制作 / 锻造\n右扳机放置设施\n靠近按 A 打开\n手册制作并领取成品","随时求助\nB → 设置重看教学\n校准眼高 / 救援返港\n序章可随时重播"}};
        private void ResetGuidePractice(){_guidePracticed=0;_guideLeftReady=_guideRightReady=false;ResetInput();}
        private void RefreshGuide()
        {
            _status.fontSize=21;_status.text="手柄教学 · "+(_guideLesson+1)+" / 3\n右手 A/B · 左手 X/Y · 扳机在食指处\n握把在手柄侧面，由中指按住\n"+(_guideLesson<2?"安全练习：按下 / 推动下方五项操作\n已完成 "+PracticeCount(_guidePracticed)+" / 5 · 此时不操作游戏":"操作说明已读完，完成后返回海湾")+"\nB 可关闭，设置页可重新开始";
            for(int i=0;i<6;i++){Set(i,GuideCards[_guideLesson][i],null);_labels[i].fontSize=17;}
            if(_guideLesson<2)
            {
                int[] cards=_guideLesson==0?new[]{0,2,3,4,5}:new[]{0,1,2,3,4};
                for(int bit=0;bit<cards.Length;bit++)if((_guidePracticed&(1<<bit))!=0)_plates[cards[bit]].color=new Color(.12f,.35f,.27f);
            }
            Set(6,"重新练习本页",()=>{ResetGuidePractice();Refresh();});
            Set(7,_guideLesson==2?"完成教学":"练习完成后下一页",_guideLesson==2 || _guidePracticed==31?(Action)(()=>{if(_guideLesson<2){_guideLesson++;ResetGuidePractice();Refresh();}else{PlayerPrefs.SetInt(GuidePreference,1);PlayerPrefs.Save();Close();_c.Notice("手柄教学完成。B 可随时打开潮汐手册，查看地图、背包、任务和设置。",8);}}):null);
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
                left.TryGetFeatureValue(CommonUsages.triggerButton,out bool lt);left.TryGetFeatureValue(CommonUsages.gripButton,out bool lg);right.TryGetFeatureValue(CommonUsages.gripButton,out bool rg);left.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool boost);
                if(trigger)_guidePracticed|=1;if(_guideLeftTrigger.Step(true,lt))_guidePracticed|=2;if(_guideRightGrip.Step(true,rg))_guidePracticed|=4;if(_guideLeftGrip.Step(true,lg))_guidePracticed|=8;if(_guideBoost.Step(true,boost))_guidePracticed|=16;
            }
        }
        private void Pager(int count)
        {
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

            _c.SelectFieldSlot(slot);Close();
        }
        private void Craft(CraftRecipe recipe)
        {
            if(recipe.Station!=CraftStation.Furnace){_c.WorkshopWorld.Craft(recipe.Id);return;}
            var bag=_c.Career.Backpack;
            foreach(var fuel in new[]{FieldItem.Coal,FieldItem.Charcoal,FieldItem.Driftwood,FieldItem.Plank})
                if(bag.Count(fuel)>0){_c.WorkshopWorld.Smelt(recipe.Id,1,fuel);return;}
            _c.Notice("熔炉需要煤、木炭或木材燃料。",4);
        }
        public bool Tick(bool allow)
        {
            if(!XRSettings.isDeviceActive){Close();return false;}
            bool tracked=allow && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out _,out _) && FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out _,out _);
            if(!tracked){ResetInput();_ray.enabled=false;if(!allow)Close();return true;}
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool menu);right.TryGetFeatureValue(CommonUsages.primaryButton,out bool primary);
            right.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);left.TryGetFeatureValue(CommonUsages.primaryButton,out bool board);left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool slot);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis,out var axis);
            bool action=_primary.Step(true,primary),boardEdge=_board.Step(true,board),slotEdge=_slot.Step(true,slot),click=_trigger.Step(true,trigger);
            if(_menu.Step(true,menu)){if(_c.Expedition.Aligning)_c.Expedition.Cancel();else if(IsOpen)Close();else if(_c.DockConsole.Visible)_c.DockConsole.Hide();else Open();return true;}
            if(IsOpen)
            {
                if(_page==11)PracticeGuide(left,right,axis,action,boardEdge,slotEdge,click);
                FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out var origin,out var rotation,true);var direction=rotation*Vector3.forward;
                _ray.enabled=true;_ray.SetPosition(0,origin);_ray.SetPosition(1,origin+direction*2);
                if(Time.unscaledTime>=_nextText){Refresh();_nextText=Time.unscaledTime+.2f;}
                int homeHover=-1;
                if(Physics.Raycast(origin,direction,out var hit,3,1<<5,QueryTriggerInteraction.Collide))
                {
                    var button=hit.collider.GetComponent<FishingNotebookButton>();
                    if(button!=null && button.Owner==this && _c.Shore.ClearSight(origin,hit.point))
                    {
                        _ray.SetPosition(1,hit.point);
                        homeHover=button.Index;
                        if(click){if(_page==0 && _actions[button.Index]!=null)_homeSelected=button.Index;_actions[button.Index]?.Invoke();if(IsOpen)Refresh();}
                    }
                }
                UpdateHomePointer(homeHover,trigger);
                return true;
            }
            left.TryGetFeatureValue(CommonUsages.primary2DAxis,out var leftAxis);
            left.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool boost);
            if(!ReArmMotion(leftAxis,axis,boost)){_c.Notice("请松开双摇杆和左摇杆按压，再继续移动或使用工具。",2);return true;}
            if(_c.Paused){if(action)_c.TogglePause();return true;}
            if(_c.Expedition.Aligning){_c.Expedition.Dial.Rotate(axis.x*Time.unscaledDeltaTime*45);if(action)_c.Expedition.Confirm();return true;}
            if(Mathf.Abs(axis.x)<.25f)_turnReady=true;
            if(_c.Ashore && _turnReady && Mathf.Abs(axis.x)>.7f){_c.XRSeat.SnapTurn(Mathf.Sign(axis.x)*30);_turnReady=false;}
            if(boardEdge){if(_c.Ashore)_c.Shore.TryBoard();else _c.Shore.TryDisembark();return true;}
            if(slotEdge){_c.SelectFieldSlot((_c.Career.Backpack.Selected+1)%9);return true;}
            if(action && !_c.XRSeat.HeadBlocked)
            {
                if(_c.Hands.HoldingRod){if(_c.Session.Phase==CatchPhase.Bite)_c.Hook();else _c.Cast();}
                else InteractTracked();return true;
            }
            if(_c.Ashore && click && !_c.XRSeat.HeadBlocked){UseTrackedTool();return true;}
            return false;
        }
        private void UseTrackedTool()
        {
            var item=_c.Career.Backpack.Equipped;
            if(FishingRecipes.IsStation(item)){_c.WorkshopWorld.PlaceEquipped();return;}
            if(item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe){TryStrike(true);return;}
            _c.UseFieldItem();
        }
        public void SampleTool()
        {
            if(!XRSettings.isDeviceActive || BlocksGameplay || _c.Paused || _c.StoryBlocksGameplay || _c.XRSeat.HeadBlocked || _c.FieldWorld.HeldTool==null){_tipSample=false;return;}
            var item=_c.FieldWorld.PresentedItem;
            if(item!=FieldItem.StonePickaxe && item!=FieldItem.IronPickaxe){_tipSample=false;return;}
            var tip=_c.XRSeat.Origin.transform.InverseTransformPoint(_c.FieldWorld.HeldTool.TransformPoint(FishingFieldWorld.PickTip));float dt=Time.deltaTime;
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
            var tip=tool.TransformPoint(FishingFieldWorld.PickTip);
            if(Vector3.Distance(tip,FishingContactGeometry.ClosestPoint(node.Root,tip))>.10f || !_c.Shore.ClearSight(tool.position,tip))
            {if(manual)_c.Notice("把镐尖贴近资源后按 A / 右扳机，或挥动采集；未接触不会消耗材料。",4);return false;}
            _nextStrike=Time.time+.65f;return _c.WorkshopWorld.HarvestNode(node.Id,node.Item,_c.Career.Backpack.Equipped);
        }
        private void InteractTracked()
        {
            if(!_c.Ashore){_c.Interact();return;}
            if(_c.HomeInteractionTargetSelected){_c.RequestWorldInteraction();return;}
            if(_c.Realms.TryTarget(out _,out _)){_c.Realms.Interact();return;}
            if(_c.Residents.TryNearby(out int npc)){_c.Residents.Talk(npc);return;}
            if(_c.Shore.TryClimb())return;
            if(_c.TackleShop!=null && _c.TackleShop.NearCounter){Open(4);return;}
            if(_c.WorkshopWorld.TryInteractionTarget(out int station,out var resource) && station>=0){_c.WorkshopWorld.OpenStation(station);return;}
            if(!_c.Hands.TryControllerPalm(false,out var palm,out _))return;
            if(_c.RifleHunting.TryLootTarget(out var carcass))
            {
                if(!CollectCarcass(palm))_c.Notice("请靠近猎物，将右手伸到猎物旁再按 A；背包满时猎物会保留。",5);
                return;
            }
            if(_c.FieldWorld.TryPickupTarget(out var pickup) && Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(pickup.Root,palm))<.24f)
            {_c.FieldWorld.PickupById(pickup.Id,pickup.Item,pickup.Count);return;}
            if(resource!=null)
            {
                if(_c.Career.Backpack.Equipped==FieldItem.StonePickaxe || _c.Career.Backpack.Equipped==FieldItem.IronPickaxe){TryStrike(true);return;}
                if(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(resource.Root,palm))<.24f)_c.WorkshopWorld.HarvestNode(resource.Id,resource.Item,_c.Career.Backpack.Equipped);return;
            }
            int target=_c.Expedition.Resolve();var root=_c.Expedition.InteractionRoot(target);
            if(root!=null && Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(root,palm))<.24f){_c.Expedition.InteractAt(target);return;}
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
