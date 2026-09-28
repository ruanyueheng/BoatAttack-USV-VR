using System;
using BoatAttack.Fishing;

internal static class ChartTests
{
    internal static void Run(Action<bool, string> check)
    {
        var east = FishingChartMath.Project(160, 0, 0, 0, 160, 72);
        var north = FishingChartMath.Project(0, 160, 0, 0, 160, 72);
        check(Near(east.X,72) && Near(east.Y,0) && !east.OffRange, "Chart east uses the full range without distortion");
        check(Near(north.X,0) && Near(north.Y,-72), "Chart north stays at the top");
        var diagonal = FishingChartMath.Project(320, 160, 0, 0, 160, 72);
        check(diagonal.OffRange && Near(diagonal.X,-diagonal.Y*2) && Near(diagonal.X*diagonal.X+diagonal.Y*diagonal.Y,72*72), "Chart off-range marker keeps its bearing on the rim");
        var translated = FishingChartMath.Project(1030, 2040, 1000, 2000, 160, 72);
        var original = FishingChartMath.Project(30,40,0,0,160,72);
        check(Near(translated.X,original.X) && Near(translated.Y,original.Y), "Chart follows the player without changing north");
        var zoom = FishingChartMath.Project(30,40,0,0,320,72);
        check(Near(zoom.X*2,original.X) && Near(zoom.Y*2,original.Y), "Chart range zoom uses one uniform scale");
        var bad = FishingChartMath.Project(float.NaN,0,0,0,160,72);
        check(bad.X==0 && bad.Y==0, "Chart rejects non-finite positions");
        bad = FishingChartMath.Project(10,10,0,0,0,72);
        check(bad.X==0 && bad.Y==0, "Chart rejects invalid ranges");
        var huge = FishingChartMath.Project(float.MaxValue,float.MaxValue,-float.MaxValue,0,160,72);
        check(!float.IsNaN(huge.X) && !float.IsInfinity(huge.Y) && huge.OffRange, "Chart extreme coordinates remain finite");
        bool bounded = true;
        for(int x=-500;x<=500;x+=17) for(int z=-500;z<=500;z+=19)
        {
            var point=FishingChartMath.Project(x,z,20,-30,80,72);
            bounded &= point.X*point.X+point.Y*point.Y <= 72*72+.02f;
        }
        check(bounded, "Chart all sampled markers remain inside the circular map");
    }
    private static bool Near(float a,float b) => Math.Abs(a-b)<.002f;
}
