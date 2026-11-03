using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    [DefaultExecutionOrder(200)]
    public sealed class FishingHomeEcologyWorld : MonoBehaviour
    {
        public sealed class Choice { public string Label;public Action Run;public bool Enabled=true; }
        private FishingPrototype _c;
        private Transform _display,_net;
        private readonly List<UnityEngine.Object> _owned=new List<UnityEngine.Object>();
        private readonly List<Transform> _fish=new List<Transform>();
        private sealed class Pet { public Transform Root;public LandAnimalKind Kind;public int Id,Slot; }
        private readonly List<Pet> _pets=new List<Pet>();
        private readonly Dictionary<int,float> _petCalls=new Dictionary<int,float>();
        private readonly Dictionary<int,Transform> _traps=new Dictionary<int,Transform>();
        private FishingCareer _profile;
        private int _day=-1,_sourceCount,_growthDays;
        private Material _glass;
        private MeshCollider _homeLand;
        private Vector3 _heartPoint;
        private float _trapAt,_heartUntil,_petTime,_petCallAt;
        private int _habitatFilter=-1;
        private Transform _hearts;
        public LandAnimalKind TrapKind=LandAnimalKind.Rabbit;
        public int Page;
        public bool NearHome=>_c.Ashore && FishingPrototype.FlatDistance(_c.ExplorerPosition,_c.Home.Origin)<22 && !_c.Shore.Swimming && !_c.Shore.Climbing;
        public Vector3 AquariumPosition=>HomePlot(new Vector3(8.3f,0,-4));
        public Vector3 HabitatPosition(LandAnimalKind kind)
        {
            return HomePlot(new Vector3(-7.5f+(int)kind*4,0,7.5f));
        }
        private Vector3 HomePlot(Vector3 offset)
        {
            var point=_c.Home.Origin+offset;
            // These permanent plots belong to the authored home island. General physics ground can
            // select an overlapping legacy terrain or another fixture and lift the entire enclosure.
            if(_homeLand!=null && _homeLand.Raycast(new Ray(point+Vector3.up*40,Vector3.down),out var hit,80))point.y=hit.point.y;
            return point;
        }
        public void Build(FishingPrototype c)
        {_c=c;_homeLand=c.Home.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();Refresh();}
        private bool Active=>_c!=null && !_c.Paused && !_c.XRNotebook.BlocksGameplay && !_c.DockConsole.BlocksGameplay
            && !_c.ShopBlocksGameplay && !_c.StoryBlocksGameplay && (Application.isFocused || _c.Automated);
        private void Update()
        {
            if(_c==null)return;
            if(Active && _c.Career.TickHomeEcology(Time.deltaTime,false))_c.RifleHunting.RefreshAnimals();
            if(_profile!=_c.Career || _day!=_c.Career.Calendar.Day || _sourceCount!=_c.RifleHunting.Targets.Count || _growthDays!=_c.Career.AquariumGrowthDays){Refresh();}
            if(Active && Time.time>_trapAt){CheckTraps();_trapAt=Time.time+.2f;}
            for(int i=0;i<_fish.Count;i++)
            {
                float t=Time.time*.5f+i*1.8f;var f=_fish[i];f.localPosition=new Vector3(Mathf.Sin(t)*.8f,.7f+(i%2)*.25f,Mathf.Cos(t)*.27f);f.localRotation=Quaternion.Slerp(f.localRotation,Quaternion.Euler(0,Mathf.Cos(t)>0?90:270,0),Mathf.Min(1,Time.deltaTime*3));
            }
            if(Active){AnimatePets(Time.deltaTime);TickPetCalls();}
            if(_hearts!=null){_hearts.gameObject.SetActive(Time.time<_heartUntil);_hearts.position=_heartPoint+Vector3.up*(1.1f+(1-(_heartUntil-Time.time)/3)*.5f);_hearts.rotation=Quaternion.LookRotation(_hearts.position-_c.viewCamera.transform.position);}
        }
        private void AnimatePets(float delta)
        {
            _petTime+=delta;
            foreach(var pet in _pets)
            {
                if(pet.Root==null)continue;
                float rate=pet.Kind==LandAnimalKind.Tortoise?.045f:pet.Kind==LandAnimalKind.SeaTurtle?.07f:pet.Kind==LandAnimalKind.Rabbit?.18f:.13f;
                float phase=Mathf.Repeat(_petTime*rate+pet.Id*.37f,2),leg=phase<1?1:-1,progress=Mathf.Min(1,(phase%1)/.65f);
                bool perched=pet.Kind==LandAnimalKind.Gull;
                var anchor=perched?new Vector3(-.72f+pet.Slot%4*.48f,.595f+(pet.Slot/4)*.61f,-.2f)
                    :new Vector3(-1.04f+pet.Slot%4*.53f,.08f,1.16f+(pet.Slot/4)*.75f);
                var offset=perched?new Vector3(.025f,0,0):new Vector3(0,0,.10f);
                var point=anchor+offset*(leg*Mathf.Lerp(-1,1,Mathf.SmoothStep(0,1,progress)));
                float moving=progress<1?1:0;
                float hop=pet.Kind==LandAnimalKind.Rabbit?.065f:pet.Kind==LandAnimalKind.Tortoise || pet.Kind==LandAnimalKind.SeaTurtle?.005f:.018f;
                point.y+=Mathf.Abs(Mathf.Sin(progress*Mathf.PI*6))*hop*moving;
                pet.Root.localPosition=point;
                float yaw=Mathf.Atan2(offset.x*leg,offset.z*leg)*Mathf.Rad2Deg;
                var facing=Quaternion.Euler(0,yaw+Mathf.Sin(_petTime*1.4f+pet.Id)*8*(1-moving),0);
                pet.Root.localRotation=Quaternion.Slerp(pet.Root.localRotation,facing,Mathf.Min(1,delta*5));
            }
        }
        private static string PetCue(LandAnimalKind kind)=>kind==LandAnimalKind.Rabbit?"RabbitCall":kind==LandAnimalKind.Fox?"FoxCall"
            :kind==LandAnimalKind.Gull?"GullCall":kind==LandAnimalKind.Tortoise?"TortoiseCall":"SeaTurtleCall";
        private bool PlayPetCall(Pet pet)
        {
            if(!Active || _c.StartMenuOpen || pet.Root==null || !pet.Root.gameObject.activeInHierarchy
                || Vector3.Distance(_c.viewCamera.transform.position,pet.Root.position)>14)return false;
            var audio=_c.WorldAudio;if(audio==null)return false;
            string cue=PetCue(pet.Kind);
            return audio.Play(audio.HasCue(cue)?cue:"AnimalRustle",pet.Root.position+Vector3.up*.18f,1,FishingAudioCategory.Ambient);
        }
        private void TickPetCalls()
        {
            if(_petTime<_petCallAt)return;
            foreach(var pet in _pets)
            {
                if(!_petCalls.TryGetValue(pet.Id,out float next) || _petTime<next)continue;
                // Retry out-of-range or muted animals slowly; refreshing a menu never restarts their call clock.
                _petCalls[pet.Id]=_petTime+9+(pet.Id*7%8);
                if(!PlayPetCall(pet))continue;
                _petCallAt=_petTime+1.5f;break;
            }
        }
        private int CarriedCount(LandAnimalKind kind)
        {int count=0;foreach(var a in _c.Career.HomeAnimals)if(a.kind==kind && !a.housed && a.trapId==0)count++;return count;}
        private int NearbyHabitat()
        {
            if(!NearHome)return -1;int result=-1;float nearest=2.5f;
            for(int i=0;i<5;i++)
            {float distance=Vector3.Distance(_c.ExplorerPosition,HabitatPosition((LandAnimalKind)i)+Vector3.forward*2.6f);if(distance<nearest){nearest=distance;result=i;}}
            return result;
        }
        public string NearbyHint
        {
            get
            {
                if(!_c.Ashore)return null;
                string key=XRSettings.isDeviceActive?"A ":"E ";
                if(NearHome && Vector3.Distance(_c.ExplorerPosition,AquariumPosition)<2.4f)return key+"家园水族箱 · 展示、喂养与成长";
                int nearby=NearbyHabitat();
                if(nearby>=0)
                {
                    var kind=(LandAnimalKind)nearby;var c=_c.Career;
                    string action=c.HabitatLevel(kind)==0?" · 部署设施":CarriedCount(kind)>0 && c.HousedAnimalCount(kind)<c.HabitatCapacity(kind)?" · 安置携带的"+FishingBackpack.AnimalName(kind):" · 饲养与繁殖";
                    return key+FishingCareer.HabitatName(kind)+action;
                }
                return NearbyTrap()!=null?key+"诱捕笼 · 上饵、收取或收回":null;
            }
        }
        public bool TryInteract()
        {
            if(NearbyHint==null)return false;
            int mode=NearbyTrap()!=null?4:0;
            if(NearHome && Vector3.Distance(_c.ExplorerPosition,AquariumPosition)<2.4f)mode=_c.Career.AquariumLevel>0?2:0;
            else
            {
                int nearby=NearbyHabitat();
                if(nearby>=0)
                {
                    var kind=(LandAnimalKind)nearby;
                    if(_c.Career.HousedAnimalCount(kind)<_c.Career.HabitatCapacity(kind))
                        foreach(var a in _c.Career.HomeAnimals)if(a.kind==kind && !a.housed && a.trapId==0){MoveAnimal(a.id,false);return true;}
                    _habitatFilter=nearby;OpenPanel(_c.Career.HabitatLevel(kind)>0?3:0);return true;
                }
            }
            Open(mode);return true;
        }
        public void Open(int mode=0)
        {ClearHabitatFilter();OpenPanel(mode);}
        public void ClearHabitatFilter(){_habitatFilter=-1;Page=0;}
        private void OpenPanel(int mode)
        {
            Page=0;
            if(XRSettings.isDeviceActive){_c.XRNotebook.OpenHomeEcology(mode,true);return;}
            if(!_c.InventoryOpen)_c.ToggleInventory();
            if(_c.InventoryOpen)_c.OpenHomeEcologyPage(mode,true);
        }
        private string AnimalMoveBlocked(int id)
        {
            var c=_c.Career;var a=c.HomeAnimal(id);
            if(a==null)return "这只动物已不在载具或住所中。";
            if(a.trapId>0)return "先到诱捕笼旁收取这只动物，再带回家园。";
            if(a.housed)return c.CarriedAnimalCount>=FishingCareer.AnimalCarrierCapacity?"活体载具已满，先安置其他动物。":null;
            if(c.HabitatLevel(a.kind)==0)return "先在设施建设中部署"+FishingCareer.HabitatName(a.kind)+"。";
            return c.HousedAnimalCount(a.kind)>=c.HabitatCapacity(a.kind)?FishingCareer.HabitatName(a.kind)+"已满，先扩建或带走一只动物。":null;
        }
        private void MoveAnimal(int id,bool expectedHoused)
        {
            string reason=AnimalMoveBlocked(id);if(reason!=null){_c.Notice(reason,5);return;}
            var animal=_c.Career.HomeAnimal(id);bool housed=animal.housed;
            if(housed!=expectedHoused){_c.Notice("动物安置状态已更新，请重新选择操作。",4);return;}
            Commit(()=>housed?_c.Career.CarryHomeAnimal(id):_c.Career.HouseAnimal(id),housed?"动物已进入活体载具。":"已安置"+FishingBackpack.AnimalName(animal.kind)+"，可在住所内看见并听见它。" );
        }
        private AnimalTrapData NearbyTrap()
        {
            foreach(var t in _c.Career.AnimalTraps)if(TrapReach(t))return t;return null;
        }
        private bool TrapReach(AnimalTrapData t)=>_c.Ashore && Vector3.Distance(_c.ExplorerPosition,new Vector3(t.x,t.y,t.z))<2.5f
            && _c.Shore.ClearSight(_c.Shore.Character.EyePosition,new Vector3(t.x,t.y+.3f,t.z));
        private void Commit(Func<bool> action,string success,bool home=true)
        {
            if((home && !NearHome) || _c.WorldActionBusy || _c.StoryBlocksGameplay){_c.Notice(home?"请带回家园，在设施旁操作。":"当前无法操作。",4);return;}
            if(!action()){_c.Notice("未完成：检查材料、容量、蓝图与今日喂养/繁殖记录；没有扣除额外物资。",5);return;}
            _c.Save();Refresh();_c.RifleHunting.RefreshAnimals();_c.WorldAudio?.Play("UIConfirm",_c.ExplorerPosition,.35f);_c.Notice(success,4);
        }
        public List<Choice> Choices(int mode)
        {
            var c=_c.Career;var list=new List<Choice>();
            void Add(string label,Func<bool> action,string message,bool enabled=true,bool home=true)=>list.Add(new Choice{Label=label,Enabled=enabled,Run=()=>Commit(action,message,home)});
            if(mode==0)
            {
                int aquariumKit=c.Backpack.Count(FieldItem.Aquarium);
                if(_habitatFilter<0)Add(c.AquariumLevel==0?"部署水族箱 · "+(c.CreativeMode?"免套件":"套件 "+aquariumKit+"/1"):c.AquariumLevel==1?"扩建水族箱至8格 · 潮境蓝图"+(c.CreativeMode?" · 免费":" +300币"):"水族箱 · 已建成8格",c.AquariumLevel==0?(Func<bool>)c.BuildAquarium:c.UpgradeAquarium,"水族箱已更新。",NearHome && c.AquariumLevel<2);
                for(int i=0;i<5;i++)
                {
                    if(_habitatFilter>=0 && _habitatFilter!=i)continue;
                    var kind=(LandAnimalKind)i;int level=c.HabitatLevel(kind);
                    Add(FishingCareer.HabitatName(kind)+(level==0?" · 部署 · "+(c.CreativeMode?"免套件":"套件 "+c.Backpack.Count(FishingCareer.HabitatKit(kind))+"/1"):" · "+c.HousedAnimalCount(kind)+"/"+c.HabitatCapacity(kind)+(level==1?" · 林境蓝图"+(c.CreativeMode?"免费扩建":"+250币扩建"):" · 已满级")),()=>level==0?c.BuildAnimalHabitat(kind):c.UpgradeAnimalHabitat(kind),FishingCareer.HabitatName(kind)+"已部署在菜园西侧的专用地块。",NearHome && level<2);
                }
            }
            else if(mode==1)
            {
                foreach(var fish in c.Inventory){string id=fish.id;Add("展示 "+FishingSpecies.Name(fish.kind)+" · "+FishingText.Weight(fish.grams),()=>c.DisplayFish(id),"鱼已移入水族箱，不会被出售或烹饪。",NearHome && c.AquariumCount<c.AquariumCapacity);}
            }
            else if(mode==2)
            {
                foreach(var f in c.AquariumFish)
                {
                    string id=f.fish.id;string name=FishingSpecies.Name(f.fish.kind)+" · 成长"+Mathf.RoundToInt(c.AquariumGrowth(id)*100)+"%";
                    Add(name+" · 喂鱼食",()=>c.FeedDisplayedFish(id),"喂食成功；需不同日喂养"+c.AquariumGrowthDays+"次并度过"+c.AquariumGrowthDays+"天。",NearHome && f.lastFedDay!=c.Calendar.Day && f.fedDays<c.AquariumGrowthDays);
                    Add(name+" · 移回鱼箱",()=>c.ReturnDisplayedFish(id),"鱼已安全移回鱼箱。",NearHome && c.Count<c.Capacity);
                }
            }
            else if(mode==3)
            {
                var animals=new List<HomeAnimalData>(c.HomeAnimals);
                animals.Sort((a,b)=>a.housed!=b.housed?a.housed.CompareTo(b.housed):a.id.CompareTo(b.id));
                foreach(var a in animals)
                {
                    if(_habitatFilter>=0 && (int)a.kind!=_habitatFilter)continue;
                    int id=a.id;string name="#"+id+" "+FishingBackpack.AnimalName(a.kind)+(a.female?" ♀":" ♂")+(c.AnimalAdult(id)?" 成年":" 幼兽")+(a.housed?" · 已安家":a.trapId>0?" · 笼内":" · 携带中");
                    if(a.trapId>0)continue;
                    string blocked=AnimalMoveBlocked(id);
                    bool expectedHoused=a.housed;
                    string moveLabel=name+(a.housed?" · 带上旅途":" · 安置住所");
                    if(blocked!=null)moveLabel=(a.housed?"活体载具已满":c.HabitatLevel(a.kind)==0?"先部署"+FishingCareer.HabitatName(a.kind):"住所已满 · 需扩建")+"\n#"+id+" "+FishingBackpack.AnimalName(a.kind);
                    list.Add(new Choice{Label=moveLabel,Enabled=NearHome && blocked==null,Run=()=>MoveAnimal(id,expectedHoused)});
                    if(a.housed)Add("喂养 #"+id+" · "+FishingBackpack.Name(FishingCareer.AnimalFood(a.kind)),()=>c.FeedHomeAnimal(id),"今天已喂养。",NearHome && a.lastFedDay!=c.Calendar.Day);
                    if(a.housed && c.AnimalAdult(id) && !a.female)
                        foreach(var b in animals)if(b.housed && b.kind==a.kind && b.female && c.AnimalAdult(b.id))
                        {
                            int other=b.id;Add("#"+id+" 与 #"+other+" 繁殖 · 为未喂养的父母提供食物",()=>{int child=c.BreedHomeAnimals(id,other);if(child>0){_heartUntil=Time.time+3;_heartPoint=HabitatPosition(a.kind);return true;}return false;},"诞生了幼兽！两天后成年。",NearHome && a.lastBreedDay!=c.Calendar.Day && b.lastBreedDay!=c.Calendar.Day);
                        }
                    Add("放归 #"+id+" "+FishingBackpack.AnimalName(a.kind),()=>c.ReleaseHomeAnimal(id),"动物已放归；原野外来源仍按正常时间恢复。",NearHome);
                }
            }
            else if(mode==4)
            {
                foreach(var trap in c.AnimalTraps)
                {
                    int id=trap.id;bool reach=TrapReach(trap);string name="#"+id+" "+FishingBackpack.AnimalName(trap.kind)+"诱捕笼";
                    if(trap.animalId>0)Add(name+" · 收取动物",()=>{var t=c.AnimalTrap(id);return t!=null && TrapReach(t) && c.CollectTrappedAnimal(id);},"动物已收入活体载具。",reach,false);
                    else
                    {
                        Add(name+(trap.baited?" · 已上饵":" · 添加诱饵"),()=>{var t=c.AnimalTrap(id);return t!=null && TrapReach(t) && c.BaitAnimalTrap(id);},"诱饵已放好，附近匹配的动物会进入笼中。",reach && !trap.baited,false);
                        Add(name+" · 收回空笼",()=>{var t=c.AnimalTrap(id);return t!=null && TrapReach(t) && c.RemoveAnimalTrap(id);},"空笼已回到背包。",reach,false);
                    }
                }
            }
            if(mode==3 && list.Count==0)list.Add(new Choice{Label="尚无可安置的"+(_habitatFilter>=0?FishingBackpack.AnimalName((LandAnimalKind)_habitatFilter):"动物")+" · 活捉网右扳机捕获；诱捕笼须先收取",Enabled=false});
            return list;
        }
        public string Status
        {
            get
            {
                if(_habitatFilter>=0)
                {
                    var kind=(LandAnimalKind)_habitatFilter;var c=_c.Career;
                    return FishingCareer.HabitatName(kind)+" · 已安置 "+c.HousedAnimalCount(kind)+"/"+c.HabitatCapacity(kind)+" · 携带同类 "+CarriedCount(kind)+"只\n"+
                        (c.HabitatLevel(kind)==0?"先用对应套件部署住所。":CarriedCount(kind)==0?"用活捉网捕获或从诱捕笼收取，带回门口按 A / E 安置。":c.HousedAnimalCount(kind)>=c.HabitatCapacity(kind)?"住所已满：在设施建设中扩建，或先将一只动物带上旅途。":"关闭菜单，在门口按 A / E 逐只安置携带的动物。");
                }
                return "水族箱 "+_c.Career.AquariumCount+"/"+_c.Career.AquariumCapacity+" · 活体载具 "+_c.Career.CarriedAnimalCount+"/4 · 动物 "+_c.Career.HomeAnimalCount+"/16\n水族箱在渔屋东侧的岛上。活捉后带回对应住所门口，A / E 安置；也可在动物页选择安置。套件在工作台制作；幼兽2日成年。";
            }
        }
        public bool UseNet()
        {
            if(!Active || !_c.Ashore || _c.Career.Backpack.Equipped!=FieldItem.CaptureNet)return false;
            // Input can arrive before the presentation frame updates the controller pose.
            _c.FieldWorld.ApplyHeldTool();
            if(_net==null || !_net.gameObject.activeInHierarchy)return false;
            var mouth=_net.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,0,0));
            FishingFieldWorld.Animal best=null;float nearest=1.8f;
            foreach(var a in _c.RifleHunting.Targets)
            {
                if(a.Dead || a.Root==null || !a.Root.gameObject.activeInHierarchy || !_c.Career.WildAnimalAvailable(a.Id,a.Kind))continue;
                var point=a.Root.position+Vector3.up*.2f;float d=Vector3.Distance(_c.ExplorerPosition,point);
                // The tracked net mouth defines VR aim. Requiring the headset to face the same point
                // rejects a correct low reach whenever the player looks toward their hand or habitat.
                bool aimed=XRSettings.isDeviceActive || Vector3.Dot(_c.viewCamera.transform.forward,(point-_c.viewCamera.transform.position).normalized)>.65f;
                if(d<nearest && Vector3.Distance(mouth,point)<.6f && aimed && _c.Shore.ClearSight(_c.Shore.Character.EyePosition,point)){best=a;nearest=d;}
            }
            if(best==null){_c.Notice("手持活捉网，蹲下接近动物，把网口送到它身旁；F / 右扳机活捉。",5);return false;}
            int id=_c.Career.CaptureAnimalWithNet(best.Id,best.Kind);
            if(id==0){_c.Notice("活体载具已满或动物不可捕捉；先带回住所。",4);return false;}
            _c.Hands.ReachGesture(best.Root.position);_c.Save();_c.RifleHunting.RefreshAnimals();Refresh();_c.Notice("活捉 "+FishingBackpack.AnimalName(best.Kind)+" #"+id+" · 带回家园安置。",5);return true;
        }
        public bool PlaceTrap()
        {
            if(!Active || !_c.Ashore || _c.Career.Backpack.Equipped!=FieldItem.AnimalTrap || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            var seed=_c.ExplorerPosition+_c.Shore.Actor.forward*1.2f;
            if(!_c.Shore.Ground(seed,out var feet) || !_c.Shore.Clearance(feet) || Mathf.Abs(feet.y-_c.ExplorerPosition.y)>.5f || !_c.Shore.ClearSight(_c.Shore.Character.EyePosition,feet+Vector3.up*.2f)){_c.Notice("前方需要平坦、无遮挡的陆地。",4);return false;}
            int id=_c.Career.PlaceAnimalTrap(TrapKind,feet.x,feet.y,feet.z);
            if(id==0){_c.Notice("无法放笼：最多4个，笼间至少2米；海龟请使用网。",4);return false;}
            _c.Save();Refresh();_c.Notice("已放置"+FishingBackpack.AnimalName(TrapKind)+"诱捕笼 · 靠近按E添加诱饵。",5);return true;
        }
        private void CheckTraps()
        {
            bool changed=false;
            foreach(var t in _c.Career.AnimalTraps)
            {
                if(!t.baited || t.animalId>0)continue;
                foreach(var a in _c.RifleHunting.Targets)
                {
                    if(a.Kind!=t.kind || a.Dead || a.Root==null || !a.Root.gameObject.activeInHierarchy || !_c.Career.WildAnimalAvailable(a.Id,a.Kind))continue;
                    var p=a.Feet;if(!_c.Shore.ClearSight(new Vector3(t.x,t.y+.3f,t.z),p+Vector3.up*.2f))continue;
                    if(_c.Career.CaptureAnimalInTrap(t.id,a.Id,a.Kind,p.x,p.y,p.z)>0){changed=true;break;}
                }
            }
            if(changed){_c.Save();Refresh();_c.RifleHunting.RefreshAnimals();_c.Notice("诱捕笼捕获了动物；靠近笼子收取。",5);}
        }
        private Transform Model(string resource,Transform parent,Vector3 point,float scale=1)
        {
            var source=Resources.Load<GameObject>(resource);if(source==null)return null;
            var go=Instantiate(source,parent);go.name=resource;go.transform.localPosition=point;go.transform.localScale=Vector3.one*scale;
            foreach(var c in go.GetComponentsInChildren<Collider>())Destroy(c);
            if(resource=="FishingProps/CrateBody" || resource=="FishingV023/Items/Plank")foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterial=_c.wood;return go.transform;
        }
        private void Pane(Transform parent,Vector3 center,Vector3 size,Material material=null)
        {
            var pane=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(pane.GetComponent<Collider>());pane.name=material==null?"Aquarium glass pane":material.name;
            pane.transform.SetParent(parent,false);pane.transform.localPosition=center;pane.transform.localScale=size;
            var renderer=pane.GetComponent<Renderer>();renderer.sharedMaterial=material??_glass;if(material==null || material.shader==_glass.shader)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public void Refresh()
        {
            if(_c==null)return;_profile=_c.Career;_day=_profile.Calendar.Day;_sourceCount=_c.RifleHunting.Targets.Count;_growthDays=_profile.AquariumGrowthDays;_fish.Clear();_pets.Clear();_traps.Clear();
            if(_display!=null){_display.gameObject.SetActive(false);Destroy(_display.gameObject);}
            foreach(var owned in _owned)if(owned!=null)Destroy(owned);_owned.Clear();
            _glass=new Material(Resources.Load<Shader>("FishingGlass")){name="Aquarium clear glass"};_owned.Add(_glass);
            _display=new GameObject("家园生态展示").transform;_display.SetParent(transform,false);
            var aquarium=new GameObject("家园水族箱").transform;aquarium.SetParent(_display,false);aquarium.position=AquariumPosition;
            if(_profile.AquariumLevel>0)
            {
                // V044 authored cedar stand and tank interior (sand, mossy rocks, weeds, corals) in the vertex palette.
                var palette=Resources.Load<Material>("FishingV023/CoastalPalette");
                foreach(var (resource,y) in new[]{("FishingV044/AquariumStand",0f),("FishingV044/AquariumScape",.5f)})
                {var part=Model(resource,aquarium,new Vector3(0,y,0));if(part!=null){foreach(var r in part.GetComponentsInChildren<Renderer>())r.sharedMaterial=palette;FishingPaintedArt.Apply(part,resource);}}
                var stand=aquarium.gameObject.AddComponent<BoxCollider>();stand.center=new Vector3(0,.25f,0);stand.size=new Vector3(2.1f,.5f,1);
                var water=new Material(_glass){name="Aquarium water volume"};water.SetColor("_BaseColor",new Color(.35f,.78f,.8f,.16f));_owned.Add(water);
                Pane(aquarium,new Vector3(0,.92f,0),new Vector3(1.97f,.72f,.87f),water);
                // Single flat panes: the Glass item model is a layered bottle and stacked into a milky blue block when stretched.
                foreach(float z in new[]{-.46f,.46f})Pane(aquarium,new Vector3(0,.94f,z),new Vector3(2,.8f,.01f));
                foreach(float x in new[]{-1f,1f})Pane(aquarium,new Vector3(x,.94f,0),new Vector3(.01f,.8f,.9f));
                foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-.46f,.46f})Pane(aquarium,new Vector3(x,.94f,z),new Vector3(.045f,.82f,.045f),_c.wood);
                foreach(float z in new[]{-.46f,.46f})Pane(aquarium,new Vector3(0,1.355f,z),new Vector3(2.05f,.035f,.045f),_c.wood);
                foreach(float x in new[]{-1f,1f})Pane(aquarium,new Vector3(x,1.355f,0),new Vector3(.045f,.035f,.95f),_c.wood);
                foreach(var f in _profile.AquariumFish)
                {
                    var fish=FishingArt.Fish(aquarium,_owned,_c.PaletteShader,f.fish.kind,f.fish.rare).transform;
                    var renderers=fish.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    float mature=Mathf.Clamp(FishingSpecies.LengthMeters(f.fish.kind),.22f,.38f);
                    fish.localScale*=Mathf.Lerp(mature*.65f,mature,_profile.AquariumGrowth(f.fish.id))/Mathf.Max(.001f,bounds.size.x,bounds.size.y,bounds.size.z);_fish.Add(fish);
                }
            }
            for(int i=0;i<5;i++)
            {
                var kind=(LandAnimalKind)i;var root=new GameObject(FishingCareer.HabitatName(kind)).transform;root.SetParent(_display,false);root.position=HabitatPosition(kind);
                if(_profile.HabitatLevel(kind)>0)
                {
                    var mesh=FishingHomeHabitatArt.Build(kind,_profile.HabitatLevel(kind));_owned.Add(mesh);
                    var shell=new GameObject("Shelter, exercise run and feeding station",typeof(MeshFilter),typeof(MeshRenderer));shell.transform.SetParent(root,false);
                    shell.GetComponent<MeshFilter>().sharedMesh=mesh;shell.GetComponent<MeshRenderer>().sharedMaterial=FishingArt.Palette(_owned,_c.PaletteShader,"Coastal animal habitats",.12f,.04f);
                    FishingHomeHabitatArt.AddCollision(root);
                }
                else
                {
                    var mesh=FishingHomeHabitatArt.BuildSite();_owned.Add(mesh);
                    var plot=new GameObject("Marked habitat deployment plot",typeof(MeshFilter),typeof(MeshRenderer));plot.transform.SetParent(root,false);
                    plot.GetComponent<MeshFilter>().sharedMesh=mesh;plot.GetComponent<MeshRenderer>().sharedMaterial=FishingArt.Palette(_owned,_c.PaletteShader,"Habitat plot stakes",.12f,.04f);
                }
                string habitatLabel=FishingCareer.HabitatName(kind)+(_profile.HabitatLevel(kind)==0?" · 待部署套件":" · "+_profile.HousedAnimalCount(kind)+"/"+_profile.HabitatCapacity(kind));
                FishingV019Art.Sign(habitatLabel,root,new Vector3(0,1.24f,2.6f),.011f,new Color(.85f,.92f,.82f));
                int slot=0;
                foreach(var a in _profile.HomeAnimals)if(a.housed && a.kind==kind)
                {
                    Transform pet=null;
                    foreach(var source in _c.RifleHunting.Targets)if(source.Kind==kind && source.Root!=null)
                    {
                        pet=Instantiate(source.Root,root);pet.gameObject.SetActive(true);pet.localRotation=Quaternion.identity;
                        foreach(var col in pet.GetComponentsInChildren<Collider>())Destroy(col);
                        break;
                    }
                    if(pet!=null)
                    {
                        var actor=new GameObject("Housed "+kind+" #"+a.id).transform;actor.SetParent(root,false);pet.SetParent(actor,false);pet.localPosition=Vector3.zero;
                        var bounds=PetBounds(actor);
                        float span=(kind==LandAnimalKind.Fox || kind==LandAnimalKind.SeaTurtle?.52f:.46f)*(_profile.AnimalAdult(a.id)?1:.6f);
                        // Fit the turning diameter: a square turtle grows wider in its AABB halfway through a turn.
                        pet.localScale*=span/Mathf.Max(.001f,new Vector2(bounds.size.x,bounds.size.z).magnitude);
                        bounds=PetBounds(actor);pet.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                        _pets.Add(new Pet{Root=actor,Kind=kind,Id=a.id,Slot=slot});
                        if(!_petCalls.ContainsKey(a.id))_petCalls.Add(a.id,_petTime+.5f+slot*.3f);
                    }
                    slot++;
                }
            }
            foreach(int id in new List<int>(_petCalls.Keys))if(!_pets.Exists(p=>p.Id==id))_petCalls.Remove(id);
            AnimatePets(0);
            foreach(var t in _profile.AnimalTraps){var trap=Model("FishingProps/CrateBody",_display,new Vector3(t.x,t.y,t.z),.45f);if(trap!=null){_traps[t.id]=trap;FishingV019Art.Sign(t.animalId>0?"已捕获":t.baited?"诱饵就绪":"空诱捕笼",trap,Vector3.up*1.3f,.018f,new Color(.85f,.92f,.82f));}}
            _hearts=FishingV019Art.Sign("♥  ♥",_display,_c.Home.Origin+new Vector3(0,1.5f,7.5f),.035f,new Color(1,.35f,.45f)).transform;
        }
        private static Bounds PetBounds(Transform actor)
        {
            var result=new Bounds();bool first=true;
            foreach(var filter in actor.GetComponentsInChildren<MeshFilter>())
            {
                var bounds=filter.sharedMesh.bounds;var matrix=actor.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var corner=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){result=new Bounds(corner,Vector3.zero);first=false;}else result.Encapsulate(corner);
                }
            }
            return result;
        }
        public void PresentNet(bool show)
        {
            if(_net==null && show)
            {_net=Instantiate(_c.LandingNet,transform);_net.name="手持活捉网";foreach(var col in _net.GetComponentsInChildren<Collider>())Destroy(col);}
            if(_net==null)return;_net.gameObject.SetActive(show);if(!show)return;
            _c.Hands.GetToolFrame(out var shoulder,out var aim);
            var palm=shoulder+aim*new Vector3(.16f,-.16f,.40f);
            var rotation=_c.Shore.Actor.rotation*Quaternion.Euler(0,-90,0)*Quaternion.Euler(0,0,-25);
            var hand=Quaternion.LookRotation(Vector3.forward,Vector3.up);
            if(XRSettings.isDeviceActive)
            {
                if(!_c.Hands.TryControllerPalm(false,out var tracked,out var frame)){_net.gameObject.SetActive(false);return;}
                palm=tracked;rotation=frame*Quaternion.Inverse(hand);
            }
            _net.localScale=Vector3.one*.6f;_net.SetPositionAndRotation(palm-rotation*new Vector3(.05f,.035f,0)*.6f,rotation);
            _c.Hands.SetToolTarget(false,_net,new Vector3(.05f,.035f,0),hand,.85f,fingers:FishingFingerPose.Handle);
            _c.Hands.Right.SetGripSurface(_net.TransformPoint(Vector3.zero),_net.TransformPoint(Vector3.right*.22f),.022f*.6f);
        }

        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
