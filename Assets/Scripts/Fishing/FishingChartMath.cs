using System;

namespace BoatAttack.Fishing
{
    /// <summary>Uniform north-up projection. Off-range markers retain their bearing at the rim.</summary>
    public static class FishingChartMath
    {
        public readonly struct Point
        {
            public readonly float X, Y;
            public readonly bool OffRange;
            public Point(float x, float y, bool offRange) { X = x; Y = y; OffRange = offRange; }
        }

        public static Point Project(float x, float z, float playerX, float playerZ, float range, float radius)
        {
            if (!Finite(x) || !Finite(z) || !Finite(playerX) || !Finite(playerZ)
                || !Finite(range) || !Finite(radius) || range <= 0f || radius <= 0f)
                return new Point(0f, 0f, false);
            double dx = (double)x - playerX, dz = (double)z - playerZ;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            double scale = radius / Math.Max(range, distance);
            return new Point((float)(dx * scale), (float)(-dz * scale), distance > range);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
