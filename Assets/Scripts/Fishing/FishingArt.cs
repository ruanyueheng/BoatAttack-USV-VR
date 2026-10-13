using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    /// <summary>Shared procedural art and the supplied Hunyuan fish assets.</summary>
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

        /// <summary>One renderer per fish: a cached single vertex-coloured mesh with the shared gloss palette.</summary>
        public static GameObject Fish(Transform parent, List<Object> owned, Shader palette, FishKind kind, bool rare = false)
        {
            var go = new GameObject("Original " + kind + (rare ? " pearl variant" : " fish"), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var source=Resources.Load<GameObject>("FishingFish/"+kind);
            var imported=source!=null?source.GetComponentInChildren<MeshFilter>():null;
            go.GetComponent<MeshFilter>().sharedMesh = imported!=null?imported.sharedMesh:Cached(owned, FishingMeshBuilder.FishMeshName(kind, rare), () => FishingMeshBuilder.Fish(kind, rare));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Palette(owned, palette, "Fishing v0.4 fish gloss", .55f, .3f);
            if(imported!=null)
            {
                go.name="Hunyuan / "+kind+(rare?" pearl":"");
                var material=Palette(owned,palette,"Hunyuan fish / "+kind+(rare?" pearl":""),.22f,.12f);
                material.SetTexture("_BaseMap",Resources.Load<Texture2D>("FishingFish/"+kind+"Albedo"));
                material.SetFloat("_Textured",1);
                material.SetVector("_TailMotion",new Vector4(.28f,4,FishingSpecies.LengthMeters(kind)*.15f,0));
                material.SetColor("_BaseColor",rare?new Color(1,.86f,.62f):Color.white);
                renderer.sharedMaterial=material;
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Buoy(Transform parent, List<Object> owned, Shader palette, Vector3 position, float scale)
        {
            var go = new GameObject("Navigation buoy", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = Cached(owned, "Fishing v0.4 navigation buoy", FishingMeshBuilder.Buoy);
            var collider=go.AddComponent<CapsuleCollider>();
            var bounds=go.GetComponent<MeshFilter>().sharedMesh.bounds;
            collider.center=bounds.center;collider.radius=Mathf.Max(bounds.extents.x,bounds.extents.z);collider.height=bounds.size.y;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Palette(owned, palette, "Fishing v0.4 buoy paint", .3f, .12f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Instanced palette material shared by every renderer that asks for the same name.</summary>
        public static Material Palette(List<Object> owned, Shader palette, string name, float gloss, float rim)
        {
            foreach (var o in owned) if (o is Material existing && existing.name == name) return existing;
            var material = new Material(palette) { name = name, enableInstancing = true };
            material.SetFloat("_Gloss", gloss);
            material.SetFloat("_Rim", rim);
            owned.Add(material);
            return material;
        }

        private static T Cached<T>(List<Object> owned, string name, Func<T> create) where T : Object
        {
            foreach (var o in owned) if (o is T found && found.name == name) return found;
            var made = create();
            made.name = name;
            owned.Add(made);
            return made;
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
