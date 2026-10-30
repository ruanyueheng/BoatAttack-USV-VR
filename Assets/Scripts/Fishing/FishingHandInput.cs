using System;

namespace BoatAttack.Fishing
{
    /// <summary>Hardware-free latch policy: recovery requires release; a stationary crank cannot reel.</summary>
    public sealed class FishingHandInput
    {
        public bool Holding { get; private set; }
        public bool Activated { get; private set; }
        public bool Reeling { get; private set; }
        private bool _gripReady,_triggerReady,_cranking;
        private float _angle;
        public void Update(bool available,float grip,bool trigger,bool nearRod,float leftGrip,bool nearCrank,float angle,bool buttonHolding=false)
        {
            Activated=Reeling=false;
            if(!available || !Finite(grip) || !Finite(leftGrip) || !Finite(angle))
            {Holding=_gripReady=_triggerReady=_cranking=false;return;}
            if(grip<.35f){_gripReady=true;Holding=false;}
            if(!trigger)_triggerReady=true;
            if(grip>.65f && _gripReady && !Holding && nearRod){Holding=true;_gripReady=false;}
            if(!Holding && !buttonHolding){_cranking=false;return;}
            if(trigger && _triggerReady){Activated=true;_triggerReady=false;}
            bool crank=nearCrank && leftGrip>.55f;
            float delta=((angle-_angle)%360+540)%360-180;
            Reeling=crank && _cranking && Math.Abs(delta)>1 && Math.Abs(delta)<60;
            _cranking=crank;_angle=angle;
        }
        private static bool Finite(float x)=>!float.IsNaN(x) && !float.IsInfinity(x);
        public static bool CaptureShorePointer(bool ashore,bool desktop,bool focused,bool automated,bool paused,bool modal,bool alt)
            => ashore && desktop && focused && !automated && !paused && !modal && !alt;
    }
}
