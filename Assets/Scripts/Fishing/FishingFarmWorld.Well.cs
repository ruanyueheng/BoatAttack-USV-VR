using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFarmWorld
    {
        public const float WellWaterLevel=-.35f;
        public const float WellShaftBottom=-5.4f;
        private Material _wellWaterMaterial;
        private Transform _canWater;
        private Mesh _canLiquidMesh;
        private readonly Vector3[] _liquidVertices=new Vector3[65],_liquidNormals=new Vector3[65];
        private Material _canWaterMaterial;
        // shortcut: ellipsoid-calibrated fill heights; use a volume solver if exact physical litres are required.
        private static readonly Vector3 LiquidCentre=new Vector3(0,-.112f,-.12f),LiquidRadii=new Vector3(.130f,.097f,.114f);
        private static readonly float[] CanHeights={-.218f,-.211f,-.186f,-.14f,-.09f,-.047f,-.013f,.008f},CanRadii={.066f,.095f,.119f,.139f,.144f,.128f,.101f,.092f};
        private Vector3 _liquidNormal;
        private int _liquidAmount=-1;
        public Transform CanWater=>_canWater;
        public Transform WellWater {get;private set;}

        private void BuildWellWater()
        {
            const int count=80;
            var vertices=new Vector3[count+1];var triangles=new int[count*3];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;vertices[i+1]=new Vector3(Mathf.Cos(a)*.706f,0,Mathf.Sin(a)*.706f);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%count+1;triangles[i*3+2]=i+1;
            }
            var mesh=new Mesh{name="V036 deep well water surface"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();_canMeshes.Add(mesh);
            _wellWaterMaterial=new Material(Resources.Load<Shader>("FishingSanctuaryWater")){name="V036 clear blue well water"};
            _wellWaterMaterial.SetColor("_DeepColor",new Color(.035f,.22f,.29f));_wellWaterMaterial.SetColor("_ShallowColor",new Color(.16f,.43f,.46f));
            var water=new GameObject("深井清水 · 井沿下方的真实水面",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            WellWater=water.transform;WellWater.SetParent(Root,false);WellWater.localPosition=new Vector3(10.2f,WellWaterLevel,-8);water.layer=4;
            water.GetComponent<MeshFilter>().sharedMesh=mesh;water.GetComponent<MeshCollider>().sharedMesh=mesh;
            var renderer=water.GetComponent<MeshRenderer>();renderer.sharedMaterial=_wellWaterMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            var contents=new GameObject("Water inside the ivory can",typeof(MeshFilter),typeof(MeshRenderer));_canWater=contents.transform;_canWater.SetParent(_can,false);
            _canLiquidMesh=new Mesh{name="V038 contained gravity-level liquid"};_canLiquidMesh.MarkDynamic();
            var cap=new int[64*3];for(int i=0;i<64;i++){cap[i*3]=0;cap[i*3+1]=i+1;cap[i*3+2]=(i+1)%64+1;}
            _canLiquidMesh.vertices=_liquidVertices;_canLiquidMesh.triangles=cap;_canMeshes.Add(_canLiquidMesh);
            _canWaterMaterial=new Material(_wellWaterMaterial){name="V038 contained can water"};_canWaterMaterial.SetFloat("_WaveAmplitude",.0005f);_canWaterMaterial.SetFloat("_WaveFrequency",22);
            contents.GetComponent<MeshFilter>().sharedMesh=_canLiquidMesh;var contentsRenderer=contents.GetComponent<MeshRenderer>();contentsRenderer.sharedMaterial=_canWaterMaterial;contentsRenderer.shadowCastingMode=ShadowCastingMode.Off;contentsRenderer.receiveShadows=false;
            var lamp=new GameObject("井壁水色反光");lamp.transform.SetParent(WellWater,false);lamp.transform.localPosition=Vector3.up*.32f;
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.42f,.73f,.83f);light.intensity=.28f;light.range=2.2f;light.shadows=LightShadows.None;
        }
        private void UpdateCanWater()
        {
            if(_canWater==null)return;int water=_c.Career.Farm.AvailableWater;
            _canWater.gameObject.SetActive(water>0);if(water<=0)return;
            var normal=_can.InverseTransformDirection(Vector3.up).normalized;
            if(water==_liquidAmount && (normal-_liquidNormal).sqrMagnitude<1e-10f)return;
            var scaledNormal=Vector3.Scale(normal,LiquidRadii).normalized;
            // A sphere cap has fraction (2 + 3h - h^3)/4. Its affine ellipsoid stays inside the pear-shaped shell.
            float low=-1,high=1,fraction=.94f*water/FishingFarm.WaterCapacity;
            for(int i=0;i<16;i++){float h=(low+high)*.5f;if((2+3*h-h*h*h)*.25f<fraction)low=h;else high=h;}
            float height=(low+high)*.5f,radius=Mathf.Sqrt(1-height*height);
            var u=Vector3.Cross(scaledNormal,Mathf.Abs(scaledNormal.z)<.9f?Vector3.forward:Vector3.right).normalized;
            var v=Vector3.Cross(scaledNormal,u);
            _liquidVertices[0]=LiquidCentre+Vector3.Scale(scaledNormal*height,LiquidRadii);_liquidNormals[0]=normal;
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64;
                var direction=Vector3.Scale((u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius,LiquidRadii);
                float inside=0,outside=4;
                for(int j=0;j<14;j++)
                {
                    float step=(inside+outside)*.5f;
                    if(InsideCan(_liquidVertices[0]+direction*step))inside=step;else outside=step;
                }
                _liquidVertices[i+1]=_liquidVertices[0]+direction*inside;
                _liquidNormals[i+1]=normal;
            }
            _canLiquidMesh.vertices=_liquidVertices;_canLiquidMesh.normals=_liquidNormals;_canLiquidMesh.RecalculateBounds();
            _liquidAmount=water;_liquidNormal=normal;
        }
        private static bool InsideCan(Vector3 p)
        {
            if(p.y<=-.2105f || p.y>=.007f)return false;
            for(int i=1;i<CanHeights.Length;i++)if(p.y>=CanHeights[i-1] && p.y<=CanHeights[i])
            {
                float r=Mathf.Lerp(CanRadii[i-1],CanRadii[i],Mathf.InverseLerp(CanHeights[i-1],CanHeights[i],p.y))-.006f;
                return p.x*p.x+(p.z+.12f)*(p.z+.12f)/(.88f*.88f)<=r*r;
            }
            return false;
        }
    }
}
