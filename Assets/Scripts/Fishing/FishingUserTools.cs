using UnityEngine;
namespace BoatAttack.Fishing
{
    public static class FishingUserTools
    {
        private static Material[] _pickMaterials,_lanternMaterials;
        private static string Name(FieldItem item)=>IsPick(item)?"Pickaxe":item==FieldItem.Lantern?"Lantern":null;
        public static bool IsPick(FieldItem item)=>item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;
        public static Mesh Mesh(FieldItem item)
        {
            var name=Name(item);if(name==null)return null;
            var source=Resources.Load<GameObject>("FishingUserTools/"+name);
            return source==null?null:source.GetComponentInChildren<MeshFilter>().sharedMesh;
        }
        public static Material Material(FieldItem item)=>Name(item)==null?null:Resources.Load<Material>("FishingUserTools/"+Name(item));
        public static Material[] Materials(FieldItem item)
        {
            if(IsPick(item))return _pickMaterials ?? (_pickMaterials=new[]{Material(item)});
            if(item==FieldItem.Lantern)return _lanternMaterials ?? (_lanternMaterials=new[]{Material(item),Resources.Load<Material>("FishingUserTools/LanternGlow")});
            return null;
        }
        public static Texture2D Icon(FieldItem item)=>Name(item)==null?null:Resources.Load<Texture2D>("FishingUserTools/"+Name(item)+"Thumbnail");
    }
}
