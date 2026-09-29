namespace BoatAttack.Fishing
{
    /// <summary>Stable save IDs: two field tasks per region. Never reorder existing regions.</summary>
    public static class FishingExploration
    {
        public const int SiteCount = 4;
        public const int TaskMask = 255;
        public const int RewardMask = 15;
        public static string SiteName(int site) => site == 0 ? "西湾科考岸" : site == 1 ? "北岛潮池" : site == 2 ? "东湾旧港" : "南岸漂流湾";
        public static string TaskName(int task)
        {
            switch (task)
            {
                case 0: return "采集岸边水样";
                case 1: return "记录岩层剖面";
                case 2: return "调查潮池生物";
                case 3: return "记录候鸟栖地";
                case 4: return "测绘旧港栈道";
                case 5: return "修复海岸信标";
                case 6: return "回收漂流物";
                default: return "登记海滩样带";
            }
        }
        public static bool Done(int mask, int task) => task >= 0 && task < 8 && (mask & (1 << task)) != 0;
        public static bool SiteDone(int mask, int site) => site >= 0 && site < SiteCount && (mask & (3 << (site * 2))) == (3 << (site * 2));
        public static int CompletedTasks(int mask)
        {
            int count = 0;
            for (int i = 0; i < 8; i++) if (Done(mask, i)) count++;
            return count;
        }
        public static bool Valid(int tasks, int rewards)
        {
            if (tasks < 0 || tasks > TaskMask || rewards < 0 || rewards > RewardMask) return false;
            for (int i = 0; i < SiteCount; i++) if ((rewards & (1 << i)) != 0 && !SiteDone(tasks, i)) return false;
            return true;
        }
    }
}
