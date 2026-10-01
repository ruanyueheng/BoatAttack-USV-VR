using System;

namespace BoatAttack.Fishing
{
    /// <summary>Transient dial state; cancellation never changes expedition facts.</summary>
    public sealed class FishingBeaconDial
    {
        public float Heading { get; private set; }
        public float Target { get; }
        public float Tolerance { get; }
        public bool Active { get; private set; }
        public FishingBeaconDial(float target, float tolerance = 8f)
        {
            if(!Finite(target) || !Finite(tolerance) || tolerance < 0 || tolerance > 45) throw new ArgumentOutOfRangeException();
            Target=Wrap(target); Tolerance=tolerance; Heading=Wrap(Target-75);
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static float Wrap(float n) => (n % 360 + 360) % 360;
        public float Error => Wrap(Target-Heading+180)-180;
        public bool Aligned => Math.Abs(Error)<=Tolerance;
        public void Begin() { Active=true; }
        public void Cancel() { Active=false; }
        public void Rotate(float degrees) { if(Active && Finite(degrees))Heading=Wrap(Heading+degrees%360); }
        public bool Confirm(FishingExpedition expedition)
        {
            if(!Active || !Aligned || expedition==null || !expedition.RepairBeacon())return false;
            Active=false; return true;
        }
    }
}
