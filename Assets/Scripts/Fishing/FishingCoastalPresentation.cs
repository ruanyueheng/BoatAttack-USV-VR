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
            var ground=Resources.Load<Material>("FishingV035/CoastGround");
            if(ground==null)throw new System.InvalidOperationException("Islets need the shared detailed coastal ground material.");
            var palette=new Material(c.PaletteShader){name="Ivory arch · coastal shading"};palette.SetFloat("_Shadows",1);palette.SetFloat("_Rim",.08f);_owned.Add(palette);
            var islands=new List<GameObject>();
            for(int id=0;id<2;id++)
            {
                var island=new GameObject(IsletName(id)).transform;island.SetParent(transform,false);island.position=Islets[id];islands.Add(island.gameObject);
                var mesh=IslandMesh(id);_owned.Add(mesh);
                var land=new GameObject("Walkable sculpted shoreline",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));land.transform.SetParent(island,false);
                land.layer=10;
                land.GetComponent<MeshFilter>().sharedMesh=mesh;land.GetComponent<MeshCollider>().sharedMesh=mesh;land.GetComponent<MeshRenderer>().sharedMaterial=ground;
                DressIslet(island,id);
                var campLight=new Vector2(-16.7f,-14.5f);
                FishingV019Art.Spawn("Lantern",island,new Vector3(campLight.x,Height(campLight.x,campLight.y,id)+.04f,campLight.y),.6f,0,"FishingV023/Items");
                var lamp=new GameObject("Camp lantern warm pool",typeof(Light));lamp.transform.SetParent(island,false);lamp.transform.localPosition=new Vector3(campLight.x,Height(campLight.x,campLight.y,id)+.65f,campLight.y);
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
            foreach(var island in islands)StaticBatchingUtility.Combine(island);
            Physics.SyncTransforms();Debug.Log("[V032] Removed "+RemovedCards+" collidable background cards; built two solid, walkable islets.");
        }
        private static bool ProtectedFooting(Vector2 point,int id,float margin)
            =>(Mathf.Abs(point.x)<5+margin && point.y>13-margin)
                || (id==0 && Vector2.Distance(point,new Vector2(11,19))<5+margin)
                || (id==1 && point.magnitude<21.2f+margin);
        private Transform CoastalProp(string name,Transform island,Vector2 point,int id,float span,float yaw,bool foliage=false,bool solid=false)
        {
            var root=FishingV019Art.Spawn(name,island,new Vector3(point.x,Height(point.x,point.y,id)-.06f,point.y),span,yaw,"FishingV035");
            FishingV035Materials.Apply(root,foliage);
            if(solid)foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var source=filter.sharedMesh;var collision=source;
                if(name=="CoastalPine" || name=="DunePalm")
                {
                    collision=new Mesh{name=name+" solid trunk and branches"};collision.vertices=source.vertices;
                    collision.SetTriangles(source.GetTriangles(0),0);collision.RecalculateBounds();_owned.Add(collision);
                }
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh=collision;
            }
            return root;
        }
        private void DressIslet(Transform island,int id)
        {
            var trees=id==0?new[]{new Vector2(-17,-5),new Vector2(-19,4),new Vector2(-10,-17),new Vector2(-3,-19),new Vector2(14,-14),new Vector2(19,-4),new Vector2(16,11),new Vector2(-16,14)}
                :new[]{new Vector2(-24.5f,4),new Vector2(-24,-3),new Vector2(-22.3f,-10.5f),new Vector2(18.6f,14.6f),new Vector2(-22,8.5f),new Vector2(21,11),new Vector2(20.4f,12.5f),new Vector2(-20.5f,13.5f)};
            for(int i=0;i<trees.Length;i++)
            {
                var p=trees[i];if(Height(p.x,p.y,id)<.7f || ProtectedFooting(p,id,1.8f))continue;
                CoastalProp((i+id)%3==0?"DunePalm":"CoastalPine",island,p,id,6.2f+(i%3)*.8f,i*83+id*37,solid:true);
            }
            var rocks=new[]{new Vector2(-25,-8),new Vector2(-26,3),new Vector2(-22,14),new Vector2(-16,-20),new Vector2(-3,-24),new Vector2(14,-20),new Vector2(25,-6),new Vector2(24,9),new Vector2(18,19),new Vector2(8,22),new Vector2(-14,20)};
            for(int i=0;i<rocks.Length;i++)
            {
                var p=rocks[i];float span=2.4f+(i%3)*.45f;
                if(Height(p.x,p.y,id)<-.8f || ProtectedFooting(p,id,span*.5f))continue;
                CoastalProp("ShoreRock",island,p,id,span,i*71,solid:true);
            }
            // Uneven clumps follow sheltered pockets; the approach and arena remain readable.
            var pockets=new[]{new Vector2(-18,1),new Vector2(-12,13),new Vector2(12,-15),new Vector2(16,7),new Vector2(-11,-14)};
            for(int i=0;i<20;i++)
            {
                var pocket=pockets[(i+id)%pockets.Length];float a=i*2.13f;
                var p=pocket+new Vector2(Mathf.Sin(a)*2.8f,Mathf.Cos(a*1.37f)*2.1f);
                if(Height(p.x,p.y,id)<1.1f || (Mathf.Abs(p.x)<5 && p.y>10) || (id==0 && Vector2.Distance(p,new Vector2(11,19))<5))continue;
                CoastalProp(i%4==0?"FernClump":"DuneGrass",island,p,id,i%4==0?1.05f:1.15f+(i%3)*.15f,i*137,true);
            }
            var campPoint=new Vector2(-18.5f,-15.5f);
            var source=Resources.Load<GameObject>("FishingV040/IsletCamp");
            if(source==null)throw new System.InvalidOperationException("Missing the textured coastal shelter.");
            var camp=new GameObject("Weathered timber shelter").transform;camp.SetParent(island,false);
            Instantiate(source,camp,false);
            camp.localPosition=new Vector3(campPoint.x,Height(campPoint.x,campPoint.y,id),campPoint.y);
            camp.localRotation=Quaternion.LookRotation(new Vector3(campPoint.y,0,-campPoint.x));
            FishingV035Materials.Apply(camp);
            foreach(var filter in camp.GetComponentsInChildren<MeshFilter>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
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
                float path=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.1f,3.2f,Mathf.Abs(x-Mathf.Sin(z*.17f)*2)));
                path*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-4,5,z));
                float pocket=Mathf.PerlinNoise(x*.13f+id*7,z*.13f+17);
                float cover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,2.2f,y))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.54f,pocket))*(1-path*.94f);
                var sand=Color.Lerp(new Color(.70f,.65f,.51f),new Color(.82f,.75f,.59f),Mathf.PerlinNoise(x*.31f+4,z*.31f));
                // GroundDetail selects the grass atlas from green-minus-red after the sRGB conversion.
                var grass=Color.Lerp(new Color(.29f,.51f,.19f),new Color(.39f,.59f,.25f),pocket);
                colors[n]=Color.Lerp(sand,grass,cover);
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
