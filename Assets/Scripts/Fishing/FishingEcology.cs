using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>Fixed population, shared meshes and sight-based discovery. No spawning loop or physics herd.</summary>
    public sealed class FishingEcology : MonoBehaviour
    {
        public FishingPrototype Controller;
        public MarineKind? Candidate { get; private set; }
        public int CreatureCount => _creatures.Count;
        public int VisibleCount { get; private set; }
        public int MeshVertices { get; private set; }
        private sealed class Creature
        {
            public Transform transform; public MarineKind kind; public Vector3 home; public float phase, radius;
        }
        private readonly List<Creature> _creatures=new List<Creature>();
        private readonly List<Object> _owned=new List<Object>();
        private readonly List<GameObject> _reefs=new List<GameObject>();
        private readonly RaycastHit[] _hits=new RaycastHit[24];
        private float _nextSense;
        private Material _palette;
        private Transform _candidate;
        private void Start()
        {
            _palette=new Material(Controller.PaletteShader){name="Living bay vertex palette",enableInstancing=true};_owned.Add(_palette);
            for(int kind=0;kind<5;kind++)
            {
                var mesh=FishingMeshBuilder.Animal((MarineKind)kind);_owned.Add(mesh);MeshVertices+=mesh.vertexCount;
                int count=kind==3 || kind==4?3:1;
                for(int i=0;i<count;i++)
                {
                    int zone=kind==0?0:kind==1?2:kind==2?1:i;
                    Vector3 home=kind==4?Controller.harbor:Controller.fishingSpots[zone];
                    var go=MeshObject("Wildlife / "+BayClimate.MarineName((MarineKind)kind),mesh);
                    if(kind==0 || kind==1 || kind==4)
                    {
                        var motion=new MaterialPropertyBlock();
                        motion.SetVector("_FinMotion",new Vector4(kind==4?.2f:kind==1?.16f:.07f,kind==4?3.2f:2f,.25f,i));
                        go.GetComponent<MeshRenderer>().SetPropertyBlock(motion);
                    }
                    // Surface swimmers circle between the 9 m station ring and the reef rocks (~10.8 m inner edge),
                    // inside the 15 m radius the scene generator verified as open water. Schools stay underwater.
                    _creatures.Add(new Creature{transform=go.transform,kind=(MarineKind)kind,home=home,radius=kind==4?10:kind==2?10.5f:kind==3?7.5f:10f,phase=i*2.1f+kind*.65f});
                }
            }
            for(int i=0;i<3;i++)BuildReef(i);
            Debug.Log("[FISHING] Living bay: "+CreatureCount+" wildlife renderers, 3 habitat meshes; "+MeshVertices+" unique vertices.");
        }
        private GameObject MeshObject(string name,Mesh mesh)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=_palette;renderer.shadowCastingMode=ShadowCastingMode.Off;return go;
        }
        private void BuildReef(int zone)
        {
            var b=new FishingMeshBuilder();
            var coral=new Color(.83f,.41f,.31f);var kelp=new Color(.26f,.47f,.35f);var sand=new Color(.73f,.71f,.54f);
            // Build coherent, curved habitat accents outside the fishing station, not scores of GameObjects.
            Vector3 p=new Vector3(0,0,13.5f);
            if(zone==2)
            {
                b.Arc(p+Vector3.up*.55f,2.2f,.32f,0,180,new Color(.58f,.65f,.57f),20);
                b.Ellipsoid(p+new Vector3(-2.2f,.3f,0),new Vector3(.8f,.42f,.65f),sand);
                b.Ellipsoid(p+new Vector3(2.2f,.3f,0),new Vector3(.8f,.42f,.65f),sand);
            }
            for(int i=0;i<6;i++)
            {
                Vector3 basePos=p+new Vector3((i-2.5f)*.67f,.08f,Mathf.Sin(i*4)*.8f);
                if(zone==0)
                {
                    for(int branch=0;branch<3;branch++)
                    {
                        Vector3 mid=basePos+new Vector3((branch-1)*.25f,.46f,0),tip=mid+new Vector3((branch-1)*.12f,.32f,.04f);
                        b.Tube(basePos,mid,.08f,.047f,coral);b.Tube(mid,tip,.047f,.028f,coral);
                        b.Tube(mid,mid+new Vector3(.18f,.15f,0),.035f,.012f,coral);
                        b.Ellipsoid(tip,Vector3.one*.045f,new Color(.94f,.63f,.4f),8,4);
                    }
                }
                else
                {
                    for(int leaf=0;leaf<3;leaf++)
                    {
                        Vector3 mid=basePos+new Vector3((leaf-1)*.17f,.55f,0),tip=mid+new Vector3(.18f,.38f,.06f);
                        b.Fin(basePos,mid+Vector3.right*.085f,mid-Vector3.right*.085f,kelp);
                        b.Fin(mid-Vector3.right*.085f,mid+Vector3.right*.085f,tip,kelp);
                    }
                }
                b.Ellipsoid(basePos+new Vector3(.16f,0,.25f),new Vector3(.17f,.07f,.12f),new Color(.88f,.8f,.66f),12,6);
                if(i%2==0)
                    for(int arm=0;arm<5;arm++)
                    {
                        Vector3 center=basePos+new Vector3(-.18f,.04f,.38f),end=center+Quaternion.Euler(0,arm*72,0)*Vector3.forward*.22f;
                        b.Tube(center,end,.055f,.008f,new Color(.85f,.44f,.27f),6);
                    }
            }
            Mesh mesh=b.Finish("Original sculpted habitat "+zone);_owned.Add(mesh);MeshVertices+=mesh.vertexCount;
            var go=MeshObject("Living reef garden "+zone,mesh);go.transform.position=Controller.fishingSpots[zone];_reefs.Add(go);
        }
        private void Update()
        {
            if(Controller==null || Controller.Player==null || Controller.Paused)return;
            var climate=Controller.Weather.Climate;
            foreach(var c in _creatures)
            {
                // Integrate speed rather than multiplying absolute time: weather changes cannot teleport animals.
                float activity=Mathf.Lerp(BayClimate.Activity(c.kind,climate.Previous),BayClimate.Activity(c.kind,climate.Current),climate.Blend);
                float speed=c.kind==MarineKind.Dolphin?.34f:c.kind==MarineKind.Gull?.22f:c.kind==MarineKind.School?.25f:.11f;
                c.phase=Mathf.Repeat(c.phase+Time.deltaTime*speed*activity,Mathf.PI*2);
                float a=c.phase;Vector3 pos=c.home+new Vector3(Mathf.Cos(a)*c.radius,0,Mathf.Sin(a)*c.radius);
                float roll=0,pitch=0;
                if(c.kind==MarineKind.Gull){pos.y=6+Mathf.Sin(a*2)*.6f;roll=Mathf.Sin(a)*12;}
                else if(c.kind==MarineKind.Dolphin)
                {
                    float leap=Mathf.Max(0,Mathf.Sin(a*3));pos.y=.02f+leap*activity*.95f;pitch=-Mathf.Cos(a*3)*leap*27;
                }
                // The turtle rides low so only its shell breaks the surface.
                else {pos.y=c.kind==MarineKind.School?-.06f:c.kind==MarineKind.Turtle?-.04f:.04f;roll=Mathf.Sin(a*4)*(c.kind==MarineKind.Ray?9:4);}
                c.transform.SetPositionAndRotation(pos,Quaternion.Euler(pitch,-a*Mathf.Rad2Deg,roll));
            }
            if(Time.time<_nextSense)return;_nextSense=Time.time+.25f;Sense();
        }
        private void Sense()
        {
            VisibleCount=0;Candidate=null;_candidate=null;float nearest=28;
            foreach(var c in _creatures)
            {
                bool visible=(Controller.Player.transform.position-c.home).sqrMagnitude<160*160;
                c.transform.gameObject.SetActive(visible);if(!visible)continue;VisibleCount++;
                if(Controller.Career.Seen(c.kind))continue;
                var delta=c.transform.position-Controller.viewCamera.transform.position;float distance=delta.magnitude;
                if(distance>=nearest || !BayClimate.CanObserve(distance,Vector3.Dot(Controller.viewCamera.transform.forward,delta.normalized),true))continue;
                if(!ClearSight(c.transform.position))continue;
                nearest=distance;Candidate=c.kind;_candidate=c.transform;
            }
            foreach(var reef in _reefs)reef.SetActive((Controller.Player.transform.position-reef.transform.position).sqrMagnitude<200*200);
        }
        private bool ClearSight(Vector3 target)
        {
            Vector3 origin=Controller.viewCamera.transform.position,delta=target-origin;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,_hits,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore);
            // A saturated buffer cannot prove visibility.
            if(count==_hits.Length)return false;
            for(int i=0;i<count;i++)if(!_hits[i].transform.IsChildOf(Controller.Player.transform) && _hits[i].collider.GetComponentInParent<WaterSystem.Water>()==null)return false;
            return true;
        }
        public bool Observe()
        {
            if(Controller.Paused)return false;
            Sense();
            if(!Candidate.HasValue || _candidate==null){Controller.Notice("放慢船速，靠近并看向生物（28m 内），再按 T 观察。",4);return false;}
            var kind=Candidate.Value;
            bool added=Controller.RecordWildlife(kind);if(added){Candidate=null;_candidate=null;}return added;
        }
        public Transform FindCreatureForValidation(MarineKind kind)
        {
            if(!Controller.Automated)return null;
            foreach(var c in _creatures)if(c.kind==kind)return c.transform;return null;
        }
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
