using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public enum FishingDockAction { MarkOrder, Deliver, Sell, UpgradeLine, NextWaypoint, Anchor, Store, Release, Pause }
    public sealed class FishingDockButton : MonoBehaviour { public FishingDockConsole Owner;public FishingDockAction Action; }
    /// <summary>World-space command surface. Disabled targets still consume the trigger, never leaking into casting.</summary>
    public sealed class FishingDockConsole : MonoBehaviour
    {
        private FishingPrototype _c;private Canvas _canvas;private Text _title,_status,_message,_hint;private LineRenderer _ray;
        private RawImage _fishPortrait;private string _pendingId;private bool _catchPoseReady;
        private readonly Dictionary<FishingDockAction,Image> _buttons=new Dictionary<FishingDockAction,Image>();
        private readonly Dictionary<FishingDockAction,Text> _buttonLabels=new Dictionary<FishingDockAction,Text>();
        private readonly FishingPressEdge _edge=new FishingPressEdge();
        private FishingDockButton _hover;private FishingDockAction? _offer;private string _quote;private float _until;
        private Transform[] _rings;private bool _preview,_pointerHeld,_requested,_pendingDismissed;
        public bool Visible=>_canvas!=null && _canvas.gameObject.activeSelf;
        public void Show(){_requested=true;_pendingDismissed=false;_catchPoseReady=false;Present();}
        public void Hide(){_requested=false;_pendingDismissed=_c.HasPendingCatch;_hover=null;_pointerHeld=false;_edge.Reset();ClearOffer();if(_ray!=null)_ray.enabled=false;Present();}
        public void PreviewForValidation(){if(!_c.Automated)throw new InvalidOperationException("Isolated validation only");_preview=true;Present();}
        public Vector3 ButtonPosition(FishingDockAction action)=>_buttons[action].transform.TransformPoint(_buttons[action].GetComponent<BoxCollider>().center);
        public bool BlocksGameplay=>XRSettings.isDeviceActive && ((_hover!=null && _pointerHeld) || _offer.HasValue);
        public int ButtonCount=>_buttons.Count;
        public void Build(FishingPrototype c)
        {
            _c=c;var root=new GameObject("XR dock and catch console",typeof(RectTransform),typeof(Canvas));root.layer=5;root.transform.SetParent(transform,false);
            _canvas=root.GetComponent<Canvas>();_canvas.renderMode=RenderMode.WorldSpace;_canvas.worldCamera=c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(680,820);rect.localScale=Vector3.one*.0015f;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit=4;
            var panel=root.AddComponent<Image>();panel.color=FishingUITheme.Paper;panel.sprite=FishingUITheme.StorybookPaperSprite;panel.type=Image.Type.Sliced;panel.material=FishingUITheme.WorldMaterial;panel.raycastTarget=false;
            FishingUITheme.Bind(panel,0);
            var frame=new GameObject("Gilt star reading edge",typeof(RectTransform),typeof(Image));frame.layer=5;frame.transform.SetParent(root.transform,false);
            var fr=frame.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
            var edge=frame.GetComponent<Image>();edge.sprite=FishingUITheme.GiltFrameSprite;edge.type=Image.Type.Sliced;edge.material=FishingUITheme.WorldMaterial;edge.raycastTarget=false;FishingUITheme.Bind(edge,4);
            _title=Label(root.transform,new Vector2(26,-18),new Vector2(628,60),40);_title.text="码头事务";_title.font=FishingUITheme.Heading;
            FishingUITheme.StyleHeading(_title);
            _status=Label(root.transform,new Vector2(26,-85),new Vector2(628,250),21);
            _message=Label(root.transform,new Vector2(26,-704),new Vector2(628,76),21);
            _hint=Label(root.transform,new Vector2(26,-784),new Vector2(628,30),20);
            var portrait=new GameObject("Shared Windows fish portrait",typeof(RectTransform),typeof(RawImage));portrait.layer=5;portrait.transform.SetParent(root.transform,false);
            _fishPortrait=portrait.GetComponent<RawImage>();_fishPortrait.material=FishingUITheme.WorldMaterial;_fishPortrait.raycastTarget=false;portrait.SetActive(false);
            int i=0;foreach(FishingDockAction action in Enum.GetValues(typeof(FishingDockAction)))
            {
                var go=new GameObject(action.ToString(),typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(root.transform,false);
                var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(26+(i%2)*322,-350-(i/2)*70);rt.sizeDelta=new Vector2(306,58);
                var image=go.GetComponent<Image>();image.sprite=FishingUITheme.StorybookPaperSprite;image.type=Image.Type.Sliced;image.material=FishingUITheme.WorldMaterial;image.raycastTarget=false;_buttons.Add(action,image);
                var label=Label(go.transform,new Vector2(14,-9),new Vector2(278,42),21);label.text=Name(action);_buttonLabels[action]=label;
                var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(306,58,4);collider.center=new Vector3(153,-29,0);collider.isTrigger=true;
                var command=go.AddComponent<FishingDockButton>();command.Owner=this;command.Action=action;i++;
            }
            _ray=FishingArt.Line("XR controller action ray",transform,c.lineMaterial,.006f);_ray.positionCount=2;_ray.enabled=false;
            _rings=new Transform[2];for(int r=0;r<2;r++)
            {
                _rings[r]=FishingArt.Shape("Line upgrade marker "+r,PrimitiveType.Cylinder,c.Hands.Rod,new Vector3(0,0,.21f+r*.035f),new Vector3(.035f,.004f,.035f),c.orange).transform;
                _rings[r].localRotation=Quaternion.Euler(90,0,0);
            }
            Present();
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject("Chinese command label",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=fontSize;text.color=FishingUITheme.Ink;text.material=FishingUITheme.WorldMaterial;text.raycastTarget=false;FishingUITheme.Bind(text,2);return text;
        }
        public bool Allowed(FishingDockAction action)
        {
            if(action==FishingDockAction.Pause)return true;
            if(_c.Paused)return false;
            switch(action)
            {
                case FishingDockAction.MarkOrder:return _c.AtHarbor && !_c.Career.OrderMarked;
                case FishingDockAction.Deliver:return _c.AtHarbor && _c.Career.OrderReady;
                case FishingDockAction.Sell:return _c.AtHarbor && _c.Career.SaleValue>0;
                case FishingDockAction.UpgradeLine:return _c.AtHarbor && _c.Career.NextUpgradeCost(UpgradeTrack.Line)>0 && _c.Career.CanAfford(_c.Career.NextUpgradeCost(UpgradeTrack.Line));
                case FishingDockAction.NextWaypoint:return !_c.HasPendingCatch;
                case FishingDockAction.Anchor:return !_c.Ashore && !_c.HasPendingCatch && (_c.Anchored || (!_c.AtHarbor && _c.SpotDistance<=22 && _c.Speed<=3));
                case FishingDockAction.Store:return _c.HasPendingCatch;
                case FishingDockAction.Release:return _c.HasPendingCatch && (!FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary || _c.Career.PendingCatch.measured);
                default:return false;
            }
        }
        private string Quote()=>_c.Career.Coins+":"+_c.Career.Count+":"+_c.Career.SaleValue+":"+_c.Career.OrdersCompleted+":"+_c.Career.LineLevel+":"+(_c.Career.PendingCatch?.fish.id??"");
        public bool ResolvePendingFromPrimary()
        {
            if(!_c.HasPendingCatch || _c.Paused)return false;
            if(_c.CatchRig.LeftHandInspecting)return _c.CatchRig.PlaceLeftFishInNet();
            Show();
            bool legend=FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary;
            return Press(legend && _c.Career.PendingCatch.measured?FishingDockAction.Release:FishingDockAction.Store);
        }
        public bool Press(FishingDockAction action)
        {
            if(!Allowed(action)){ClearOffer();_message.text=FishingXRText.Adapt(action==FishingDockAction.Release && _c.HasPendingCatch?"请先举起传说鱼测量，或点击「存放 / 测量」。":"此处暂不可操作，请检查鱼货、金币和位置。");return false;}
            bool confirm=action==FishingDockAction.Release || action==FishingDockAction.UpgradeLine || action==FishingDockAction.Sell || action==FishingDockAction.Deliver;
            if(confirm)
            {
                if(_offer!=action || Time.unscaledTime>_until)
                {_offer=action;_quote=Quote();_until=Time.unscaledTime+5;_message.text=FishingXRText.Adapt("确认："+Name(action)+"\n松开右扳机，再点一次确认。");return false;}
                if(_quote!=Quote()){ClearOffer();_message.text=FishingXRText.Adapt("鱼货或价格已变化，请重新确认。");return false;}
            }
            ClearOffer();bool ok=true;
            switch(action)
            {
                case FishingDockAction.MarkOrder:_c.MarkFishingOrder();break;
                case FishingDockAction.Deliver:ok=_c.DeliverOrder()>0;break;
                case FishingDockAction.Sell:ok=_c.Sell()>0;break;
                case FishingDockAction.UpgradeLine:ok=_c.BuyUpgrade(UpgradeTrack.Line);break;
                case FishingDockAction.NextWaypoint:
                    int next=_c.NavigationTarget;
                    for(int i=0;i<_c.NavigationCount;i++){next=(next+1)%_c.NavigationCount;if(next==0 || next>_c.fishingSpots.Length || FishingGrounds.AvailableForCareer(next-1,_c.Career))break;}
                    _c.SelectNavigation(next);break;
                case FishingDockAction.Anchor:if(_c.Anchored)_c.LeaveStation();else _c.Interact();break;
                case FishingDockAction.Store:
                    if(FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary){ok=_c.MeasureCatch();_message.text=FishingXRText.Adapt("已测量："+FishingText.Weight(_c.Career.PendingCatch.fish.grams)+"。\n点击放生并确认，领取纪念奖励。");return ok;}
                    ok=_c.ResolveCatch(true);if(!ok){_message.text=_c.VisibleMessage;return false;}break;
                case FishingDockAction.Release:ok=_c.ResolveCatch(false);break;
                case FishingDockAction.Pause:_c.TogglePause();break;
            }
            _message.text=FishingXRText.Adapt(ok?"操作完成。B 收起面板，B 再次打开手册。":"操作未完成，待处理的鱼仍会保留。");
            if(ok && !_c.HasPendingCatch && (action==FishingDockAction.Store || action==FishingDockAction.Release))Hide();
            return ok;
        }
        public bool Tick(bool allowInput)
        {
            if(_offer.HasValue && Time.unscaledTime>_until)ClearOffer();
            bool available=allowInput && Visible && !_c.StoryBlocksGameplay && XRSettings.isDeviceActive && Application.isFocused;
            Vector3 origin=Vector3.zero;Quaternion rotation=Quaternion.identity;
            available=available && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out origin,out rotation,true)
                && FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out _,out _);
            bool held=FishingXRInput.Trigger(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));
            return TickRay(available,origin,rotation,held);
        }
        public bool TickRay(bool available,Vector3 origin,Quaternion rotation,bool held)
        {
            _pointerHeld=available && held;
            if(!available){_edge.Reset();_hover=null;_ray.enabled=false;ClearOffer();return false;}
            var direction=rotation*Vector3.forward;_ray.enabled=true;_ray.SetPosition(0,origin);_ray.SetPosition(1,origin+direction*4);
            _hover=null;
            if(Physics.Raycast(origin,direction,out var hit,4,1<<5,QueryTriggerInteraction.Collide))
            {
                var button=hit.collider.GetComponent<FishingDockButton>();
                if(button!=null && button.Owner==this && !Physics.Raycast(origin,direction,hit.distance-.02f,~((1<<5)|(1<<4)),QueryTriggerInteraction.Ignore))
                {_hover=button;_ray.SetPosition(1,hit.point);}
            }
            bool pressed=_edge.Step(true,held);
            if(pressed && _hover!=null){Press(_hover.Action);return true;}
            return false;
        }
        private void ClearOffer(){_offer=null;_quote=null;}
        public void Present()
        {
            if(_canvas==null)return;
            string id=_c.Career.PendingCatch?.fish.id;
            if(id!=_pendingId){_pendingId=id;_pendingDismissed=false;_catchPoseReady=false;_message.text="";ClearOffer();_fishPortrait.texture=id==null?null:Resources.Load<Texture2D>("FishingFish/"+_c.Career.PendingCatch.fish.kind+"Thumbnail");}
            if(!_c.HasPendingCatch){_pendingDismissed=false;_catchPoseReady=false;}
            _canvas.gameObject.SetActive((XRSettings.isDeviceActive && (!_c.Ashore || _c.HasPendingCatch) && !_c.StoryBlocksGameplay && (_requested || (_c.HasPendingCatch && !_pendingDismissed)) && (_c.XRNotebook==null || !_c.XRNotebook.IsOpen)) || _preview);
            if(!_canvas.gameObject.activeSelf)return;
            _title.text=_c.HasPendingCatch?"钓到了！":"码头事务";
            FishingUITheme.StyleHeading(_title);
            Layout(_c.HasPendingCatch);
            if(_c.HasPendingCatch){PresentCatch();return;}
            var yaw=Quaternion.Euler(0,_c.Player.transform.eulerAngles.y+(_c.Anchored?35:0),0);
            var boat=_c.Player.transform.position;
            float height=_c.XRSeat.Ready?_c.XRSeat.Origin.transform.position.y+1.3f:boat.y+1.55f;
            _canvas.transform.SetPositionAndRotation(new Vector3(boat.x,height,boat.z)+yaw*new Vector3(-.75f,0,2),yaw);
            for(int i=0;i<_rings.Length;i++)_rings[i].gameObject.SetActive(_c.Career.LineLevel>i);
            foreach(var p in _buttons)p.Value.color=!Allowed(p.Key)?FishingUITheme.Disabled:_offer==p.Key?FishingUITheme.Selected:_hover!=null && _hover.Action==p.Key?FishingUITheme.Selected:FishingUITheme.Light;
            string state=_c.HasPendingCatch?"待处理："+FishingSpecies.Name(_c.Career.PendingCatch.fish.kind)+" "+FishingText.Weight(_c.Career.PendingCatch.fish.grams)+(_c.Career.PendingCatch.measured?"（已测量）":""):_c.Anchored?"驻钓中":"航行："+(_c.Speed*3.6f).ToString("0")+" 千米/时";
            _status.text=FishingXRText.Adapt("潮汐渔港  ·  "+state+"\n金币 "+_c.Career.Coins+"    鱼箱 "+_c.Career.Count+"/"+_c.Career.Capacity+"    鱼线 "+_c.Career.LineLevel+" 级\n委托「"+_c.Career.CurrentOrder.Name+"」"+(_c.Career.OrderReady?" · 可交付":"")+"\n");
            for(int i=0;i<_c.Career.CurrentOrder.LineCount;i++){var line=_c.Career.CurrentOrder.Line(i);_status.text+=FishingSpecies.Name(line.Kind)+" ×"+line.Count+(line.Rare?" 珍珠":"")+"  ";}
            _status.text+="\n"+(!_c.Anchored?"动力温度 "+(_c.Boost.Heat*100).ToString("0")+"%"+(_c.Boost.Overheated?" · 冷却至 25%":"")
                :_c.Session.Phase==CatchPhase.Fighting?"鱼线张力 "+(_c.Session.Tension*100).ToString("0")+"% · "+(_c.Session.Beat==FishBeat.Rest?"回气时转轮收线":"冲刺或跃水时松开左握把")
                :_c.Session.Phase==CatchPhase.Landing?"抄网："+_c.Session.Remaining.ToString("0.0")+" 秒 · 套住后举起":"抄网套住举起 · 鱼箱轻放 / 缓慢倾倒");
            var delta=_c.NavigationPosition-_c.Player.transform.position;
            float bearing=Mathf.DeltaAngle(_c.Player.transform.eulerAngles.y,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
            string turn=Mathf.Abs(bearing)<12?"正前方":(bearing<0?"向左 ":"向右 ")+Mathf.Abs(bearing).ToString("0")+"°";
            _status.text+="\n"+_c.NavigationName+" "+FishingPrototype.FlatDistance(_c.ExplorerPosition,_c.NavigationPosition).ToString("0")+"m  "+turn;
        }
        private static void Rect(RectTransform t,float x,float y,float width,float height)
        {t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(width,height);}
        private void Layout(bool pending)
        {
            ((RectTransform)_canvas.transform).sizeDelta=pending?new Vector2(860,580):new Vector2(680,820);
            _canvas.GetComponent<Image>().sprite=pending?FishingUITheme.TornParchmentSprite:FishingUITheme.StorybookPaperSprite;
            _canvas.transform.localScale=Vector3.one*(pending?.0012f:.0015f);
            Rect(_title.rectTransform,pending?40:26,pending?25:18,pending?780:628,pending?70:60);
            Rect(_status.rectTransform,pending?400:26,pending?116:85,pending?420:628,pending?205:250);
            Rect(_message.rectTransform,pending?40:26,pending?434:704,pending?780:628,pending?62:76);
            Rect(_hint.rectTransform,pending?40:26,pending?500:784,pending?780:628,30);
            _status.fontSize=pending?24:21;_hint.text=pending?"A 快捷处理鱼获  ·  右扳机选择  ·  B 暂时收起":"右扳机指向并选择  ·  B 收起";
            _fishPortrait.gameObject.SetActive(pending);int i=0;
            foreach(var pair in _buttons)
            {
                bool catchButton=pair.Key==FishingDockAction.Store || pair.Key==FishingDockAction.Release;
                pair.Value.gameObject.SetActive(!pending || catchButton);
                float x=pending?(pair.Key==FishingDockAction.Store?40:442):26+i%2*322;
                float y=pending?344:350+i/2*70,width=pending?378:306,height=pending?76:58;
                Rect(pair.Value.rectTransform,x,y,width,height);
                var box=pair.Value.GetComponent<BoxCollider>();box.size=new Vector3(width,height,4);box.center=new Vector3(width*.5f,-height*.5f,0);
                var label=_buttonLabels[pair.Key];Rect(label.rectTransform,14,pending?16:9,width-28,pending?46:42);label.fontSize=pending?27:21;label.text=Name(pair.Key);i++;
            }
        }
        private void PresentCatch()
        {
            var eye=_c.viewCamera.transform;var direction=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
            if(direction.sqrMagnitude<.5f)direction=_c.Player.transform.forward;
            if(!_catchPoseReady || Vector3.Angle(direction,_canvas.transform.position-eye.position)>45 || Vector3.Distance(eye.position,_canvas.transform.position)>2)
            {
                _canvas.transform.SetPositionAndRotation(eye.position+direction*1.25f-Vector3.up*.14f,Quaternion.LookRotation(direction));_catchPoseReady=true;
            }
            var fish=_c.Career.PendingCatch.fish;bool legend=FishingSpecies.Of(fish.kind).Legendary;
            _title.text=FishingSpecies.Name(fish.kind)+(fish.rare?" · 珍珠收获":" · 本次收获");
            _status.text=FishingText.Weight(fish.grams)+"\n"+(legend?"传说鱼 · 测量后放生留念":"预计鱼价 "+fish.value+" 金币")+"\n鱼箱 "+_c.Career.Count+" / "+_c.Career.Capacity
                +"\n\n"+(legend?(_c.Career.PendingCatch.measured?"按 A 确认放生；再按一次完成。":"按 A 测量，再选择放生。"):_c.Ashore?"按 A 放入鱼箱，即可继续探索。":"按 A 放入鱼箱，即可继续抛竿。");
            _buttonLabels[FishingDockAction.Store].text=legend?"测量并记录":"A  放入鱼箱";
            _buttonLabels[FishingDockAction.Release].text=_offer==FishingDockAction.Release?"再次选择 · 确认放生":"放生 · 保留图鉴";
            foreach(var pair in _buttons)pair.Value.color=!Allowed(pair.Key)?FishingUITheme.Disabled:_offer==pair.Key || _hover!=null && _hover.Action==pair.Key?FishingUITheme.Selected:pair.Key==FishingDockAction.Store?FishingUITheme.Selected:FishingUITheme.Light;
            var texture=_fishPortrait.texture;
            if(texture!=null){float width=Mathf.Min(320,200*texture.width/(float)texture.height);Rect(_fishPortrait.rectTransform,40+(320-width)*.5f,115,width,width*texture.height/texture.width);}
            if(!_offer.HasValue && string.IsNullOrEmpty(_message.text))_message.text="鱼仍安全保留。选好存放或放生后，才会结算这次收获。";
        }
        private static string Name(FishingDockAction a)
        {
            switch(a){case FishingDockAction.MarkOrder:return "接取 / 标记委托";case FishingDockAction.Deliver:return "交付委托";case FishingDockAction.Sell:return "出售非委托鱼货";case FishingDockAction.UpgradeLine:return "升级鱼线";case FishingDockAction.NextWaypoint:return "下一航点";case FishingDockAction.Anchor:return "驻钓 / 继续航行";case FishingDockAction.Store:return "存放 / 测量鱼获";case FishingDockAction.Release:return "放生鱼获";default:return "暂停 / 继续";}
        }
    }
}
