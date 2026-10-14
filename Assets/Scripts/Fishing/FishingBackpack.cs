using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    // Mainline IDs 0..25 are stable; local v8 gun IDs are migrated when loading.
    public enum FieldItem { Empty, FieldGuide, Binoculars, Lantern, Shell, Driftwood, Stone, Fiber, Clay, IronOre, Coal, Sand, Plank, Stick, Rope, Charcoal, IronIngot, Brick, Glass, Workbench, Furnace, StonePickaxe, IronPickaxe, HuntingSpear, RawMeat, CookedMeat, HuntingRifle, Meat, Fur, RabbitMeat, RabbitFur, FoxMeat, FoxFur, TurtleMeat, TurtleShell, GullMeat, GullFeather, Forge }
    public enum LandAnimalKind { Rabbit, Fox, Tortoise, Gull, SeaTurtle }
    [Serializable]
    public sealed class FieldStack
    {
        public FieldItem item;
        public int count;
        public FieldStack Copy() => new FieldStack { item=item, count=count };
    }
    /// <summary>36 bounded slots. Mutations are transactional; world pickups cannot be duplicated by reloading.</summary>
    public sealed class FishingBackpack
    {
        public const int HotbarCount=9, SlotCount=36, PickupCount=8;
        private readonly FieldStack[] _slots=new FieldStack[SlotCount];
        public int Selected { get; private set; }
        public int CollectedMask { get; private set; }
        public int AnimalMask { get; private set; }
        public FieldItem Equipped => _slots[Selected].item;
        public int UsedSlots { get { int n=0;foreach(var s in _slots)if(s.count>0)n++;return n; } }
        public FishingBackpack(bool starter=true)
        {
            for(int i=0;i<SlotCount;i++)_slots[i]=new FieldStack();
            if(starter)_slots[0]=new FieldStack {item=FieldItem.FieldGuide,count=1};
        }
        public static bool Defined(FieldItem item) => item>=FieldItem.Empty && item<=FieldItem.Forge;
        public static int Limit(FieldItem item) => !Defined(item) || item==FieldItem.Empty?0:item==FieldItem.Forge || item==FieldItem.HuntingRifle || item==FieldItem.HuntingSpear || item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe || item==FieldItem.Workbench || item==FieldItem.Furnace?1:item>=FieldItem.Shell?64:1;
        public static string Name(FieldItem item)
        {
            switch(item)
            {
                case FieldItem.HuntingRifle:return "猎枪";
                case FieldItem.Forge:return "锻造台";
                case FieldItem.Meat:return "旧兽肉";case FieldItem.Fur:return "旧毛皮";
                case FieldItem.RabbitMeat:return "兔肉";case FieldItem.RabbitFur:return "兔毛";
                case FieldItem.FoxMeat:return "狐狸肉";case FieldItem.FoxFur:return "狐皮";
                case FieldItem.TurtleMeat:return "海龟肉";case FieldItem.TurtleShell:return "龟壳";
                case FieldItem.GullMeat:return "海鸥肉";case FieldItem.GullFeather:return "海鸥羽毛";
            }
            switch(item){case FieldItem.FieldGuide:return "自然手册";case FieldItem.Binoculars:return "望远镜";case FieldItem.Lantern:return "提灯";case FieldItem.Shell:return "海贝";case FieldItem.Driftwood:return "漂流木";case FieldItem.Stone:return "石料";case FieldItem.Fiber:return "植物纤维";case FieldItem.Clay:return "黏土";case FieldItem.IronOre:return "铁矿石";case FieldItem.Coal:return "煤";case FieldItem.Sand:return "砂";case FieldItem.Plank:return "木板";case FieldItem.Stick:return "木棍";case FieldItem.Rope:return "绳索";case FieldItem.Charcoal:return "木炭";case FieldItem.IronIngot:return "铁锭";case FieldItem.Brick:return "砖";case FieldItem.Glass:return "玻璃";case FieldItem.Workbench:return "工作台";case FieldItem.Furnace:return "熔炉";case FieldItem.StonePickaxe:return "石镐";case FieldItem.IronPickaxe:return "铁镐";case FieldItem.HuntingSpear:return "猎矛";case FieldItem.RawMeat:return "生兽肉";case FieldItem.CookedMeat:return "炭烤兽肉";default:return "空槽";}
        }
        public static string AnimalName(LandAnimalKind kind) => kind==LandAnimalKind.Rabbit?"海岛野兔":kind==LandAnimalKind.Fox?"赤狐":kind==LandAnimalKind.Gull?"海鸥":kind==LandAnimalKind.SeaTurtle?"海龟":"陆龟";
        public FieldStack At(int slot) => slot>=0 && slot<SlotCount?_slots[slot].Copy():new FieldStack();
        public bool Select(int slot){if(slot<0 || slot>=HotbarCount)return false;Selected=slot;return true;}
        public bool Collected(int id)=>id>=0 && id<PickupCount && (CollectedMask&(1<<id))!=0;
        public bool Seen(LandAnimalKind kind)=>kind>=0 && kind<=LandAnimalKind.Tortoise && (AnimalMask&(1<<(int)kind))!=0;
        public bool Observe(LandAnimalKind kind)
        {
            if(kind<0 || kind>LandAnimalKind.Tortoise || Seen(kind))return false;
            AnimalMask|=1<<(int)kind;return true;
        }
        public bool TryAdd(FieldItem item,int count)
        {
            int limit=Limit(item);if(limit==0 || count<=0 || count>SlotCount*limit)return false;
            int free=0;foreach(var s in _slots)if(s.item==item || s.count==0)free+=limit-s.count;
            if(free<count)return false;
            // Fill existing stacks first, then empties, keeping selected tools stable.
            for(int pass=0;pass<2;pass++)for(int i=0;i<SlotCount && count>0;i++)
            {
                var s=_slots[i];if(pass==0?s.item!=item:s.count!=0)continue;
                int moved=Math.Min(count,limit-s.count);s.item=item;s.count+=moved;count-=moved;
            }
            return true;
        }
        public int Count(FieldItem item){int n=0;foreach(var slot in _slots)if(slot.item==item)n+=slot.count;return n;}
        public bool TryAddBundle(params FieldStack[] items)
        {
            if(items==null || items.Length==0)return false;
            var next=new FishingBackpack(false);
            for(int i=0;i<SlotCount;i++)next._slots[i]=_slots[i].Copy();
            foreach(var item in items)if(item==null || !next.TryAdd(item.item,item.count))return false;
            for(int i=0;i<SlotCount;i++)_slots[i]=next._slots[i];
            return true;
        }
        public static List<FieldStack> MigrateLocalV8(List<FieldStack> slots,int version)
        {
            if(slots==null || version>8)return slots;
            var result=new List<FieldStack>();
            foreach(var slot in slots)
            {
                if(slot==null){result.Add(null);continue;}
                var copy=slot.Copy();
                if((int)copy.item==23)copy.item=FieldItem.HuntingRifle;
                else if((int)copy.item==24)copy.item=FieldItem.Meat;
                else if((int)copy.item==25)copy.item=FieldItem.Fur;
                result.Add(copy);
            }
            return result;
        }
        /// <summary>Copy, consume, then add; no loss/duplication on missing input or a full output bag.</summary>
        public bool Exchange(FieldStack[] costs,FieldItem output,int count)
        {
            if(costs==null || count<0 || (output==FieldItem.Empty)!=(count==0))return false;
            var next=new FishingBackpack(false);
            for(int i=0;i<SlotCount;i++)next._slots[i]=_slots[i].Copy();
            foreach(var cost in costs)
            {
                if(cost==null || !Defined(cost.item) || cost.item==FieldItem.Empty || cost.count<=0 || next.Count(cost.item)<cost.count)return false;
                int left=cost.count;
                foreach(var slot in next._slots)
                {
                    if(slot.item!=cost.item)continue;int take=Math.Min(left,slot.count);slot.count-=take;left-=take;
                    if(slot.count==0)slot.item=FieldItem.Empty;
                }
            }
            if(count>0 && !next.TryAdd(output,count))return false;
            for(int i=0;i<SlotCount;i++)_slots[i]=next._slots[i];
            return true;
        }
        public bool TryCollect(int id,FieldItem item,int count)
        {
            if(id<0 || id>=PickupCount || Collected(id) || !TryAdd(item,count))return false;
            CollectedMask|=1<<id;return true;
        }
        public bool Move(int from,int to,bool half=false)
        {
            if(from<0 || from>=SlotCount || to<0 || to>=SlotCount || from==to || _slots[from].count==0)return false;
            var a=_slots[from];var b=_slots[to];
            if(b.count>0 && b.item!=a.item)
            {if(half)return false;_slots[from]=b;_slots[to]=a;return true;}
            int n=Math.Min(half?(a.count+1)/2:a.count,Limit(a.item)-b.count);
            if(n<=0)return false;b.item=a.item;b.count+=n;a.count-=n;if(a.count==0)a.item=FieldItem.Empty;return true;
        }
        public bool Discard(int slot,FieldItem expected,int count)
        {
            if(slot<0 || slot>=SlotCount || expected==FieldItem.Empty || expected==FieldItem.FieldGuide || count<=0)return false;
            var stack=_slots[slot];if(stack.item!=expected || stack.count!=count)return false;
            stack.item=FieldItem.Empty;stack.count=0;return true;
        }
        public bool QuickMove(int from)
        {
            if(from<0 || from>=SlotCount || _slots[from].count==0)return false;
            int start=from<HotbarCount?HotbarCount:0,end=from<HotbarCount?SlotCount:HotbarCount;
            bool moved=false;
            for(int pass=0;pass<2;pass++)for(int i=start;i<end;i++)
            {
                if(_slots[from].count==0)return moved;
                if(pass==0?_slots[i].item!=_slots[from].item:_slots[i].count!=0)continue;
                moved=Move(from,i)||moved;
            }
            return moved;
        }
        public List<FieldStack> Snapshot(){var result=new List<FieldStack>();foreach(var s in _slots)result.Add(s.Copy());return result;}
        public static bool TryRestore(List<FieldStack> slots,int selected,int pickups,int animals,out FishingBackpack bag)
        {
            bag=null;if(slots==null || slots.Count!=SlotCount || selected<0 || selected>=HotbarCount || pickups<0 || pickups>255 || animals<0 || animals>7)return false;
            var next=new FishingBackpack(false);
            for(int i=0;i<SlotCount;i++)
            {
                var s=slots[i];if(s==null || !Defined(s.item) || s.count<0 || s.count>Limit(s.item) || (s.item==FieldItem.Empty)!=(s.count==0))return false;
                next._slots[i]=s.Copy();
            }
            next.Selected=selected;next.CollectedMask=pickups;next.AnimalMask=animals;bag=next;return true;
        }
    }
}
