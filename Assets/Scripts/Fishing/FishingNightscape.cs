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
        private Material _stars,_moon,_beam,_lens;
        private readonly List<Object> _owned=new List<Object>();
        private readonly Light[] _lights=new Light[2];
        public float Visibility { get; private set; }
        public Transform Moon { get; private set; }
        public Transform LighthouseSweep=>_sweep;
        public int StarCount=>280;
        public void Build(FishingPrototype controller)
        {
            _c=controller;
            _sky=new GameObject("V031 moon and constellations").transform;_sky.SetParent(transform,false);
            _stars=Material("FishingNightSky",new Color(.82f,.90f,1),"Starlight");
            _moon=Material("FishingNightSky",new Color(1,.88f,.64f),"Crescent moon");_moon.SetFloat("_Moon",1);
            var v=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            var random=new System.Random(31016);float radius=Mathf.Min(290,_c.viewCamera.farClipPlane*.75f);
            void Quad(Vector3 center,float size,Color color)
            {
                var forward=center.normalized;var right=Vector3.Cross(forward,Vector3.up).normalized*size;var up=Vector3.Cross(right.normalized,forward)*size;
                int start=v.Count;v.Add(center-right-up);v.Add(center+right-up);v.Add(center+right+up);v.Add(center-right+up);
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});for(int i=0;i<4;i++)colors.Add(color);
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            for(int i=0;i<StarCount;i++)
            {
                float azimuth=(float)random.NextDouble()*Mathf.PI*2,elevation=Mathf.Asin(.13f+(float)random.NextDouble()*.85f);
                var direction=new Vector3(Mathf.Cos(azimuth)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Sin(azimuth)*Mathf.Cos(elevation));
                float size=(i%19==0?.62f:.22f+(float)random.NextDouble()*.23f)*radius/250;
                Quad(direction*radius,size,new Color(1,1-i%3*.035f,1-i%3*.10f,.65f+(float)random.NextDouble()*.35f));
            }
            Mesh Finish(string name){var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();_owned.Add(mesh);return mesh;}
            Render("Stable star dome",_sky,Finish("280 star quads"),_stars);
            v.Clear();uv.Clear();colors.Clear();triangles.Clear();
            Quad(new Vector3(-.28f,.40f,.87f).normalized*radius,radius*.038f,Color.white);
            Moon=Render("Waxing coastal moon",_sky,Finish("Crescent disc"),_moon);
            _sweep=new GameObject("Home lighthouse rotating amber beam").transform;_sweep.SetParent(transform,false);
            // V027 authored lens: island origin + (-14, base 3.680129 + 14.46, 10).
            _sweep.position=_c.Home.Origin+new Vector3(-14,18.14013f,10);
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
            lens.transform.SetParent(_sweep,false);lens.transform.localScale=new Vector3(3.02f,.34f,3.02f);lens.GetComponent<Renderer>().sharedMaterial=_lens;
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
            float weather=_c.Weather.Climate.Current==BayWeather.Mist?.13f:_c.Weather.Climate.Current==BayWeather.Rain?.3f:1;
            Visibility=(1-_c.Career.Calendar.Daylight)*weather;
            _sky.position=_c.viewCamera.transform.position;
            _stars.SetFloat("_Visibility",Visibility);_moon.SetFloat("_Visibility",Visibility);
            float light=1-_c.Career.Calendar.Daylight;_beam.SetFloat("_Visibility",light);_lens.SetFloat("_Visibility",light);
            _sweep.Rotate(0,seconds*9,0,Space.World);
            foreach(var spot in _lights){spot.enabled=light>.02f;spot.intensity=light*(XRSettings.isDeviceActive?5:8);}
        }
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
