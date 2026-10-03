using System;
using BoatAttack.Fishing;

internal static class MapTests
{
    internal static void Run(Action<bool, string> check)
    {
        var bounds = new FishingMapMath.BoundsXZ(-300, -80, 700, 420);
        check(bounds.Valid && Near(bounds.Width, 1000) && Near(bounds.Height, 500), "Map non-square coverage retains independent measured extents");
        check(FishingMapMath.TryWorldToUV(bounds, 200, 170, out double u, out double v) && Near(u, .5) && Near(v, .5), "Map coverage center is UV center");
        var corners = new double[,] { { -300, -80, 0, 0 }, { 700, -80, 1, 0 }, { -300, 420, 0, 1 }, { 700, 420, 1, 1 } };
        for (int i = 0; i < 4; i++)
        {
            check(FishingMapMath.TryWorldToUV(bounds, corners[i, 0], corners[i, 1], out u, out v)
                && Near(u, corners[i, 2]) && Near(v, corners[i, 3]) && FishingMapMath.KnownUV(u, v), "Map corner " + i + " has north-up UV without a half-texel offset");
            check(FishingMapMath.TryUVToWorld(bounds, corners[i, 2], corners[i, 3], out double x, out double z)
                && Near(x, corners[i, 0]) && Near(z, corners[i, 1]), "Map corner " + i + " inverse retains world position");
        }
        bool roundTrip = true;
        for (double x = -800; x <= 1200; x += 37)
            for (double z = -400; z <= 800; z += 23)
            {
                roundTrip &= FishingMapMath.TryWorldToUV(bounds, x, z, out u, out v);
                roundTrip &= FishingMapMath.TryUVToWorld(bounds, u, v, out double restoredX, out double restoredZ)
                    && Near(x, restoredX) && Near(z, restoredZ);
            }
        check(roundTrip, "Map world-UV round trips inside AND outside non-square coverage");
        check(FishingMapMath.TryWorldToUV(bounds, 800, -180, out u, out v) && Near(u, 1.1) && Near(v, -.2)
            && !FishingMapMath.KnownUV(u, v), "Map outside geography remains outside instead of stretching coastlines");
        check(!FishingMapMath.KnownUV(-.001, .5) && !FishingMapMath.KnownUV(1.001, .5)
            && !FishingMapMath.KnownUV(.5, -.001) && !FishingMapMath.KnownUV(.5, 1.001), "Map all four outside edges are unknown");
        check(!FishingMapMath.KnownUV(double.NaN, .5) && !FishingMapMath.KnownUV(.5, double.PositiveInfinity), "Map invalid UV is never known coverage");
        var invalidBounds = new[] {
            new FishingMapMath.BoundsXZ(0, 0, 0, 1), new FishingMapMath.BoundsXZ(0, 1, 1, 1),
            new FishingMapMath.BoundsXZ(1, 0, 0, 1), new FishingMapMath.BoundsXZ(double.NaN, 0, 1, 1),
            new FishingMapMath.BoundsXZ(0, 0, double.PositiveInfinity, 1),
            new FishingMapMath.BoundsXZ(-double.MaxValue, 0, double.MaxValue, 1) };
        foreach (var bad in invalidBounds)
            check(!bad.Valid && !FishingMapMath.TryWorldToUV(bad, 0, 0, out u, out v)
                && !FishingMapMath.TryUVToWorld(bad, .5, .5, out _, out _), "Map rejects zero/reversed/nonfinite/overflowing bounds");
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            check(!FishingMapMath.TryWorldToUV(bounds, bad, 0, out u, out v) && u == 0 && v == 0
                && !FishingMapMath.TryWorldToUV(bounds, 0, bad, out _, out _), "Map rejects nonfinite world positions with safe outputs");
            check(!FishingMapMath.TryUVToWorld(bounds, bad, 0, out _, out _)
                && !FishingMapMath.TryUVToWorld(bounds, 0, bad, out _, out _), "Map rejects nonfinite UV inputs");
        }
        check(FishingMapMath.TryViewUV(bounds, 200, 170, 80, out var view)
            && Near(view.MinU, .42) && Near(view.MinV, .34) && Near(view.Width, .16) && Near(view.Height, .32), "Map view encodes a square metre footprint on a non-square texture");
        check(FishingMapMath.TryViewUV(bounds, 700, 420, 80, out view)
            && view.MinU + view.Width > 1 && view.MinV + view.Height > 1, "Map explorer at capture corner preserves unknown view area");
        check(FishingMapMath.TryViewUV(bounds, 1800, -700, 80, out view)
            && view.MinU > 1 && view.MinV + view.Height < 0, "Map explorer outside capture does not clamp back into geography");
        foreach (double bad in new[] { 0, -1, double.NaN, double.PositiveInfinity })
        {
            check(!FishingMapMath.TryViewUV(bounds, 0, 0, bad, out _), "Map rejects invalid view range");
            check(!FishingMapMath.TryProject(1, 1, 0, 0, bad, 96, out _, out _)
                && !FishingMapMath.TryProject(1, 1, 0, 0, 80, bad, out _, out _), "Map rejects invalid project range/radius");
            check(FishingMapMath.ScaleMeters(bad) == 0, "Map scale bar rejects invalid range");
        }
        bool sameScale = true;
        foreach (int range in new[] { 20, 40, 80, 160, 320 })
        {
            FishingMapMath.TryViewUV(bounds, 30, 70, range, out view);
            double wx = 30 + range * .25, wz = 70 + range * .5;
            FishingMapMath.TryWorldToUV(bounds, wx, wz, out u, out v);
            double textureX = ((u - view.MinU) / view.Width - .5) * 192;
            double textureY = (.5 - (v - view.MinV) / view.Height) * 192;
            var marker = FishingChartMath.Project((float)wx, (float)wz, 30, 70, range, 96);
            FishingMapMath.TryProject(wx, wz, 30, 70, range, 96, out double px, out double py);
            sameScale &= Near(textureX, marker.X) && Near(textureY, marker.Y)
                && Near(px, marker.X) && Near(py, marker.Y) && !marker.OffRange;
            double scale = FishingMapMath.ScaleMeters(range);
            sameScale &= scale > 0 && scale <= range * .5;
        }
        check(sameScale, "Map texture, unclamped geography and chart markers share 96px metric radius at every shore/boat zoom");
        check(FishingMapMath.TryProject(320, 160, 0, 0, 160, 96, out double sx, out double sy)
            && Near(sx, 192) && Near(sy, -96), "Map geography outside radius stays outside; only bearing badges may clamp");
        check(!FishingMapMath.TryProject(double.NaN, 0, 0, 0, 80, 96, out _, out _)
            && !FishingMapMath.TryProject(0, 0, double.PositiveInfinity, 0, 80, 96, out _, out _), "Map projection rejects invalid centers/positions");
        check(FishingMapMath.TryProject(0, 80, 0, 0, 80, 96, out sx, out sy) && sx == 0 && sy == -96,
            "Map north is screen up while texture north is positive V");
        check(Near(FishingMapMath.ScaleMeters(20), 10) && Near(FishingMapMath.ScaleMeters(40), 20)
            && Near(FishingMapMath.ScaleMeters(80), 20) && Near(FishingMapMath.ScaleMeters(160), 50)
            && Near(FishingMapMath.ScaleMeters(320), 100), "Map scale bar uses stable 1-2-5 metre lengths");
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < .00001;
}
