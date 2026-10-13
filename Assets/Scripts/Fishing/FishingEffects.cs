using UnityEngine;
using UnityEngine.Rendering;
namespace BoatAttack.Fishing
{
    /// <summary>A bounded shared emitter and a single star mesh; no per-spark lights or physics.</summary>
    public sealed class FishingEffects : MonoBehaviour
    {
        public FishingPrototype Controller;
        private ParticleSystem _sparks;
        private Transform _stars;
        private Mesh _starMesh;
        private Material _material,_starMaterial;
        private void Start()
        {
            _material=new Material(Controller.PaletteShader){name="Coastal soft motes"};_material.SetFloat("_Unlit",1);
            var root=new GameObject("Shared interaction spark pool");root.transform.SetParent(transform,false);
            _sparks=root.AddComponent<ParticleSystem>();_sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=_sparks.main;main.maxParticles=48;main.startLifetime=.55f;main.startSize=.025f;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=_sparks.emission;emission.enabled=false;var shape=_sparks.shape;shape.enabled=false;
            var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var b=new FishingMeshBuilder();
            for(int i=0;i<64;i++)
            {
                float yaw=i*2.399963f,height=.25f+(i%17)*.06f;
                var point=new Vector3(Mathf.Cos(yaw),height,Mathf.Sin(yaw)).normalized*90;
                var right=Vector3.Cross(point.normalized,Vector3.up).normalized*(.07f+(i%3)*.03f);var up=Vector3.Cross(right.normalized,point.normalized)*right.magnitude;
                var color=i%4==0?new Color(1,.84f,.57f):new Color(.75f,.88f,1);
                b.Fin(point-up,point-right,point+up,color);b.Fin(point-up,point+up,point+right,color);
            }
            _starMesh=b.Finish("64 coastal stars / 256 triangles");
            var stars=new GameObject("Coastal night sky",typeof(MeshFilter),typeof(MeshRenderer));_stars=stars.transform;_stars.SetParent(transform,false);
            _starMaterial=new Material(_material){name="Night star palette"};stars.GetComponent<MeshFilter>().sharedMesh=_starMesh;
            var sr=stars.GetComponent<MeshRenderer>();sr.sharedMaterial=_starMaterial;sr.shadowCastingMode=ShadowCastingMode.Off;sr.receiveShadows=false;
        }
        public void Burst(Vector3 at,Color color,bool splash=false)
        {
            if(_sparks==null || Controller.Paused)return;
            int count=Controller.Weather.ReducedEffects?4:12;
            for(int i=0;i<count;i++)
            {
                float angle=i*2.399963f;
                _sparks.Emit(new ParticleSystem.EmitParams{position=at,startColor=color,startSize=splash?.07f:.025f,
                    velocity=new Vector3(Mathf.Cos(angle)*.25f,.15f+(i%4)*.08f,Mathf.Sin(angle)*.25f)*(splash?4f:1f)},1);
            }
        }
        private void LateUpdate()
        {
            if(_stars==null || Controller.Player==null)return;
            float light=1-Controller.Weather.Daylight;
            bool clear=Controller.Weather.Climate.Current!=BayWeather.Rain && Controller.Weather.Climate.Current!=BayWeather.Mist;
            _stars.gameObject.SetActive(clear && light>.08f);_stars.position=Controller.viewCamera.transform.position;
            _starMaterial.SetColor("_BaseColor",Color.white*light);
        }
        private void OnDestroy(){if(_material!=null)Destroy(_material);if(_starMaterial!=null)Destroy(_starMaterial);if(_starMesh!=null)Destroy(_starMesh);}
    }
}
