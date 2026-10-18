using UnityEngine;
using System.Collections.Generic;
namespace BoatAttack.Fishing
{
    public static class FishingV029Art
    {
        public static void Apply(Renderer renderer,string name,List<Object> owned,Shader shader)
        {
            if(name==null)return;
            var texture=Resources.Load<Texture2D>("FishingV029/"+name+"Albedo");if(texture==null)return;
            var material=FishingArt.Palette(owned,shader,"User wildlife / "+name,.12f,.05f);
            material.SetTexture("_BaseMap",texture);material.SetFloat("_Textured",1);renderer.sharedMaterial=material;
        }
        public static Mesh Animal(string name)
        {
            if(name==null)return null;
            var source=Resources.Load<GameObject>("FishingV029/"+name);
            return source==null?null:source.GetComponentInChildren<MeshFilter>().sharedMesh;
        }
    }
}
