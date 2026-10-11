using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>One combined display mesh, a clear central aisle and two collidable display islands.</summary>
    public sealed class FishingShopDisplay : MonoBehaviour
    {
        private Mesh _mesh;private Material _material;
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
                        var p=new Vector3(x+(k%2-.5f)*.55f,y+.13f,-2+(k/2)*.48f);
                        if(shelf==0){b.Box(p,new Vector3(.35f,.22f,.3f),colors[k%4]);b.Box(p+Vector3.up*.01f+Vector3.back*.155f,new Vector3(.22f,.09f,.01f),cream);}
                        else if(shelf==1){b.Tube(p-Vector3.up*.09f,p+Vector3.up*.09f,.12f,.12f,colors[k%4],12);b.Tube(p-Vector3.up*.1f,p-Vector3.up*.08f,.15f,.15f,brass,12);b.Tube(p+Vector3.up*.08f,p+Vector3.up*.1f,.15f,.15f,brass,12);}
                        else {b.Box(p,new Vector3(.23f,.19f,.24f),colors[k%4]);b.Tube(p+Vector3.up*.12f,p+Vector3.up*.2f,.05f,.05f,brass,8);}
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
                        var p=new Vector3(x+(k-3)*.28f,y+.18f,4.45f);
                        b.Box(p,new Vector3(.22f,.28f,.4f),colors[(k+shelf)%4]);b.Box(p+Vector3.back*.205f,new Vector3(.13f,.12f,.01f),cream);
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
            Sign("航海设备  /  船只升级",new Vector3(-2.9f,2.22f,-2.28f));Sign("探索工具  /  制作补给",new Vector3(2.9f,2.22f,-2.28f));
            Sign("TIDE & TACKLE  ·  海湾用品",new Vector3(0,2.9f,4.75f));
            var light=new GameObject("Chandlery warm pendant").AddComponent<Light>();light.transform.SetParent(transform,false);light.transform.localPosition=new Vector3(0,2.7f,-.3f);
            light.type=LightType.Point;light.color=new Color(1,.84f,.65f);light.range=7;light.intensity=3.2f;light.shadows=LightShadows.None;
        }
        private void Sign(string text,Vector3 point)
        {
            var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(transform,false);label.transform.localPosition=point;
            label.text=text;label.font=FishingUIFont.Font;label.fontSize=64;label.characterSize=.026f;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.93f,.82f,.53f);
            label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
