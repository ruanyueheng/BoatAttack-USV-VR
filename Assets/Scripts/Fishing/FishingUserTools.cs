using UnityEngine;
namespace BoatAttack.Fishing
{
    public static class FishingUserTools
    {
        private static Material[] _pickMaterials,_lanternMaterials,_roastMaterials;
        private static string Path(FieldItem item)=>IsPick(item)?"FishingUserTools/Pickaxe":item==FieldItem.Lantern?"FishingUserTools/Lantern":item==FieldItem.GrilledFish?"FishingV039/GrilledFish":null;
        public static bool IsPick(FieldItem item)=>item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;
        public static Mesh Mesh(FieldItem item)
        {
            var path=Path(item);if(path==null)return null;
            var source=Resources.Load<GameObject>(path);
            return source==null?null:source.GetComponentInChildren<MeshFilter>().sharedMesh;
        }
        public static Material Material(FieldItem item)=>Path(item)==null?null:Resources.Load<Material>(Path(item));
        public static Material[] Materials(FieldItem item)
        {
            if(IsPick(item))return _pickMaterials ?? (_pickMaterials=new[]{Material(item)});
            if(item==FieldItem.Lantern)return _lanternMaterials ?? (_lanternMaterials=new[]{Material(item),Resources.Load<Material>("FishingUserTools/LanternGlow")});
            if(item==FieldItem.GrilledFish)return _roastMaterials ?? (_roastMaterials=new[]{Material(item)});
            return null;
        }
        public static Texture2D Icon(FieldItem item)=>Path(item)==null?null:Resources.Load<Texture2D>(Path(item)+"Thumbnail");
    }
}
