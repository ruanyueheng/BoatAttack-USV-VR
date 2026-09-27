using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    /// <summary>Original procedural meshes. No commercial-game assets are used.</summary>
    public static class FishingArt
    {
        public static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider); }
            return go;
        }
        public static GameObject Fish(Transform parent, Material body, Material cream, Material dark, List<Object> owned, FishKind kind = FishKind.Coral, bool rare = false)
        {
            var root = new GameObject("Original " + kind + (rare ? " pearl variant" : " fish"));
            root.transform.SetParent(parent, false);
            if (kind == FishKind.Sailfish) kind = FishKind.Dart;
            var mesh = CachedMesh(kind, owned);
            var bodyGo = new GameObject("Fish body", typeof(MeshFilter), typeof(MeshRenderer));
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.GetComponent<MeshFilter>().sharedMesh = mesh;
            bodyGo.GetComponent<MeshRenderer>().sharedMaterial = body;
            float eyeX = kind == FishKind.Puffer ? 0.3f : kind == FishKind.Dart ? 0.095f : 0.16f;
            float eyeZ = kind == FishKind.Dart ? 0.56f : 0.37f;
            foreach (float side in new[] { -1f, 1f })
            {
                Shape("Ivory eye",PrimitiveType.Sphere,root.transform,new Vector3(side*eyeX,0.09f,eyeZ),Vector3.one*0.105f,cream);
                Shape("Glossy pupil",PrimitiveType.Sphere,root.transform,new Vector3(side*(eyeX+0.042f),0.09f,eyeZ+0.009f),Vector3.one*0.058f,dark);
                Shape("Eye glint",PrimitiveType.Sphere,root.transform,new Vector3(side*(eyeX+0.068f),0.106f,eyeZ+0.02f),Vector3.one*0.018f,cream);
                var gill = Line("Gill",root.transform,dark,0.015f,false);gill.positionCount=3;
                gill.SetPositions(new[]{new Vector3(side*(eyeX+0.02f),0.12f,0.17f),new Vector3(side*(eyeX+0.035f),0,0.14f),new Vector3(side*eyeX,-0.15f,0.18f)});
            }
            Shape("Mouth",PrimitiveType.Sphere,root.transform,new Vector3(0,0,kind==FishKind.Dart?0.85f:0.56f),new Vector3(0.085f,0.065f,0.025f),dark);
            if(kind==FishKind.Coral)
            {
                Band(root.transform,-0.24f,0.145f,0.28f,cream);
                Band(root.transform,0.1f,0.21f,0.35f,cream);
            }
            else if(kind==FishKind.Dart)
            {
                foreach(float side in new[]{-1f,1f})
                {
                    var stripe=Line("Silver lateral stripe",root.transform,cream,0.03f,false);stripe.positionCount=4;
                    stripe.SetPositions(new[]{new Vector3(side*0.06f,0,-0.62f),new Vector3(side*0.15f,0,-0.1f),new Vector3(side*0.13f,0,0.38f),new Vector3(side*0.035f,0,0.74f)});
                }
            }
            else
            {
                for(int i=0;i<12;i++)
                {
                    float angle=i*Mathf.PI*2/12;
                    var dot=Shape("Puffer spot",PrimitiveType.Sphere,root.transform,new Vector3(Mathf.Cos(angle)*0.39f,Mathf.Sin(angle)*0.39f,-0.06f),new Vector3(0.07f,0.07f,0.06f),dark);
                    dot.transform.localScale*=i%2==0?1f:0.7f;
                }
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI*2/8;Vector3 normal=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                    Pole("Puffer spine",root.transform,normal*0.4f+Vector3.back*0.19f,normal*0.52f+Vector3.back*0.19f,0.018f,cream);
                }
            }
            if(rare)Band(root.transform,-0.36f,kind==FishKind.Puffer?0.29f:0.10f,kind==FishKind.Puffer?0.29f:0.19f,cream);
            return root;
        }
        private static void Band(Transform parent,float z,float width,float height,Material mat)
        {
            var line=Line("Ivory body band",parent,mat,0.07f,false);line.positionCount=25;
            for(int i=0;i<25;i++){float a=i*Mathf.PI*2f/24;line.SetPosition(i,new Vector3(Mathf.Cos(a)*width,Mathf.Sin(a)*height,z));}
        }
        private static Mesh CachedMesh(FishKind kind,List<Object> owned)
        {
            string name="Fishing v0.2 mesh "+kind;
            foreach(var asset in owned)if(asset is Mesh mesh && mesh.name==name)return mesh;
            var verts=new List<Vector3>();var triangles=new List<int>();
            float[] zs,rx,ry;
            if(kind==FishKind.Dart)
            {
                zs=new[]{-0.75f,-0.48f,-0.12f,0.3f,0.64f,0.86f};rx=new[]{0.026f,0.08f,0.16f,0.145f,0.065f,0.01f};ry=new[]{0.06f,0.12f,0.2f,0.18f,0.085f,0.01f};
            }
            else if(kind==FishKind.Puffer)
            {
                zs=new[]{-0.55f,-0.38f,-0.12f,0.16f,0.39f,0.57f};rx=new[]{0.04f,0.28f,0.42f,0.41f,0.29f,0.03f};ry=new[]{0.05f,0.3f,0.43f,0.41f,0.28f,0.03f};
            }
            else
            {
                zs=new[]{-0.55f,-0.36f,-0.13f,0.13f,0.38f,0.57f};rx=new[]{0.035f,0.095f,0.20f,0.205f,0.12f,0.025f};ry=new[]{0.045f,0.2f,0.35f,0.34f,0.18f,0.035f};
            }
            const int sides=20;
            for(int r=0;r<zs.Length;r++)for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;verts.Add(new Vector3(Mathf.Cos(a)*rx[r],Mathf.Sin(a)*ry[r],zs[r]));}
            for(int r=0;r<zs.Length-1;r++)for(int j=0;j<sides;j++)
            {
                int a=r*sides+j,b=r*sides+(j+1)%sides,c=a+sides,d=b+sides;triangles.AddRange(new[]{a,b,c,b,d,c});
            }
            int back=verts.Count;verts.Add(new Vector3(0,0,zs[0]));int front=verts.Count;verts.Add(new Vector3(0,0,zs[zs.Length-1]));
            for(int j=0;j<sides;j++){triangles.AddRange(new[]{back,(j+1)%sides,j,front,(zs.Length-1)*sides+j,(zs.Length-1)*sides+(j+1)%sides});}
            float tail=kind==FishKind.Dart?-1.02f:-0.89f;
            Fin(verts,triangles,new Vector3(0,0,zs[0]+0.04f),new Vector3(0,0.31f,tail),new Vector3(0,0.07f,tail+0.09f));
            Fin(verts,triangles,new Vector3(0,0,zs[0]+0.04f),new Vector3(0,-0.31f,tail),new Vector3(0,-0.07f,tail+0.09f));
            Fin(verts,triangles,new Vector3(0,ry[2]*0.9f,-0.31f),new Vector3(0,ry[2]+0.20f,-0.20f),new Vector3(0,ry[3]*0.9f,0.2f));
            foreach(float side in new[]{-1f,1f})Fin(verts,triangles,new Vector3(side*rx[2],-0.035f,0.16f),new Vector3(side*(rx[2]+0.25f),-0.18f,-0.1f),new Vector3(side*rx[2],-0.10f,-0.22f));
            var result=new Mesh{name=name};result.SetVertices(verts);result.SetTriangles(triangles,0);
            result.RecalculateNormals();result.RecalculateBounds();owned.Add(result);return result;
        }
        private static void Fin(List<Vector3> verts,List<int> tris,Vector3 a,Vector3 b,Vector3 c)
        {
            // The reverse face needs its own vertices; shared opposite triangles cancel their normals.
            int n=verts.Count;verts.AddRange(new[]{a,b,c,c,b,a});tris.AddRange(new[]{n,n+1,n+2,n+3,n+4,n+5});
        }
        public static Transform Pole(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var pole=Shape(name,PrimitiveType.Cylinder,parent,(a+b)*0.5f,new Vector3(thickness,Vector3.Distance(a,b)*0.5f,thickness),material);
            pole.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);return pole.transform;
        }
        public static LineRenderer Line(string name, Transform parent, Material material, float width, bool world = true)
        {
            var line=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(parent,false);
            line.generateLightingData=true;line.sharedMaterial=material;line.startWidth=line.endWidth=width;line.useWorldSpace=world;line.numCapVertices=3;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        public static void Ring(string name, Transform parent, Vector3 center, float radius, Material material)
        {
            var line=Line(name,parent,material,0.085f);line.positionCount=65;
            for(int i=0;i<65;i++){float a=i*Mathf.PI*2f/64f;line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        }
    }
}
