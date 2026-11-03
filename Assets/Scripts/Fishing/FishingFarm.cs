using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class FarmPlotData
    {
        public bool tilled,planted;
        public int growth,wateredDay=-1;
        public FarmPlotData Copy()=>new FarmPlotData{tilled=tilled,planted=planted,growth=growth,wateredDay=wateredDay};
    }
    [Serializable]
    public sealed class FarmData
    {
        public int day,water=FishingFarm.WaterCapacity;
        public bool starterClaimed;
        public List<FarmPlotData> plots=new List<FarmPlotData>();
    }
    /// <summary>One watered calendar day earns one growth stage. Dry days pause growth; menus never simulate it.</summary>
    public sealed class FishingFarm
    {
        public const int PlotCount=12,GrowingDays=3,WaterCapacity=12;
        private readonly FarmPlotData[] _plots=new FarmPlotData[PlotCount];
        public int Day {get;private set;}
        public int Water {get;private set;}=WaterCapacity;
        public bool StarterClaimed {get;private set;}
        public bool FreeMaterials {get;internal set;}
        public int AvailableWater=>FreeMaterials?WaterCapacity:Water;
        public FishingFarm(int day=0){Day=day;for(int i=0;i<PlotCount;i++)_plots[i]=new FarmPlotData();}
        public FarmPlotData Plot(int id)=>id>=0 && id<PlotCount?_plots[id].Copy():null;
        public bool Ready(int id)=>id>=0 && id<PlotCount && _plots[id].planted && _plots[id].growth==GrowingDays;
        public bool AdvanceDay(int day)
        {
            if(day<=Day || day>FishingCalendar.MaxDay)return false;
            foreach(var p in _plots)
                if(p.planted && p.growth<GrowingDays && p.wateredDay==Day)p.growth++;
            Day=day;return true;
        }
        public bool Till(int id)
        {if(id<0 || id>=PlotCount || _plots[id].tilled)return false;_plots[id].tilled=true;return true;}
        public bool Plant(int id,FishingBackpack bag)
        {
            if(id<0 || id>=PlotCount || !_plots[id].tilled || _plots[id].planted || bag==null
                || !bag.Exchange(new[]{FishingRecipes.Stack(FieldItem.TurnipSeeds,1)},FieldItem.Empty,0))return false;
            _plots[id].planted=true;_plots[id].growth=0;_plots[id].wateredDay=-1;return true;
        }
        public bool WaterPlot(int id)
        {
            if(id<0 || id>=PlotCount || AvailableWater<=0 || !_plots[id].planted || Ready(id) || _plots[id].wateredDay==Day)return false;
            _plots[id].wateredDay=Day;if(!FreeMaterials)Water--;return true;
        }
        public bool Harvest(int id,FishingBackpack bag)
        {
            if(!Ready(id) || bag==null || !bag.TryAddBundle(FishingRecipes.Stack(FieldItem.Turnip,1),FishingRecipes.Stack(FieldItem.TurnipSeeds,1)))return false;
            _plots[id]=new FarmPlotData{tilled=true};return true;
        }
        public bool Refill(){if(Water==WaterCapacity)return false;Water=WaterCapacity;return true;}
        public bool ClaimStarter(FishingBackpack bag)
        {
            if(StarterClaimed || bag==null || !bag.TryAdd(FieldItem.TurnipSeeds,PlotCount))return false;
            StarterClaimed=true;return true;
        }
        public FarmData Snapshot()
        {
            var data=new FarmData{day=Day,water=Water,starterClaimed=StarterClaimed};
            foreach(var p in _plots)data.plots.Add(p.Copy());return data;
        }
        public static bool Pristine(FarmData data)
        {
            if(data==null)return true;
            if(data.day!=0 || data.water!=WaterCapacity || data.starterClaimed || data.plots==null)return false;
            foreach(var p in data.plots)if(p==null || p.tilled || p.planted || p.growth!=0 || p.wateredDay!=-1)return false;
            return data.plots.Count==0 || data.plots.Count==PlotCount;
        }
        public static bool TryRestore(FarmData data,int calendarDay,out FishingFarm farm)
        {
            farm=null;
            if(data==null || data.day<0 || data.day>calendarDay || data.water<0 || data.water>WaterCapacity || data.plots==null || data.plots.Count!=PlotCount)return false;
            var next=new FishingFarm(data.day){Water=data.water,StarterClaimed=data.starterClaimed};
            for(int i=0;i<PlotCount;i++)
            {
                var p=data.plots[i];
                if(p==null || p.growth<0 || p.growth>GrowingDays || p.wateredDay< -1 || p.wateredDay>data.day
                    || (!p.planted && (p.growth!=0 || p.wateredDay!=-1)) || (p.planted && !p.tilled))return false;
                next._plots[i]=p.Copy();
            }
            next.AdvanceDay(calendarDay);farm=next;return true;
        }
    }
    public sealed partial class FishingCareer
    {
        public FishingFarm Farm {get;private set;}=new FishingFarm();
        public bool BuyFarmSeeds()
        {
            if(!CanAfford(12) || !Backpack.TryAdd(FieldItem.TurnipSeeds,6))return false;
            SpendCoins(12);return true;
        }
        public int SellTurnips()
        {
            int count=Backpack.Count(FieldItem.Turnip),value=count*18;
            if(count==0 || Coins>100000000-value || !Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.Turnip,count)},FieldItem.Empty,0,allowFreeMaterials:false))return 0;
            Coins+=value;return value;
        }
    }
}
