using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Camera-centred celestial dome and a two-sided rotating light on the existing home lighthouse.</summary>
    public sealed class FishingNightscape : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _sky,_sweep;
        private Material _beam,_lens;
        private readonly List<Object> _owned=new List<Object>();
        private readonly Light[] _lights=new Light[2];
        public float Visibility { get; private set; }
        public Transform Moon { get; private set; }
        public Transform LighthouseSweep=>_sweep;
        public void Build(FishingPrototype controller)
        {
            _c=controller;
            // Stars, moon, milky way and meteors are drawn by the FishingCoastalSky skybox; this marker follows the moon.
            _sky=new GameObject("V031 moon and constellations").transform;_sky.SetParent(transform,false);
            Moon=new GameObject("Waxing coastal moon").transform;Moon.SetParent(_sky,false);
            _sweep=new GameObject("Home lighthouse rotating amber beam").transform;_sweep.SetParent(transform,false);
            _sweep.position=_c.Home.LighthouseLens;
            _beam=Material("FishingLighthouseBeam",new Color(4,3.6f,2.8f,.055f),"Lighthouse sea mist beam");
            var cone=BeamMesh();_owned.Add(cone);
            for(int i=0;i<2;i++)
            {
                var ray=Render("Sweeping sea beam "+i,_sweep,cone,_beam);ray.localRotation=Quaternion.Euler(8,i*180,0);ray.localPosition=ray.forward*1.55f;
                var light=new GameObject("Lighthouse real spot "+i).AddComponent<Light>();light.transform.SetParent(ray,false);
                light.type=LightType.Spot;light.color=new Color(1,.80f,.50f);light.range=170;light.spotAngle=20;light.innerSpotAngle=12;
                light.intensity=0;light.shadows=LightShadows.None;_lights[i]=light;
            }
            _lens=Material("FishingNightSky",new Color(1.6f,1.1f,.45f),"Warm lighthouse lens");_lens.SetFloat("_Solid",1);
            var lens=GameObject.CreatePrimitive(PrimitiveType.Sphere);lens.name="Home lighthouse warm exterior lens";Destroy(lens.GetComponent<Collider>());
            lens.transform.SetParent(_sweep,false);lens.transform.localScale=new Vector3(2.6f,.26f,2.6f);lens.GetComponent<Renderer>().sharedMaterial=_lens;
            var lensMesh=Object.Instantiate(lens.GetComponent<MeshFilter>().sharedMesh);lensMesh.name="Luminous lighthouse lens with white vertices";
            var lensColors=new Color[lensMesh.vertexCount];for(int i=0;i<lensColors.Length;i++)lensColors[i]=Color.white;lensMesh.colors=lensColors;
            lens.GetComponent<MeshFilter>().sharedMesh=lensMesh;_owned.Add(lensMesh);
            Apply(0);
        }
        private Material Material(string shader,Color tint,string name)
        {
            var material=new Material(Resources.Load<Shader>(shader)){name=name,enableInstancing=true};material.SetColor("_Tint",tint);_owned.Add(material);return material;
        }
        private static Transform Render(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;
        }
        private static Mesh BeamMesh()
        {
            const int sides=24;var vertices=new Vector3[sides*2];var uv=new Vector2[sides*2];var triangles=new int[sides*6];
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                vertices[i]=radial*.25f;vertices[i+sides]=radial*10+Vector3.forward*135;
                uv[i]=new Vector2((float)i/sides,0);uv[i+sides]=new Vector2((float)i/sides,1);
                int next=(i+1)%sides,j=i*6;triangles[j]=i;triangles[j+1]=next;triangles[j+2]=i+sides;triangles[j+3]=next;triangles[j+4]=next+sides;triangles[j+5]=i+sides;
            }
            var mesh=new Mesh{name="Soft tapered 135m lighthouse beam",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void LateUpdate(){if(_c==null || _c.Career==null)return;Apply(_c.Paused || _c.StoryBlocksGameplay?0:Time.deltaTime);}
        public void Apply(float seconds)
        {
            var climate=_c.Weather.Climate;
            float before=climate.Previous==BayWeather.Mist?.13f:climate.Previous==BayWeather.Cloudy?.42f:1;
            float after=climate.Current==BayWeather.Mist?.13f:climate.Current==BayWeather.Cloudy?.42f:1;
            float weather=Mathf.Lerp(before,after,Mathf.SmoothStep(0,1,climate.Blend))*(1-_c.Weather.StormAmount);
            Visibility=(1-_c.Career.Calendar.Daylight)*weather;
            _sky.position=_c.viewCamera.transform.position;
            Moon.localPosition=_c.Weather.MoonDirection*Mathf.Min(290,_c.viewCamera.farClipPlane*.75f);
            if(RenderSettings.skybox!=null)RenderSettings.skybox.SetFloat("_StarVisibility",Visibility);
            float light=1-_c.Career.Calendar.Daylight;_beam.SetFloat("_Visibility",light);_lens.SetFloat("_Visibility",light);
            _sweep.Rotate(0,seconds*9,0,Space.World);
            foreach(var spot in _lights){spot.enabled=light>.02f;spot.intensity=light*(XRSettings.isDeviceActive?5:8);}
        }
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
