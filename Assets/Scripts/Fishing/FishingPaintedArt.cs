using System.Collections.Generic;
using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>V044 hand-painted basecolor for remodelled props: looks up "&lt;resource&gt;Albedo" (or "&lt;resource&gt;-&lt;mesh&gt;Albedo"
    /// for multi-part models) next to the model and swaps in a textured FishingPalette material. Props without a texture keep their vertex colours.</summary>
    public static class FishingPaintedArt
    {
        private static readonly Dictionary<Texture2D,Material> Materials=new Dictionary<Texture2D,Material>();
        public static void Apply(Transform root,string resource)
        {
            if(resource.EndsWith("View"))resource=resource.Substring(0,resource.Length-4);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var texture=Resources.Load<Texture2D>(resource+"-"+filter.name.Replace("(Clone)","")+"Albedo") ?? Resources.Load<Texture2D>(resource+"Albedo");
                var renderer=filter.GetComponent<Renderer>();if(texture==null || renderer==null)continue;
                renderer.sharedMaterial=Material(texture);
            }
        }
        /// <summary>Textured palette material for one resource, or null when it has no painted albedo.</summary>
        public static Material For(string resource){var texture=Resources.Load<Texture2D>(resource+"Albedo");return texture==null?null:Material(texture);}
        private static Material Material(Texture2D texture)
        {
            if(Materials.TryGetValue(texture,out var material) && material!=null)return material;
            material=new Material(Resources.Load<Shader>("FishingPalette")){name="V044 painted / "+texture.name};
            material.SetTexture("_BaseMap",texture);material.SetFloat("_Textured",1);material.SetColor("_BaseColor",Color.white);
            return Materials[texture]=material;
        }
    }
}
