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
            var root=new GameObject("Articulated spinning rod").transform;root.SetParent(boat,false);
            root.localPosition=new Vector3(.75f,.95f,.9f);root.localRotation=Quaternion.LookRotation(new Vector3(1.9f,1.7f,1.35f));
            Gear("RodBody",root,Vector3.zero);
            crank=Gear("ReelCrank",root,new Vector3(-.053f,-.14f,-.015f)).transform;
            tip=new GameObject("Rod tip").transform;tip.SetParent(root,false);tip.localPosition=new Vector3(0,-.015f,2.03f);
            root.gameObject.AddComponent<FishingRodDynamics>().Build(root,tip);
        }
        private static GameObject Gear(string name,Transform parent,Vector3 position)
        {
            var asset=Resources.Load<GameObject>("FishingGear/"+name);
            if(asset==null)throw new System.InvalidOperationException("Missing fishing gear: "+name);
            var go=Object.Instantiate(asset,parent,false);go.name=name;go.transform.localPosition=position;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=Resources.Load<Material>("FishingProps/HarborPalette");
            return go;
        }
        public const float NetMouthOffset=.86f;
        public const float NetRadius=.28f;
        public static Transform Net(Transform boat,Material dark,Material rim,Material cord)
        {
            var root=new GameObject("Woven landing net").transform;root.SetParent(boat,false);root.localPosition=new Vector3(1.0f,0.95f,0.15f);
            FishingArt.Pole("Net handle",root,Vector3.zero,new Vector3(.60f,0,0),0.014f,dark);
            FishingArt.Pole("Net grip",root,Vector3.zero,new Vector3(0.22f,0,0),0.022f,rim);
            for(int layer=0;layer<4;layer++)
            {
                float radius=NetRadius*(1-layer/4.5f);var hoop=FishingArt.Line(layer==0?"Rigid hoop":"Woven basket ring",root,layer==0?rim:cord,layer==0?.014f:.004f,false);hoop.positionCount=33;
                for(int j=0;j<33;j++){float a=j*Mathf.PI*2/32;hoop.SetPosition(j,new Vector3(NetMouthOffset+Mathf.Cos(a)*radius,-layer*.07f,Mathf.Sin(a)*radius*.8f));}
            }
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;var line=FishingArt.Line("Net basket strand",root,cord,.003f,false);line.positionCount=5;
                for(int j=0;j<5;j++){float r=NetRadius*(1-j/4.5f);line.SetPosition(j,new Vector3(NetMouthOffset+Mathf.Cos(a)*r,-j*.07f,Mathf.Sin(a)*r*.8f));}
            }
            return root;
        }
        public static void Market(FishingPrototype c,List<Object> owned)
        {
            var dock=new GameObject("Tide and Fin floating fish market").transform;dock.SetParent(c.transform);dock.position=c.harbor;
            FishingArt.Shape("Dock collision",PrimitiveType.Cube,dock,new Vector3(0,-.18f,0),new Vector3(8,1.6f,6),c.wood,true);
            // A low floating deck can launch the bow over it; the stall envelope stops boats while pedestrians use the actual deck.
            var barrier=new GameObject("Boat building barrier");barrier.transform.SetParent(dock,false);barrier.transform.localPosition=Vector3.up*1.1f;
            barrier.AddComponent<BoxCollider>().size=new Vector3(8,4.2f,6);
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
            FishingArt.Shape("Counter base",PrimitiveType.Cube,dock,new Vector3(0,1.22f,0.55f),new Vector3(5.6f,1.05f,1.1f),c.teal,true);
            FishingArt.Shape("Counter top",PrimitiveType.Cube,dock,new Vector3(0,1.80f,0.55f),new Vector3(5.8f,0.12f,1.25f),c.wood,true);
            for(int i=0;i<3;i++)
            {
                var tray=Crate(dock,new Vector3(i*1.8f-1.8f,1.90f,0.55f),c.wood,c.teal,c.dark);tray.localScale=Vector3.one*0.9f;
                var fish=FishingArt.Fish(tray,owned,c.PaletteShader,(FishKind)i);fish.transform.localPosition=new Vector3(0,0.18f,0);fish.transform.localRotation=Quaternion.Euler(0,90,65);fish.transform.localScale=Vector3.one*0.7f;
            }
            for(int i=0;i<2;i++){var crate=Crate(dock,new Vector3(2.7f,0.73f,-1.4f+i*0.9f),c.wood,c.teal,c.dark);crate.localScale=Vector3.one*0.65f;}
            FishingArt.Shape("Market sign board",PrimitiveType.Cube,dock,new Vector3(0,2.60f,-1.78f),new Vector3(2.9f,0.61f,0.08f),c.dark);
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var sign=new GameObject("Market name",typeof(TextMesh));sign.transform.SetParent(dock,false);sign.transform.localPosition=new Vector3(0,2.6f,-1.84f);sign.transform.localRotation=Quaternion.identity;
            var text=sign.GetComponent<TextMesh>();text.text="TIDE & FIN";text.font=font;text.fontSize=64;text.characterSize=0.058f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(0.95f,0.85f,0.6f);FishingWorldText.Apply(text);
        }
    }
}
