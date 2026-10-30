using System;
namespace BoatAttack.Fishing
{
    /// <summary>Hold trigger to charge, release to cast; a forward swing adds power. Recovery requires a fresh press.</summary>
    public sealed class FishingCastGesture
    {
        private bool _ready,_held;
        private float _age,_peak;
        public float Power { get; private set; } = .55f;
        public void Reset(){_ready=_held=false;_age=_peak=0;}
        public bool Step(bool available,bool trigger,float forwardSpeed,float dt)
        {
            if(!available || float.IsNaN(forwardSpeed) || float.IsInfinity(forwardSpeed)
                || float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0 || dt>.1f){Reset();return false;}
            if(Math.Abs(forwardSpeed)>12)forwardSpeed=0; // Ignore pose jumps without losing a deliberate trigger charge.
            if(!trigger && !_held){_ready=true;return false;}
            if(trigger)
            {
                if(!_ready)return false;
                if(!_held){_held=true;_age=_peak=0;}
                _age=Math.Min(1.2f,_age+dt);_peak=Math.Max(_peak,forwardSpeed);return false;
            }
            bool cast=_held;
            if(cast)Power=.35f+Math.Max(_age/1.2f,Math.Max(0,Math.Min(1,(_peak-1.15f)/2)))*.65f;
            _held=false;_peak=_age=0;return cast;
        }
    }
}
