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
        private readonly List<Transform> _fish=new List<Transform>(),_pets=new List<Transform>();
        private readonly Dictionary<int,Transform> _traps=new Dictionary<int,Transform>();
        private FishingCareer _profile;
        private int _day=-1,_sourceCount,_growthDays;
        private Material _glass;
        private Vector3 _heartPoint;
        private float _trapAt,_heartUntil;
        private Transform _hearts;
        public LandAnimalKind TrapKind=LandAnimalKind.Rabbit;
        public int Page;
        public bool NearHome=>_c.Ashore && FishingPrototype.FlatDistance(_c.ExplorerPosition,_c.Home.Origin)<22 && !_c.Shore.Swimming && !_c.Shore.Climbing;
        public Vector3 AquariumPosition=>_c.Home.Origin+new Vector3(2,.25f,-1.5f);
        public Vector3 HabitatPosition(LandAnimalKind kind)=>_c.Home.Origin+new Vector3(-10+(int)kind*4,0,7.5f);
        public void Build(FishingPrototype c){_c=c;Refresh();}
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
            for(int i=0;i<_pets.Count;i++)if(_pets[i]!=null && Active)_pets[i].localRotation=Quaternion.Euler(0,Mathf.Sin(Time.time*.35f+i)*35,0);
            if(_hearts!=null){_hearts.gameObject.SetActive(Time.time<_heartUntil);_hearts.position=_heartPoint+Vector3.up*(1.1f+(1-(_heartUntil-Time.time)/3)*.5f);_hearts.rotation=Quaternion.LookRotation(_hearts.position-_c.viewCamera.transform.position);}
        }
        public string NearbyHint
        {
            get
            {
                if(!_c.Ashore)return null;
                if(NearHome && Vector3.Distance(_c.ExplorerPosition,AquariumPosition)<2.4f)return "E 家园水族箱 · 展示、喂养与成长";
                for(int i=0;i<5;i++)if(NearHome && Vector3.Distance(_c.ExplorerPosition,HabitatPosition((LandAnimalKind)i))<2.5f)return "E "+FishingCareer.HabitatName((LandAnimalKind)i)+" · 饲养与繁殖";
                return NearbyTrap()!=null?"E 诱捕笼 · 上饵、收取或收回":null;
            }
        }
        public bool TryInteract(){if(NearbyHint==null)return false;Open();return true;}
        public void Open()
        {
            Page=0;
            if(XRSettings.isDeviceActive){_c.XRNotebook.Open(21);return;}
            if(!_c.InventoryOpen)_c.ToggleInventory();
            if(_c.InventoryOpen)_c.OpenHomeEcologyPage();
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
                Add(c.AquariumLevel==0?"安装水族箱 · 需要水族箱套件":"扩建水族箱至8格 · 潮境蓝图 +300币",c.AquariumLevel==0?(Func<bool>)c.BuildAquarium:c.UpgradeAquarium,"水族箱已更新。",NearHome && c.AquariumLevel<2);
                for(int i=0;i<5;i++)
                {
                    var kind=(LandAnimalKind)i;int level=c.HabitatLevel(kind);
                    Add(FishingCareer.HabitatName(kind)+" "+c.HousedAnimalCount(kind)+"/"+c.HabitatCapacity(kind)+(level==0?" · 安装套件":level==1?" · 林境蓝图+250币扩建":" · 已满级"),()=>level==0?c.BuildAnimalHabitat(kind):c.UpgradeAnimalHabitat(kind),"栖息设施已更新。",NearHome && level<2);
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
                foreach(var a in animals)
                {
                    int id=a.id;string name="#"+id+" "+FishingBackpack.AnimalName(a.kind)+(a.female?" ♀":" ♂")+(c.AnimalAdult(id)?" 成年":" 幼兽")+(a.housed?" · 已安家":a.trapId>0?" · 笼内":" · 携带中");
                    if(a.trapId>0)continue;
                    Add(name+(a.housed?" · 带上旅途":" · 安置住所"),()=>a.housed?c.CarryHomeAnimal(id):c.HouseAnimal(id),"动物安置已更新。",NearHome);
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
            return list;
        }
        public string Status=>"水族箱 "+_c.Career.AquariumCount+"/"+_c.Career.AquariumCapacity+" · 活体载具 "+_c.Career.CarriedAnimalCount+"/4 · 动物 "+_c.Career.HomeAnimalCount+"/16\n鱼需"+_c.Career.AquariumGrowthDays+"日与不同日喂食"+_c.Career.AquariumGrowthDays+"次；幼兽2日成年。暂停/离线不成长；兔吃种子，龟吃萝卜，狐/鸟吃兔肉。";
        public bool UseNet()
        {
            if(!Active || !_c.Ashore || _c.Career.Backpack.Equipped!=FieldItem.CaptureNet)return false;
            PresentNet();if(_net==null || !_net.gameObject.activeInHierarchy)return false;
            var mouth=_net.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,0,0));
            FishingFieldWorld.Animal best=null;float nearest=1.8f;
            foreach(var a in _c.RifleHunting.Targets)
            {
                if(a.Dead || a.Root==null || !a.Root.gameObject.activeInHierarchy || !_c.Career.WildAnimalAvailable(a.Id,a.Kind))continue;
                var point=a.Root.position+Vector3.up*.2f;float d=Vector3.Distance(_c.ExplorerPosition,point);
                if(d<nearest && Vector3.Distance(mouth,point)<.6f && Vector3.Dot(_c.viewCamera.transform.forward,(point-_c.viewCamera.transform.position).normalized)>.65f && _c.Shore.ClearSight(_c.Shore.Character.EyePosition,point)){best=a;nearest=d;}
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
        private Transform FittedModel(string resource,Transform parent,Vector3 center,Vector3 size,bool solid=false)
        {
            var part=Model(resource,parent,center);if(part==null)return null;
            var renderers=part.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return part;
            if(resource=="FishingV023/Items/Glass")foreach(var renderer in renderers){renderer.sharedMaterial=_glass;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            part.localScale=Vector3.Scale(part.localScale,new Vector3(size.x/Mathf.Max(.001f,bounds.size.x),size.y/Mathf.Max(.001f,bounds.size.y),size.z/Mathf.Max(.001f,bounds.size.z)));
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);part.position+=parent.TransformPoint(center)-bounds.center;
            if(solid){var box=parent.gameObject.AddComponent<BoxCollider>();box.center=center;box.size=size;}
            return part;
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
                FittedModel("FishingV023/Home/tableCoffee",aquarium,new Vector3(0,.25f,0),new Vector3(2.1f,.5f,1),true);
                FittedModel("FishingV023/Items/Plank",aquarium,new Vector3(0,.51f,0),new Vector3(2,.08f,.9f));
                foreach(float z in new[]{-.46f,.46f})FittedModel("FishingV023/Items/Glass",aquarium,new Vector3(0,.94f,z),new Vector3(2,.8f,.025f));
                foreach(float x in new[]{-1f,1f})FittedModel("FishingV023/Items/Glass",aquarium,new Vector3(x,.94f,0),new Vector3(.025f,.8f,.9f));
                FittedModel("FishingV035/ShoreRock",aquarium,new Vector3(-.65f,.58f,.25f),new Vector3(.30f,.14f,.22f));
                FittedModel("FishingV035/ShoreRock",aquarium,new Vector3(.63f,.57f,-.22f),new Vector3(.23f,.12f,.19f));
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
                var kind=(LandAnimalKind)i;var root=new GameObject(FishingCareer.HabitatName(kind)).transform;root.SetParent(_display,false);var desired=HabitatPosition(kind);if(_c.Shore.Ground(new Vector3(desired.x,0,desired.z),out var ground))desired.y=ground.y;root.position=desired;
                if(_profile.HabitatLevel(kind)>0)
                {
                    if(kind==LandAnimalKind.Rabbit || kind==LandAnimalKind.Fox)
                    {
                        FittedModel("FishingProps/CrateBody",root,new Vector3(0,.35f,0),new Vector3(1,.7f,.7f),true);
                        foreach(float side in new[]{-1f,1f}){var roof=FittedModel("FishingV023/Items/Plank",root,new Vector3(side*.28f,.82f,0),new Vector3(.7f,.08f,1));if(roof!=null)roof.localRotation=Quaternion.Euler(0,0,side*-25);}
                    }
                    foreach(float x in new[]{-1.3f,1.3f})foreach(float z in new[]{-.5f,2.3f})FittedModel("FishingV023/Items/Plank",root,new Vector3(x,.35f,z),new Vector3(.1f,.7f,.1f),true);
                    foreach(float z in new[]{-.5f,2.3f})FittedModel("FishingV023/Items/Plank",root,new Vector3(0,.4f,z),new Vector3(2.6f,.1f,.08f),true);
                    foreach(float x in new[]{-1.3f,1.3f})FittedModel("FishingV023/Items/Plank",root,new Vector3(x,.4f,.9f),new Vector3(.08f,.1f,2.8f),true);
                    if(kind==LandAnimalKind.Gull)FittedModel("FishingV023/Items/Plank",root,new Vector3(0,.9f,.8f),new Vector3(2.1f,.08f,.08f));
                    if(kind==LandAnimalKind.SeaTurtle)
                    {var water=FittedModel("FishingV023/Items/Glass",root,new Vector3(0,.09f,.9f),new Vector3(2.3f,.08f,2.4f));if(water!=null)foreach(var r in water.GetComponentsInChildren<Renderer>()){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.2f,.6f,.75f,.3f));r.SetPropertyBlock(block);}}
                }
                FishingV019Art.Sign(FishingCareer.HabitatName(kind),root,Vector3.up*1.3f,.011f,new Color(.85f,.92f,.82f));
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
                    if(pet!=null){pet.localPosition=new Vector3((slot%4-1.5f)*.5f,.08f,1+(slot/4)*.55f);pet.localScale*=_profile.AnimalAdult(a.id)?.5f:.28f;_pets.Add(pet);}slot++;
                }
            }
            foreach(var t in _profile.AnimalTraps){var trap=Model("FishingProps/CrateBody",_display,new Vector3(t.x,t.y,t.z),.45f);if(trap!=null){_traps[t.id]=trap;FishingV019Art.Sign(t.animalId>0?"已捕获":t.baited?"诱饵就绪":"空诱捕笼",trap,Vector3.up*1.3f,.018f,new Color(.85f,.92f,.82f));}}
            _hearts=FishingV019Art.Sign("♥  ♥",_display,_c.Home.Origin+new Vector3(0,1.5f,7.5f),.035f,new Color(1,.35f,.45f)).transform;
        }
        private void PresentNet()
        {
            bool show=_c.Ashore && _c.Career.Backpack.Equipped==FieldItem.CaptureNet && _c.Hands.CanPresentTools;
            if(_net==null && show)
            {_net=Instantiate(_c.LandingNet,transform);_net.name="手持活捉网";foreach(var col in _net.GetComponentsInChildren<Collider>())Destroy(col);}
            if(_net==null)return;_net.gameObject.SetActive(show);if(!show)return;
            var palm=_c.Shore.Character.PalmPosition(false);
            var rotation=_c.Shore.Actor.rotation*Quaternion.Euler(0,-90,0)*Quaternion.Euler(0,0,-25);
            if(XRSettings.isDeviceActive && _c.Hands.TryControllerPalm(false,out var tracked,out var frame))
            {palm=tracked;rotation=frame*Quaternion.Inverse(Quaternion.LookRotation(Vector3.right,Vector3.up));}
            _net.localScale=Vector3.one*.6f;_net.SetPositionAndRotation(palm-rotation*new Vector3(.05f,.035f,0)*.6f,rotation);
        }
        private void LateUpdate(){if(_c!=null)PresentNet();}

        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
