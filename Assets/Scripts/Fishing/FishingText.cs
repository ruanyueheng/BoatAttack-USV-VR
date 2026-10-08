using System.Globalization;

namespace BoatAttack.Fishing
{
    public enum CatchFailure { None, MissedBite, LineSnapped, LeapSnapped, SlackLine, SwamAway, LandingTimeout, NetMissed }

    /// <summary>
    /// Player-facing wording. The catch rules report failure codes; this desktop wording names mouse
    /// and keyboard controls, so a future XR adapter replaces only this layer.
    /// </summary>
    public static class FishingText
    {
        public static string Failure(CatchFailure failure)
        {
            switch (failure)
            {
                case CatchFailure.MissedBite: return "错过咬钩时机：看到提示后点击左键。";
                case CatchFailure.LineSnapped: return "鱼线绷断：红色张力过高时松开鼠标。";
                case CatchFailure.LeapSnapped: return "旗鱼跃水时仍在收线，鱼线被扯断：看到“跃出水面”立即松开。";
                case CatchFailure.SlackLine: return "鱼挣脱了：不要连续放线太久。";
                case CatchFailure.SwamAway: return "鱼游远了：收线与放线交替进行。";
                case CatchFailure.LandingTimeout: return "没有及时抄鱼：指针进入绿色区时按 E。";
                case CatchFailure.NetMissed: return "抄网落空三次：看准绿色区域再按 E。";
                default: return string.Empty;
            }
        }

        public static string Weight(int grams)
            => grams <= 0 ? "—" : (grams / 1000m).ToString(grams >= 10000 ? "0.0" : "0.00#", CultureInfo.InvariantCulture) + " kg";

        public static string UpgradeName(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Reel: return "卷线器";
                case UpgradeTrack.Hold: return "鱼箱";
                default: return "鱼线";
            }
        }

        public static string ShopProductName(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Line: return "强化鱼线 / 钓竿套装";
                case UpgradeTrack.Reel: return "精密卷线器";
                case UpgradeTrack.Hold: return "扩容鱼箱";
                default: return string.Empty;
            }
        }

        public static string ShopProductDescription(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Line: return "沿用现有钓竿，换装抗拉鱼线，降低斗鱼张力。";
                case UpgradeTrack.Reel: return "升级现有卷线器，提高收线速度。";
                case UpgradeTrack.Hold: return "升级船上鱼箱，增加鱼获储存容量。";
                default: return string.Empty;
            }
        }
        public static string UpgradeEffect(UpgradeTrack track, int level)
        {
            switch (track)
            {
                case UpgradeTrack.Reel: return "收线速度 +" + (20 * level) + "%";
                case UpgradeTrack.Hold: return "容量 " + (FishingCareer.BaseCapacity + 2 * level) + " 条";
                default: return level == 0 ? "标准张力" : "张力增长 -" + (level == 1 ? 28 : 42) + "%";
            }
        }
    }
}
