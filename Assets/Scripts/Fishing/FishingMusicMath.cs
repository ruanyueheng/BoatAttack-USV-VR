using System;

namespace BoatAttack.Fishing
{
    public readonly struct FishingMusicLoopWindow
    {
        public readonly float End, StartLead, Crossfade;
        internal FishingMusicLoopWindow(float end, float crossfade)
        { End = end; Crossfade = crossfade; StartLead = end - crossfade; }
    }

    /// <summary>Four captured channel amplitudes. User volume and dialogue ducking are applied separately.</summary>
    public readonly struct FishingMusicGains
    {
        public readonly float A, B, C, D;
        public FishingMusicGains(float a, float b, float c, float d) { A = a; B = b; C = c; D = d; }
        public float At(int channel) => channel == 0 ? A : channel == 1 ? B : channel == 2 ? C : channel == 3 ? D : 0;
        public float Power => A * A + B * B + C * C + D * D;
    }

    public static class FishingMusicMath
    {
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Phase(float phase) => !Finite(phase) ? 0 : Math.Max(0, Math.Min(1, phase));
        private static float Clean(float value) => Finite(value) && value > 0 ? value : 0;
        public static float EqualPowerIn(float phase)
        {
            phase = Phase(phase);
            return phase == 0 ? 0 : phase == 1 ? 1 : (float)Math.Sin(phase * Math.PI * .5);
        }
        public static float EqualPowerOut(float phase)
        {
            phase = Phase(phase);
            return phase == 0 ? 1 : phase == 1 ? 0 : (float)Math.Cos(phase * Math.PI * .5);
        }

        /// <summary>Rejects unusable inputs; short tracks keep a positive hold section between two fades.</summary>
        public static bool TryLoopWindow(float clipLength, float tailCutSeconds, float crossfadeSeconds, out FishingMusicLoopWindow window)
        {
            window = default;
            if (!Finite(clipLength) || !Finite(tailCutSeconds) || !Finite(crossfadeSeconds)
                || clipLength <= 0 || tailCutSeconds < 0 || crossfadeSeconds <= 0 || tailCutSeconds >= clipLength) return false;
            float end = clipLength - tailCutSeconds;
            if (end < .25f) return false;
            float fade = Math.Min(crossfadeSeconds, end * .45f);
            float lead = end - fade;
            if (fade <= 0 || lead <= 0 || lead >= end) return false;
            window = new FishingMusicLoopWindow(end, fade); return true;
        }

        public static FishingMusicGains NormalizePower(FishingMusicGains gains)
        {
            double a = Clean(gains.A), b = Clean(gains.B), c = Clean(gains.C), d = Clean(gains.D);
            double length = Math.Sqrt(a * a + b * b + c * c + d * d);
            return length <= 0 ? default : new FishingMusicGains((float)(a / length), (float)(b / length), (float)(c / length), (float)(d / length));
        }

        /// <summary>Blend from a captured frame to channel 0..3, or -1 for silence. Re-capture on redirection.</summary>
        public static FishingMusicGains Blend(FishingMusicGains captured, int targetChannel, float phase)
        {
            var from = new FishingMusicGains(Clean(captured.A), Clean(captured.B), Clean(captured.C), Clean(captured.D));
            double power = (double)from.A * from.A + (double)from.B * from.B + (double)from.C * from.C + (double)from.D * from.D;
            if (power > 1) { from = NormalizePower(from); power = 1; }
            if (targetChannel < -1 || targetChannel > 3) return from;
            phase = Phase(phase);
            if (phase == 0) return from;
            double fadeOut = EqualPowerOut(phase), fadeIn = EqualPowerIn(phase);
            if (targetChannel == -1)
                return new FishingMusicGains((float)(from.A * fadeOut), (float)(from.B * fadeOut), (float)(from.C * fadeOut), (float)(from.D * fadeOut));
            double a = from.A * fadeOut + (targetChannel == 0 ? fadeIn : 0);
            double b = from.B * fadeOut + (targetChannel == 1 ? fadeIn : 0);
            double c = from.C * fadeOut + (targetChannel == 2 ? fadeIn : 0);
            double d = from.D * fadeOut + (targetChannel == 3 ? fadeIn : 0);
            double actual = a * a + b * b + c * c + d * d;
            // Existing gain in the destination must not add a correlated gain spike on a redirected fade.
            double desired = power * fadeOut * fadeOut + fadeIn * fadeIn;
            double scale = actual > 0 ? Math.Sqrt(desired / actual) : 0;
            return new FishingMusicGains((float)(a * scale), (float)(b * scale), (float)(c * scale), (float)(d * scale));
        }
    }

    /// <summary>A region must remain a candidate for a continuous hold; paused or missing samples cannot confirm it.</summary>
    public sealed class FishingMusicZoneGate
    {
        private double _held;
        public int Current { get; private set; }
        public int Candidate { get; private set; }
        public float HoldSeconds { get; }
        public float PendingSeconds => (float)_held;
        public FishingMusicZoneGate(int initialZone = 0, float holdSeconds = 1.5f)
        {
            HoldSeconds = float.IsNaN(holdSeconds) || float.IsInfinity(holdSeconds) || holdSeconds <= 0 ? 1.5f : holdSeconds;
            Reset(initialZone);
        }
        public void Reset(int zone) { Current = Candidate = zone; _held = 0; }
        public bool Tick(int zone, float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0 || dt > .25f)
            { Candidate = Current; _held = 0; return false; }
            if (zone == Current) { Candidate = Current; _held = 0; return false; }
            if (zone != Candidate) { Candidate = zone; _held = 0; }
            _held = Math.Min(HoldSeconds, _held + dt);
            if (_held + .000001 < HoldSeconds) return false;
            Current = Candidate; _held = 0; return true;
        }
    }
}
