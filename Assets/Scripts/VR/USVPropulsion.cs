using System;
namespace BoatAttack.VR
{
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
