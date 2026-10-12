using System;
namespace BoatAttack.Fishing
{
    /// <summary>Shared desktop/XR presentation clock; a bite cannot occur before the float lands.</summary>
    public sealed class FishingCastMotion
    {
        private float _charge, _elapsed = -1;
        public bool Preparing { get; private set; }
        public float Power { get; private set; } = .55f;
        public float Charge => Math.Min(1, _charge / 1.2f);
        public float FlightSeconds => .65f + Power * .35f;
        public bool Casting => _elapsed >= 0 && _elapsed < .24f + FlightSeconds;
        public float FlightProgress => _elapsed < 0 ? 1 : Math.Max(0, Math.Min(1, (_elapsed - .24f) / FlightSeconds));
        public float RodPitch
        {
            get
            {
                if (Preparing) return -32 - Charge * 45;
                if (_elapsed < 0) return -32;
                if (_elapsed < .24f) return -32 - (float)Math.Sin(_elapsed / .24f * Math.PI) * 42;
                return -32 + (float)Math.Sin(Math.Min(1, (_elapsed - .24f) / (FlightSeconds + .3f)) * Math.PI) * 24;
            }
        }
        public void Prepare() { Preparing = true; _charge = 0; }
        public float Release() { float power = .35f + Charge * .65f; CancelPreparation(); return power; }
        public void CancelPreparation() { Preparing = false; _charge = 0; }
        public void Launch(float power)
        {
            CancelPreparation(); Power = float.IsNaN(power) || float.IsInfinity(power) ? .55f : Math.Max(0, Math.Min(1, power)); _elapsed = 0;
        }
        public void Tick(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            dt = Math.Min(dt, .1f);
            if (Preparing) _charge = Math.Min(1.2f, _charge + dt);
            if (_elapsed >= 0) { _elapsed += dt; if (_elapsed > .24f + FlightSeconds + .3f) _elapsed = -1; }
        }
        public void Reset() { CancelPreparation(); _elapsed = -1; }
    }
}
