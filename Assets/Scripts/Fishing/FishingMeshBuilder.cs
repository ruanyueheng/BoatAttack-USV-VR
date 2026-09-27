using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Small original mesh toolkit: vertex-colored curved surfaces, not separate primitive renderers.</summary>
    public sealed class FishingMeshBuilder
    {
        private readonly List<Vector3> _vertices=new List<Vector3>();
        private readonly List<Color> _colors=new List<Color>();
        private readonly List<int> _indices=new List<int>();
        public void Ellipsoid(Vector3 center, Vector3 scale, Color color, int sides=12, int rings=8, Quaternion? rotation=null)
        {
            int start=_vertices.Count;Quaternion q=rotation??Quaternion.identity;
            for(int r=0;r<=rings;r++)for(int j=0;j<sides;j++)
            {
                float a=r*Mathf.PI/rings,b=j*Mathf.PI*2/sides;
                Add(center+q*Vector3.Scale(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)),scale),color);
            }
            for(int r=0;r<rings;r++)for(int j=0;j<sides;j++)
            {int a=start+r*sides+j,b=start+r*sides+(j+1)%sides,c=a+sides,d=b+sides;Face(a,b,c);Face(b,d,c);}
        }
        public void Tube(Vector3 from, Vector3 to, float startRadius, float endRadius, Color color, int sides=9)
        {
            Vector3 axis=to-from;if(axis.sqrMagnitude<.0001f)return;
            Quaternion q=Quaternion.FromToRotation(Vector3.up,axis.normalized);int start=_vertices.Count;
            for(int r=0;r<2;r++)for(int j=0;j<sides;j++)
            {float a=j*Mathf.PI*2/sides;Add((r==0?from:to)+q*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(r==0?startRadius:endRadius),color);}
            int bottom=_vertices.Count;Add(from,color);int top=_vertices.Count;Add(to,color);
            for(int j=0;j<sides;j++){int a=start+j,b=start+(j+1)%sides,c=a+sides,d=b+sides;Face(a,c,b);Face(b,c,d);Face(bottom,a,b);Face(top,d,c);}
        }
        public void Fin(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int start=_vertices.Count;Add(a,color);Add(b,color);Add(c,color);Face(start,start+1,start+2);
            start=_vertices.Count;Add(a,color);Add(c,color);Add(b,color);Face(start,start+1,start+2);
        }
        public void Arc(Vector3 center, float radius, float tube, float from, float to, Color color, int sections=16)
        {
            for(int i=0;i<sections;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)sections)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1)/(float)sections)*Mathf.Deg2Rad;
                Tube(center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),center+new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0),tube,tube,color);
            }
        }
        public Mesh Finish(string name)
        {
            var mesh=new Mesh{name=name};if(_vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);mesh.SetColors(_colors);mesh.SetTriangles(_indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void Add(Vector3 p,Color color){_vertices.Add(p);_colors.Add(color);}
        private void Face(int a,int b,int c){_indices.Add(a);_indices.Add(b);_indices.Add(c);}

        public static Mesh Animal(MarineKind kind)
        {
            var b=new FishingMeshBuilder();var dark=new Color(.055f,.11f,.15f);var ivory=new Color(.91f,.88f,.7f);
            if(kind==MarineKind.Turtle)
            {
                var green=new Color(.23f,.43f,.28f);
                b.Ellipsoid(Vector3.zero,new Vector3(.49f,.23f,.68f),green,16,10);
                b.Ellipsoid(new Vector3(0,-.08f,0),new Vector3(.46f,.10f,.64f),ivory);
                // Slightly raised scutes follow the shell rather than looking like a single sphere.
                for(int r=-1;r<=1;r++)for(int j=-1;j<=1;j++)
                {
                    var p=new Vector3(j*.22f,.22f-Mathf.Abs(j)*.042f-Mathf.Abs(r)*.03f,r*.29f);
                    b.Ellipsoid(p,new Vector3(.14f,.038f,.19f),new Color(.4f+r*.025f,.52f+j*.02f,.28f),6,3);
                }
                b.Ellipsoid(new Vector3(0,.02f,.78f),new Vector3(.16f,.12f,.24f),green);
                foreach(float side in new[]{-1f,1f})
                {
                    b.Ellipsoid(new Vector3(side*.53f,-.035f,.35f),new Vector3(.39f,.045f,.14f),green,12,5,Quaternion.Euler(0,side*35,side*8));
                    b.Ellipsoid(new Vector3(side*.36f,-.055f,-.53f),new Vector3(.23f,.04f,.13f),green,10,5,Quaternion.Euler(0,-side*30,0));
                    b.Ellipsoid(new Vector3(side*.132f,.085f,.87f),new Vector3(.037f,.034f,.038f),dark,8,5);
                }
            }
            else if(kind==MarineKind.Ray)
            {
                var blue=new Color(.15f,.35f,.47f);b.Ellipsoid(Vector3.zero,new Vector3(.4f,.11f,.58f),blue,14,7);
                foreach(float side in new[]{-1f,1f})
                {
                    b.Ellipsoid(new Vector3(side*.52f,0,0),new Vector3(.62f,.055f,.36f),blue,14,6,Quaternion.Euler(0,-side*20,side*8));
                    b.Ellipsoid(new Vector3(side*.16f,.11f,.34f),new Vector3(.045f,.036f,.055f),dark,8,5);
                    for(int j=0;j<5;j++)b.Ellipsoid(new Vector3(side*(.26f+j*.13f),.077f,.12f*Mathf.Sin(j*3)),new Vector3(.037f,.012f,.032f),new Color(.34f,.76f,.82f),8,4);
                }
                b.Tube(new Vector3(0,0,-.48f),new Vector3(.15f,0,-1.5f),.038f,.007f,dark);
            }
            else if(kind==MarineKind.Dolphin)
            {
                var gray=new Color(.27f,.48f,.57f);b.Ellipsoid(Vector3.zero,new Vector3(.27f,.3f,1.02f),gray,16,10);
                b.Ellipsoid(new Vector3(0,-.16f,.20f),new Vector3(.235f,.11f,.72f),new Color(.7f,.8f,.8f));
                b.Ellipsoid(new Vector3(0,0,1.02f),new Vector3(.115f,.10f,.35f),gray);
                b.Fin(new Vector3(0,.22f,.13f),new Vector3(0,.68f,-.21f),new Vector3(0,.22f,-.54f),gray);
                foreach(float side in new[]{-1f,1f})
                {
                    b.Ellipsoid(new Vector3(side*.3f,-.14f,.22f),new Vector3(.39f,.035f,.14f),gray,10,5,Quaternion.Euler(0,side*35,side*20));
                    b.Ellipsoid(new Vector3(side*.28f,0,-.96f),new Vector3(.37f,.035f,.18f),gray,12,5,Quaternion.Euler(0,-side*18,0));
                    b.Ellipsoid(new Vector3(side*.205f,.08f,.64f),Vector3.one*.033f,dark,8,5);
                }
            }
            else if(kind==MarineKind.School)
            {
                for(int i=0;i<7;i++)
                {
                    Vector3 p=new Vector3((i%3-1)*.48f,(i%2)*.045f,-i*.27f);
                    b.Ellipsoid(p,new Vector3(.06f,.09f,.22f),new Color(.56f,.75f,.78f),10,5);
                    b.Fin(p+new Vector3(0,0,-.2f),p+new Vector3(0,.1f,-.34f),p+new Vector3(0,-.1f,-.34f),new Color(.17f,.4f,.48f));
                }
            }
            else
            {
                b.Ellipsoid(Vector3.zero,new Vector3(.09f,.11f,.30f),ivory);
                b.Ellipsoid(new Vector3(0,.05f,.25f),Vector3.one*.105f,ivory,10,6);
                b.Tube(new Vector3(0,.04f,.32f),new Vector3(0,.015f,.48f),.055f,.004f,new Color(.95f,.64f,.22f));
                foreach(float side in new[]{-1f,1f})
                {
                    b.Ellipsoid(new Vector3(side*.34f,.02f,-.04f),new Vector3(.36f,.027f,.14f),ivory,12,5,Quaternion.Euler(0,side*12,side*12));
                    b.Ellipsoid(new Vector3(side*.66f,-.05f,-.13f),new Vector3(.24f,.024f,.09f),dark,10,5,Quaternion.Euler(0,side*25,-side*18));
                }
            }
            var mesh=b.Finish("Original bay wildlife / "+kind);
            var bounds=mesh.bounds;bounds.Expand(new Vector3(0,.4f,0));mesh.bounds=bounds;return mesh;
        }
        public static Mesh ReefRock()
        {
            var b=new FishingMeshBuilder();const int sides=9;var rings=new Vector3[4*sides];
            for(int r=0;r<4;r++)for(int j=0;j<sides;j++)
            {
                float a=(j+(r%2)*.18f)*Mathf.PI*2/sides,rad=(r==0?.7f:r==1?1:r==2?.84f:.31f)*(1+.11f*Mathf.Sin(j*7+r*4));
                rings[r*sides+j]=new Vector3(Mathf.Cos(a)*rad,-.6f+r*.43f,Mathf.Sin(a)*rad);
            }
            for(int r=0;r<3;r++)for(int j=0;j<sides;j++)
            {
                int a=r*sides+j,n=r*sides+(j+1)%sides;var c=Color.Lerp(new Color(.3f,.41f,.41f),new Color(.65f,.7f,.61f),(r+1)/3f);
                b.Fin(rings[a],rings[a+sides],rings[n],c);b.Fin(rings[n],rings[a+sides],rings[n+sides],c);
            }
            for(int j=0;j<sides;j++)b.Fin(new Vector3(0,.78f,0),rings[3*sides+j],rings[3*sides+(j+1)%sides],new Color(.64f,.68f,.59f));
            return b.Finish("Layered salt-weathered reef rock");
        }
    }
}
