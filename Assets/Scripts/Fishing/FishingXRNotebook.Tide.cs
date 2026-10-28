namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        /// <summary>Calendar-page lines for the tide timetable, low-tide finds and lamp watch (Docs/LIGHTHOUSE_TIDE_V044.md).</summary>
        internal string TideStatus()
        {
            var lore = _c.Career.Lore; int finds = 0;
            for (int i = 0; i < FishingTide.ItemCount; i++) if (lore.TideFlag(i)) finds++;
            return "\n今日潮汐 " + FishingTide.Today(_c.Career.Calendar) + "\n潮间图鉴 " + finds + " / " + FishingTide.ItemCount
                + (lore.GrandTreasureFound ? " · 守灯 " + lore.KeeperNights + " 夜" : "");
        }
    }
}
