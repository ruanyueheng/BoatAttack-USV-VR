using System;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCareer
    {
        public const int GrandTreasureCoins = 1200;
        public bool PirateKing => Lore.GrandTreasureFound;
        public string PlayerTitle => PirateKing ? "海贼王" : Lore.MayorAccepted ? "回潮村村长" : "返乡旅人";
        public bool AcceptMayorship() => Lore.AcceptMayorship();
        public bool DiscoverChart() => Lore.DiscoverChart();

        public string MainObjective
        {
            get
            {
                if (!Lore.MayorAccepted) return "与港口林舟交谈，接任回潮村村长。";
                if (!Lore.ChartFound) return "回到家中航线书桌，寻找村里留下的秘密旧航海图。";
                if (Lore.ClearCount < 4) return "循旧航海图探索风、林、潮、星四境，收集失落图页（" + Lore.ClearCount + " / 4）。";
                for (int i = 0; i < 4; i++)
                    if (!Lore.RewardClaimed(i)) return "领取" + FishingLore.ShrineName(i) + "的遗物与设施蓝图；可回秘境或向守望者补领。";
                if (PirateKing) return "大航海宝藏已找到。以海贼王之名，继续建设村庄、收藏与远航。";
                if (Backpack.Count(FieldItem.VoyageCompass) > 0) return "带上远航罗盘，前往白帆拱湾小岛内侧的隐藏宝藏台。";
                if (HomeStorage.Count(FieldItem.VoyageCompass) > 0) return "从家园储物箱取回远航罗盘，再前往白帆拱湾的隐藏宝藏台。";
                return "在工作台用四境遗物、铁锭 ×2、玻璃 ×2 组装远航罗盘。";
            }
        }

        public static FieldStack[] ShrineReward(int island)
        {
            var relic = FishingLore.Relic(island);
            if (relic == FieldItem.Empty) return Array.Empty<FieldStack>();
            var material = island == 0 ? FieldItem.Rope : island == 1 ? FieldItem.Plank : island == 2 ? FieldItem.IronIngot : FieldItem.Glass;
            return new[] { FishingRecipes.Stack(relic, 1), FishingRecipes.Stack(material, island == 0 || island == 2 ? 4 : 6) };
        }

        public bool ClaimShrineReward(int island)
        {
            if (!Lore.ChartFound || !Lore.Cleared(island) || Lore.RewardClaimed(island)) return false;
            if (!Backpack.TryAddBundle(ShrineReward(island))) return false;
            Lore.MarkRewardClaimed(island);
            return true;
        }

        public bool CraftVoyageCompass(CraftStation station)
        {
            if (!Lore.ChartFound || Lore.RewardedShrines != 15 || PirateKing
                || Backpack.Count(FieldItem.VoyageCompass) + HomeStorage.Count(FieldItem.VoyageCompass) > 0) return false;
            return FishingRecipes.Craft(Backpack, "voyage-compass", station);
        }

        public int OpenGrandTreasure()
        {
            if (!Lore.CanOpenGrandTreasure || Coins > 100000000 - GrandTreasureCoins
                || !Backpack.Exchange(new[] { FishingRecipes.Stack(FieldItem.VoyageCompass, 1) }, FieldItem.Empty, 0)) return 0;
            Lore.OpenGrandTreasure();
            Coins += GrandTreasureCoins;
            return GrandTreasureCoins;
        }

        private bool RestoreVoyageQuest(int version)
        {
            if (version < 16)
            {
                for (int i = 0; i < 5; i++)
                    if (QuestCount((FieldItem)((int)FieldItem.WindRelic + i)) != 0) return false;
                var adventure = Adventure.Snapshot();
                if (PrologueSeen || Lore.Snapshot().residents != 0 || Lore.ClearCount > 0
                    || adventure.visited != 0 || adventure.beacons != 0 || adventure.treasures != 0 || adventure.finale)
                {
                    Lore.AcceptMayorship();
                    Lore.DiscoverChart();
                }
                return true;
            }
            int compass = QuestCount(FieldItem.VoyageCompass);
            if (compass > 1 || (compass > 0 && (!Lore.ChartFound || Lore.RewardedShrines != 15 || PirateKing))) return false;
            bool consumedRelics = compass == 1 || PirateKing;
            for (int island = 0; island < 4; island++)
                if (QuestCount(FishingLore.Relic(island)) != (Lore.RewardClaimed(island) && !consumedRelics ? 1 : 0)) return false;
            return true;
        }

        private int QuestCount(FieldItem item) => Backpack.Count(item) + HomeStorage.Count(item);
    }
}
