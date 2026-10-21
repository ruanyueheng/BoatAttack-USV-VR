using System;

namespace BoatAttack.Fishing
{
    /// <summary>One footstep event at a time, driven by observed grounded displacement.</summary>
    public sealed class FishingFootstepClock
    {
        private float _distance, _sinceStep, _idle;
        private bool _stepped;
        public void Reset()
        {
            _distance = _sinceStep = _idle = 0;
            _stepped = false;
        }
        public bool Tick(float distance, float verticalDelta, float dt, bool enabled, bool grounded, bool crouched)
        {
            if (!enabled || !grounded || !Finite(distance) || !Finite(verticalDelta) || !Finite(dt)
                || distance < 0 || dt <= 0 || dt > .25f || distance > 1.5f || distance / dt > 8f || Math.Abs(verticalDelta) > .5f)
            {
                Reset(); return false;
            }
            _sinceStep = Math.Min(1, _sinceStep + dt);
            if (distance <= .0001f)
            {
                _idle += dt;
                if (_idle > .2f) Reset();
                return false;
            }
            _idle = 0;
            float stride = crouched ? .72f : distance / dt > 3.3f ? 1.35f : 1.08f;
            float next = _stepped ? stride : stride * .5f;
            _distance += distance;
            if (_distance + .00001f < next || _sinceStep + .00001f < .17f) return false;
            _distance = Math.Max(0, _distance - next);
            _sinceStep = 0; _stepped = true;
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Tracks actual inspection animation peaks without replaying missed or muted beats.</summary>
    public sealed class FishingFishFlapClock
    {
        public const float Period = (float)(Math.PI * 2 / 8);
        public const float FirstPeak = .196f + Period;
        private string _id;
        private float _time;
        private double _cycle = -1;
        private bool _initialized, _playing;
        public void Reset()
        {
            _id = null; _time = 0; _cycle = -1; _initialized = _playing = false;
        }
        public bool Tick(string id, float motionTime, bool playing)
        {
            if (string.IsNullOrWhiteSpace(id) || float.IsNaN(motionTime) || float.IsInfinity(motionTime) || motionTime < 0)
            {
                Reset(); return false;
            }
            double cycle = motionTime < FirstPeak ? -1 : Math.Floor(((double)motionTime - FirstPeak) / Period);
            if (!_initialized || !string.Equals(_id, id, StringComparison.Ordinal) || motionTime < _time)
            {
                _id = id; _time = motionTime; _cycle = cycle; _initialized = true; _playing = playing;
                return false;
            }
            float elapsed = motionTime - _time;
            bool fire = playing && _playing && elapsed > 0 && elapsed <= .25f && cycle >= 0 && cycle == _cycle + 1;
            // Suppressed frames consume their cycle; resuming only permits a future peak.
            _time = motionTime; _cycle = cycle; _playing = playing;
            return fire;
        }
    }
}
