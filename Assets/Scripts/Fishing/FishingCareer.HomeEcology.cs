using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class AquariumFishData
    {
        public CatchRecord fish;
        public int placedDay, fedDays, lastFedDay=-1;
        public AquariumFishData Copy()=>new AquariumFishData{fish=fish?.Copy(),placedDay=placedDay,fedDays=fedDays,lastFedDay=lastFedDay};
    }

    [Serializable]
    public sealed class HomeAnimalData
    {
        public int id, sourceId=-1, birthDay, lastFedDay=-1, lastBreedDay=-1, parentA, parentB, trapId;
        public LandAnimalKind kind;
        public bool female, housed;
        public HomeAnimalData Copy()=>(HomeAnimalData)MemberwiseClone();
    }

    [Serializable]
    public sealed class LiveCaptureData
    {
        public int sourceId;
        public LandAnimalKind kind;
        public double remaining=FishingAnimalLoot.RespawnSeconds;
        public LiveCaptureData Copy()=>(LiveCaptureData)MemberwiseClone();
    }

    [Serializable]
    public sealed class AnimalTrapData
    {
        public int id, animalId;
        public LandAnimalKind kind;
        public float x,y,z;
        public bool baited;
        public AnimalTrapData Copy()=>(AnimalTrapData)MemberwiseClone();
    }

    [Serializable]
    public sealed class HomeEcologyData
    {
        public int aquariumLevel, nextAnimalId=1, nextTrapId=1;
        public int[] habitatLevels=new int[5];
        public List<AquariumFishData> fish=new List<AquariumFishData>();
        public List<HomeAnimalData> animals=new List<HomeAnimalData>();
        public List<AnimalTrapData> traps=new List<AnimalTrapData>();
        public List<LiveCaptureData> captures=new List<LiveCaptureData>();
        public HomeEcologyData Copy()
        {
            var result=new HomeEcologyData{aquariumLevel=aquariumLevel,nextAnimalId=nextAnimalId,nextTrapId=nextTrapId,habitatLevels=(int[])habitatLevels.Clone()};
            foreach(var item in fish)result.fish.Add(item.Copy());
            foreach(var item in animals)result.animals.Add(item.Copy());
            foreach(var item in traps)result.traps.Add(item.Copy());
            foreach(var item in captures)result.captures.Add(item.Copy());
            return result;
        }
    }

    public sealed partial class FishingCareer
    {
        public const int MaxHomeAnimals=16, AnimalCarrierCapacity=4, MaxAnimalTraps=4;
        public const int AquariumUpgradeCost=300, HabitatUpgradeCost=250, AnimalAdultDays=2;
        private HomeEcologyData _ecology=new HomeEcologyData();
        public HomeEcologyData HomeEcology=>_ecology.Copy();
        public int AquariumLevel=>_ecology.aquariumLevel;
        public int AquariumCapacity=>AquariumLevel*4;
        public int AquariumCount=>_ecology.fish.Count;
        public int AquariumGrowthDays=>Lore.BlueprintUnlocked(3)?2:3;
        public int HomeAnimalCount=>_ecology.animals.Count;
        public int AnimalTrapCount=>_ecology.traps.Count;
        public int CarriedAnimalCount
        {get{int count=0;foreach(var animal in _ecology.animals)if(!animal.housed && animal.trapId==0)count++;return count;}}
        public IEnumerable<AquariumFishData> AquariumFish
        {get{foreach(var fish in _ecology.fish)yield return fish.Copy();}}
        public IEnumerable<HomeAnimalData> HomeAnimals
        {get{foreach(var animal in _ecology.animals)yield return animal.Copy();}}
        public IEnumerable<AnimalTrapData> AnimalTraps
        {get{foreach(var trap in _ecology.traps)yield return trap.Copy();}}

        private static bool AnimalKindValid(LandAnimalKind kind)=>kind>=LandAnimalKind.Rabbit && kind<=LandAnimalKind.SeaTurtle;
        public static FieldItem HabitatKit(LandAnimalKind kind)=>kind==LandAnimalKind.Rabbit?FieldItem.RabbitHutch
            :kind==LandAnimalKind.Fox?FieldItem.FoxDen:kind==LandAnimalKind.Tortoise?FieldItem.TortoisePen
            :kind==LandAnimalKind.Gull?FieldItem.BirdAviary:kind==LandAnimalKind.SeaTurtle?FieldItem.SeaTurtlePond:FieldItem.Empty;
        public static string HabitatName(LandAnimalKind kind)=>kind==LandAnimalKind.Rabbit?"兔子小窝":kind==LandAnimalKind.Fox?"林间狐舍"
            :kind==LandAnimalKind.Tortoise?"陆龟围栏":kind==LandAnimalKind.Gull?"海鸟栖架":kind==LandAnimalKind.SeaTurtle?"海龟浅水池":"未知栖息地";
        public static FieldItem AnimalFood(LandAnimalKind kind)=>kind==LandAnimalKind.Rabbit?FieldItem.TurnipSeeds
            :kind==LandAnimalKind.Fox || kind==LandAnimalKind.Gull?FieldItem.RabbitMeat
            :kind==LandAnimalKind.Tortoise || kind==LandAnimalKind.SeaTurtle?FieldItem.Turnip:FieldItem.Empty;
        public int HabitatLevel(LandAnimalKind kind)=>AnimalKindValid(kind)?_ecology.habitatLevels[(int)kind]:0;
        public int HabitatCapacity(LandAnimalKind kind)=>HabitatLevel(kind)*4;
        public int HousedAnimalCount(LandAnimalKind kind)
        {int count=0;foreach(var animal in _ecology.animals)if(animal.housed && animal.kind==kind)count++;return count;}
        public HomeAnimalData HomeAnimal(int id)=>_ecology.animals.Find(a=>a.id==id)?.Copy();
        public AnimalTrapData AnimalTrap(int id)=>_ecology.traps.Find(t=>t.id==id)?.Copy();
        public int AnimalAgeDays(int id)
        {var animal=_ecology.animals.Find(a=>a.id==id);return animal==null?-1:Calendar.Day-animal.birthDay;}
        public bool AnimalAdult(int id)=>AnimalAgeDays(id)>=AnimalAdultDays;
        public float AquariumGrowth(string id)
        {var fish=_ecology.fish.Find(f=>f.fish.id==id);return fish==null?0:Math.Min(fish.fedDays,Math.Min(AquariumGrowthDays,Calendar.Day-fish.placedDay))/(float)AquariumGrowthDays;}
        private bool AquariumContains(string id)=>_ecology.fish.Exists(f=>f.fish.id==id);

        public bool BuildAquarium()
        {
            if(AquariumLevel!=0 || !Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.Aquarium,1)},FieldItem.Empty,0))return false;
            _ecology.aquariumLevel=1;return true;
        }
        public bool UpgradeAquarium()
        {
            if(AquariumLevel!=1 || !Lore.BlueprintUnlocked(2) || !CanAfford(AquariumUpgradeCost))return false;
            SpendCoins(AquariumUpgradeCost);_ecology.aquariumLevel=2;return true;
        }
        public bool BuildAnimalHabitat(LandAnimalKind kind)
        {
            if(!AnimalKindValid(kind) || HabitatLevel(kind)!=0 || !Backpack.Exchange(new[]{FishingRecipes.Stack(HabitatKit(kind),1)},FieldItem.Empty,0))return false;
            _ecology.habitatLevels[(int)kind]=1;return true;
        }
        public bool UpgradeAnimalHabitat(LandAnimalKind kind)
        {
            if(!AnimalKindValid(kind) || HabitatLevel(kind)!=1 || !Lore.BlueprintUnlocked(1) || !CanAfford(HabitatUpgradeCost))return false;
            SpendCoins(HabitatUpgradeCost);_ecology.habitatLevels[(int)kind]=2;return true;
        }

        public bool DisplayFish(string id)
        {
            if(HasPendingCatch || AquariumCount>=AquariumCapacity || string.IsNullOrWhiteSpace(id) || AquariumContains(id))return false;
            int index=_inventory.FindIndex(f=>f.id==id);if(index<0)return false;
            _ecology.fish.Add(new AquariumFishData{fish=_inventory[index].Copy(),placedDay=Calendar.Day});
            _inventory.RemoveAt(index);return true;
        }
        public bool ReturnDisplayedFish(string id)
        {
            if(HasPendingCatch || Count>=Capacity)return false;
            int index=_ecology.fish.FindIndex(f=>f.fish.id==id);if(index<0 || _inventory.Exists(f=>f.id==id))return false;
            _inventory.Add(_ecology.fish[index].fish.Copy());_ecology.fish.RemoveAt(index);return true;
        }
        public bool FeedDisplayedFish(string id)
        {
            var fish=_ecology.fish.Find(f=>f.fish.id==id);
            if(fish==null || fish.fedDays>=AquariumGrowthDays || fish.lastFedDay==Calendar.Day || !Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.FishFeed,1)},FieldItem.Empty,0))return false;
            fish.lastFedDay=Calendar.Day;fish.fedDays=Math.Min(3,fish.fedDays+1);return true;
        }

        public bool IsWildAnimalCaptured(int id)
        {foreach(var capture in _ecology.captures)if(capture.sourceId==id && capture.remaining>0)return true;return false;}
        public bool WildAnimalAvailable(int id,LandAnimalKind kind)=>FishingAnimalLoot.Valid(id,kind) && !IsWildAnimalCaptured(id)
            && HuntedAnimal(id)==null && (id>=FishingHunting.SpawnCount || Hunting.Available(id,Calendar.TotalMinutes));
        public int CaptureAnimalWithNet(int sourceId,LandAnimalKind kind)
        {
            if(Backpack.Equipped!=FieldItem.CaptureNet || CarriedAnimalCount>=AnimalCarrierCapacity)return 0;
            return CaptureAnimal(sourceId,kind,0);
        }
        private int CaptureAnimal(int sourceId,LandAnimalKind kind,int trapId)
        {
            if(HomeAnimalCount>=MaxHomeAnimals || _ecology.nextAnimalId>=1000000 || !WildAnimalAvailable(sourceId,kind))return 0;
            int id=_ecology.nextAnimalId++;
            _ecology.animals.Add(new HomeAnimalData{id=id,sourceId=sourceId,kind=kind,female=(id&1)==0,birthDay=Calendar.Day-AnimalAdultDays,trapId=trapId});
            _ecology.captures.RemoveAll(c=>c.sourceId==sourceId);
            _ecology.captures.Add(new LiveCaptureData{sourceId=sourceId,kind=kind});
            Backpack.Observe(kind);
            Hunting.SetLiveCapture(sourceId,true);return id;
        }
        public bool TickHomeEcology(float seconds,bool paused=false)
        {
            if(paused || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<=0)return false;
            bool changed=false;
            for(int i=_ecology.captures.Count-1;i>=0;i--)
            {
                var claim=_ecology.captures[i];claim.remaining=Math.Max(0,claim.remaining-Math.Min(seconds,1));
                if(claim.remaining>0)continue;
                Hunting.SetLiveCapture(claim.sourceId,false);_ecology.captures.RemoveAt(i);changed=true;
            }
            return changed;
        }
        public bool HouseAnimal(int id)
        {
            var animal=_ecology.animals.Find(a=>a.id==id);
            if(animal==null || animal.housed || animal.trapId!=0 || HousedAnimalCount(animal.kind)>=HabitatCapacity(animal.kind))return false;
            animal.housed=true;return true;
        }
        public bool CarryHomeAnimal(int id)
        {
            var animal=_ecology.animals.Find(a=>a.id==id);
            if(animal==null || !animal.housed || CarriedAnimalCount>=AnimalCarrierCapacity)return false;
            animal.housed=false;return true;
        }
        public bool ReleaseHomeAnimal(int id)
        {
            var animal=_ecology.animals.Find(a=>a.id==id);if(animal==null)return false;
            if(animal.trapId!=0){var trap=_ecology.traps.Find(t=>t.id==animal.trapId);if(trap!=null)trap.animalId=0;}
            return _ecology.animals.Remove(animal);
        }
        public bool FeedHomeAnimal(int id)
        {
            var animal=_ecology.animals.Find(a=>a.id==id);
            if(animal==null || animal.trapId!=0 || animal.lastFedDay==Calendar.Day
                || !Backpack.Exchange(new[]{FishingRecipes.Stack(AnimalFood(animal.kind),1)},FieldItem.Empty,0))return false;
            animal.lastFedDay=Calendar.Day;return true;
        }
        public int BreedHomeAnimals(int first,int second)
        {
            var a=_ecology.animals.Find(v=>v.id==first);var b=_ecology.animals.Find(v=>v.id==second);
            if(a==null || b==null || a==b || a.kind!=b.kind || a.female==b.female || !a.housed || !b.housed
                || !AnimalAdult(a.id) || !AnimalAdult(b.id) || a.lastBreedDay==Calendar.Day || b.lastBreedDay==Calendar.Day
                || HousedAnimalCount(a.kind)>=HabitatCapacity(a.kind) || HomeAnimalCount>=MaxHomeAnimals || _ecology.nextAnimalId>=1000000)return 0;
            int food=(a.lastFedDay==Calendar.Day?0:1)+(b.lastFedDay==Calendar.Day?0:1);
            if(food>0 && !Backpack.Exchange(new[]{FishingRecipes.Stack(AnimalFood(a.kind),food)},FieldItem.Empty,0))return 0;
            int id=_ecology.nextAnimalId++;
            a.lastFedDay=b.lastFedDay=a.lastBreedDay=b.lastBreedDay=Calendar.Day;
            _ecology.animals.Add(new HomeAnimalData{id=id,kind=a.kind,female=(id&1)==0,birthDay=Calendar.Day,housed=true,parentA=first,parentB=second});
            return id;
        }

        private static bool TrapLocation(float x,float y,float z)=>FiniteHunt(x) && FiniteHunt(y) && FiniteHunt(z)
            && Math.Abs(x)<=2000 && Math.Abs(z)<=2000 && y>=-.5f && y<=100;
        public int PlaceAnimalTrap(LandAnimalKind kind,float x,float y,float z)
        {
            if(!AnimalKindValid(kind) || kind==LandAnimalKind.SeaTurtle || !TrapLocation(x,y,z) || AnimalTrapCount>=MaxAnimalTraps || _ecology.nextTrapId>=1000000)return 0;
            foreach(var trap in _ecology.traps)if((trap.x-x)*(trap.x-x)+(trap.z-z)*(trap.z-z)<4)return 0;
            if(!Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.AnimalTrap,1)},FieldItem.Empty,0))return 0;
            int id=_ecology.nextTrapId++;_ecology.traps.Add(new AnimalTrapData{id=id,kind=kind,x=x,y=y,z=z});return id;
        }
        public bool BaitAnimalTrap(int id)
        {
            var trap=_ecology.traps.Find(t=>t.id==id);
            if(trap==null || trap.baited || trap.animalId!=0 || !Backpack.Exchange(new[]{FishingRecipes.Stack(AnimalFood(trap.kind),1)},FieldItem.Empty,0))return false;
            trap.baited=true;return true;
        }
        public int CaptureAnimalInTrap(int trapId,int sourceId,LandAnimalKind kind,float x,float y,float z)
        {
            var trap=_ecology.traps.Find(t=>t.id==trapId);
            if(trap==null || !trap.baited || trap.animalId!=0 || trap.kind!=kind || !TrapLocation(x,y,z)
                || (trap.x-x)*(trap.x-x)+(trap.y-y)*(trap.y-y)+(trap.z-z)*(trap.z-z)>2.25f)return 0;
            int id=CaptureAnimal(sourceId,kind,trapId);if(id==0)return 0;
            trap.animalId=id;trap.baited=false;return id;
        }
        public bool CollectTrappedAnimal(int id)
        {
            var trap=_ecology.traps.Find(t=>t.id==id);
            if(trap==null || trap.animalId==0 || CarriedAnimalCount>=AnimalCarrierCapacity)return false;
            var animal=_ecology.animals.Find(a=>a.id==trap.animalId);if(animal==null)return false;
            animal.trapId=0;trap.animalId=0;return true;
        }
        public bool RemoveAnimalTrap(int id)
        {
            var trap=_ecology.traps.Find(t=>t.id==id);
            if(trap==null || trap.animalId!=0 || !Backpack.TryAdd(FieldItem.AnimalTrap,1))return false;
            return _ecology.traps.Remove(trap);
        }

        private static bool EcologyPristine(HomeEcologyData data)
        {
            if(data==null)return true;
            if(data.aquariumLevel!=0 || data.nextAnimalId!=1 || data.nextTrapId!=1 || data.habitatLevels==null || data.habitatLevels.Length!=5
                || data.fish==null || data.animals==null || data.traps==null || data.captures==null
                || data.fish.Count!=0 || data.animals.Count!=0 || data.traps.Count!=0 || data.captures.Count!=0)return false;
            foreach(int level in data.habitatLevels)if(level!=0)return false;return true;
        }
        private bool RestoreHomeEcology(FishingSaveData save)
        {
            if(save.version<17)
            {
                if(!EcologyPristine(save.homeEcology))return false;
                foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))
                    if(item>=FieldItem.CaptureNet && (Backpack.Count(item)>0 || HomeStorage.Count(item)>0))return false;
                return true;
            }
            var data=save.homeEcology;
            if(data==null || data.habitatLevels==null || data.habitatLevels.Length!=5 || data.aquariumLevel<0 || data.aquariumLevel>2
                || data.nextAnimalId<1 || data.nextAnimalId>1000000 || data.nextTrapId<1 || data.nextTrapId>1000000
                || data.fish==null || data.animals==null || data.traps==null || data.captures==null
                || data.fish.Count>data.aquariumLevel*4 || data.animals.Count>MaxHomeAnimals || data.traps.Count>MaxAnimalTraps || data.captures.Count>FishingAnimalLoot.TargetCount)return false;
            foreach(int level in data.habitatLevels)if(level<0 || level>2 || (level==2 && !Lore.BlueprintUnlocked(1)))return false;
            if(data.aquariumLevel==2 && !Lore.BlueprintUnlocked(2))return false;
            var fishIds=new HashSet<string>();
            foreach(var fish in data.fish)
            {
                if(fish==null || !Valid(fish.fish) || FishingSpecies.Of(fish.fish.kind).Legendary || !fishIds.Add(fish.fish.id)
                    || _inventory.Exists(f=>f.id==fish.fish.id) || (_pending!=null && _pending.fish.id==fish.fish.id)
                    || (_barbecue.fish!=null && _barbecue.fish.id==fish.fish.id) || !_recorded.Contains(fish.fish.id)
                    || !Discovered(fish.fish.kind,fish.fish.rare) || BestGrams(fish.fish.kind)<fish.fish.grams
                    || fish.placedDay<0 || fish.placedDay>Calendar.Day || fish.fedDays<0 || fish.fedDays>3
                    || fish.lastFedDay< -1 || fish.lastFedDay>Calendar.Day || (fish.fedDays==0)!=(fish.lastFedDay==-1)
                    || (fish.lastFedDay>=0 && (fish.lastFedDay<fish.placedDay || fish.fedDays>fish.lastFedDay-fish.placedDay+1)))return false;
            }
            if(Catches<Count+LegendCatches+data.fish.Count+(_barbecue.fish==null?0:1))return false;
            var animalIds=new HashSet<int>();var sourceIds=new HashSet<int>();var trapIds=new HashSet<int>();
            int carrier=0;var housed=new int[5];
            foreach(var animal in data.animals)
            {
                if(animal==null || !AnimalKindValid(animal.kind) || animal.id<=0 || animal.id>=data.nextAnimalId || !animalIds.Add(animal.id)
                    || animal.birthDay< -AnimalAdultDays || animal.birthDay>Calendar.Day || animal.lastFedDay< -1 || animal.lastFedDay>Calendar.Day
                    || animal.lastBreedDay< -1 || animal.lastBreedDay>Calendar.Day || animal.trapId<0 || (animal.housed && animal.trapId!=0)
                    || (animal.lastFedDay>=0 && animal.lastFedDay<animal.birthDay)
                    || (animal.lastBreedDay>=0 && (animal.lastBreedDay<animal.birthDay+AnimalAdultDays || animal.lastFedDay<animal.lastBreedDay)))return false;
                if(animal.sourceId>=0)
                {
                    if(!FishingAnimalLoot.Valid(animal.sourceId,animal.kind) || animal.parentA!=0 || animal.parentB!=0 || Calendar.Day-animal.birthDay<AnimalAdultDays)return false;
                }
                else
                {
                    if(animal.sourceId!=-1 || animal.parentA<=0 || animal.parentB<=0 || animal.parentA==animal.parentB || animal.parentA>=animal.id || animal.parentB>=animal.id)return false;
                    var a=data.animals.Find(v=>v!=null && v.id==animal.parentA);var b=data.animals.Find(v=>v!=null && v.id==animal.parentB);
                    if((a!=null && (a.kind!=animal.kind || a.birthDay+AnimalAdultDays>animal.birthDay || a.lastBreedDay<animal.birthDay))
                        || (b!=null && (b.kind!=animal.kind || b.birthDay+AnimalAdultDays>animal.birthDay || b.lastBreedDay<animal.birthDay))
                        || (a!=null && b!=null && a.female==b.female))return false;
                }
                if(animal.housed)housed[(int)animal.kind]++;else if(animal.trapId==0)carrier++;
            }
            if(carrier>AnimalCarrierCapacity)return false;
            for(int i=0;i<5;i++)if(housed[i]>data.habitatLevels[i]*4)return false;
            foreach(var trap in data.traps)
            {
                if(trap==null || trap.id<=0 || trap.id>=data.nextTrapId || !trapIds.Add(trap.id) || !AnimalKindValid(trap.kind)
                    || trap.kind==LandAnimalKind.SeaTurtle || !TrapLocation(trap.x,trap.y,trap.z) || trap.animalId<0 || (trap.animalId>0 && trap.baited))return false;
                if(trap.animalId>0 && !data.animals.Exists(a=>a.id==trap.animalId && a.trapId==trap.id && !a.housed && a.kind==trap.kind))return false;
                foreach(var other in data.traps)if(other!=null && other.id!=trap.id && (other.x-trap.x)*(other.x-trap.x)+(other.z-trap.z)*(other.z-trap.z)<4)return false;
            }
            foreach(var animal in data.animals)if(animal.trapId>0 && !data.traps.Exists(t=>t.id==animal.trapId && t.animalId==animal.id))return false;
            foreach(var capture in data.captures)
            {
                if(capture==null || !FishingAnimalLoot.Valid(capture.sourceId,capture.kind) || !sourceIds.Add(capture.sourceId)
                    || double.IsNaN(capture.remaining) || double.IsInfinity(capture.remaining) || capture.remaining<=0 || capture.remaining>FishingAnimalLoot.RespawnSeconds
                    || HuntedAnimal(capture.sourceId)!=null || (capture.sourceId<8 && !Hunting.Available(capture.sourceId,Calendar.TotalMinutes)))return false;
            }
            _ecology=data.Copy();
            foreach(var capture in _ecology.captures)Hunting.SetLiveCapture(capture.sourceId,true);
            return true;
        }
    }
}
