using System;
namespace BoatAttack.Fishing
{
    /// <summary>Grip the rod, hold trigger, swing forward and release; a fresh release is required after recovery.</summary>
    public sealed class FishingCastGesture
    {
        private bool _ready,_held;
        private float _age,_peak;
        public float Power { get; private set; } = .55f;
        public void Reset(){_ready=_held=false;_age=_peak=0;}
        public bool Step(bool available,bool trigger,float forwardSpeed,float dt)
        {
            if(!available || float.IsNaN(forwardSpeed) || float.IsInfinity(forwardSpeed) || Math.Abs(forwardSpeed)>12
                || float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0 || dt>.1f){Reset();return false;}
            if(!trigger && !_held){_ready=true;return false;}
            if(trigger)
            {
                if(!_ready)return false;
                if(!_held){_held=true;_age=_peak=0;}
                _age+=dt;_peak=Math.Max(_peak,forwardSpeed);return false;
            }
            bool cast=_held && _age>=.05f && _age<2.5f && _peak>=1.15f;
            if(cast)Power=.35f+Math.Max(0,Math.Min(1,(_peak-1.15f)/2))* .65f;
            _held=false;_peak=_age=0;return cast;
        }
    }
}
