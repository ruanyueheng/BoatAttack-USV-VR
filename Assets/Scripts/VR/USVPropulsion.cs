using System;
namespace BoatAttack.VR
{
    public sealed class USVBoost
    {
        public float Heat { get; private set; }
        public bool Overheated { get; private set; }
        public bool Active { get; private set; }
        public void Tick(float dt, bool requested, bool blocked)
        {
            Active = false;
            if (blocked || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            dt = Math.Min(dt, .1f);
            Active = requested && !Overheated;
            Heat = Math.Max(0f, Math.Min(1f, Heat + dt * (Active ? 1f / 8f : -1f / 12f)));
            if (Heat >= 1f) { Overheated = true; Active = false; }
            else if (Overheated && Heat <= .25f) Overheated = false;
        }
    }

    public static class USVPropulsion
    {
        public const float ReverseScale=.55f, MaxReverseSpeed=4f;
        // Negative throttle is actual astern thrust, never an implicit velocity brake.
        public static float Thrust(float throttle,float longitudinalSpeed)
        {
            if(float.IsNaN(throttle) || float.IsInfinity(throttle) || float.IsNaN(longitudinalSpeed) || float.IsInfinity(longitudinalSpeed))return 0;
            throttle=Math.Max(-1,Math.Min(1,throttle));
            return throttle<0?(longitudinalSpeed<=-MaxReverseSpeed?0:throttle*ReverseScale):throttle;
        }
    }
}
