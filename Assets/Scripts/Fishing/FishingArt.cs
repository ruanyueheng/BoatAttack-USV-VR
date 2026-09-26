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
            if (!solid) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
        public static GameObject Fish(Transform parent, Material body, Material cream, Material dark, List<Object> owned)
        {
            var root = new GameObject("Original low-poly fish"); root.transform.SetParent(parent, false);
            Mesh mesh = null;
            foreach (var asset in owned)
                if (asset is Mesh existing && existing.name == "Fishing prototype original fish") { mesh = existing; break; }
            if (mesh == null) { mesh = CreateFishMesh(); owned.Add(mesh); }
            var bodyGo = new GameObject("Fish body",typeof(MeshFilter),typeof(MeshRenderer)); bodyGo.transform.SetParent(root.transform,false);
            bodyGo.GetComponent<MeshFilter>().sharedMesh=mesh; bodyGo.GetComponent<MeshRenderer>().sharedMaterial=body;
            foreach(float side in new[]{-1f,1f})
            {
                Shape("Eye",PrimitiveType.Sphere,root.transform,new Vector3(side*0.135f,0.12f,0.32f),Vector3.one*0.12f,cream);
                Shape("Pupil",PrimitiveType.Sphere,root.transform,new Vector3(side*0.182f,0.12f,0.345f),Vector3.one*0.062f,dark);
            }
            return root;
        }
        private static Mesh CreateFishMesh()
        {
            var verts = new List<Vector3>(); var triangles = new List<int>();
            float[] zs = { -0.55f, -0.3f, 0.18f, 0.53f }; float[] radii = { 0.06f, 0.28f, 0.34f, 0.09f };
            for (int ring = 0; ring < 4; ring++) for (int side = 0; side < 8; side++)
            {
                float a = side * Mathf.PI * 0.25f;
                verts.Add(new Vector3(Mathf.Cos(a) * radii[ring] * 0.65f, Mathf.Sin(a) * radii[ring], zs[ring]));
            }
            for (int ring = 0; ring < 3; ring++) for (int side = 0; side < 8; side++)
            {
                int a = ring * 8 + side, b = ring * 8 + (side + 1) % 8, c = a + 8, d = b + 8;
                triangles.AddRange(new[] { a, b, c, b, d, c });
            }
            int tail = verts.Count;
            verts.AddRange(new[] { new Vector3(0,0,-0.48f), new Vector3(0,0.33f,-0.95f), new Vector3(0,-0.33f,-0.95f), new Vector3(0,0.28f,-0.08f),new Vector3(0,0.57f,-0.2f),new Vector3(0,0.18f,-0.4f) });
            triangles.AddRange(new[]{tail,tail+1,tail+2,tail+2,tail+1,tail,tail+3,tail+4,tail+5,tail+5,tail+4,tail+3});
            // Split vertices for intentionally faceted shading.
            var flat = new Vector3[triangles.Count]; var indices = new int[triangles.Count];
            for (int i = 0; i < flat.Length; i++) { flat[i] = verts[triangles[i]]; indices[i] = i; }
            var mesh = new Mesh { name = "Fishing prototype original fish" };
            mesh.vertices = flat; mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        public static Transform Pole(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var pole=Shape(name,PrimitiveType.Cylinder,parent,(a+b)*0.5f,new Vector3(thickness,Vector3.Distance(a,b)*0.5f,thickness),material);
            pole.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);return pole.transform;
        }
        public static LineRenderer Line(string name, Transform parent, Material material, float width, bool world = true)
        {
            var line=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(parent,false);
            line.sharedMaterial=material;line.startWidth=line.endWidth=width;line.useWorldSpace=world;line.numCapVertices=3;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        public static void Ring(string name, Transform parent, Vector3 center, float radius, Material material)
        {
            var line=Line(name,parent,material,0.085f);line.positionCount=65;
            for(int i=0;i<65;i++){float a=i*Mathf.PI*2f/64f;line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        }
    }
}
