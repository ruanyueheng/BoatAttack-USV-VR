using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingMeshBuilder
    {
        public void Box(Vector3 center,Vector3 size,Color color)
        {
            var e=size*.5f;
            var v=new[]{new Vector3(-e.x,-e.y,-e.z),new Vector3(e.x,-e.y,-e.z),new Vector3(e.x,e.y,-e.z),new Vector3(-e.x,e.y,-e.z),new Vector3(-e.x,-e.y,e.z),new Vector3(e.x,-e.y,e.z),new Vector3(e.x,e.y,e.z),new Vector3(-e.x,e.y,e.z)};
            int[,] quads={{0,3,2,1},{5,6,7,4},{4,7,3,0},{1,2,6,5},{3,7,6,2},{4,0,1,5}};
            for(int f=0;f<6;f++){int start=_vertices.Count;for(int j=0;j<4;j++)Add(center+v[quads[f,j]],color);Face(start,start+1,start+2);Face(start,start+2,start+3);}
        }
    }
    /// <summary>Original vertex-colour craft assets. One shared mesh/material per type.</summary>
    public static class FishingWorkshopArt
    {
        private static readonly Color Wood=new Color(.43f,.24f,.1f),Edge=new Color(.7f,.48f,.22f),Iron=new Color(.13f,.19f,.21f),Rock=new Color(.39f,.43f,.43f);
        public static Mesh Make(FieldItem item)
        {
            var imported=FishingV023Art.ItemMesh(item);
            if(imported!=null)return Object.Instantiate(imported);
            var b=new FishingMeshBuilder();
            if(item==FieldItem.HuntingSpear)
            {
                b.Tube(new Vector3(0,0,-.38f),new Vector3(0,0,.75f),.022f,.014f,Wood,8);
                b.Tube(new Vector3(0,0,.70f),new Vector3(0,0,.92f),.045f,.001f,Iron,4);
                for(int i=0;i<5;i++)b.Tube(new Vector3(0,0,-.045f+i*.022f),new Vector3(0,0,-.035f+i*.022f),.025f,.025f,Edge,8);
            }
            else if(item==FieldItem.RawMeat || item==FieldItem.CookedMeat)
            {
                b.Ellipsoid(new Vector3(0,.03f,0),new Vector3(.16f,.045f,.12f),item==FieldItem.RawMeat?new Color(.67f,.20f,.17f):new Color(.40f,.22f,.10f),12,6);
                for(int i=0;i<3;i++)b.Tube(new Vector3(-.09f,.07f,(i-1)*.055f),new Vector3(.09f,.07f,(i-1)*.055f),.006f,.006f,item==FieldItem.RawMeat?new Color(.92f,.66f,.50f):Iron,5);
            }
            else if(item==FieldItem.Workbench)
            {
                foreach(float x in new[]{-.53f,.53f})foreach(float z in new[]{-.32f,.32f})b.Box(new Vector3(x,.43f,z),new Vector3(.13f,.86f,.13f),Wood);
                for(int i=0;i<5;i++)b.Box(new Vector3(0,.9f,(i-2)*.18f),new Vector3(1.3f,.13f,.166f),Edge);
                b.Box(new Vector3(0,.25f,0),new Vector3(1.16f,.07f,.67f),Wood);
                b.Box(new Vector3(.4f,1,.28f),new Vector3(.23f,.17f,.19f),Iron);
                b.Tube(new Vector3(.4f,1,.4f),new Vector3(.4f,1,.54f),.026f,.026f,Iron,8);
                b.Tube(new Vector3(-.32f,.99f,-.1f),new Vector3(.06f,.99f,.1f),.028f,.028f,Wood,8);
                b.Box(new Vector3(.06f,1,.1f),new Vector3(.1f,.08f,.21f),Iron);
            }
            else if(item==FieldItem.Forge)
            {
                b.Box(new Vector3(0,.08f,0),new Vector3(1.25f,.16f,.9f),Rock);
                foreach(float x in new[]{-.46f,.46f})foreach(float z in new[]{-.28f,.28f})b.Box(new Vector3(x,.4f,z),new Vector3(.16f,.66f,.16f),Wood);
                b.Box(new Vector3(0,.72f,0),new Vector3(1.25f,.17f,.9f),Rock*.8f);
                // Anvil base, waist, face and tapered horn form a clear silhouette.
                b.Box(new Vector3(-.18f,.84f,0),new Vector3(.54f,.09f,.35f),Iron);
                b.Box(new Vector3(-.16f,.97f,0),new Vector3(.2f,.21f,.21f),Iron);
                b.Box(new Vector3(-.15f,1.1f,0),new Vector3(.65f,.1f,.30f),new Color(.47f,.57f,.6f));
                b.Tube(new Vector3(-.47f,1.08f,0),new Vector3(-.64f,1.09f,0),.09f,.008f,Iron,6);
                b.Ellipsoid(new Vector3(.41f,.84f,.10f),new Vector3(.18f,.07f,.22f),Iron,10,4);
                for(int i=0;i<6;i++)b.Box(new Vector3(.33f+(i%3)*.07f,.89f,.03f+(i/3)*.12f),new Vector3(.055f,.045f,.07f),i%2==0?new Color(.9f,.22f,.035f):new Color(1,.52f,.1f));
                b.Box(new Vector3(.46f,1.12f,.33f),new Vector3(.16f,.58f,.16f),Rock*.7f);
                b.Box(new Vector3(.46f,1.415f,.33f),new Vector3(.10f,.015f,.10f),Color.black);
                b.Tube(new Vector3(-.24f,.84f,-.32f),new Vector3(.11f,.84f,-.24f),.024f,.024f,Wood,8);
                b.Box(new Vector3(.10f,.86f,-.24f),new Vector3(.12f,.09f,.22f),Iron);
                b.Tube(new Vector3(.26f,.83f,-.31f),new Vector3(.51f,.83f,-.2f),.012f,.012f,Iron,6);
            }
            else if(item==FieldItem.Furnace)
            {
                b.Box(new Vector3(0,.08f,0),new Vector3(1.08f,.16f,1.02f),Rock);
                // Open firebox: not a solid black cube. Stone courses surround the front opening.
                for(int row=0;row<4;row++)
                {
                    float y=.24f+row*.2f;
                    b.Box(new Vector3(-.42f,y,0),new Vector3(.2f,.185f,.9f),Rock*(.9f+row*.06f));
                    b.Box(new Vector3(.42f,y,0),new Vector3(.2f,.185f,.9f),Rock*(.9f+row*.06f));
                    b.Box(new Vector3(0,y,.36f),new Vector3(.7f,.185f,.18f),Rock);
                    if(row>=2)b.Box(new Vector3(0,y,-.36f),new Vector3(.7f,.185f,.18f),Rock);
                }
                b.Box(new Vector3(0,.98f,0),new Vector3(1.06f,.16f,1.01f),Rock);
                b.Box(new Vector3(0,1.23f,.18f),new Vector3(.34f,.4f,.32f),Iron);
                b.Box(new Vector3(0,1.435f,.18f),new Vector3(.23f,.018f,.21f),Color.black);
                for(int i=0;i<5;i++)b.Tube(new Vector3(-.28f+i*.14f,.2f,-.42f),new Vector3(-.28f+i*.14f,.2f,.25f),.018f,.018f,Iron,6);
            }
            else if(item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe)
            {
                b.Tube(new Vector3(0,-.26f,0),new Vector3(0,.33f,0),.025f,.038f,Wood,8);
                var color=item==FieldItem.IronPickaxe?new Color(.65f,.74f,.76f):Rock;
                b.Tube(new Vector3(-.28f,.26f,0),new Vector3(0,.34f,0),.015f,.065f,color,6);
                b.Tube(new Vector3(0,.34f,0),new Vector3(.28f,.26f,0),.065f,.015f,color,6);
            }
            else if(item==FieldItem.Driftwood)
            {
                for(int i=0;i<3;i++)b.Tube(new Vector3(-.4f,.07f+i*.035f,i*.12f),new Vector3(.38f,.08f+i*.035f,i*.1f-.1f),.07f,.045f,Wood,7);
            }
            else if(item==FieldItem.Fiber)
            {
                for(int i=0;i<7;i++)b.Tube(new Vector3((i-3)*.08f,0,0),new Vector3((i-3)*.13f,.45f+(i%3)*.16f,.03f),.018f,.004f,new Color(.25f,.45f,.19f),5);
            }
            else
            {
                Color color=item==FieldItem.Clay?new Color(.67f,.33f,.2f):item==FieldItem.Sand?new Color(.79f,.69f,.43f):item==FieldItem.Coal?new Color(.12f,.14f,.16f):Rock;
                for(int i=0;i<4;i++)b.Ellipsoid(new Vector3((i%2-.5f)*.35f,.13f,i/2*.23f),new Vector3(.26f,.2f,.23f),color,5,3,Quaternion.Euler(0,i*43,0));
                if(item==FieldItem.IronOre)for(int i=0;i<5;i++)b.Box(new Vector3((i%3-1)*.17f,.3f,i/3*.22f),new Vector3(.09f,.06f,.1f),new Color(.76f,.39f,.21f));
            }
            return b.Finish("Workshop / "+item);
        }
    }
}
