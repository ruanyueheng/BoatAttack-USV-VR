using System;

namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class FishingBarbecueData
    {
        public CatchRecord fish;
        public float remaining;
        public FishingBarbecueData Copy()=>new FishingBarbecueData{fish=fish?.Copy(),remaining=remaining};
    }

    public static class FishingCoastRules
    {
        public const float RoastSeconds=18;
        public const int TreasureCoins=80;
        public static string FindQuip(FieldItem item)=>item==FieldItem.OldBoot?"哈哈，原来是鞋子……这条鱼还挺有脚气！"
            :item==FieldItem.RustyCan?"罐头上钩了！可惜海鲜已经下班了。"
            :item==FieldItem.SeaTreasure?"这回真捞着了！海底也有神秘快递。":"海风送来了一份意外收获。";
        public static bool Edible(FishKind kind)=>kind==FishKind.Coral || kind==FishKind.Dart || kind==FishKind.Mackerel
            || kind==FishKind.SeaBream || kind==FishKind.Sardine || kind==FishKind.Grouper || kind==FishKind.Butterfly || kind==FishKind.Tuna;

        // A catch ID owns its surprise roll, so reloading never rerolls the same successful catch.
        public static FieldItem SeaFind(CatchRecord fish,bool alreadyDiscovered)
        {
            if(fish==null || !alreadyDiscovered || fish.rare || !FishingSpecies.IsDefined(fish.kind)
                || FishingSpecies.Of(fish.kind).Legendary || string.IsNullOrWhiteSpace(fish.id))return FieldItem.Empty;
            uint hash=2166136261;
            foreach(char c in fish.id)hash=unchecked((hash^c)*16777619);
            uint roll=hash%1000;
            return roll<30?FieldItem.SeaTreasure:roll<85?FieldItem.OldBoot:roll<140?FieldItem.RustyCan:FieldItem.Empty;
        }
    }

    public sealed partial class FishingCareer
    {
        private FishingBarbecueData _barbecue=new FishingBarbecueData();
        public FishingBarbecueData Barbecue=>_barbecue.Copy();
        public bool BarbecueReady=>_barbecue.fish!=null && _barbecue.remaining<=0;
        public bool CollectSeaFind(CatchRecord source,FieldItem item)
        {
            if(!Valid(source) || HasPendingCatch || _recorded.Contains(source.id) || IsAboard(source.id)
                || FishingCoastRules.SeaFind(source,Discovered(source.kind,source.rare))!=item || item==FieldItem.Empty
                || !Backpack.TryAdd(item,1))return false;
            Remember(source.id);return true;
        }
        public bool OpenSeaTreasure()
        {
            if(Coins>100000000-FishingCoastRules.TreasureCoins
                || !Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.SeaTreasure,1)},FieldItem.Empty,0))return false;
            Coins+=FishingCoastRules.TreasureCoins;return true;
        }
        public bool BeginBarbecue(string id)
        {
            if(_barbecue.fish!=null || HasPendingCatch || string.IsNullOrWhiteSpace(id))return false;
            int index=_inventory.FindIndex(f=>f.id==id);
            if(index<0 || !FishingCoastRules.Edible(_inventory[index].kind))return false;
            _barbecue=new FishingBarbecueData{fish=_inventory[index].Copy(),remaining=FishingCoastRules.RoastSeconds};
            _inventory.RemoveAt(index);return true;
        }
        public bool TickBarbecue(float seconds)
        {
            if(_barbecue.fish==null || _barbecue.remaining<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<=0)return false;
            _barbecue.remaining=Math.Max(0,_barbecue.remaining-Math.Min(seconds,.25f));return BarbecueReady;
        }
        public bool CollectGrilledFish()
        {
            if(!BarbecueReady || !Backpack.TryAdd(FieldItem.GrilledFish,1))return false;
            _barbecue=new FishingBarbecueData();return true;
        }
        private bool RestoreBarbecue(FishingBarbecueData data,int version)
        {
            if(data==null)return version<13;
            if(float.IsNaN(data.remaining) || float.IsInfinity(data.remaining) || data.remaining<0 || data.remaining>FishingCoastRules.RoastSeconds)return false;
            if(data.fish==null || (string.IsNullOrEmpty(data.fish.id) && data.fish.grams==0 && data.fish.value==0
                && data.fish.kind==FishKind.Coral && !data.fish.rare))return data.remaining==0;
            if(version<13 || !Valid(data.fish) || !FishingCoastRules.Edible(data.fish.kind)
                || _inventory.Exists(f=>f.id==data.fish.id) || (_pending!=null && _pending.fish.id==data.fish.id)
                || !_recorded.Contains(data.fish.id) || Catches<Count+LegendCatches+1)return false;
            _barbecue=data.Copy();return true;
        }
    }
}
