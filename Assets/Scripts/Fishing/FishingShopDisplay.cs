using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>One combined display mesh, a clear central aisle and two collidable display islands.</summary>
    public sealed class FishingShopDisplay : MonoBehaviour
    {
        private Mesh _mesh,_counterMesh;private Material _material;
        public bool DetailedCounter=>_counterMesh!=null;
        public static void Ensure(FishingTackleShop shop)
        {
            if(shop.transform.Find("V018 stocked chandlery")!=null)return;
            var go=new GameObject("V018 stocked chandlery");go.transform.SetParent(shop.transform,false);go.AddComponent<FishingShopDisplay>().Build();
            for(int i=0;i<shop.customerSpaces.Length;i++)
                if(shop.customerSpaces[i]!=null)shop.customerSpaces[i].position=shop.transform.TransformPoint(new Vector3(i%2==0?-1.1f:1.1f,0,i<2?-1.5f:.3f));
        }
        private void Build()
        {
            var b=new FishingMeshBuilder();Color wood=new Color(.43f,.25f,.12f),brass=new Color(.89f,.64f,.26f),cream=new Color(.91f,.87f,.73f);
            Color[] colors={new Color(.17f,.48f,.45f),new Color(.74f,.3f,.19f),new Color(.16f,.29f,.48f),cream};
            foreach(float x in new[]{-2.9f,2.9f})
            {
                b.Box(new Vector3(x,.22f,-1),new Vector3(1.25f,.44f,2.5f),wood);
                foreach(float dx in new[]{-.57f,.57f})foreach(float dz in new[]{-1.15f,1.15f})b.Box(new Vector3(x+dx,1.05f,-1+dz),new Vector3(.065f,2.1f,.065f),brass);
                for(int shelf=0;shelf<3;shelf++)
                {
                    float y=.48f+shelf*.57f;b.Box(new Vector3(x,y,-1),new Vector3(1.25f,.07f,2.5f),cream);
                    for(int k=0;k<10;k++)
                    {
                        var p=new Vector3(x+(k%2-.5f)*.55f,y+.04f,-2+(k/2)*.48f);
                        string[] goods=x<0?new[]{"Compass","RopeCoil","Lantern","Binoculars","Telescope","WindBell","bottle","tool-paddle","tool-shovel","StarPrism"}
                            :new[]{"bread","apple","banana","fish","cheese","bag","bottle-oil","mug","pot","crate-bottles"};
                        FishingV019Art.Spawn(goods[(k+shelf*3)%goods.Length],transform,p,.34f,160+k*19+shelf*30);
                    }
                }
                var collider=gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(x,1.05f,-1);collider.size=new Vector3(1.25f,2.1f,2.5f);
            }
            for(int shelf=0;shelf<3;shelf++)
            {
                float y=.7f+shelf*.55f;
                foreach(float x in new[]{-3.8f,3.8f})
                {
                    b.Box(new Vector3(x,y,4.45f),new Vector3(2.2f,.08f,.65f),wood);
                    for(int k=0;k<7;k++)
                    {
                        var p=new Vector3(x+(k-3)*.28f,y+.05f,4.45f);
                        string[] goods={"RopeCoil","bottle-large","bag","Compass","apple","Lantern","bread"};
                        FishingV019Art.Spawn(goods[(k+shelf)%goods.Length],transform,p,.24f,180+k*12);
                    }
                }
            }
            for(int i=0;i<12;i++)
            {
                float x=(i-5.5f)*.75f;b.Fin(new Vector3(x-.25f,3.1f,-3.2f),new Vector3(x,2.73f,-3.2f),new Vector3(x+.25f,3.1f,-3.2f),colors[i%4]);
            }
            foreach(float x in new[]{-4.55f,4.55f})
            {
                b.Box(new Vector3(x,.48f,-3.65f),new Vector3(1.3f,.08f,1.1f),wood);
                b.Tube(new Vector3(x,.5f,-3.65f),new Vector3(x,1.05f,-3.65f),.09f,.07f,brass);
                b.Box(new Vector3(x,1.3f,-3.65f),new Vector3(.6f,.5f,.4f),colors[2]);
                b.Tube(new Vector3(x,1.05f,-3.65f),new Vector3(x,1.05f,-3.25f),.055f,.055f,brass);
            }
            for(int stripe=0;stripe<16;stripe++)b.Box(new Vector3((stripe-7.5f)*.66f,3.03f,-5.75f),new Vector3(.66f,.07f,1.45f),stripe%2==0?colors[0]:cream);
            b.Box(new Vector3(0,2.9f,-6.45f),new Vector3(10.6f,.22f,.05f),colors[0]);
            foreach(float x in new[]{-5.15f,5.15f})b.Tube(new Vector3(x,0,-6.15f),new Vector3(x,3,-6.15f),.035f,.035f,brass,10);
            _mesh=b.Finish("Stocked tackle, tools and marine supplies");
            _material=new Material(Resources.Load<Shader>("FishingPalette")){name="Chandlery merchandise palette"};
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            DressCounter();
            Sign("航海设备  /  船只升级",new Vector3(-2.9f,2.22f,-2.28f));Sign("探索工具  /  制作补给",new Vector3(2.9f,2.22f,-2.28f));
            Sign("TIDE & TACKLE  ·  海湾用品",new Vector3(0,2.9f,4.75f));
            Sign("鱼货回收\n"+(UnityEngine.XR.XRSettings.isDeviceActive?"B → 卖鱼与委托":"柜台也可出售鱼货"),new Vector3(0,2.55f,-2.3f));
            FishingV019Art.Spawn("Telescope",transform,new Vector3(-4.55f,.53f,-3.65f),.64f);
            FishingV019Art.Spawn("chest",transform,new Vector3(4.55f,.53f,-3.65f),.74f,180);
            var light=new GameObject("Chandlery warm pendant").AddComponent<Light>();light.transform.SetParent(transform,false);light.transform.localPosition=new Vector3(0,2.7f,-.3f);
            light.type=LightType.Point;light.color=new Color(1,.84f,.65f);light.range=7;light.intensity=3.2f;light.shadows=LightShadows.None;
        }
        private void DressCounter()
        {
            Transform counter=null;
            foreach(var part in transform.parent.GetComponentsInChildren<Transform>())if(part.name=="Countertop"){counter=part;break;}
            if(counter==null)throw new System.InvalidOperationException("The shop counter surface is missing");
            var boards=new FishingMeshBuilder();
            for(int i=0;i<6;i++)
            {
                float z=(i+.5f)/6-.5f;
                boards.Box(new Vector3(0,0,z),new Vector3(1,.98f,1f/6-.003f),i%2==0?new Color(.57f,.37f,.20f):new Color(.62f,.42f,.24f));
                for(int grain=0;grain<3;grain++)boards.Tube(new Vector3(-.47f,.4905f,z-.05f+grain*.045f),new Vector3(.47f,.4905f,z-.047f+grain*.045f),.0014f,.0008f,new Color(.36f,.24f,.14f),5);
                foreach(float x in new[]{-.47f,.47f})boards.Ellipsoid(new Vector3(x,.494f,z),new Vector3(.0014f,.012f,.006f),new Color(.35f,.27f,.16f),8,4);
            }
            _counterMesh=boards.Finish("Cedar counter boards and inlaid grain");
            var surface=new GameObject("Planked cedar counter",typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(counter,false);
            surface.GetComponent<MeshFilter>().sharedMesh=_counterMesh;surface.GetComponent<MeshRenderer>().sharedMaterial=_material;
            counter.GetComponent<Renderer>().enabled=false;
        }
        private void Sign(string text,Vector3 point)
        {
            var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(transform,false);label.transform.localPosition=point;
            FishingXRText.Bind(label,text);label.font=FishingUIFont.Font;label.fontSize=64;label.characterSize=.026f;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.93f,.82f,.53f);
            label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_counterMesh!=null)Destroy(_counterMesh);if(_material!=null)Destroy(_material);}
    }
}
