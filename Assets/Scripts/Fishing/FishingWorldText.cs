using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Depth-tested world lettering; leave the shared UI font material untouched.</summary>
    public static class FishingWorldText
    {
        private static readonly Dictionary<Font,Material> Materials=new Dictionary<Font,Material>();
        private static bool _listening;
        public static void Apply(TextMesh text)
        {
            var font=text.font;
            if(!_listening){Font.textureRebuilt+=RefreshAtlas;_listening=true;}
            if(!Materials.TryGetValue(font,out var material))
            {
                // Font atlas RGB is undefined: use its alpha alone to preserve the TextMesh ink colour.
                material=new Material(Resources.Load<Shader>("FishingWorldText")){name="World lettering / "+font.name,hideFlags=HideFlags.DontSave};
                Materials.Add(font,material);
            }
            material.mainTexture=font.material.mainTexture;
            text.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static void RefreshAtlas(Font font)
        {if(Materials.TryGetValue(font,out var material) && material!=null)material.mainTexture=font.material.mainTexture;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if(_listening)Font.textureRebuilt-=RefreshAtlas;
            _listening=false;foreach(var material in Materials.Values)if(material!=null)Object.Destroy(material);Materials.Clear();
        }
    }
}
