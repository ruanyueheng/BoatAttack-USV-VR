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
            var contents=new GameObject("Water inside the ivory can",typeof(MeshFilter),typeof(MeshRenderer));_canWater=contents.transform;_canWater.SetParent(_can,false);_canWater.localScale=new Vector3(.114f,1,.114f);
            contents.GetComponent<MeshFilter>().sharedMesh=mesh;var contentsRenderer=contents.GetComponent<MeshRenderer>();contentsRenderer.sharedMaterial=_wellWaterMaterial;contentsRenderer.shadowCastingMode=ShadowCastingMode.Off;contentsRenderer.receiveShadows=false;
            var lamp=new GameObject("井壁水色反光");lamp.transform.SetParent(WellWater,false);lamp.transform.localPosition=Vector3.up*.32f;
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.42f,.73f,.83f);light.intensity=.28f;light.range=2.2f;light.shadows=LightShadows.None;
        }
        private void UpdateCanWater()
        {
            if(_canWater==null)return;int water=_c.Career.Farm.Water;
            _canWater.gameObject.SetActive(water>0);_canWater.localPosition=new Vector3(0,Mathf.Lerp(-.18f,.002f,water/(float)FishingFarm.WaterCapacity),.12f);
        }
    }
}
