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
        private FishingPrototype _c;private Canvas _canvas;private Text _status,_message;private LineRenderer _ray;
        private readonly Dictionary<FishingDockAction,Image> _buttons=new Dictionary<FishingDockAction,Image>();
        private readonly FishingPressEdge _edge=new FishingPressEdge();
        private FishingDockButton _hover;private FishingDockAction? _offer;private string _quote;private float _until;
        private Transform[] _rings;private bool _preview,_pointerHeld;
        public void PreviewForValidation(){if(!_c.Automated)throw new InvalidOperationException("Isolated validation only");_preview=true;Present();}
        public Vector3 ButtonPosition(FishingDockAction action)=>_buttons[action].transform.TransformPoint(new Vector3(112.5f,-24.5f,0));
        public bool BlocksGameplay=>XRSettings.isDeviceActive && ((_hover!=null && _pointerHeld) || _offer.HasValue);
        public int ButtonCount=>_buttons.Count;
        public void Build(FishingPrototype c)
        {
            _c=c;var root=new GameObject("XR dock and catch console",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);
            _canvas=root.GetComponent<Canvas>();_canvas.renderMode=RenderMode.WorldSpace;_canvas.worldCamera=c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(500,570);rect.localScale=Vector3.one*.002f;
            var panel=root.AddComponent<Image>();panel.color=new Color(.025f,.075f,.1f,.97f);panel.raycastTarget=false;
            _status=Label(root.transform,new Vector2(18,-16),new Vector2(464,196),21);
            _message=Label(root.transform,new Vector2(18,-500),new Vector2(464,58),19);
            int i=0;foreach(FishingDockAction action in Enum.GetValues(typeof(FishingDockAction)))
            {
                var go=new GameObject(action.ToString(),typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(root.transform,false);
                var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(18+(i%2)*239,-202-(i/2)*57);rt.sizeDelta=new Vector2(225,49);
                var image=go.GetComponent<Image>();image.raycastTarget=false;_buttons.Add(action,image);
                var label=Label(go.transform,new Vector2(8,-8),new Vector2(210,34),20);label.text=Name(action);
                var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(225,49,4);collider.center=new Vector3(112.5f,-24.5f,0);collider.isTrigger=true;
                var command=go.AddComponent<FishingDockButton>();command.Owner=this;command.Action=action;i++;
            }
            _ray=FishingArt.Line("XR controller action ray",transform,c.lineMaterial,.006f);_ray.positionCount=2;_ray.enabled=false;
            _rings=new Transform[2];for(int r=0;r<2;r++)_rings[r]=FishingArt.Shape("Line upgrade marker "+r,PrimitiveType.Cylinder,c.Hands.Rod,new Vector3(0,.08f+r*.045f,0),new Vector3(.1f,.015f,.1f),c.orange).transform;
            Present();
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject("Readable ASCII label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=fontSize;text.color=Color.white;text.raycastTarget=false;return text;
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
                case FishingDockAction.UpgradeLine:return _c.AtHarbor && _c.Career.NextUpgradeCost(UpgradeTrack.Line)>0 && _c.Career.Coins>=_c.Career.NextUpgradeCost(UpgradeTrack.Line);
                case FishingDockAction.NextWaypoint:return !_c.HasPendingCatch;
                case FishingDockAction.Anchor:return !_c.Ashore && !_c.HasPendingCatch && (_c.Anchored || (!_c.AtHarbor && _c.SpotDistance<=22 && _c.Speed<=3));
                case FishingDockAction.Store:return _c.HasPendingCatch;
                case FishingDockAction.Release:return _c.HasPendingCatch && (!FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary || _c.Career.PendingCatch.measured);
                default:return false;
            }
        }
        private string Quote()=>_c.Career.Coins+":"+_c.Career.Count+":"+_c.Career.SaleValue+":"+_c.Career.OrdersCompleted+":"+_c.Career.LineLevel+":"+(_c.Career.PendingCatch?.fish.id??"");
        public bool Press(FishingDockAction action)
        {
            if(!Allowed(action)){ClearOffer();_message.text=action==FishingDockAction.Release && _c.HasPendingCatch?"Measure the legend first: lift it or choose Store / measure.":"Unavailable here. Check hold, coins and location.";return false;}
            bool confirm=action==FishingDockAction.Release || action==FishingDockAction.UpgradeLine || action==FishingDockAction.Sell || action==FishingDockAction.Deliver;
            if(confirm)
            {
                if(_offer!=action || Time.unscaledTime>_until)
                {_offer=action;_quote=Quote();_until=Time.unscaledTime+5;_message.text="CONFIRM: "+Name(action)+"\nRelease trigger, then click again.";return false;}
                if(_quote!=Quote()){ClearOffer();_message.text="Offer changed. Review and confirm again.";return false;}
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
                    for(int i=0;i<=_c.fishingSpots.Length;i++){next=(next+1)%(_c.fishingSpots.Length+1);if(next==0 || FishingGrounds.Available(next-1,_c.Career.Expedition))break;}
                    _c.SelectNavigation(next);break;
                case FishingDockAction.Anchor:if(_c.Anchored)_c.LeaveStation();else _c.Interact();break;
                case FishingDockAction.Store:
                    if(FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary){ok=_c.MeasureCatch();_message.text="MEASURED: "+_c.Career.PendingCatch.fish.grams+"g.\nChoose Release and confirm for the commemorative bounty.";return ok;}
                    ok=_c.ResolveCatch(true);break;
                case FishingDockAction.Release:ok=_c.ResolveCatch(false);break;
                case FishingDockAction.Pause:_c.TogglePause();break;
            }
            _message.text=ok?"Done. No keyboard required.":"Not completed. Your pending fish remains safe.";return ok;
        }
        public bool Tick(bool allowInput)
        {
            if(_offer.HasValue && Time.unscaledTime>_until)ClearOffer();
            bool available=allowInput && XRSettings.isDeviceActive && Application.isFocused;
            Vector3 origin=Vector3.zero;Quaternion rotation=Quaternion.identity;
            available=available && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out origin,out rotation,true)
                && FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out _,out _);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.triggerButton,out bool held);
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
            if(_canvas==null)return;_canvas.gameObject.SetActive(XRSettings.isDeviceActive || _preview);
            var yaw=Quaternion.Euler(0,_c.Player.transform.eulerAngles.y+(_c.Anchored?35:0),0);
            _canvas.transform.SetPositionAndRotation(_c.Player.transform.position+Vector3.up*1.55f+yaw*new Vector3(-.75f,0,2),yaw);
            for(int i=0;i<_rings.Length;i++)_rings[i].gameObject.SetActive(_c.Career.LineLevel>i);
            foreach(var p in _buttons)p.Value.color=_hover!=null && _hover.Action==p.Key && Allowed(p.Key)?new Color(.2f,.55f,.4f):Allowed(p.Key)?new Color(.07f,.24f,.27f):new Color(.1f,.12f,.13f);
            string state=_c.HasPendingCatch?"PENDING: "+_c.Career.PendingCatch.fish.kind+" "+_c.Career.PendingCatch.fish.grams+"g"+(_c.Career.PendingCatch.measured?" [MEASURED]":""):_c.Anchored?"FISHING: "+_c.Session.Phase:"HELM: "+(_c.Speed*3.6f).ToString("0")+" km/h";
            _status.text="TIDE & FIN  /  "+state+"\nCOINS "+_c.Career.Coins+"    HOLD "+_c.Career.Count+"/"+_c.Career.Capacity+"    LINE "+_c.Career.LineLevel+"\nORDER "+(_c.Career.OrdersCompleted+1)+(_c.Career.OrderMarked?" [MARKED]":"")+(_c.Career.OrderReady?" READY":"")+"\n";
            for(int i=0;i<_c.Career.CurrentOrder.LineCount;i++){var line=_c.Career.CurrentOrder.Line(i);_status.text+=line.Kind+" x"+line.Count+(line.Rare?" pearl":"")+"  ";}
            _status.text+="\n"+(_c.Session.Phase==CatchPhase.Fighting?(_c.Session.Beat+" / LINE "+(_c.Session.Tension*100).ToString("0")+"%  "+(_c.Session.Beat==FishBeat.Rest?"REEL":"EASE / RELEASE")):"NET: COVER, LIFT; BOX: GENTLE TILT");
            _status.text+="\nBEACON: "+(_c.Career.Expedition.Has(FishingExpedition.TargetCaught)?"EVIDENCE RECORDED":_c.Career.Expedition.Has(FishingExpedition.BeaconRepaired)?"NORTH UNLOCKED":"SHORE ROUTE (DESKTOP)");
            var delta=_c.NavigationPosition-_c.Player.transform.position;
            float bearing=Mathf.DeltaAngle(_c.Player.transform.eulerAngles.y,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
            string turn=Mathf.Abs(bearing)<12?"AHEAD":(bearing<0?"LEFT ":"RIGHT ")+Mathf.Abs(bearing).ToString("0")+" deg";
            _status.text+="\n"+(_c.NavigationTarget==0?"HARBOR":"FISH #"+_c.NavigationTarget)+" "+FishingPrototype.FlatDistance(_c.ExplorerPosition,_c.NavigationPosition).ToString("0")+"m  "+turn;
        }
        private static string Name(FishingDockAction a)
        {
            switch(a){case FishingDockAction.MarkOrder:return "Accept / mark order";case FishingDockAction.Deliver:return "Deliver order";case FishingDockAction.Sell:return "Sell unreserved fish";case FishingDockAction.UpgradeLine:return "Upgrade line";case FishingDockAction.NextWaypoint:return "Next waypoint";case FishingDockAction.Anchor:return "Anchor / sail";case FishingDockAction.Store:return "Store / measure fish";case FishingDockAction.Release:return "Release fish";default:return "Pause / resume";}
        }
    }
}
