using System.Globalization;

namespace BoatAttack.Fishing
{
    public enum CatchFailure { None, MissedBite, LineSnapped, LeapSnapped, SlackLine, SwamAway, LandingTimeout, NetMissed }

    /// <summary>
    /// Player-facing wording. The catch rules report failure codes; this desktop wording names mouse
    /// and keyboard controls, with tracked-controller instructions selected by the same failure code.
    /// </summary>
    public static class FishingText
    {
        public static string Failure(CatchFailure failure, bool tracked = false)
        {
            if (tracked)
                switch (failure)
                {
                    case CatchFailure.MissedBite: return "错过咬钩：提示出现时按右手扳机或 A 提竿。";
                    case CatchFailure.LineSnapped: return "鱼线绷断：张力过高时松开左手握键，暂停摇轮。";
                    case CatchFailure.LeapSnapped: return "旗鱼跃水时仍在收线：跃水时松开左手握键，等落水后再摇轮。";
                    case CatchFailure.LandingTimeout: return "抄鱼超时：左手握住抄网，覆盖鱼获后缓慢提起。";
                    case CatchFailure.NetMissed: return "抄网未接住：将网口移到鱼下方，覆盖鱼获后再缓慢提起。";
                }
            switch (failure)
            {
                case CatchFailure.MissedBite: return "错过咬钩时机：看到提示后点击左键。";
                case CatchFailure.LineSnapped: return "鱼线绷断：红色张力过高时松开鼠标。";
                case CatchFailure.LeapSnapped: return "旗鱼跃水时仍在收线，鱼线被扯断：看到“跃出水面”立即松开。";
                case CatchFailure.SlackLine: return "鱼挣脱了：不要连续放线太久。";
                case CatchFailure.SwamAway: return "鱼游远了：收线与放线交替进行。";
                case CatchFailure.LandingTimeout: return "鱼在船边挣脱了：收至船边后松开左键，在10秒内按 E 抄鱼。";
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
                case UpgradeTrack.Engine: return "船用动力";
                case UpgradeTrack.Rudder: return "精密操舵";
                case UpgradeTrack.Cabin: return "豪华甲板";
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
                case UpgradeTrack.Engine: return "巡航舷外机";
                case UpgradeTrack.Rudder: return "液压操舵系统";
                case UpgradeTrack.Cabin: return "游艇甲板装饰";
                default: return string.Empty;
            }
        }

        public static string ShopProductDescription(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Line: return "沿用现有钓竿，换装抗拉鱼线，降低斗鱼张力。";
                case UpgradeTrack.Reel: return "升级现有卷线器，提高收线速度。";
                case UpgradeTrack.Hold: return "增加鱼获容量；二级载重专精与二级动力、操控互斥。";
                case UpgradeTrack.Engine: return "提升巡航速度；二级动力专精与二级载重、操控互斥。";
                case UpgradeTrack.Rudder: return "更快响应转向；二级操控专精与二级载重、动力互斥。";
                case UpgradeTrack.Cabin: return "黄铜饰条、软垫与柚木；二级增加遮阳顶。";
                default: return string.Empty;
            }
        }
        public static string UpgradeEffect(UpgradeTrack track, int level)
        {
            switch (track)
            {
                case UpgradeTrack.Reel: return "收线速度 +" + (20 * level) + "%";
                case UpgradeTrack.Hold: return "容量 " + (FishingCareer.BaseCapacity + 2 * level) + " 条";
                case UpgradeTrack.Engine: return level==0?"标准巡航动力":"推进力 +"+(level==1?36:73)+"%";
                case UpgradeTrack.Rudder: return "转向响应 +" + (22 * level) + "%";
                case UpgradeTrack.Cabin: return level == 0 ? "实用钓艇" : level == 1 ? "黄铜 / 软垫 / 柚木" : "豪华遮阳甲板";
                default: return level == 0 ? "标准张力" : "张力增长 -" + (level == 1 ? 28 : 42) + "%";
            }
        }
    }
}
