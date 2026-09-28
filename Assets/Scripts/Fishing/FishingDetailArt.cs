using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    /// <summary>Small reusable handcrafted-by-code props, sharing the project's own material palette.</summary>
    public static class FishingDetailArt
    {
        public static Transform Crate(Transform parent,Vector3 position,Material wood,Material trim,Material metal)
        {
            var root=new GameObject("Slatted catch crate").transform;root.SetParent(parent,false);root.localPosition=position;
            FishingArt.Shape("Crate bottom",PrimitiveType.Cube,root,Vector3.zero,new Vector3(1.55f,0.08f,1.05f),wood);
            for(int row=0;row<3;row++)foreach(float side in new[]{-1f,1f})
            {
                FishingArt.Shape("Wooden side slat",PrimitiveType.Cube,root,new Vector3(side*0.77f,0.12f+row*0.16f,0),new Vector3(0.07f,0.125f,1.12f),wood);
                FishingArt.Shape("Wooden end slat",PrimitiveType.Cube,root,new Vector3(0,0.12f+row*0.16f,side*0.52f),new Vector3(1.55f,0.125f,0.07f),wood);
            }
            foreach(float x in new[]{-0.72f,0.72f})foreach(float z in new[]{-0.48f,0.48f})
                FishingArt.Shape("Metal corner strap",PrimitiveType.Cube,root,new Vector3(x,0.25f,z),new Vector3(0.10f,0.58f,0.10f),trim);
            foreach(float side in new[]{-1f,1f})
            {
                FishingArt.Shape("Top rim",PrimitiveType.Cube,root,new Vector3(side*0.78f,0.51f,0),new Vector3(0.11f,0.08f,1.15f),trim);
                FishingArt.Pole("Carry handle",root,new Vector3(-0.26f,0.40f,side*0.58f),new Vector3(0.26f,0.40f,side*0.58f),0.025f,metal);
            }
            return root;
        }
        public static void Rod(Transform boat,Material dark,Material cork,Material metal,Material accent,out Transform tip,out Transform crank)
        {
            var root=new GameObject("Detailed rod and reel").transform;root.SetParent(boat,false);
            Vector3 a=new Vector3(0.75f,0.95f,0.9f),b=new Vector3(2.65f,2.65f,2.25f);
            for(int i=0;i<5;i++)FishingArt.Pole("Tapered rod section",root,Vector3.Lerp(a,b,i/5f),Vector3.Lerp(a,b,(i+1)/5f),Mathf.Lerp(0.04f,0.012f,i/4f),dark);
            FishingArt.Pole("Cork grip",root,a,Vector3.Lerp(a,b,0.22f),0.068f,cork);
            for(int i=0;i<7;i++)
            {
                var p=Vector3.Lerp(a,b,0.015f+i*0.027f);var ring=FishingArt.Shape("Grip wrap",PrimitiveType.Sphere,root,p,new Vector3(0.09f,0.025f,0.09f),dark);
                ring.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            }
            for(int i=1;i<=5;i++)
            {
                Vector3 p=Vector3.Lerp(a,b,i/5f)+Vector3.down*0.05f;var ring=FishingArt.Line("Line guide",root,metal,0.012f,false);ring.positionCount=13;
                for(int j=0;j<13;j++){float ang=j*Mathf.PI*2/12;ring.SetPosition(j,p+new Vector3(Mathf.Cos(ang)*0.065f,Mathf.Sin(ang)*0.065f,0));}
            }
            var reel=new GameObject("Spinning reel").transform;reel.SetParent(root,false);reel.localPosition=Vector3.Lerp(a,b,0.16f)+new Vector3(0,-0.14f,0);
            var spool=FishingArt.Shape("Reel spool",PrimitiveType.Cylinder,reel,Vector3.zero,new Vector3(0.2f,0.12f,0.2f),metal);spool.transform.localRotation=Quaternion.Euler(0,0,90);
            foreach(float x in new[]{-0.13f,0.13f})
            {
                var disk=FishingArt.Shape("Spool flange",PrimitiveType.Cylinder,reel,new Vector3(x,0,0),new Vector3(0.26f,0.018f,0.26f),accent);disk.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            crank=new GameObject("Reel crank").transform;crank.SetParent(reel,false);crank.localPosition=new Vector3(-0.18f,0,0);
            FishingArt.Pole("Crank arm",crank,Vector3.zero,new Vector3(0,-0.16f,0),0.022f,metal);
            FishingArt.Pole("Crank grip",crank,new Vector3(0,-0.16f,0),new Vector3(-0.12f,-0.16f,0),0.043f,dark);
            tip=new GameObject("Rod tip").transform;tip.SetParent(root,false);tip.localPosition=b;
        }
        public static Transform Net(Transform boat,Material dark,Material rim,Material cord)
        {
            var root=new GameObject("Woven landing net").transform;root.SetParent(boat,false);root.localPosition=new Vector3(1.0f,0.95f,0.15f);
            FishingArt.Pole("Net handle",root,Vector3.zero,new Vector3(1.3f,0,0),0.035f,dark);
            FishingArt.Pole("Net grip",root,Vector3.zero,new Vector3(0.3f,0,0),0.055f,rim);
            for(int layer=0;layer<4;layer++)
            {
                float radius=0.6f*(1-layer/4.5f);var hoop=FishingArt.Line(layer==0?"Rigid hoop":"Woven basket ring",root,layer==0?rim:cord,layer==0?0.035f:0.012f,false);hoop.positionCount=33;
                for(int j=0;j<33;j++){float a=j*Mathf.PI*2/32;hoop.SetPosition(j,new Vector3(1.85f+Mathf.Cos(a)*radius,-layer*0.14f,Mathf.Sin(a)*radius*0.8f));}
            }
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;var line=FishingArt.Line("Net basket strand",root,cord,0.01f,false);line.positionCount=5;
                for(int j=0;j<5;j++){float r=0.6f*(1-j/4.5f);line.SetPosition(j,new Vector3(1.85f+Mathf.Cos(a)*r,-j*0.14f,Mathf.Sin(a)*r*0.8f));}
            }
            return root;
        }
        public static void Market(FishingPrototype c,List<Object> owned)
        {
            var dock=new GameObject("Tide and Fin floating fish market").transform;dock.SetParent(c.transform);dock.position=c.harbor;
            FishingArt.Shape("Dock collision",PrimitiveType.Cube,dock,new Vector3(0,0.43f,0),new Vector3(8,0.38f,6),c.wood,true);
            for(int i=0;i<21;i++)FishingArt.Shape("Deck plank",PrimitiveType.Cube,dock,new Vector3(-3.8f+i*0.38f,0.65f,0),new Vector3(0.35f,0.09f,5.9f),i%3==0?c.wood:c.cream);
            foreach(float x in new[]{-3f,3f})foreach(float z in new[]{-2.4f,2.4f})
            {
                FishingArt.Pole("Mooring post",dock,new Vector3(x,-0.2f,z),new Vector3(x,1.3f,z),0.15f,c.wood);
                for(int k=0;k<3;k++){var wrap=FishingArt.Shape("Rope wrap",PrimitiveType.Cylinder,dock,new Vector3(x,0.88f+k*0.08f,z),new Vector3(0.21f,0.025f,0.21f),c.cream);}
                FishingArt.Pole("Canopy support",dock,new Vector3(x,0.7f,z*0.72f),new Vector3(x,3.4f,z*0.72f),0.065f,c.dark);
            }
            foreach(float side in new[]{-1f,1f})
            {
                var pontoon=FishingArt.Shape("Floating pontoon",PrimitiveType.Cylinder,dock,new Vector3(side*2.7f,0,0),new Vector3(0.95f,2.8f,0.95f),c.teal);pontoon.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            for(int i=0;i<12;i++)foreach(float slope in new[]{-1f,1f})
            {
                var strip=FishingArt.Shape("Striped fabric canopy",PrimitiveType.Cube,dock,new Vector3(-3.15f+i*0.57f,3.6f,slope*1.04f),new Vector3(0.56f,0.075f,2.28f),i%2==0?c.orange:c.cream);
                strip.transform.localRotation=Quaternion.Euler(slope*13f,0,0);
                FishingArt.Shape("Awning valance",PrimitiveType.Cube,dock,new Vector3(-3.15f+i*0.57f,3.20f,slope*2.13f),new Vector3(0.56f,0.25f,0.06f),i%2==0?c.orange:c.cream);
            }
            FishingArt.Shape("Counter base",PrimitiveType.Cube,dock,new Vector3(0,1.22f,0.55f),new Vector3(5.6f,1.05f,1.1f),c.teal);
            FishingArt.Shape("Counter top",PrimitiveType.Cube,dock,new Vector3(0,1.80f,0.55f),new Vector3(5.8f,0.12f,1.25f),c.wood);
            for(int i=0;i<3;i++)
            {
                var tray=Crate(dock,new Vector3(i*1.8f-1.8f,1.90f,0.55f),c.wood,c.teal,c.dark);tray.localScale=Vector3.one*0.9f;
                var fish=FishingArt.Fish(tray,owned,c.PaletteShader,(FishKind)i);fish.transform.localPosition=new Vector3(0,0.18f,0);fish.transform.localRotation=Quaternion.Euler(0,90,65);fish.transform.localScale=Vector3.one*0.7f;
            }
            for(int i=0;i<2;i++){var crate=Crate(dock,new Vector3(2.7f,0.73f,-1.4f+i*0.9f),c.wood,c.teal,c.dark);crate.localScale=Vector3.one*0.65f;}
            FishingArt.Shape("Market sign board",PrimitiveType.Cube,dock,new Vector3(0,2.60f,-1.78f),new Vector3(2.9f,0.61f,0.08f),c.dark);
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var sign=new GameObject("Market name",typeof(TextMesh));sign.transform.SetParent(dock,false);sign.transform.localPosition=new Vector3(0,2.6f,-1.84f);sign.transform.localRotation=Quaternion.identity;
            var text=sign.GetComponent<TextMesh>();text.text="TIDE & FIN";text.font=font;text.fontSize=64;text.characterSize=0.058f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(0.95f,0.85f,0.6f);sign.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
    }
}
