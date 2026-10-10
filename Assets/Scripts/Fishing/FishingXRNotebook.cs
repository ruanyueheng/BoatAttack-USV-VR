using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public sealed class FishingNotebookButton : MonoBehaviour { public FishingXRNotebook Owner; public int Index; }
    /// <summary>World-locked, ray-selectable notebook; all mutations use the existing inventory/workshop transactions.</summary>
    public sealed class FishingXRNotebook : MonoBehaviour
    {
        private FishingPrototype _c;
        private Canvas _canvas;
        private Text _status;
        private readonly Text[] _labels=new Text[9];
        private readonly Image[] _plates=new Image[9];
        private readonly Action[] _actions=new Action[9];
        private readonly FishingPressEdge _menu=new FishingPressEdge(),_primary=new FishingPressEdge(),_board=new FishingPressEdge(),_slot=new FishingPressEdge(),_trigger=new FishingPressEdge();
        private LineRenderer _ray;
        private int _page,_offset;
        private bool _turnReady;
        private float _nextText,_nextStrike;
        private Vector3 _lastTip;
        private bool _tipSample;
        public bool IsOpen { get; private set; }
        public void Build(FishingPrototype c)
        {
            _c=c;
            var root=new GameObject("Coastal XR notebook",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);
            _canvas=root.GetComponent<Canvas>();_canvas.renderMode=RenderMode.WorldSpace;_canvas.worldCamera=c.viewCamera;
            var rt=root.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(580,660);rt.localScale=Vector3.one*.0016f;
            var panel=root.AddComponent<Image>();panel.color=new Color(.98f,.97f,.93f);panel.raycastTarget=false;
            _status=Label(root.transform,new Vector2(24,-20),new Vector2(532,224),23);
            for(int i=0;i<9;i++)
            {
                var go=new GameObject("Notebook action "+i,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(root.transform,false);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
                rect.anchoredPosition=new Vector2(24+(i%3)*181,-258-(i/3)*128);rect.sizeDelta=new Vector2(170,114);
                _plates[i]=go.GetComponent<Image>();_plates[i].raycastTarget=false;
                _labels[i]=Label(go.transform,new Vector2(10,-10),new Vector2(150,94),21);
                var collider=go.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(170,114,3);collider.center=new Vector3(85,-57,0);
                var button=go.AddComponent<FishingNotebookButton>();button.Owner=this;button.Index=i;
            }
            _ray=FishingArt.Line("Notebook controller ray",transform,c.lineMaterial,.003f);_ray.positionCount=2;Close();
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject("Notebook label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=fontSize;text.color=new Color(.08f,.20f,.21f);text.raycastTarget=false;return text;
        }
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
            IsOpen=true;_page=page;_offset=0;_canvas.gameObject.SetActive(true);_c.WorkshopWorld.SelectNearby();Refresh();
        }
        public void Close(){IsOpen=false;if(_canvas!=null)_canvas.gameObject.SetActive(false);if(_ray!=null)_ray.enabled=false;_trigger.Reset();_tipSample=false;}
        private void Set(int index,string label,Action action){_labels[index].text=label;_actions[index]=action;_plates[index].color=action==null?new Color(.91f,.91f,.88f):new Color(.84f,.91f,.85f);}
        private void Refresh()
        {
            var day=_c.Career.Calendar;var bag=_c.Career.Backpack;
            _status.text="潮汐手册  ·  "+day.DateLabel+"  "+day.TimeLabel+"\n"+BayClimate.Name(_c.Weather.Climate.Current)+"   金币 "+_c.Career.Coins+
                "\n装备："+FishingBackpack.Name(bag.Equipped)+"\nB 关闭 · 右手射线 + 扳机选择\n"+_c.Message;
            for(int i=0;i<9;i++)Set(i,"—",null);
            if(_page==0)
            {
                Set(0,"背包 / 装备",()=>Open(1));Set(1,"制作 / 熔炉",()=>Open(2));Set(2,"日历 / 预报",()=>Open(3));
                Set(3,_c.Ashore?"返回船上":"靠岸下船",()=>{Close();if(_c.Ashore)_c.Shore.TryBoard();else _c.Shore.TryDisembark();});
                Set(4,"航海 / 委托 / 升级",()=>Open(4));Set(5,"领取炉内成品",()=>_c.WorkshopWorld.Collect());
                Set(6,"校准眼高",()=>_c.XRSeat.Recenter());Set(7,"切换鱼饵",()=>{Close();_c.CycleBait();});Set(8,"返回海湾",Close);
            }
            else if(_page==1)
            {
                _status.text+="\n背包格点选：换入当前快捷位";
                for(int i=0;i<6;i++)
                {
                    int slot=_offset+i;if(slot>=FishingBackpack.SlotCount)break;var item=bag.At(slot);
                    Set(i,(slot+1)+"  "+FishingBackpack.Name(item.item)+"\n×"+item.count,item.count==0?(Action)null:()=>EquipSlot(slot));
                }
                Pager(FishingBackpack.SlotCount);
            }
            else if(_page==2)
            {
                _status.text="制作 · "+FishingRecipes.StationName(_c.WorkshopWorld.Context)+"\n点选制作 1 份 / 熔炉开始烤制\n燃料优先煤、木炭、漂流木、木板\n成品在首页领取；关手册后炉火继续\n"+_c.Message;
                for(int i=0;i<6;i++)
                {
                    int recipe=_offset+i;if(recipe>=FishingRecipes.All.Length)break;var r=FishingRecipes.All[recipe];string costs="";
                    foreach(var cost in r.Costs)costs+=FishingBackpack.Name(cost.item)+" "+bag.Count(cost.item)+"/"+cost.count+" ";
                    Set(i,FishingBackpack.Name(r.Output)+" ×"+r.Count+"\n"+costs,()=>Craft(r));
                }
                Pager(FishingRecipes.All.Length);
            }
            else if(_page==3)
            {
                _status.text=day.DateLabel+"   "+day.TimeLabel+"\n每季 28 天 · 每日 20 分钟\n今日："+BayClimate.Name(_c.Weather.Climate.Current)+
                    "\n明日："+BayClimate.Name(FishingCalendar.Forecast(day.Day+1))+"\n夜间提灯照路 · 星灯鱼仅在夜间出现\n打开手册时日历和熔炉暂停";
                Set(0,"休息至清晨",_c.CanRest?(Action)(()=>{Close();_c.RestUntilMorning();}):null);
                Set(1,"观察附近动物",()=>{Close();if(_c.Ashore)_c.FieldWorld.Observe();else _c.Ecology.Observe();});
                Set(2,"图鉴：12 种鱼\n按季节、海域、昼夜寻找",null);Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
            }
            else
            {
                Set(0,"接取 / 提交信标任务",()=>{Close();_c.Expedition.PortAction();});
                Set(1,"升级鱼线",()=>{Close();_c.BuyUpgrade(UpgradeTrack.Line);});
                Set(2,"升级卷线器",()=>{Close();_c.BuyUpgrade(UpgradeTrack.Reel);});
                Set(3,"升级鱼箱",()=>{Close();_c.BuyUpgrade(UpgradeTrack.Hold);});
                Set(6,"下一航点\n"+_c.NavigationName,()=>{_c.CycleNavigation();Refresh();});
                Set(4,"交付海岸调查",()=>{Close();_c.ClaimShoreRewards();});
                Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
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
            if(!tracked){_menu.Reset();_primary.Reset();_board.Reset();_slot.Reset();_trigger.Reset();_turnReady=false;Close();return true;}
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool menu);right.TryGetFeatureValue(CommonUsages.primaryButton,out bool primary);
            right.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);left.TryGetFeatureValue(CommonUsages.primaryButton,out bool board);left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool slot);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis,out var axis);
            bool action=_primary.Step(true,primary),boardEdge=_board.Step(true,board),slotEdge=_slot.Step(true,slot),click=_trigger.Step(true,trigger);
            if(_menu.Step(true,menu)){if(_c.Expedition.Aligning)_c.Expedition.Cancel();else if(IsOpen)Close();else Open();return true;}
            if(IsOpen)
            {
                FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out var origin,out var rotation,true);var direction=rotation*Vector3.forward;
                _ray.enabled=true;_ray.SetPosition(0,origin);_ray.SetPosition(1,origin+direction*2);
                if(Time.unscaledTime>=_nextText){Refresh();_nextText=Time.unscaledTime+.2f;}
                if(Physics.Raycast(origin,direction,out var hit,3,1<<5,QueryTriggerInteraction.Collide))
                {
                    var button=hit.collider.GetComponent<FishingNotebookButton>();
                    if(button!=null && button.Owner==this && _c.Shore.ClearSight(origin,hit.point))
                    {
                        _ray.SetPosition(1,hit.point);_plates[button.Index].color=new Color(.65f,.82f,.70f);
                        if(click){_actions[button.Index]?.Invoke();if(IsOpen)Refresh();}
                    }
                }
                return true;
            }
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
            if(item==FieldItem.Workbench || item==FieldItem.Furnace){_c.WorkshopWorld.PlaceEquipped();return;}
            if(item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe){TryStrike();return;}
            _c.UseFieldItem();
        }
        public void SampleTool()
        {
            if(!XRSettings.isDeviceActive || IsOpen || _c.Paused || _c.XRSeat.HeadBlocked || _c.FieldWorld.HeldTool==null){_tipSample=false;return;}
            var item=_c.FieldWorld.PresentedItem;
            if(item!=FieldItem.StonePickaxe && item!=FieldItem.IronPickaxe){_tipSample=false;return;}
            var tip=_c.XRSeat.Origin.transform.InverseTransformPoint(_c.FieldWorld.HeldTool.TransformPoint(FishingFieldWorld.PickTip));float dt=Time.deltaTime;
            if(_tipSample && dt>.001f && dt<.1f && (tip-_lastTip).magnitude<.4f && (tip-_lastTip).magnitude/dt>.85f)TryStrike();
            _lastTip=tip;_tipSample=true;
        }
        private bool TryStrike()
        {
            if(Time.time<_nextStrike || !_c.WorkshopWorld.TryHarvestTarget(out var node))return false;
            var tool=_c.FieldWorld.HeldTool;if(tool==null || !tool.gameObject.activeSelf)return false;
            var tip=tool.TransformPoint(FishingFieldWorld.PickTip);
            if(Vector3.Distance(tip,FishingContactGeometry.ClosestPoint(node.Root,tip))>.10f || !_c.Shore.ClearSight(tool.position,tip))return false;
            _nextStrike=Time.time+.65f;return _c.WorkshopWorld.HarvestNode(node.Id,node.Item,_c.Career.Backpack.Equipped);
        }
        private void InteractTracked()
        {
            if(!_c.Ashore){_c.Interact();return;}
            if(_c.TackleShop!=null && _c.TackleShop.NearCounter){Open(4);return;}
            if(_c.WorkshopWorld.TryInteractionTarget(out int station,out var resource) && station>=0){_c.WorkshopWorld.OpenStation(station);return;}
            if(!_c.Hands.TryControllerPalm(false,out var palm,out _))return;
            if(_c.FieldWorld.TryPickupTarget(out var pickup) && Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(pickup.Root,palm))<.24f)
            {_c.FieldWorld.PickupById(pickup.Id,pickup.Item,pickup.Count);return;}
            if(resource!=null)
            {
                if(_c.Career.Backpack.Equipped==FieldItem.StonePickaxe || _c.Career.Backpack.Equipped==FieldItem.IronPickaxe){TryStrike();return;}
                if(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(resource.Root,palm))<.24f)_c.WorkshopWorld.HarvestNode(resource.Id,resource.Item,_c.Career.Backpack.Equipped);return;
            }
            int target=_c.Expedition.Resolve();var root=_c.Expedition.InteractionRoot(target);
            if(root!=null && Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(root,palm))<.24f){_c.Expedition.InteractAt(target);return;}
            if(_c.Shore.NearbyTask>=0){_c.Shore.SurveyTask(_c.Shore.NearbyTask);return;}
            if(_c.Career.Backpack.Equipped==FieldItem.FieldGuide || _c.Career.Backpack.Equipped==FieldItem.Binoculars){_c.FieldWorld.Observe();return;}
            _c.Notice("右手接近目标后按 A；镐尖触碰矿石可挥动采集。B 打开手册，X 上下船，Y 换装备。",5);
        }
        private void OnApplicationFocus(bool focus){if(!focus)Close();}
        private void OnApplicationPause(bool pause){if(pause)Close();}
    }
}
