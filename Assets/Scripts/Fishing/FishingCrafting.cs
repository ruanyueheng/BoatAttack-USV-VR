using System;
using System.Collections.Generic;
namespace BoatAttack.Fishing
{
    public enum CraftStation { Hand, Workbench, Furnace, Forge }
    public sealed class CraftRecipe
    {
        public readonly string Id;
        public readonly CraftStation Station;
        public readonly FieldItem Output;
        public readonly int Count;
        public readonly float Seconds;
        public readonly FieldStack[] Costs;
        public CraftRecipe(string id,CraftStation station,FieldItem output,int count,float seconds,params FieldStack[] costs)
        {Id=id;Station=station;Output=output;Count=count;Seconds=seconds;Costs=costs;}
    }
    public static class FishingRecipes
    {
        public static FieldStack Stack(FieldItem item,int count)=>new FieldStack{item=item,count=count};
        private static CraftRecipe R(string id,CraftStation station,FieldItem output,int count,params FieldStack[] costs)=>new CraftRecipe(id,station,output,count,station==CraftStation.Furnace?8:0,costs);
        // Append-only identifiers are persisted in active furnace jobs. UI always derives from these rules.
        public static readonly CraftRecipe[] All={
            R("hunting-spear",CraftStation.Workbench,FieldItem.HuntingSpear,1,Stack(FieldItem.Stick,3),Stack(FieldItem.Stone,2),Stack(FieldItem.Rope,1)),
            R("roast-meat",CraftStation.Furnace,FieldItem.CookedMeat,1,Stack(FieldItem.RawMeat,1)),
            R("planks",CraftStation.Hand,FieldItem.Plank,4,Stack(FieldItem.Driftwood,1)),
            R("sticks",CraftStation.Hand,FieldItem.Stick,4,Stack(FieldItem.Driftwood,1)),
            R("sticks-from-planks",CraftStation.Hand,FieldItem.Stick,4,Stack(FieldItem.Plank,2)),
            R("rope",CraftStation.Hand,FieldItem.Rope,1,Stack(FieldItem.Fiber,3)),
            R("bench",CraftStation.Hand,FieldItem.Workbench,1,Stack(FieldItem.Plank,4)),
            R("furnace",CraftStation.Workbench,FieldItem.Furnace,1,Stack(FieldItem.Stone,8)),
            R("stone-pick",CraftStation.Workbench,FieldItem.StonePickaxe,1,Stack(FieldItem.Stone,3),Stack(FieldItem.Stick,2),Stack(FieldItem.Rope,1)),
            R("iron-pick",CraftStation.Workbench,FieldItem.IronPickaxe,1,Stack(FieldItem.IronPickHead,1),Stack(FieldItem.Stick,2),Stack(FieldItem.Rope,1)),
            R("lantern",CraftStation.Workbench,FieldItem.Lantern,1,Stack(FieldItem.IronIngot,2),Stack(FieldItem.Glass,2),Stack(FieldItem.Charcoal,1)),
            R("binoculars",CraftStation.Workbench,FieldItem.Binoculars,1,Stack(FieldItem.IronIngot,3),Stack(FieldItem.Glass,4),Stack(FieldItem.Rope,1)),
            R("charcoal",CraftStation.Furnace,FieldItem.Charcoal,1,Stack(FieldItem.Driftwood,1)),
            R("iron",CraftStation.Furnace,FieldItem.IronIngot,1,Stack(FieldItem.IronOre,1)),
            R("brick",CraftStation.Furnace,FieldItem.Brick,1,Stack(FieldItem.Clay,1)),
            R("glass",CraftStation.Furnace,FieldItem.Glass,1,Stack(FieldItem.Sand,1)),
            R("brick-furnace",CraftStation.Workbench,FieldItem.Furnace,1,Stack(FieldItem.Brick,6),Stack(FieldItem.Stone,2)),
            R("shell-lantern",CraftStation.Workbench,FieldItem.Lantern,1,Stack(FieldItem.Shell,6),Stack(FieldItem.Glass,2),Stack(FieldItem.Charcoal,1)),
            R("roast-rabbit",CraftStation.Furnace,FieldItem.CookedMeat,1,Stack(FieldItem.RabbitMeat,1)),
            R("roast-fox",CraftStation.Furnace,FieldItem.CookedMeat,1,Stack(FieldItem.FoxMeat,1)),
            R("roast-turtle",CraftStation.Furnace,FieldItem.CookedMeat,1,Stack(FieldItem.TurtleMeat,1)),
            R("roast-gull",CraftStation.Furnace,FieldItem.CookedMeat,1,Stack(FieldItem.GullMeat,1)),
            R("roast-legacy-meat",CraftStation.Furnace,FieldItem.CookedMeat,1,Stack(FieldItem.Meat,1)),
            R("forge",CraftStation.Workbench,FieldItem.Forge,1,Stack(FieldItem.Stone,8),Stack(FieldItem.Clay,4),Stack(FieldItem.Plank,4)),
            R("iron-pick-head",CraftStation.Forge,FieldItem.IronPickHead,1,Stack(FieldItem.IronIngot,3)),
            R("gun-parts",CraftStation.Forge,FieldItem.GunParts,1,Stack(FieldItem.IronIngot,6)),
            R("hunting-rifle",CraftStation.Workbench,FieldItem.HuntingRifle,1,Stack(FieldItem.GunParts,1),Stack(FieldItem.Plank,2),Stack(FieldItem.Rope,1)),
            R("coal-rabbit-lantern",CraftStation.Workbench,FieldItem.Lantern,1,Stack(FieldItem.Coal,2),Stack(FieldItem.RabbitFur,1)),
            R("coal-fox-lantern",CraftStation.Workbench,FieldItem.Lantern,1,Stack(FieldItem.Coal,2),Stack(FieldItem.FoxFur,1)),
            R("coal-legacy-lantern",CraftStation.Workbench,FieldItem.Lantern,1,Stack(FieldItem.Coal,2),Stack(FieldItem.Fur,1))
        };
        public static readonly CraftRecipe[] HandRecipes=Array.FindAll(All,r=>r.Station==CraftStation.Hand);
        public static readonly CraftRecipe[] WorkbenchRecipes=Array.FindAll(All,r=>r.Station==CraftStation.Workbench);
        public static readonly CraftRecipe[] FurnaceRecipes=MakeFurnacePage();
        public static readonly CraftRecipe[] ForgeRecipes=Array.FindAll(All,r=>r.Station==CraftStation.Forge);
        private static CraftRecipe[] MakeFurnacePage()
        {
            var recipes=Array.FindAll(All,r=>r.Station==CraftStation.Furnace);
            Array.Sort(recipes,(a,b)=>FurnaceOrder(a).CompareTo(FurnaceOrder(b)));return recipes;
        }
        private static int FurnaceOrder(CraftRecipe r)=>r.Id=="iron"?0:r.Id=="charcoal"?1:r.Id=="glass"?2:r.Id=="brick"?3:10+(int)r.Costs[0].item;
        public static CraftRecipe[] ForStation(CraftStation station,bool basic=false)=>basic || station==CraftStation.Hand?HandRecipes:
            station==CraftStation.Workbench?WorkbenchRecipes:station==CraftStation.Furnace?FurnaceRecipes:station==CraftStation.Forge?ForgeRecipes:new CraftRecipe[0];
        public static CraftRecipe Find(string id)
        {
            // These former instant-craft IDs were not furnace jobs. Resolve to the
            // new correct station without keeping duplicate recipes in the UI.
            if(id=="forge-stone-pick")id="stone-pick";
            else if(id=="forge-iron-pick")id="iron-pick";
            else if(id=="forge-iron")id="iron";
            foreach(var r in All)if(r.Id==id)return r;return null;
        }
        public static string StationName(CraftStation s)=>s==CraftStation.Hand?"徒手":s==CraftStation.Workbench?"工作台":s==CraftStation.Forge?"锻造台":"熔炉";
        public static string Purpose(CraftStation s)=>s==CraftStation.Workbench?"装备组装：镐、灯笼、猎枪":s==CraftStation.Furnace?"冶炼烧制：铁锭、木炭、玻璃、砖":s==CraftStation.Forge?"金属塑形：铁镐头、枪械铁件":"随身加工：木板、木棍、绳索";
        public static FieldItem StationItem(CraftStation kind)=>kind==CraftStation.Workbench?FieldItem.Workbench:kind==CraftStation.Furnace?FieldItem.Furnace:kind==CraftStation.Forge?FieldItem.Forge:FieldItem.Empty;
        public static CraftStation StationKind(FieldItem item)=>item==FieldItem.Workbench?CraftStation.Workbench:item==FieldItem.Furnace?CraftStation.Furnace:item==FieldItem.Forge?CraftStation.Forge:CraftStation.Hand;
        public static bool IsStation(FieldItem item)=>StationKind(item)!=CraftStation.Hand;
        public static int FuelUnits(FieldItem fuel)=>fuel==FieldItem.Coal || fuel==FieldItem.Charcoal?4:fuel==FieldItem.Driftwood || fuel==FieldItem.Plank?1:0;
        public static bool CanCraft(FishingBackpack bag,CraftRecipe r,CraftStation station)
        {
            if(bag==null || r==null || r.Station==CraftStation.Furnace || (r.Station!=CraftStation.Hand && station!=r.Station))return false;
            foreach(var cost in r.Costs)if(bag.Count(cost.item)<cost.count)return false;return true;
        }
        public static bool Craft(FishingBackpack bag,string id,CraftStation station)
        {var r=Find(id);return CanCraft(bag,r,station) && bag.Exchange(r.Costs,r.Output,r.Count);}
    }
    [Serializable]
    public sealed class WorkshopStationData
    {
        public int id;
        public CraftStation kind;
        public float x,y,z,yaw;
        public bool permanent;
        public string recipe="";
        public int remaining,ready;
        public float progress;
        public WorkshopStationData Copy()=>(WorkshopStationData)MemberwiseClone();
    }
    [Serializable]
    public sealed class WorkshopData
    {
        public bool initialized;
        public int nextId=1;
        public List<WorkshopStationData> stations=new List<WorkshopStationData>();
        public List<float> resourceCooldowns=new List<float>();
    }
    /// <summary>No Unity dependency. Fuels and input are reserved atomically; finished items remain
    /// in the furnace until explicitly collected. Pausing/offline never advances simulation.</summary>
    public sealed class FishingWorkshop
    {
        public const int MaxStations=32,ResourceCount=64;
        private WorkshopData _data=new WorkshopData();
        public FishingWorkshop(){for(int i=0;i<ResourceCount;i++)_data.resourceCooldowns.Add(0);}
        public bool Initialized=>_data.initialized;
        public void FinishInitialization()=>_data.initialized=true;
        public IEnumerable<WorkshopStationData> Stations { get {foreach(var s in _data.stations)yield return s.Copy();} }
        public WorkshopStationData Station(int id){var s=Find(id);return s==null?null:s.Copy();}
        private WorkshopStationData Find(int id)=>_data.stations.Find(s=>s.id==id);
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        public int Place(FishingBackpack bag,CraftStation kind,float x,float y,float z,float yaw,bool permanent=false)
        {
            if(_data.stations.Count>=MaxStations || _data.nextId>=1000000 || FishingRecipes.StationItem(kind)==FieldItem.Empty
                || !Finite(x)||!Finite(y)||!Finite(z)||!Finite(yaw)||Math.Abs(x)>2000||Math.Abs(z)>2000||y<.1f||y>100)return -1;
            var item=FishingRecipes.StationItem(kind);
            if(!permanent && (bag==null || !bag.Exchange(new[]{FishingRecipes.Stack(item,1)},FieldItem.Empty,0)))return -1;
            int id=_data.nextId++;_data.stations.Add(new WorkshopStationData{id=id,kind=kind,x=x,y=y,z=z,yaw=yaw,permanent=permanent});return id;
        }
        public bool Remove(FishingBackpack bag,int id)
        {
            var s=Find(id);if(s==null || s.permanent || s.remaining>0 || s.ready>0)return false;
            if(!bag.TryAdd(FishingRecipes.StationItem(s.kind),1))return false;
            _data.stations.Remove(s);return true;
        }
        public bool Start(FishingBackpack bag,int id,string recipe,int batches,FieldItem fuel)
        {
            var s=Find(id);var r=FishingRecipes.Find(recipe);int units=FishingRecipes.FuelUnits(fuel);
            if(s==null || s.kind!=CraftStation.Furnace || r==null || r.Station!=CraftStation.Furnace || s.remaining>0 || s.ready>0 || batches<1 || batches>8 || units==0)return false;
            var costs=new List<FieldStack>();foreach(var cost in r.Costs)costs.Add(FishingRecipes.Stack(cost.item,cost.count*batches));
            costs.Add(FishingRecipes.Stack(fuel,(batches+units-1)/units));
            if(!bag.Exchange(costs.ToArray(),FieldItem.Empty,0))return false;
            s.recipe=r.Id;s.remaining=batches;s.progress=0;return true;
        }
        public bool Collect(FishingBackpack bag,int id)
        {
            var s=Find(id);if(s==null || s.ready<=0)return false;
            var r=FishingRecipes.Find(s.recipe);if(r==null || !bag.TryAdd(r.Output,s.ready))return false;
            s.ready=0;if(s.remaining==0){s.recipe="";s.progress=0;}return true;
        }
        public bool Tick(float seconds)
        {
            if(!Finite(seconds)||seconds<=0)return false;seconds=Math.Min(seconds,1);bool changed=false;
            for(int i=0;i<ResourceCount;i++)_data.resourceCooldowns[i]=Math.Max(0,_data.resourceCooldowns[i]-seconds);
            foreach(var s in _data.stations)
            {
                if(s.remaining==0)continue;var r=FishingRecipes.Find(s.recipe);s.progress+=seconds;
                while(s.progress>=r.Seconds && s.remaining>0){s.progress-=r.Seconds;s.remaining--;s.ready+=r.Count;changed=true;}
                if(s.remaining==0)s.progress=0;
            }
            return changed;
        }
        public bool ResourceReady(int id)=>id>=0&&id<ResourceCount&&_data.resourceCooldowns[id]<=0;
        public float ResourceWait(int id)=>id>=0&&id<ResourceCount?_data.resourceCooldowns[id]:0;
        public bool Harvest(FishingBackpack bag,int id,FieldItem item,int amount)
        {
            if(!ResourceReady(id) || amount<1 || amount>8 || item<FieldItem.Shell || item>FieldItem.Sand || !bag.TryAdd(item,amount))return false;
            _data.resourceCooldowns[id]=90;return true;
        }
        public WorkshopData Snapshot()
        {
            var copy=new WorkshopData{initialized=_data.initialized,nextId=_data.nextId};
            foreach(var s in _data.stations)copy.stations.Add(s.Copy());copy.resourceCooldowns=new List<float>(_data.resourceCooldowns);return copy;
        }
        public static bool TryRestore(WorkshopData data,out FishingWorkshop workshop)
        {
            workshop=null;
            if(data==null || data.nextId<1 || data.nextId>1000000 || data.stations==null || data.stations.Count>MaxStations || data.resourceCooldowns==null || data.resourceCooldowns.Count!=ResourceCount)return false;
            var ids=new HashSet<int>();int permanent=0;
            foreach(var c in data.resourceCooldowns)if(!Finite(c)||c<0||c>90)return false;
            foreach(var s in data.stations)
            {
                if(s==null || s.id<1 || s.id>=data.nextId || !ids.Add(s.id) || FishingRecipes.StationItem(s.kind)==FieldItem.Empty
                    || !Finite(s.x)||!Finite(s.y)||!Finite(s.z)||!Finite(s.yaw)||Math.Abs(s.x)>2000||Math.Abs(s.z)>2000||s.y<.1f||s.y>100
                    || s.remaining<0||s.remaining>8||s.ready<0||s.ready>8||s.remaining+s.ready>8||!Finite(s.progress)||s.progress<0||s.progress>=8)return false;
                if(s.permanent && ++permanent>3)return false;
                bool active=s.remaining>0 || s.ready>0;
                var r=FishingRecipes.Find(s.recipe);
                if(active?(s.kind!=CraftStation.Furnace || r==null || r.Station!=CraftStation.Furnace):!string.IsNullOrEmpty(s.recipe))return false;
                if(s.remaining==0 && s.progress!=0)return false;
            }
            var result=new FishingWorkshop();result._data=data;result._data=result.Snapshot();workshop=result;return true;
        }
    }
}
