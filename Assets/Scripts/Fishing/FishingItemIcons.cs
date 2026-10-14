using System.Collections.Generic;
using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>Small transparent pictograms. No font, character, or text fallback.</summary>
    public static class FishingItemIcons
    {
        private static readonly Dictionary<FieldItem,Texture2D> Cache=new Dictionary<FieldItem,Texture2D>();
        private static readonly Color Ink=new Color32(24,37,46,255),Wood=new Color32(176,116,62,255),Light=new Color32(244,224,183,255),Steel=new Color32(125,178,196,255),Green=new Color32(103,172,112,255),Red=new Color32(203,91,92,255);
        public static Texture2D Get(FieldItem item)
        {
            if(item==FieldItem.Empty || !FishingBackpack.Defined(item))return null;
            if(Cache.TryGetValue(item,out var cached) && cached!=null)return cached;
            var p=new Painter();
            switch(item)
            {
                case FieldItem.Forge:
                    p.Rect(13,49,38,7,Wood);p.Poly(Steel,7,17,53,17,57,23,39,32,36,45,19,45,22,29,7,26);p.Line(13,19,47,19,3,Light);
                    p.Line(44,8,57,36,4,Wood);p.Rect(38,4,16,8,Steel);p.Circle(52,47,5,Red);break;
                case FieldItem.FieldGuide:
                    p.Poly(Green,10,9,48,6,54,52,15,57);p.Poly(Light,17,13,44,10,48,47,20,51);p.Line(14,11,18,53,4,Wood);p.Line(24,21,41,18,2,Wood);p.Line(25,29,42,26,2,Wood);break;
                case FieldItem.Binoculars:
                    p.Line(19,27,45,27,9,Steel);p.Rect(9,13,19,31,Ink);p.Rect(37,13,19,31,Ink);p.Circle(19,43,12,Steel);p.Circle(46,43,12,Steel);p.Circle(19,43,7,Ink);p.Circle(46,43,7,Ink);p.Circle(17,40,3,Light);p.Circle(44,40,3,Light);break;
                case FieldItem.Lantern:
                    p.Ring(32,14,10,3,Wood);p.Rect(16,23,32,27,Light);p.Rect(12,19,40,6,Steel);p.Rect(12,49,40,7,Steel);p.Line(17,24,17,49,4,Wood);p.Line(47,24,47,49,4,Wood);p.Poly(Wood,28,45,32,29,38,45);break;
                case FieldItem.Shell:
                    p.Poly(Light,8,27,14,15,25,10,38,10,51,19,57,30,36,53,27,53);for(int i=0;i<5;i++)p.Line(31,48,13+i*9,22-Mathf.Sin(i*.8f)*7,2,Wood);break;
                case FieldItem.Driftwood:case FieldItem.Stick:
                    p.Line(12,48,49,13,item==FieldItem.Stick?7:14,Wood);p.Line(29,32,51,34,5,Wood);p.Line(14,47,47,16,2,Light);break;
                case FieldItem.Plank:
                    p.Poly(Wood,7,22,48,9,58,40,16,54);p.Line(15,28,48,17,2,Light);p.Line(18,38,51,27,2,Ink);p.Line(20,47,54,36,2,Light);break;
                case FieldItem.Fiber:
                    p.Line(22,53,40,11,3,Light);p.Line(31,52,19,13,3,Green);p.Line(35,52,50,19,3,Green);p.Poly(Green,25,30,12,21,13,36,30,43);p.Poly(Green,34,30,48,9,48,26,32,41);p.Line(24,45,40,45,4,Wood);break;
                case FieldItem.Rope:
                    p.Ring(31,31,23,7,Wood);p.Ring(31,31,12,5,Light);p.Line(48,43,53,55,5,Wood);break;
                case FieldItem.Stone:case FieldItem.IronOre:case FieldItem.Coal:
                    var rock=item==FieldItem.Coal?new Color(.22f,.27f,.31f):Steel;
                    p.Poly(rock,7,42,15,19,34,10,54,27,58,46,34,55);p.Poly(rock*.7f,7,42,34,33,34,55);p.Line(17,21,34,15,3,Light);
                    if(item==FieldItem.IronOre){p.Poly(Wood,21,28,32,24,37,35,24,41);p.Circle(45,41,5,Wood);}break;
                case FieldItem.Clay:
                    p.Circle(25,38,18,Wood);p.Circle(42,37,14,Wood);p.Poly(Light,15,31,29,22,39,31,26,37);break;
                case FieldItem.Sand:
                    p.Poly(Light,5,51,21,28,34,15,47,31,60,51);p.Circle(24,42,2,Wood);p.Circle(36,29,2,Wood);p.Circle(44,44,2,Wood);break;
                case FieldItem.Charcoal:
                    p.Line(15,48,33,15,12,Ink);p.Line(35,49,51,26,11,Ink);p.Line(16,44,30,20,2,Steel);p.Line(35,44,47,28,2,Steel);break;
                case FieldItem.IronIngot:
                    p.Poly(Steel,6,45,16,20,47,16,59,39,45,52);p.Poly(Light,16,20,47,16,49,29,12,35);p.Line(12,36,47,31,2,Ink);break;
                case FieldItem.Brick:
                    p.Poly(Red,5,26,41,12,59,25,59,44,23,57,5,44);p.Line(5,27,23,40,2,Light);p.Line(23,40,59,26,2,Light);p.Line(23,40,23,56,2,Light);break;
                case FieldItem.Glass:
                    p.Poly(Steel,11,11,47,7,54,48,19,56);p.Poly(new Color(.35f,.75f,.78f,.65f),17,16,42,13,47,44,23,49);p.Line(21,24,36,15,3,Light);p.Line(28,42,42,31,2,Light);break;
                case FieldItem.Workbench:
                    p.Rect(7,19,50,12,Wood);p.Line(15,31,13,55,7,Wood);p.Line(48,31,50,55,7,Wood);p.Rect(13,44,37,5,Light);p.Rect(39,10,12,9,Steel);break;
                case FieldItem.Furnace:
                    p.Poly(Steel,10,54,10,24,20,10,45,10,55,24,55,54);p.Rect(20,30,25,24,Ink);p.Poly(Red,23,51,26,37,31,42,37,33,44,51);p.Poly(Light,29,51,32,42,39,51);p.Line(14,22,51,22,3,Ink);break;
                case FieldItem.StonePickaxe:case FieldItem.IronPickaxe:
                    p.Line(17,54,39,15,7,Wood);p.Poly(item==FieldItem.IronPickaxe?Steel:new Color(.62f,.64f,.58f),10,18,25,10,43,12,56,24,52,34,42,24,29,21,12,26);p.Line(17,20,31,17,2,Light);break;
                case FieldItem.HuntingSpear:
                    p.Line(11,55,44,20,6,Wood);p.Poly(Steel,34,16,57,5,49,31,45,23);p.Line(35,23,42,29,3,Light);break;
                case FieldItem.HuntingRifle:
                    p.Line(5,24,37,24,4,Steel);p.Rect(22,22,22,9,Steel);p.Rect(17,26,15,6,Wood);p.Poly(Wood,43,23,60,27,59,40,42,31);p.Poly(Ink,27,31,37,31,33,49,26,47);p.Line(42,31,45,42,6,Wood);break;
                case FieldItem.RabbitFur:case FieldItem.Fur:
                    p.Poly(item==FieldItem.Fur?Wood:Light,20,16,16,3,23,4,29,18,36,18,41,4,48,4,44,21,53,30,47,36,51,51,41,48,33,58,24,50,12,52,18,38,10,31);
                    p.Circle(32,34,10,new Color(.79f,.73f,.64f));break;
                case FieldItem.FoxFur:
                    p.Poly(Wood,10,10,26,18,38,14,53,22,55,35,43,49,23,57,14,53,29,40,28,29,15,26);
                    p.Poly(Light,14,53,23,57,35,51,29,46,29,40,22,45);break;
                case FieldItem.TurtleShell:
                    p.Circle(32,33,25,Wood);p.Circle(32,32,22,Green);p.Poly(Olive,22,23,34,17,44,26,41,40,27,45,18,34);
                    p.Line(22,23,17,14,2,Ink);p.Line(34,17,35,10,2,Ink);p.Line(44,26,53,23,2,Ink);p.Line(41,40,49,48,2,Ink);p.Line(27,45,24,54,2,Ink);p.Line(18,34,10,36,2,Ink);break;
                case FieldItem.GullFeather:
                    p.Poly(Light,10,53,13,26,31,10,53,4,55,25,39,42);p.Line(10,56,47,13,3,Steel);for(int i=0;i<4;i++)p.Line(19+i*7,46-i*8,39+i*3,43-i*9,2,Steel);break;
                case FieldItem.GullMeat:case FieldItem.RabbitMeat:
                    p.Line(20,44,10,54,8,Light);p.Circle(8,51,5,Light);p.Circle(13,57,5,Light);p.Circle(37,27,21,item==FieldItem.GullMeat?new Color(.91f,.63f,.47f):Red);p.Circle(40,25,13,new Color(1,.73f,.67f));p.Line(28,22,33,16,3,Light);break;
                case FieldItem.TurtleMeat:
                    p.Circle(32,33,24,Green);p.Circle(32,33,19,Red);p.Circle(34,31,6,Light);p.Line(18,41,24,45,3,Light);break;
                case FieldItem.CookedMeat:case FieldItem.FoxMeat:case FieldItem.RawMeat:case FieldItem.Meat:
                    var meat=item==FieldItem.CookedMeat?Wood:Red;
                    p.Poly(Light,10,18,31,9,54,18,57,37,43,54,20,53,7,38);p.Poly(meat,14,22,32,15,49,23,50,37,40,48,21,47,13,35);p.Circle(37,29,7,Light);p.Circle(37,29,3,meat);
                    if(item==FieldItem.CookedMeat){p.Line(18,31,25,25,3,Ink);p.Line(22,41,29,35,3,Ink);}break;
            }
            Cache[item]=p.Finish(item.ToString());return Cache[item];
        }
        private static readonly Color Olive=new Color(.27f,.44f,.24f);
        private sealed class Painter
        {
            private const int Size=128;
            private readonly Color[] pixels=new Color[Size*Size];
            private void Put(int x,int y,Color color){if(x>=0 && x<Size && y>=0 && y<Size)pixels[(Size-1-y)*Size+x]=color;}
            public void Rect(float x,float y,float w,float h,Color c){for(int j=(int)(y*2);j<(y+h)*2;j++)for(int i=(int)(x*2);i<(x+w)*2;i++)Put(i,j,c);}
            public void Circle(float x,float y,float radius,Color c)
            {for(int j=Mathf.FloorToInt((y-radius)*2);j<=(y+radius)*2;j++)for(int i=Mathf.FloorToInt((x-radius)*2);i<=(x+radius)*2;i++)if(new Vector2(i*.5f-x,j*.5f-y).sqrMagnitude<=radius*radius)Put(i,j,c);}
            public void Ring(float x,float y,float radius,float thickness,Color c)
            {for(int j=Mathf.FloorToInt((y-radius)*2);j<=(y+radius)*2;j++)for(int i=Mathf.FloorToInt((x-radius)*2);i<=(x+radius)*2;i++){float d=new Vector2(i*.5f-x,j*.5f-y).magnitude;if(d<=radius && d>=radius-thickness)Put(i,j,c);}}
            public void Line(float x,float y,float xx,float yy,float width,Color c)
            {var a=new Vector2(x,y);var b=new Vector2(xx,yy);int steps=Mathf.CeilToInt(Vector2.Distance(a,b)*3);for(int i=0;i<=steps;i++){var p=Vector2.Lerp(a,b,i/(float)Mathf.Max(1,steps));Circle(p.x,p.y,width*.5f,c);}}
            public void Poly(Color c,params float[] p)
            {
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                {
                    bool inside=false;float px=x*.5f,py=y*.5f;int count=p.Length/2;
                    for(int i=0,j=count-1;i<count;j=i++)if((p[2*i+1]>py)!=(p[2*j+1]>py) && px<(p[2*j]-p[2*i])*(py-p[2*i+1])/(p[2*j+1]-p[2*i+1])+p[2*i])inside=!inside;
                    if(inside)Put(x,y,c);
                }
            }
            public Texture2D Finish(string name)
            {var t=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Inventory pictogram / "+name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(pixels);t.Apply();return t;}
        }
    }
}
