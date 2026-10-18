using System;

namespace BoatAttack.Fishing
{
    /// <summary>Deterministic, Unity-free rod control. No rewards, clocks or persistent state live here.</summary>
    public static class FishingFightControl
    {
        public const float DeadZone = .12f;

        public static float NormalizeSide(float side)
        {
            if (!Finite(side) || Math.Abs(side) <= DeadZone) return 0;
            return Math.Max(-1f, Math.Min(1f, side));
        }

        /// <summary>Fish left (-1) / right (+1), gently revealed before its first lateral run.</summary>
        public static float Direction(float fightSeconds, float beatSeconds, uint seed)
        {
            if (!Finite(fightSeconds) || !Finite(beatSeconds) || fightSeconds <= 0 || beatSeconds <= 0) return 0;
            double phase = (seed & 65535) / 65535.0 * Math.PI * 2;
            double wave = Math.Sin(fightSeconds / beatSeconds * Math.PI * 1.35 + phase);
            // The first half-second is a readable lean, never an instantaneous side change at hook-up.
            return (float)(wave * Math.Min(1, fightSeconds / .65));
        }

        /// <summary>+1 counters the fish, -1 follows its pull, 0 is the original neutral strategy.</summary>
        public static float Quality(float rodSide, float fishSide)
        {
            float side = NormalizeSide(rodSide);
            if (side == 0 || !Finite(fishSide)) return 0;
            return -side * Math.Max(-1f, Math.Min(1f, fishSide / .25f));
        }

        public static float TensionMultiplier(float quality)
            => quality >= 0 ? 1 - .25f * quality : 1 - .30f * quality;
        public static float RetrieveMultiplier(float quality)
            => quality >= 0 ? 1 + .30f * quality : 1 + .25f * quality;

        /// <summary>A prompt but deliberate lift scores best; the existing full bite window remains valid.</summary>
        public static float StrikeQuality(float biteSeconds)
        {
            if (!Finite(biteSeconds) || biteSeconds < 0) return 0;
            if (biteSeconds < .18f) return .78f + biteSeconds / .18f * .22f;
            if (biteSeconds <= .60f) return 1;
            return Math.Max(0, 1 - (biteSeconds - .60f) / 1.9f);
        }

        /// <summary>Small pecks in the last waiting second, not an early permission to strike.</summary>
        public static float BitePreview(float elapsed, float duration)
        {
            if (!Finite(elapsed) || !Finite(duration) || duration <= 0) return 0;
            float previewSeconds = Math.Min(1.1f, duration * .45f);
            float progress = Math.Max(0, Math.Min(1, 1 - (duration - elapsed) / previewSeconds));
            double pulse = Math.Sin(progress * Math.PI * 2.5);
            return progress * (float)(.35 + .65 * pulse * pulse);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
