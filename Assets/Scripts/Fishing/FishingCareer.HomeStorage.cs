namespace BoatAttack.Fishing
{
    public sealed partial class FishingCareer
    {
        public const int HomeFruitTreeCount=5;
        public FishingBackpack HomeStorage {get;private set;}=new FishingBackpack(false,FishingBackpack.HomeStorageSlots);
        private int[] _homeFruitDays={-1,-1,-1,-1,-1};
        public bool CanHarvestHomeFruit(int tree)=>tree>=0 && tree<HomeFruitTreeCount && _homeFruitDays[tree]<Calendar.Day;
        public bool TryHarvestHomeFruit(int tree)
        {
            if(!CanHarvestHomeFruit(tree) || !Backpack.TryAdd(FieldItem.IslandFruit,1))return false;
            _homeFruitDays[tree]=Calendar.Day;return true;
        }
        private bool RestoreHomeStorage(FishingSaveData data)
        {
            if(data.version<15)
            {
                if(data.homeStorage!=null)
                    foreach(var stack in data.homeStorage)if(stack==null || stack.item!=FieldItem.Empty || stack.count!=0)return false;
                if(data.homeFruitDays!=null)foreach(int day in data.homeFruitDays)if(day!=-1)return false;
                if(data.backpack!=null)foreach(var stack in data.backpack)if(stack!=null && stack.item==FieldItem.IslandFruit)return false;
                return true;
            }
            var slots=data.homeStorage;
            if(data.version<21)
            {
                if(slots==null || slots.Count!=FishingBackpack.SlotCount)return false;
                slots=new System.Collections.Generic.List<FieldStack>(slots);
                while(slots.Count<FishingBackpack.HomeStorageSlots)slots.Add(new FieldStack());
            }
            if(!FishingBackpack.TryRestore(slots,0,0,0,out var storage,FishingBackpack.HomeStorageSlots)
                || data.homeFruitDays==null || data.homeFruitDays.Length!=HomeFruitTreeCount)return false;
            foreach(int day in data.homeFruitDays)if(day< -1 || day>Calendar.Day)return false;
            HomeStorage=storage;_homeFruitDays=(int[])data.homeFruitDays.Clone();return true;
        }
    }
}
