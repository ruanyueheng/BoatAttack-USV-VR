using System.Collections.Generic;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld : MonoBehaviour
    {
        public sealed class Pickup
        {
            public int Id, Count;public FieldItem Item;public Transform Root;public Vector3 Feet;
            public Transform Lid;public Quaternion ClosedLid;public float OpenedAt=-1,LidAngle;
            public bool Cache=>Id>=8;
        }
        public sealed class Animal
        {
            public bool Dead;public Collider Hitbox;
            public int Id;public LandAnimalKind Kind;public Transform Root;public Vector3 Home,Feet,Target;public bool Fleeing;public float Rest;
        }
        private FishingPrototype _c;
        private readonly List<Pickup> _pickups=new List<Pickup>();
        private readonly List<Animal> _animals=new List<Animal>();
        private readonly List<Object> _owned=new List<Object>();
        private readonly Dictionary<FieldItem,Mesh> _tools=new Dictionary<FieldItem,Mesh>();
        private Material _palette;private Transform _held;private MeshFilter _heldMesh;private Light _lamp;
        private Pickup _nearby;private Animal _candidate;private float _senseAt,_aiAt;private int _wander;
        public IReadOnlyList<Pickup> Pickups=>_pickups;
        public IReadOnlyList<Animal> Animals=>_animals;
        public string ActionTitle=>_nearby!=null?(_nearby.Cache?"E 打开岸边补给箱":"E 拾取 "+FishingBackpack.Name(_nearby.Item)+" ×"+_nearby.Count):_candidate!=null?(_c.Career.Backpack.Equipped==FieldItem.HuntingSpear && FishingHunting.Huntable(_candidate.Kind)?"F 刺击 · 蹲下接近 ":"T 观察 ")+FishingBackpack.AnimalName(_candidate.Kind):null;
        public void Build(FishingPrototype c)
        {
            _c=c;_palette=new Material(c.PaletteShader){name="Field exploration vertex palette",enableInstancing=true};_owned.Add(_palette);
            _palette.SetFloat("_Gloss",.12f);_palette.SetFloat("_Rim",.06f);
            foreach(FieldItem item in new[]{FieldItem.FieldGuide,FieldItem.Binoculars,FieldItem.Lantern,FieldItem.Shell,FieldItem.Driftwood})
            {var mesh=FishingFieldArt.Tool(item);_tools.Add(item,mesh);_owned.Add(mesh);}
            foreach(var item in new[]{FieldItem.StonePickaxe,FieldItem.IronPickaxe}){var mesh=FishingWorkshopArt.Make(item);_tools.Add(item,mesh);_owned.Add(mesh);}
            var animalMeshes=new Mesh[3];for(int i=0;i<3;i++){animalMeshes[i]=FishingFieldArt.Animal((LandAnimalKind)i);_owned.Add(animalMeshes[i]);}
            for(int site=0;site<c.Shore.Sites.Length;site++)
            {
                var shore=c.Shore.Sites[site];if(!shore.Available)continue;
                for(int j=0;j<2;j++)
                {
                    int id=site*2+j;
                    if(TryLocation(shore.Landing,1.8f+j*.8f,id*77,out var feet))
                    {
                        FieldItem item=id==0?FieldItem.Binoculars:id==2?FieldItem.Lantern:id%2==0?FieldItem.Driftwood:FieldItem.Shell;
                        var root=MeshObject("Pickup "+id+" / "+item,_tools[item]);root.position=feet+Vector3.up*.3f;
                        var suppliedMaterials=FishingUserTools.Materials(item);if(suppliedMaterials!=null)root.GetComponent<Renderer>().sharedMaterials=suppliedMaterials;
                        _pickups.Add(new Pickup{Id=id,Item=item,Count=item>=FieldItem.Shell?4+id:1,Root=root,Feet=feet});
                    }
                    if(TryLocation(shore.Landing,4f+j*1.3f,40+id*113,out var home))
                    {
                        var kind=(LandAnimalKind)(id%3);var root=MeshObject("Land animal / "+kind,animalMeshes[(int)kind]);root.position=home;
                        FishingV029Art.Apply(root.GetComponent<Renderer>(),kind==LandAnimalKind.Rabbit?"Rabbit":kind==LandAnimalKind.Fox?"Fox":null,_owned,c.PaletteShader);
                        _animals.Add(new Animal{Id=id,Kind=kind,Root=root,Home=home,Feet=home,Target=home,Rest=id*.2f});
                    }
                }
            }
            var cacheLoot=new[]{FieldItem.SeaTreasure,FieldItem.Fiber,FieldItem.IronOre,FieldItem.FishFeed,FieldItem.SeaTreasure,FieldItem.TurnipSeeds,FieldItem.Glass,FieldItem.Plank};
            var cacheCounts=new[]{1,8,4,6,1,8,3,4};
            for(int i=0;i<8;i++)
            {
                var seed=FishingIslandWorld.Landing(i/2);seed.y=1.08f;
                if(!TryLocation(seed,5+i%2*8,70+i*137.5f,out var feet))throw new System.InvalidOperationException("No supported cache position: "+i);
                var source=Resources.Load<GameObject>("FishingFeedbackTools/HomeChest");
                if(source==null)throw new System.InvalidOperationException("Wild caches need the shared hinged chest");
                var chest=Instantiate(source,transform).transform;chest.name="Shore supply cache "+(8+i);chest.position=feet;
                chest.localScale*=1.15f;
                foreach(var renderer in chest.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=_palette;
                FishingPaintedArt.Apply(chest,"FishingFeedbackTools/HomeChest");
                Transform lid=null;foreach(var part in chest.GetComponentsInChildren<Transform>())if(part.name.Equals("lid",System.StringComparison.OrdinalIgnoreCase)){lid=part;break;}
                if(lid==null)throw new System.InvalidOperationException("Wild cache lid missing");
                var renderers=chest.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var collider=chest.gameObject.AddComponent<BoxCollider>();collider.center=chest.InverseTransformPoint(bounds.center);collider.size=bounds.size/chest.lossyScale.x;
                _pickups.Add(new Pickup{Id=8+i,Item=cacheLoot[i],Count=cacheCounts[i],Root=chest,Feet=feet,Lid=lid,ClosedLid=lid.localRotation,LidAngle=-Mathf.Sign(lid.GetComponent<MeshFilter>().sharedMesh.bounds.center.z)*105});
            }
            _held=MeshObject("Equipped field tool",_tools[FieldItem.FieldGuide]);_heldMesh=_held.GetComponent<MeshFilter>();
            _lamp=new GameObject("Field lantern light").AddComponent<Light>();_lamp.transform.SetParent(transform,false);
            _lamp.type=LightType.Point;_lamp.color=new Color(1,.67f,.30f);_lamp.range=11;_lamp.intensity=2.6f;
            _lamp.shadows=UnityEngine.XR.XRSettings.isDeviceActive?LightShadows.None:LightShadows.Soft;
            _lamp.shadowStrength=.72f;_lamp.shadowBias=.035f;_lamp.shadowNormalBias=.12f;_lamp.renderMode=LightRenderMode.ForcePixel;
            RefreshPickups();Debug.Log("[FIELD] Built "+_pickups.Count+" pickups and "+_animals.Count+" ground-constrained land animals.");
        }
        private Transform MeshObject(string name,Mesh mesh)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=_palette;return go.transform;
        }
        public bool TryLocation(Vector3 seed,float radius,float angle,out Vector3 feet)
        {
            feet=seed;
            for(int i=0;i<48;i++)
            {
                float a=(angle+i*137.5f)*Mathf.Deg2Rad,r=radius+(i/12)*.5f;
                var candidate=seed+new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);
                if(_c.Shore.Ground(candidate,out feet) && Mathf.Abs(feet.y-seed.y)<.6f && _c.Shore.Clearance(feet) && !OnWalkRoute(feet))return true;
            }
            return false;
        }
        private List<(Vector3 a,Vector3 b)> _routes;
        // Spawned props must leave the jetty and the authored landing-to-portal walk clear for the capsule.
        private bool OnWalkRoute(Vector3 feet)
        {
            if(_routes==null)
            {
                _routes=new List<(Vector3,Vector3)>();
                for(int island=0;island<4;island++)
                {
                    var centre=FishingIslandWorld.Center(island);
                    _routes.Add((centre+new Vector3(0,0,FishingIslandWorld.Radius+7),FishingIslandWorld.Landing(island)));
                    var path=_c.Islands!=null && _c.Islands.WorldRoot!=null && _c.Islands.WorldRoot.childCount>island?_c.Islands.WorldRoot.GetChild(island).Find("V035 crafted coastal scenery/Island approach"):null;
                    if(path==null)continue;
                    var points=new List<Transform>();foreach(Transform t in path)points.Add(t);
                    points.Sort((x,y)=>int.Parse(x.name.Substring(6)).CompareTo(int.Parse(y.name.Substring(6))));
                    for(int i=1;i<points.Count;i++)_routes.Add((points[i-1].position,points[i].position));
                }
            }
            const float clearance=2f;
            foreach(var (a,b) in _routes)
            {
                var flatA=new Vector2(a.x,a.z);var flatB=new Vector2(b.x,b.z);var p=new Vector2(feet.x,feet.z);var ab=flatB-flatA;
                float t=ab.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-flatA,ab)/ab.sqrMagnitude);
                if(Vector2.Distance(p,flatA+ab*t)<clearance)return true;
            }
            return false;
        }
        public void RefreshPickups()
        {
            foreach(var p in _pickups)if(p.Root!=null)
            {
                bool collected=_c.Career.Backpack.Collected(p.Id);
                if(!collected){p.OpenedAt=-1;if(p.Lid!=null)p.Lid.localRotation=p.ClosedLid;}
                p.Root.gameObject.SetActive(!collected || (p.OpenedAt>=0 && Time.time-p.OpenedAt<.85f));
            }
            _nearby=null;_c.RifleHunting?.RefreshAnimals();
        }
        private void Update()
        {
            if(_c==null)return;
            foreach(var animal in _animals)
            {
                if(animal.Dead)continue;
                bool available=_c.Career.WildAnimalAvailable(animal.Id,animal.Kind);
                if(available && !animal.Root.gameObject.activeSelf)
                {
                    animal.Feet=animal.Target=animal.Home;animal.Fleeing=false;animal.Rest=2;
                    animal.Root.position=animal.Home;
                }
                animal.Root.gameObject.SetActive(available);
            }
            if(_c.Paused || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)){CancelHunt();return;}
            TickHunt(Time.deltaTime);
            if(Time.time>=_aiAt){TickAnimals(.1f);_aiAt=Time.time+.1f;}
            foreach(var a in _animals)
            {
                if(a.Dead || !a.Root.gameObject.activeSelf)continue;
                float moving=Vector3.Distance(a.Feet,a.Target)>.12f?1:0;
                float hop=a.Kind==LandAnimalKind.Rabbit?Mathf.Abs(Mathf.Sin(Time.time*8))*moving*.085f:Mathf.Sin(Time.time*5)*moving*.014f;
                a.Root.position=Vector3.Lerp(a.Root.position,a.Feet+Vector3.up*hop,Mathf.Min(1,Time.deltaTime*16));
            }
            foreach(var p in _pickups)if(p.Root!=null && p.Root.gameObject.activeSelf)
            {
                if(!p.Cache){p.Root.position=p.Feet+Vector3.up*(.32f+Mathf.Sin(Time.time*2+p.Id)*.025f);continue;}
                if(p.OpenedAt<0)continue;
                float age=Time.time-p.OpenedAt;p.Lid.localRotation=p.ClosedLid*Quaternion.Euler(p.LidAngle*Mathf.SmoothStep(0,1,age/.7f),0,0);
                if(age>=.85f)p.Root.gameObject.SetActive(false);
            }
            if(Time.time>=_senseAt){Sense();_senseAt=Time.time+.1f;}
        }
        public static float AlertRadius(bool crouched,bool sprinting)=>crouched?1.5f:sprinting?7f:3.5f;
        public void TickAnimals(float dt)
        {
            if(_c.Paused || dt<=0 || float.IsNaN(dt) || float.IsInfinity(dt))return;dt=Mathf.Min(dt,.1f);
            foreach(var a in _animals)
            {
                if(a.Dead || !a.Root.gameObject.activeSelf)continue;
                var away=Vector3.ProjectOnPlane(a.Feet-_c.ExplorerPosition,Vector3.up);
                float alert=_c.Shore.Crouched && (_c.Career.Backpack.Equipped==FieldItem.HuntingSpear || _c.Career.Backpack.Equipped==FieldItem.CaptureNet)?.85f:AlertRadius(_c.Shore.Crouched,_c.Shore.WalkSpeed>3);
                a.Fleeing=_c.Ashore && away.magnitude<alert;
                a.Rest-=dt;
                if(a.Fleeing && away.sqrMagnitude>.01f)
                {
                    var next=a.Feet+away.normalized*1.5f;
                    if(Vector3.Distance(next,a.Home)<5f && SafeStep(a.Feet,next,out var flee))a.Target=flee;
                }
                else if(a.Rest<=0 && Vector3.Distance(a.Feet,a.Target)<.2f)
                {
                    if(TryLocation(a.Home,1.5f,++_wander*137.5f,out var target) && SafeStep(a.Feet,target,out var end))a.Target=end;
                    a.Rest=2.5f;
                }
                float speed=(a.Kind==LandAnimalKind.Tortoise?.3f:.8f)*(a.Fleeing?2.7f:1);
                var desired=Vector3.MoveTowards(a.Feet,a.Target,speed*dt);
                if(SafeStep(a.Feet,desired,out var point))
                {
                    var direction=Vector3.ProjectOnPlane(point-a.Feet,Vector3.up);
                    if(direction.sqrMagnitude>.00001f)a.Root.rotation=Quaternion.Slerp(a.Root.rotation,Quaternion.LookRotation(direction),.35f);
                    a.Feet=point;
                }
                else {a.Target=a.Feet;a.Rest=0;}
            }
        }
        private bool SafeStep(Vector3 from,Vector3 to,out Vector3 end)
        {
            end=from;int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.25f));
            for(int i=1;i<=steps;i++)
            {
                var seed=Vector3.Lerp(from,to,(float)i/steps);
                if(!_c.Shore.Ground(seed,out var p) || Mathf.Abs(p.y-end.y)>.25f || !_c.Shore.Clearance(p))return false;end=p;
            }
            return true;
        }
        private bool Visible(Vector3 target,float range,float dot,Transform owner=null)
        {
            var camera=_c.viewCamera.transform;var delta=target-camera.position;
            // 交互距离属于角色，不属于退到四米外的第三人称相机。
            return Vector3.Distance(_c.ExplorerPosition,target)<=range && Vector3.Dot(camera.forward,delta.normalized)>dot
                && _c.Shore.ClearSight(_c.Shore.Character.EyePosition,target,owner);
        }
        public void Sense()
        {
            _nearby=null;_candidate=null;if(!_c.Ashore || _c.Paused)return;
            float nearest=float.MaxValue;
            foreach(var p in _pickups)
            {
                if(_c.Career.Backpack.Collected(p.Id))continue;
                float d=Vector3.Distance(p.Feet,_c.Shore.Position);
                if(p.Root!=null && p.Root.gameObject.activeSelf && d<2.5f && d<nearest && Visible(p.Root.position+(p.Cache?Vector3.up*.3f:Vector3.zero),3.4f,.15f,p.Cache?p.Root:null)){_nearby=p;nearest=d;}
            }
            nearest=float.MaxValue;float range=_c.BinocularsRaised?22:8;
            foreach(var a in _animals)
            {
                if(a.Dead || !a.Root.gameObject.activeSelf)continue;
                var target=a.Root.position+Vector3.up*.4f;float d=Vector3.Distance(target,_c.viewCamera.transform.position);
                if(d<nearest && Visible(target,range,.92f)){_candidate=a;nearest=d;}
            }
        }
        public bool TryPickupTarget(out Pickup pickup,bool refresh=true){if(refresh)Sense();pickup=_nearby;return pickup!=null;}
        public bool PickupById(int id,FieldItem expectedItem,int expectedCount)
        {
            if(_c.Paused || !_c.Ashore)return false;
            Pickup pickup=null;foreach(var p in _pickups)if(p.Id==id){pickup=p;break;}
            if(pickup==null || pickup.Item!=expectedItem || pickup.Count!=expectedCount || pickup.Root==null || !pickup.Root.gameObject.activeInHierarchy
                || Vector3.Distance(pickup.Feet,_c.Shore.Position)>=2.5f || !_c.Shore.ClearSight(_c.Shore.Character.EyePosition,pickup.Root.position+(pickup.Cache?Vector3.up*.3f:Vector3.zero),pickup.Cache?pickup.Root:null))return false;
            bool collected=_c.CollectFieldItem(id,expectedItem,expectedCount);
            if(collected){if(pickup.Cache){pickup.OpenedAt=Time.time;_c.WorldAudio?.Play("MechanismClick",pickup.Root.position,.4f);}RefreshPickups();ShowPickupPreview(expectedItem);}
            return collected;
        }
        public bool TryPickup()
        {
            if(!TryPickupTarget(out var pickup))return false;
            PickupById(pickup.Id,pickup.Item,pickup.Count);
            return true; // 满包仍消费互动，不误触其他目标。
        }
        public bool Observe()
        {
            Sense();if(_candidate==null){_c.Notice("看向附近陆地动物（8m 内）再按 T；望远镜可观察至 22m。Ctrl 蹲下不易惊动动物。",5);return false;}
            return _c.RecordLandAnimal(_candidate.Kind);
        }
        public void ApplyHeldTool(){UpdateLanternGrip();PresentHeldTool();PresentLanternHolster();PresentLanternLight();}
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
