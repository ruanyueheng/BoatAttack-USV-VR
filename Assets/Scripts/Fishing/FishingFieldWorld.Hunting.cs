using UnityEngine;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        public static readonly Vector3 SpearPalm=new Vector3(.03f,0,-.02f),SpearTip=new Vector3(0,0,.92f);
        private Animal _huntTarget;
        private float _thrust,_nextHunt;
        private Vector3 _lastSpear;
        private bool _spearSample;
        public bool BeginHunt()
        {
            if(_c.Paused || !_c.Ashore || _c.WorldActionBusy || _c.Career.Backpack.Equipped!=FieldItem.HuntingSpear || Time.time<_nextHunt)return false;
            Sense();
            if(_candidate==null || !FishingHunting.Huntable(_candidate.Kind))
            {_c.Notice("猎矛用于野兔和赤狐；陆龟保留观察。蹲下接近，瞄准动物再刺击。",4);return false;}
            if(Vector3.Distance(_c.Shore.Character.ShoulderPosition(false),_candidate.Root.position+Vector3.up*.22f)>_c.Shore.Character.ArmReach(false)+.91f)
            {_c.Notice("超出猎矛可达范围，请蹲下并靠近。",3);return false;}
            _huntTarget=_candidate;_thrust=0;_nextHunt=Time.time+.65f;return true;
        }
        public void CancelHunt(){_huntTarget=null;_thrust=0;_spearSample=false;}
        private void TickHunt(float dt)
        {
            if(_huntTarget==null)return;
            if(!_c.Ashore || _c.Career.Backpack.Equipped!=FieldItem.HuntingSpear || !_c.Hands.CanPresentTools || !_huntTarget.Root.gameObject.activeSelf)
            {CancelHunt();return;}
            _thrust+=Mathf.Min(dt,.05f);
            if(_thrust>.48f){_c.Notice("刺击落空，动物没有损失；靠近后再试。",3);CancelHunt();}
        }
        private void CheckSpearContact(bool tracked)
        {
            var tip=_held.TransformPoint(SpearTip);
            float dt=Time.deltaTime;
            // Tracking-space motion excludes locomotion and snap-turns from weapon strikes.
            var sample=tracked?_c.XRSeat.Origin.transform.InverseTransformPoint(tip):tip;
            var forward=tracked?_c.XRSeat.Origin.transform.InverseTransformDirection(_held.forward):_held.forward;
            bool physical=tracked && _spearSample && dt>.001f && dt<.1f && (sample-_lastSpear).magnitude<.4f
                && Vector3.Dot((sample-_lastSpear)/dt,forward)>.8f && Time.time>=_nextHunt;
            if(physical){Sense();_huntTarget=_candidate;}
            if(_huntTarget!=null && (physical || _thrust>.16f) && FishingHunting.Huntable(_huntTarget.Kind))
            {
                var target=_huntTarget.Root.position+Vector3.up*.22f;
                if(Vector3.Distance(tip,target)<.30f && Vector3.Distance(tip,_c.ExplorerPosition)<2.1f
                    && _c.Shore.ClearSight(_held.TransformPoint(SpearPalm),target))
                {
                    bool ok=_c.Career.Hunting.Harvest(_c.Career.Backpack,_huntTarget.Id,_huntTarget.Kind,_c.Career.Calendar.TotalMinutes);
                    if(ok)
                    {
                        _huntTarget.Root.gameObject.SetActive(false);_c.Save();
                        _c.Effects?.Burst(target,new Color(.91f,.73f,.44f));
                        _c.Notice("获得生兽肉 · 熔炉可烤肉；动物在继续游玩 5 分钟后恢复，休息也会推进恢复。",5);
                        var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                        if(tracked && device.TryGetHapticCapabilities(out var h) && h.supportsImpulse)device.SendHapticImpulse(0,.35f,.06f);
                    }
                    else _c.Notice("背包已满或动物正在恢复，本次没有消耗。",3);
                    CancelHunt();_nextHunt=Time.time+.65f;
                }
            }
            _lastSpear=sample;_spearSample=true;
        }
    }
}
