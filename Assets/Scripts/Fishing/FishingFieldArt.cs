using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>Original low-poly field kit and wildlife. Shared meshes, one renderer per object.</summary>
    public static class FishingFieldArt
    {
        private static readonly Color Ink=new Color(.08f,.14f,.15f), Brass=new Color(.85f,.57f,.22f), Paper=new Color(.88f,.85f,.67f);
        public static Mesh Tool(FieldItem item)
        {
            var imported=FishingV023Art.ItemMesh(item);
            if(imported!=null)return Object.Instantiate(imported);
            var b=new FishingMeshBuilder();
            if(item==FieldItem.Binoculars)
            {
                foreach(float side in new[]{-1f,1f})
                {
                    b.Tube(new Vector3(side*.15f,0,-.15f),new Vector3(side*.15f,0,.22f),.10f,.14f,Ink,12);
                    b.Tube(new Vector3(side*.15f,0,.21f),new Vector3(side*.15f,0,.25f),.145f,.145f,Brass,12);
                    b.Ellipsoid(new Vector3(side*.15f,0,.253f),new Vector3(.118f,.118f,.009f),new Color(.2f,.7f,.75f),12,5);
                }
                b.Tube(new Vector3(-.15f,0,0),new Vector3(.15f,0,0),.065f,.065f,Ink);
            }
            else if(item==FieldItem.Lantern)
            {
                b.Ellipsoid(Vector3.up*.27f,new Vector3(.13f,.2f,.13f),new Color(1,.8f,.3f));
                b.Tube(Vector3.zero,Vector3.up*.07f,.18f,.16f,Ink,12);
                b.Tube(Vector3.up*.44f,Vector3.up*.5f,.18f,.12f,Ink,12);
                foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})b.Tube(new Vector3(x*.115f,.04f,z*.115f),new Vector3(x*.115f,.46f,z*.115f),.016f,.016f,Brass);
                b.Arc(Vector3.up*.51f,.12f,.018f,0,180,Brass,10);
            }
            else if(item==FieldItem.FieldGuide)
            {
                b.Ellipsoid(Vector3.zero,new Vector3(.23f,.03f,.3f),new Color(.15f,.39f,.28f),4,2,Quaternion.Euler(0,45,0));
                b.Ellipsoid(Vector3.up*.018f,new Vector3(.205f,.017f,.27f),Paper,4,2,Quaternion.Euler(0,45,0));
                b.Tube(new Vector3(-.04f,.04f,-.15f),new Vector3(-.04f,.04f,.15f),.009f,.009f,Brass);
            }
            else if(item==FieldItem.Shell)
            {
                for(int i=0;i<7;i++){float a=(i-3)*.22f;b.Tube(new Vector3(0,.02f,-.11f),new Vector3(Mathf.Sin(a)*.26f,.07f,.16f-Mathf.Abs(a)*.07f),.027f,.04f,new Color(.9f,.72f+i*.015f,.55f),6);}
            }
            else if(item==FieldItem.Driftwood)
            {
                b.Tube(new Vector3(-.3f,.035f,0),new Vector3(.3f,.06f,.04f),.065f,.04f,new Color(.46f,.29f,.15f),8);
                b.Tube(new Vector3(.08f,.045f,0),new Vector3(.19f,.065f,.17f),.035f,.018f,Brass,6);
            }
            return b.Finish("Field kit / "+item);
        }
        public static Mesh Animal(LandAnimalKind kind)
        {
            var b=new FishingMeshBuilder();
            Color fur=kind==LandAnimalKind.Fox?new Color(.72f,.3f,.1f):new Color(.61f,.53f,.39f);
            if(kind==LandAnimalKind.Tortoise)
            {
                var green=new Color(.34f,.43f,.22f);
                b.Ellipsoid(new Vector3(0,.24f,0),new Vector3(.32f,.21f,.42f),green);
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)b.Ellipsoid(new Vector3(x*.16f,.425f-Mathf.Abs(x)*.045f,z*.19f),new Vector3(.095f,.024f,.12f),Brass,6,3);
                b.Ellipsoid(new Vector3(0,.19f,.47f),new Vector3(.11f,.1f,.18f),green);
                foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})b.Ellipsoid(new Vector3(x*.28f,.09f,z*.28f),new Vector3(.12f,.07f,.12f),green,8,5);
                foreach(float x in new[]{-1f,1f})b.Ellipsoid(new Vector3(x*.08f,.23f,.53f),Vector3.one*.023f,Ink,8,4);
            }
            else
            {
                bool fox=kind==LandAnimalKind.Fox;float y=fox?.52f:.29f;
                b.Ellipsoid(new Vector3(0,y,0),new Vector3(fox?.24f:.22f,fox?.25f:.24f,fox?.46f:.3f),fur);
                b.Ellipsoid(new Vector3(0,y+.18f,.38f),new Vector3(.17f,.19f,.21f),fur);
                b.Ellipsoid(new Vector3(0,y+.12f,.54f),new Vector3(.12f,.08f,fox?.17f:.09f),Paper);
                b.Ellipsoid(new Vector3(0,y+.13f,fox?.69f:.61f),new Vector3(.036f,.029f,.035f),Ink,8,5);
                foreach(float side in new[]{-1f,1f})
                {
                    b.Ellipsoid(new Vector3(side*.13f,y+.23f,.51f),new Vector3(.028f,.034f,.025f),Ink,8,5);
                    b.Ellipsoid(new Vector3(side*.11f,y+(fox?.41f:.5f),.34f),new Vector3(.065f,fox?.15f:.25f,.065f),fur,8,5,Quaternion.Euler(0,0,side*-12));
                    b.Ellipsoid(new Vector3(side*.11f,y+(fox?.41f:.5f),.393f),new Vector3(.032f,fox?.1f:.18f,.016f),new Color(.8f,.52f,.42f),8,5);
                    foreach(float z in new[]{-.27f,.27f})
                    {b.Tube(new Vector3(side*.16f,y-.08f,z),new Vector3(side*.17f,.08f,z+.03f),.065f,.042f,fur);b.Ellipsoid(new Vector3(side*.17f,.07f,z+.07f),new Vector3(.07f,.06f,.12f),fox?Ink:Paper,8,5);}
                }
                if(fox){b.Tube(new Vector3(0,.52f,-.35f),new Vector3(.12f,.3f,-.95f),.16f,.09f,fur,12);b.Tube(new Vector3(.12f,.3f,-.95f),new Vector3(.14f,.29f,-1.12f),.10f,.02f,Paper,10);}
                else b.Ellipsoid(new Vector3(0,.34f,-.32f),Vector3.one*.1f,Paper,8,5);
            }
            return b.Finish("Land wildlife / "+kind);
        }
    }
}
