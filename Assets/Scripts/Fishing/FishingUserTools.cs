using UnityEngine;
namespace BoatAttack.Fishing
{
    public static class FishingUserTools
    {
        public static bool IsPick(FieldItem item)=>item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;
        public static Mesh Mesh(FieldItem item)
        {
            if(!IsPick(item))return null;
            var source=Resources.Load<GameObject>("FishingUserTools/Pickaxe");
            return source==null?null:source.GetComponentInChildren<MeshFilter>().sharedMesh;
        }
        public static Material Material(FieldItem item)=>IsPick(item)?Resources.Load<Material>("FishingUserTools/Pickaxe"):null;
        public static Texture2D Icon(FieldItem item)=>IsPick(item)?Resources.Load<Texture2D>("FishingUserTools/PickaxeThumbnail"):null;
    }
}
