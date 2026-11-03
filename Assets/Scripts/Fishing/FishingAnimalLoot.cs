using System;
using System.Collections.Generic;
namespace BoatAttack.Fishing
{
    public static class FishingAnimalLoot
    {
        public const int GullStart=8, GullCount=3, SeaTurtleId=11, TargetCount=12;
        public const double RespawnSeconds=300;
        public static bool Valid(int id,LandAnimalKind kind)=>id>=0 && id<8?(int)kind==id%3:
            id>=GullStart && id<GullStart+GullCount?kind==LandAnimalKind.Gull:id==SeaTurtleId && kind==LandAnimalKind.SeaTurtle;
        public static FieldStack[] Drops(LandAnimalKind kind,bool spear=false)
        {
            FieldItem meat,material;int count;
            switch(kind)
            {
                case LandAnimalKind.Rabbit:meat=FieldItem.RabbitMeat;material=FieldItem.RabbitFur;count=spear?2:1;break;
                case LandAnimalKind.Fox:meat=FieldItem.FoxMeat;material=FieldItem.FoxFur;count=spear?3:2;break;
                case LandAnimalKind.Tortoise:case LandAnimalKind.SeaTurtle:meat=FieldItem.TurtleMeat;material=FieldItem.TurtleShell;count=2;break;
                case LandAnimalKind.Gull:meat=FieldItem.GullMeat;material=FieldItem.GullFeather;count=1;break;
                default:return new FieldStack[0];
            }
            return new[]{new FieldStack{item=meat,count=count},new FieldStack{item=material,count=1}};
        }
        public static string Description(LandAnimalKind kind,bool spear=false)
        {
            var drops=Drops(kind,spear);var parts=new List<string>();
            foreach(var drop in drops)parts.Add(FishingBackpack.Name(drop.item)+" ×"+drop.count);
            return string.Join("、",parts);
        }
    }
    [Serializable]
    public sealed class HuntedAnimalData
    {
        public int id;public LandAnimalKind kind;public float x,y,z,yaw;public bool collected;
        public double respawnAt;
        public bool activeRecovery;
        public double respawnSeconds;
        public HuntedAnimalData Copy()=>(HuntedAnimalData)MemberwiseClone();
    }
    public sealed partial class FishingCareer
    {
        private readonly List<HuntedAnimalData> _huntedAnimals=new List<HuntedAnimalData>();
        public static int HuntingRiflePrice=>3*UpgradeCost(UpgradeTrack.Line,0);
        public bool BuyHuntingRifle()
        {
            if(!CanAfford(HuntingRiflePrice) || Backpack.Count(FieldItem.HuntingRifle)>0 || !Backpack.TryAdd(FieldItem.HuntingRifle,1))return false;
            SpendCoins(HuntingRiflePrice);return true;
        }
        public HuntedAnimalData HuntedAnimal(int id)=>FindHuntedAnimal(id)?.Copy();
        private HuntedAnimalData FindHuntedAnimal(int id)
        {
            foreach(var a in _huntedAnimals)
                if(a.id==id)return a.collected && (a.activeRecovery?a.respawnSeconds<=0:a.respawnAt>0 && Calendar.TotalMinutes>=a.respawnAt)?null:a;
            return null;
        }
        public bool KillLandAnimal(int id,LandAnimalKind kind,float x,float y,float z,float yaw)
        {
            var entry=new HuntedAnimalData{id=id,kind=kind,x=x,y=y,z=z,yaw=yaw,respawnAt=Calendar.TotalMinutes+FishingHunting.RecoveryMinutes};
            if(Backpack.Equipped!=FieldItem.HuntingRifle || !ValidHuntedAnimal(entry) || HuntedAnimal(id)!=null || IsWildAnimalCaptured(id)
                || (id<8 && !Hunting.Available(id,Calendar.TotalMinutes)))return false;
            _huntedAnimals.RemoveAll(a=>a.id==id);_huntedAnimals.Add(entry);return true;
        }
        public bool CollectHuntedAnimal(int id)
        {
            var entry=_huntedAnimals.Find(a=>a.id==id);
            if(entry==null || entry.collected || !Backpack.TryAddBundle(FishingAnimalLoot.Drops(entry.kind)))return false;
            entry.collected=true;entry.activeRecovery=true;entry.respawnSeconds=FishingAnimalLoot.RespawnSeconds;
            entry.respawnAt=Calendar.TotalMinutes+FishingHunting.RecoveryMinutes;return true;
        }
        // Only active simulation consumes this saved timer; sleeping/changing the
        // calendar or reopening the game cannot instantly respawn a new kill.
        public bool TickHuntedRespawns(float seconds,bool paused=false)
        {
            if(paused || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<=0)return false;
            double step=Math.Min(seconds,1);bool ready=false;
            foreach(var a in _huntedAnimals)
            {
                if(!a.collected || !a.activeRecovery || a.respawnSeconds<=0)continue;
                a.respawnSeconds=Math.Max(0,a.respawnSeconds-step);ready |= a.respawnSeconds==0;
            }
            return ready;
        }
        private static bool ValidHuntedAnimal(HuntedAnimalData a)=>a!=null && FishingAnimalLoot.Valid(a.id,a.kind)
            && FiniteHunt(a.x) && FiniteHunt(a.y) && FiniteHunt(a.z) && FiniteHunt(a.yaw)
            && !double.IsNaN(a.respawnAt) && !double.IsInfinity(a.respawnAt) && a.respawnAt>=0 && a.respawnAt<=(FishingCalendar.MaxDay+1d)*1440
            && !double.IsNaN(a.respawnSeconds) && !double.IsInfinity(a.respawnSeconds) && a.respawnSeconds>=0 && a.respawnSeconds<=FishingAnimalLoot.RespawnSeconds
            && (!a.activeRecovery || a.collected);
        private static bool FiniteHunt(float value)=>!float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value)<=10000;
    }
}
