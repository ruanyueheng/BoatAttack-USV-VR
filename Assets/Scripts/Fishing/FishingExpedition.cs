namespace BoatAttack.Fishing
{
    /// <summary>Monotonic expedition facts. No scene, input or Unity dependencies.</summary>
    public sealed class FishingExpedition
    {
        public const int Accepted = 1, RecordRead = 2, ComponentFound = 4;
        public const int BeaconRepaired = 8, TargetCaught = 16, Delivered = 32;
        public const int AllFacts = 63;
        public int Facts { get; private set; }
        public bool Has(int fact) => (Facts & fact) == fact;

        public static bool Valid(int facts)
        {
            if (facts < 0 || (facts & ~AllFacts) != 0) return false;
            if ((facts & BeaconRepaired) != 0 && (facts & 7) != 7) return false;
            if ((facts & TargetCaught) != 0 && (facts & BeaconRepaired) == 0) return false;
            if ((facts & Delivered) != 0 && (facts & TargetCaught) == 0) return false;
            return true;
        }
        public static bool TryRestore(int facts, out FishingExpedition expedition)
        {
            expedition = null;
            if (!Valid(facts)) return false;
            expedition = new FishingExpedition { Facts = facts };
            return true;
        }
        private bool Add(int fact)
        {
            if (Has(fact)) return false;
            Facts |= fact;
            return true;
        }
        public bool Accept() => Add(Accepted);
        public bool ReadRecord() => Add(RecordRead);
        public bool FindComponent() => Add(ComponentFound);
        public bool RepairBeacon() => Has(Accepted | RecordRead | ComponentFound) && Add(BeaconRepaired);
        // The scene adapter must supply true only for a newly stored catch in the target area/species.
        // Not called by save restoration or by scanning existing inventory.
        public bool RecordCatch(bool newlyStored, bool matchesTarget) =>
            newlyStored && matchesTarget && Has(BeaconRepaired) && Add(TargetCaught);
        public bool Deliver() => Has(TargetCaught) && Add(Delivered);
        public string NextObjective => !Has(Accepted) ? "接受港口委托"
            : !Has(RecordRead) ? "前往西湾阅读维修记录"
            : !Has(ComponentFound) ? "前往南岸寻找组件"
            : !Has(BeaconRepaired) ? "前往旧港修复信标"
            : !Has(TargetCaught) ? "完成北岛海域捕获记录"
            : !Has(Delivered) ? "返港提交捕获记录" : "旧港信标已恢复";
    }
}
