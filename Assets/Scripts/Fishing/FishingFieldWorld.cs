using System.Collections.Generic;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld : MonoBehaviour
    {
        public sealed class Pickup
        {
            public int Id, Count;public FieldItem Item;public Transform Root;public Vector3 Feet;
        }
        public sealed class Animal
        {
            public int Id;public bool Dead;public Collider Hitbox;
            public LandAnimalKind Kind;public Transform Root;public Vector3 Home,Feet,Target;public bool Fleeing;public float Rest;
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
        public string ActionTitle=>_nearby!=null?"E 拾取 "+FishingBackpack.Name(_nearby.Item)+" ×"+_nearby.Count:_candidate!=null?"T 观察 "+FishingBackpack.AnimalName(_candidate.Kind):null;
        public void Build(FishingPrototype c)
        {
            _c=c;_palette=new Material(c.PaletteShader){name="Field exploration vertex palette",enableInstancing=true};_owned.Add(_palette);
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
                        _pickups.Add(new Pickup{Id=id,Item=item,Count=item>=FieldItem.Shell?4+id:1,Root=root,Feet=feet});
                    }
                    if(TryLocation(shore.Landing,4f+j*1.3f,40+id*113,out var home))
                    {
                        var kind=(LandAnimalKind)(id%3);var root=MeshObject("Land animal / "+kind,animalMeshes[(int)kind]);root.position=home;
                        _animals.Add(new Animal{Id=id,Kind=kind,Root=root,Home=home,Feet=home,Target=home,Rest=id*.2f});
                    }
                }
            }
            _held=MeshObject("Equipped field tool",_tools[FieldItem.FieldGuide]);_heldMesh=_held.GetComponent<MeshFilter>();
            _lamp=new GameObject("Field lantern light").AddComponent<Light>();_lamp.transform.SetParent(transform,false);
            _lamp.type=LightType.Point;_lamp.color=new Color(1,.72f,.36f);_lamp.range=10;_lamp.intensity=2.5f;_lamp.shadows=LightShadows.None;
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
                if(_c.Shore.Ground(candidate,out feet) && Mathf.Abs(feet.y-seed.y)<.6f && _c.Shore.Clearance(feet))return true;
            }
            return false;
        }
        public void RefreshPickups(){foreach(var p in _pickups)if(p.Root!=null)p.Root.gameObject.SetActive(!_c.Career.Backpack.Collected(p.Id));_nearby=null;_c.Hunting?.RefreshAnimals();}
        private void Update()
        {
            if(_c==null || _c.Paused)return;
            if(Time.time>=_aiAt){TickAnimals(.1f);_aiAt=Time.time+.1f;}
            foreach(var a in _animals)
            {
                if(a.Dead)continue;
                float moving=Vector3.Distance(a.Feet,a.Target)>.12f?1:0;
                float hop=a.Kind==LandAnimalKind.Rabbit?Mathf.Abs(Mathf.Sin(Time.time*8))*moving*.085f:Mathf.Sin(Time.time*5)*moving*.014f;
                a.Root.position=Vector3.Lerp(a.Root.position,a.Feet+Vector3.up*hop,Mathf.Min(1,Time.deltaTime*16));
            }
            foreach(var p in _pickups)if(p.Root!=null && p.Root.gameObject.activeSelf)p.Root.position=p.Feet+Vector3.up*(.32f+Mathf.Sin(Time.time*2+p.Id)*.025f);
            if(Time.time>=_senseAt){Sense();_senseAt=Time.time+.1f;}
        }
        public static float AlertRadius(bool crouched,bool sprinting)=>crouched?1.5f:sprinting?7f:3.5f;
        public void TickAnimals(float dt)
        {
            if(_c.Paused || dt<=0 || float.IsNaN(dt) || float.IsInfinity(dt))return;dt=Mathf.Min(dt,.1f);
            foreach(var a in _animals)
            {
                if(a.Dead)continue;
                var away=Vector3.ProjectOnPlane(a.Feet-_c.ExplorerPosition,Vector3.up);
                a.Fleeing=_c.Ashore && away.magnitude<AlertRadius(_c.Shore.Crouched,_c.Shore.WalkSpeed>3);
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
        private bool Visible(Vector3 target,float range,float dot)
        {
            var camera=_c.viewCamera.transform;var delta=target-camera.position;
            // 交互距离属于角色，不属于退到四米外的第三人称相机。
            return Vector3.Distance(_c.ExplorerPosition,target)<=range && Vector3.Dot(camera.forward,delta.normalized)>dot
                && _c.Shore.ClearSight(_c.Shore.Character.EyePosition,target);
        }
        public void Sense()
        {
            _nearby=null;_candidate=null;if(!_c.Ashore || _c.Paused)return;
            float nearest=float.MaxValue;
            foreach(var p in _pickups)
            {
                float d=Vector3.Distance(p.Feet,_c.Shore.Position);
                if(p.Root!=null && p.Root.gameObject.activeSelf && d<2.5f && d<nearest && Visible(p.Root.position,3.4f,.15f)){_nearby=p;nearest=d;}
            }
            nearest=float.MaxValue;float range=_c.BinocularsRaised?22:8;
            foreach(var a in _animals)
            {
                if(a.Dead)continue;
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
                || Vector3.Distance(pickup.Feet,_c.Shore.Position)>=2.5f || !_c.Shore.ClearSight(_c.Shore.Character.EyePosition,pickup.Root.position))return false;
            bool collected=_c.CollectFieldItem(id,expectedItem,expectedCount);
            if(collected){RefreshPickups();ShowPickupPreview(expectedItem);}
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
        public void ApplyHeldTool()=>PresentHeldTool();
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
