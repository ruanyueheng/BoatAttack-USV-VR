using System;

namespace BoatAttack.Fishing
{
    /// <summary>North-up metric mapping. UV origin is south-west; no method clamps geography.</summary>
    public static class FishingMapMath
    {
        public readonly struct BoundsXZ
        {
            public readonly double MinX, MinZ, MaxX, MaxZ;
            public BoundsXZ(double minX, double minZ, double maxX, double maxZ)
            { MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; }
            public double Width => MaxX - MinX;
            public double Height => MaxZ - MinZ;
            public bool Valid => Finite(MinX) && Finite(MinZ) && Finite(MaxX) && Finite(MaxZ)
                && Finite(Width) && Finite(Height) && Width > 0 && Height > 0;
        }

        public readonly struct ViewUV
        {
            public readonly double MinU, MinV, Width, Height;
            public ViewUV(double minU, double minV, double width, double height)
            { MinU = minU; MinV = minV; Width = width; Height = height; }
        }

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool KnownUV(double u, double v)
            => Finite(u) && Finite(v) && u >= 0 && v >= 0 && u <= 1 && v <= 1;

        public static bool TryWorldToUV(BoundsXZ bounds, double x, double z, out double u, out double v)
        {
            u = v = 0;
            if (!bounds.Valid || !Finite(x) || !Finite(z)) return false;
            double a = (x - bounds.MinX) / bounds.Width, b = (z - bounds.MinZ) / bounds.Height;
            if (!Finite(a) || !Finite(b)) return false;
            u = a; v = b; return true;
        }

        public static bool TryUVToWorld(BoundsXZ bounds, double u, double v, out double x, out double z)
        {
            x = z = 0;
            if (!bounds.Valid || !Finite(u) || !Finite(v)) return false;
            double a = bounds.MinX + u * bounds.Width, b = bounds.MinZ + v * bounds.Height;
            if (!Finite(a) || !Finite(b)) return false;
            x = a; z = b; return true;
        }

        /// <summary>Map a full local [0,1] square to world UV. Local (0.5,0.5) is the explorer.
        /// The circle's radius, not its diameter, is rangeMeters. Out-of-bounds UV remains unknown.</summary>
        public static bool TryViewUV(BoundsXZ bounds, double x, double z, double rangeMeters, out ViewUV view)
        {
            view = default;
            if (!Finite(rangeMeters) || rangeMeters <= 0 || !TryWorldToUV(bounds, x, z, out double u, out double v)) return false;
            double w = 2 * rangeMeters / bounds.Width, h = 2 * rangeMeters / bounds.Height;
            double minU = u - w * .5, minV = v - h * .5;
            if (!Finite(w) || !Finite(h) || !Finite(minU) || !Finite(minV) || w <= 0 || h <= 0) return false;
            view = new ViewUV(minU, minV, w, h); return true;
        }

        /// <summary>Unclamped screen offset (screen Y points south). Use ChartMath only for rim bearing badges.</summary>
        public static bool TryProject(double x, double z, double centerX, double centerZ, double rangeMeters,
            double radiusPixels, out double screenX, out double screenY)
        {
            screenX = screenY = 0;
            if (!Finite(x) || !Finite(z) || !Finite(centerX) || !Finite(centerZ)
                || !Finite(rangeMeters) || !Finite(radiusPixels) || rangeMeters <= 0 || radiusPixels <= 0) return false;
            double px = (x - centerX) / rangeMeters * radiusPixels;
            double py = (centerZ - z) / rangeMeters * radiusPixels;
            if (!Finite(px) || !Finite(py)) return false;
            screenX = px; screenY = py; return true;
        }

        /// <summary>A 1/2/5 metric scale no longer than half the map radius.</summary>
        public static double ScaleMeters(double rangeMeters)
        {
            if (!Finite(rangeMeters) || rangeMeters <= 0) return 0;
            double target = rangeMeters * .5;
            double unit = Math.Pow(10, Math.Floor(Math.Log10(target)));
            if (!Finite(unit) || unit <= 0) return 0;
            double normalized = target / unit;
            return unit * (normalized >= 5 ? 5 : normalized >= 2 ? 2 : 1);
        }
    }
}
