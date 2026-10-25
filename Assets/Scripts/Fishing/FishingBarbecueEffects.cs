using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public sealed class FishingBarbecueEffects : MonoBehaviour
    {
        private FishingPrototype _c;
        private ParticleSystem _smoke,_embers;
        private Material _smokeMaterial,_emberMaterial;
        private AudioSource _sizzle;
        public void Build(FishingPrototype c)
        {
            _c=c;
            _smokeMaterial=new Material(Resources.Load<Shader>("FishingSmoke")){name="Warm coastal cooking smoke"};
            _emberMaterial=new Material(Resources.Load<Shader>("FishingSpray")){name="Tiny charcoal embers"};
            _smoke=Emitter("Soft curling grill smoke",_smokeMaterial,64,new Vector3(0,1.05f,0));
            var main=_smoke.main;main.startLifetime=new ParticleSystem.MinMaxCurve(2.4f,3.8f);main.startSize=new ParticleSystem.MinMaxCurve(.14f,.23f);
            main.startSpeed=.15f;main.startColor=new Color(.86f,.83f,.75f,.42f);main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
            var velocity=_smoke.velocityOverLifetime;velocity.enabled=true;velocity.x=.07f;velocity.y=.24f;velocity.z=.02f;
            var noise=_smoke.noise;noise.enabled=true;noise.strength=.12f;noise.frequency=.65f;noise.scrollSpeed=.25f;noise.quality=ParticleSystemNoiseQuality.Low;
            var size=_smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.45f,1,2.4f));
            _embers=Emitter("Restrained charcoal sparks",_emberMaterial,20,new Vector3(0,.93f,0));
            main=_embers.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.6f,1.4f);main.startSize=new ParticleSystem.MinMaxCurve(.012f,.025f);
            main.startSpeed=.35f;main.gravityModifier=-.02f;main.startColor=new Color(1,.57f,.21f,.8f);
            _sizzle=gameObject.AddComponent<AudioSource>();_sizzle.clip=Resources.Load<AudioClip>("FishingAudio/SFX/GrillSizzleLoop");
            _sizzle.loop=true;_sizzle.playOnAwake=false;_sizzle.spatialBlend=1;_sizzle.minDistance=.8f;_sizzle.maxDistance=9;
            _sizzle.rolloffMode=AudioRolloffMode.Linear;c.RegisterAudioSource(_sizzle,FishingAudioCategory.Effects,.30f);
        }
        private ParticleSystem Emitter(string name,Material material,int budget,Vector3 position)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.maxParticles=budget;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.56f,.015f,.25f);shape.rotation=new Vector3(-90,0,0);
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.18f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        private void Update()
        {
            if(_c==null)return;
            bool fish=_c.Career.Barbecue.fish!=null,cooking=fish && !_c.Career.BarbecueReady;
            bool nearby=(_c.ExplorerPosition-transform.position).sqrMagnitude<16*16;
            bool active=fish && nearby && !_c.Paused && !_c.StoryBlocksGameplay && !_c.XRNotebook.IsOpen;
            var emission=_smoke.emission;emission.rateOverTime=(_c.Weather.ReducedEffects?4:10)*(cooking?1:.25f);
            emission=_embers.emission;emission.rateOverTime=_c.Weather.ReducedEffects?1:3;
            if(active){if(!_smoke.isPlaying)_smoke.Play();if(cooking && !_embers.isPlaying)_embers.Play();}
            else if(_smoke.isPlaying)_smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if((!active || !cooking) && _embers.isPlaying)_embers.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(active && cooking && _sizzle.clip!=null){if(!_sizzle.isPlaying)_sizzle.Play();}
            else if(_sizzle.isPlaying)_sizzle.Stop();
        }
        private void OnDisable(){if(_sizzle!=null)_sizzle.Stop();}
        private void OnDestroy(){if(_smokeMaterial!=null)Destroy(_smokeMaterial);if(_emberMaterial!=null)Destroy(_emberMaterial);}
    }
}
