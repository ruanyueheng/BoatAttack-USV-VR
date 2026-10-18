using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.Fishing
{
    public sealed class FishingCoastalPresentation : MonoBehaviour
    {
        public static readonly Vector3[] Islets={new Vector3(-420,0,205),new Vector3(420,0,205)};
        public static string IsletName(int id)=>id==0?"白帆拱湾":"萤火沙洲";
        public static Vector3 Berth(int id)=>Islets[id]+new Vector3(0,0,29);
        private readonly List<Object> _owned=new List<Object>();
        private readonly List<GameObject> _oldSkies=new List<GameObject>();
        private RenderPipelineAsset _previous;
        private UniversalRenderPipelineAsset _pipeline;
        private FishingPrototype _c;
        private readonly Light[] _campLights=new Light[2];
        public int RemovedCards { get; private set; }
        public void Build(FishingPrototype c)
        {
            _c=c;
            foreach(var sky in FindObjectsOfType<SkyboxSystem>()){_oldSkies.Add(sky.gameObject);sky.gameObject.SetActive(false);}
            foreach(var root in gameObject.scene.GetRootGameObjects())
            {
                if(root.name!="Island Level")continue;
                var cards=root.transform.Find("Environment Objects/Env_BushCards");
                if(cards!=null){RemovedCards=cards.childCount;cards.gameObject.SetActive(false);}
            }
            if(c.DesktopDisplay)
            {
                _previous=QualitySettings.renderPipeline;
                var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if(source!=null)
                {
                    _pipeline=Instantiate(source);_pipeline.name="V032 desktop coastal shadows";
                    _pipeline.shadowDistance=100;_pipeline.shadowCascadeCount=2;_pipeline.maxAdditionalLightsCount=4;QualitySettings.renderPipeline=_pipeline;
                }
            }
            var palette=new Material(c.PaletteShader){name="V032 coast · soft shaded sand and sage"};palette.SetFloat("_Shadows",1);palette.SetFloat("_Rim",.18f);_owned.Add(palette);
            var limestone=new Material(palette){name="V032 pale coastal stone"};limestone.SetColor("_BaseColor",new Color(1.35f,1.3f,1.15f));_owned.Add(limestone);
            for(int id=0;id<2;id++)
            {
                var island=new GameObject(IsletName(id)).transform;island.SetParent(transform,false);island.position=Islets[id];
                var mesh=IslandMesh(id);_owned.Add(mesh);
                var land=new GameObject("Walkable sculpted shoreline",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));land.transform.SetParent(island,false);
                land.GetComponent<MeshFilter>().sharedMesh=mesh;land.GetComponent<MeshCollider>().sharedMesh=mesh;land.GetComponent<MeshRenderer>().sharedMaterial=palette;
                for(int j=0;j<11;j++)
                {
                    float a=j*2.399963f;float radius=8+(j%4)*2.4f;float x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius*.7f-4;
                    if(Mathf.Abs(x)<5 && z>0)continue;
                    var tree=FishingV019Art.Spawn(id==0?"tree_palmDetailedTall":"tree_oak",island,new Vector3(x,Height(x,z,id),z),4.5f+(j%4),j*61);
                    foreach(var renderer in tree.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=palette;
                }
                for(int j=0;j<12;j++)
                {
                    float a=j*2.399963f;float x=Mathf.Cos(a)*19,z=Mathf.Sin(a)*16;
                    var rock=FishingV019Art.Spawn("CoastalRock",island,new Vector3(x,Height(x,z,id)-.4f,z),2.5f+j%4,j*41,"FishingV020");
                    rock.localRotation=Quaternion.Euler(58+j%3*12,j*41,13);rock.localScale=new Vector3(1.35f,.8f,1.1f);
                    foreach(var renderer in rock.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=limestone;
                }
                var camp=FishingV019Art.Spawn("tent_detailedOpen",island,new Vector3(-7,Height(-7,-5,id),-5),3.8f,140);
                FishingV019Art.Spawn("campfire_logs",island,new Vector3(-4,Height(-4,0,id),0),1.2f);
                for(int j=0;j<44;j++)
                {
                    float a=j*2.399963f;float x=Mathf.Cos(a)*(6+j%15),z=Mathf.Sin(a)*(5+j%12)-2;
                    if(Mathf.Abs(x)<3 && z>0)continue;
                    var bush=FishingV019Art.Spawn(j%5==0?"flower_purpleA":"plant_bush",island,new Vector3(x,Height(x,z,id),z),.5f+(j%4)*.18f,j*31);
                    foreach(var renderer in bush.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=palette;
                }
                FishingV019Art.Spawn("Lantern",island,new Vector3(-4,Height(-4,-2,id),-2),.8f,0,"FishingV023/Items");
                var lamp=new GameObject("Camp lantern warm pool",typeof(Light));lamp.transform.SetParent(island,false);lamp.transform.localPosition=new Vector3(-4,Height(-4,-2,id)+.8f,-2);
                _campLights[id]=lamp.GetComponent<Light>();_campLights[id].type=LightType.Point;_campLights[id].range=6;_campLights[id].color=new Color(1,.65f,.28f);_campLights[id].shadows=LightShadows.None;
                var arch=Resources.Load<GameObject>("FishingV032/CoastalArch");
                if(id==0 && arch!=null)
                {
                    var stoneArch=FishingV019Art.Spawn("CoastalArch",island,new Vector3(6,Height(6,-5,id),-5),11,-45,"FishingV032");
                    var texture=Resources.Load<Texture2D>("FishingV032/CoastalArchAlbedo");
                    var stone=new Material(palette){name="Tripo ivory arch · original painterly albedo"};stone.SetFloat("_Textured",1);stone.SetTexture("_BaseMap",texture);_owned.Add(stone);
                    foreach(var renderer in stoneArch.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=stone;
                    foreach(var meshFilter in stoneArch.GetComponentsInChildren<MeshFilter>())meshFilter.gameObject.AddComponent<MeshCollider>().sharedMesh=meshFilter.sharedMesh;
                }
            }
            EnableWorldShading(c);
            Physics.SyncTransforms();Debug.Log("[V032] Removed "+RemovedCards+" collidable background cards; built two solid, walkable islets.");
        }
        public void SetQuality(int level)
        {
            if(_pipeline==null)return;
            _pipeline.shadowDistance=level==0?0:level==2?100:55;_pipeline.renderScale=level==0?.85f:1;
        }
        private void Update()
        {
            if(_c==null || _c.Weather==null)return;
            foreach(var light in _campLights)if(light!=null){light.intensity=2.8f*(1-_c.Weather.Daylight);light.enabled=_c.Weather.Daylight<.85f;}
        }
        private void EnableWorldShading(FishingPrototype c)
        {
            var materials=new Dictionary<Material,Material>();
            foreach(var renderer in FindObjectsOfType<MeshRenderer>())
            {
                if(renderer.GetComponent<TextMesh>()!=null || renderer.transform.IsChildOf(c.Player.transform))continue;
                var current=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<current.Length;i++)
                {
                    var source=current[i];if(source==null || source.shader!=c.PaletteShader || source.GetFloat("_Unlit")>.5f)continue;
                    if(!materials.TryGetValue(source,out var shaded))
                    {shaded=new Material(source){name=source.name+" · coastal shadows"};shaded.SetFloat("_Shadows",1);shaded.SetFloat("_Rim",.12f);materials.Add(source,shaded);_owned.Add(shaded);}
                    current[i]=shaded;changed=true;
                }
                if(changed){renderer.sharedMaterials=current;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
            }
        }
        public static float Height(float x,float z,int id)
        {
            float r=Mathf.Sqrt(x*x/(30*30f)+z*z/(25*25f));
            float ridge=3.2f*Mathf.Exp(-((x+9)*(x+9)+(z+9)*(z+9))/100);
            float cove=2.5f*Mathf.Exp(-((x-23)*(x-23)+(z-1)*(z-1))/50);
            return 3.1f*(1-Mathf.Pow(r,4))-1.2f*Mathf.Pow(r,6)+ridge-cove+.24f*Mathf.PerlinNoise(x*.11f+id*10,z*.11f+7);
        }
        private Mesh IslandMesh(int id)
        {
            const int sides=64,rings=16;var vertices=new Vector3[(rings+1)*sides];var colors=new Color[vertices.Length];var indices=new List<int>();
            for(int ring=0;ring<=rings;ring++)for(int j=0;j<sides;j++)
            {
                float a=j*Mathf.PI*2/sides;float radius=ring/(float)rings*(1+.085f*Mathf.Sin(a*3+id)+.045f*Mathf.Cos(a*5));
                float x=Mathf.Cos(a)*radius*32,z=Mathf.Sin(a)*radius*26;float y=Height(x,z,id);int n=ring*sides+j;vertices[n]=new Vector3(x,y,z);
                bool trail=Mathf.Abs(x-Mathf.Sin(z*.17f)*2)<1.6f && z>0;
                var grass=Color.Lerp(new Color(.27f,.36f,.21f),new Color(.48f,.54f,.30f),Mathf.PerlinNoise(x*.18f,z*.18f));
                colors[n]=trail?new Color(.76f,.67f,.46f):Color.Lerp(new Color(.86f,.76f,.56f),grass,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.6f,2.7f,y)));
                if(ring==0)continue;int p=n-sides,next=ring*sides+(j+1)%sides,previous=next-sides;
                indices.Add(p);indices.Add(next);indices.Add(n);indices.Add(p);indices.Add(previous);indices.Add(next);
            }
            var mesh=new Mesh{name="V032 sculpted islet "+id};mesh.vertices=vertices;mesh.colors=colors;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void OnDestroy()
        {
            if(_pipeline!=null){if(QualitySettings.renderPipeline==_pipeline)QualitySettings.renderPipeline=_previous;Destroy(_pipeline);}
            foreach(var item in _owned)if(item!=null)Destroy(item);
            foreach(var sky in _oldSkies)if(sky!=null)sky.SetActive(true);
        }
    }
}
