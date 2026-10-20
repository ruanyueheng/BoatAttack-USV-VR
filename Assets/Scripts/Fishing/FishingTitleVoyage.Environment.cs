using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingTitleVoyage
    {
        private Transform _environment;
        private readonly Transform[] _clouds=new Transform[5],_gulls=new Transform[4];
        private Mesh _cloudMesh,_gullMesh;
        private Material _skyPalette;
        public Transform Clouds=>_clouds[0];
        public Transform Gull=>_gulls[0];
        private void PrepareEnvironment()
        {
            if(_environment==null)
            {
                _environment=new GameObject("Title sea breeze · visual only").transform;_environment.SetParent(transform,false);
                _skyPalette=new Material(_c.PaletteShader){name="Title sunlit cloud and gull palette",enableInstancing=true};
                _skyPalette.SetFloat("_Unlit",.45f);
                var cloud=new FishingMeshBuilder();
                cloud.Ellipsoid(Vector3.zero,new Vector3(5,1,1.8f),new Color(.94f,.96f,.94f));
                cloud.Ellipsoid(new Vector3(-1.8f,.6f,0),new Vector3(2.5f,1.4f,1.6f),Color.white);
                cloud.Ellipsoid(new Vector3(1.3f,.9f,0),new Vector3(2.3f,1.6f,1.6f),Color.white);
                _cloudMesh=cloud.Finish("Soft sea cloud clusters");_gullMesh=FishingMeshBuilder.Animal(MarineKind.Gull);
                Transform Visual(string name,Mesh mesh)
                {
                    var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(_environment,false);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=_skyPalette;
                    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;
                }
                for(int i=0;i<_clouds.Length;i++)_clouds[i]=Visual("Drifting cloud "+i,_cloudMesh);
                for(int i=0;i<_gulls.Length;i++)
                {
                    _gulls[i]=Visual("Circling sea gull "+i,_gullMesh);_gulls[i].localScale=Vector3.one*1.3f;
                    var flap=new MaterialPropertyBlock();flap.SetVector("_FinMotion",new Vector4(.28f,4,.12f,i));
                    _gulls[i].GetComponent<Renderer>().SetPropertyBlock(flap);
                }
            }
            _environment.gameObject.SetActive(true);
        }
        private void AnimateEnvironment(float time)
        {
            if(_environment==null)return;
            var camera=_c.viewCamera.transform;
            for(int i=0;i<_clouds.Length;i++)
            {
                // Broad loops have no visible reset while the real career and physics stay paused.
                float phase=time*.012f+i*1.2566f;
                _clouds[i].position=_origin+camera.right*(Mathf.Sin(phase)*60)+new Vector3(0,15+i%2*3,50+i*7);
                _clouds[i].rotation=Quaternion.Euler(0,camera.eulerAngles.y,0);
            }
            for(int i=0;i<_gulls.Length;i++)
            {
                float angle=time*.30f+i*1.45f;
                var p=_origin+new Vector3(Mathf.Cos(angle)*(12+i),8+i*.7f+Mathf.Sin(angle*2)*.6f,14+Mathf.Sin(angle)*9);
                var heading=new Vector3(-Mathf.Sin(angle)*(12+i),Mathf.Cos(angle*2)*1.2f,Mathf.Cos(angle)*9);
                _gulls[i].SetPositionAndRotation(p,Quaternion.LookRotation(heading)*Quaternion.Euler(0,0,Mathf.Sin(angle)*14));
            }
        }
        private void OnDestroy()
        {
            Close(true);if(_skyPalette!=null)Destroy(_skyPalette);if(_cloudMesh!=null)Destroy(_cloudMesh);if(_gullMesh!=null)Destroy(_gullMesh);
        }
    }
}
