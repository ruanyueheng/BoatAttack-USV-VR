using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>The mesh photographed for each icon is also the mesh held and placed in the world.</summary>
    public static class FishingV023Art
    {
        private static readonly Dictionary<FieldItem,Texture2D> Icons=new Dictionary<FieldItem,Texture2D>();
        public static Mesh ItemMesh(FieldItem item)
        {
            if(item==FieldItem.Axe || item==FieldItem.Shovel || item==FieldItem.Rake)
            {var tool=Resources.Load<GameObject>("FishingFeedbackTools/"+item);return tool==null?null:tool.GetComponentInChildren<MeshFilter>().sharedMesh;}
            if(FishingBackpack.IsQuestItem(item))return FishingVoyageArt.ItemMesh(item);
            if(item==FieldItem.HuntingRifle)
            {
                var rifle=Resources.Load<GameObject>("FishingHunting/HuntingRifle");
                return rifle==null?null:rifle.GetComponentInChildren<MeshFilter>().sharedMesh;
            }
            var supplied=FishingUserTools.Mesh(item);if(supplied!=null)return supplied;
            var source=(item==FieldItem.FieldGuide?Resources.Load<GameObject>("FishingV031/FieldGuide"):null) ?? Resources.Load<GameObject>("FishingV029/"+item) ?? Resources.Load<GameObject>("FishingV028/Coast/"+item) ?? Resources.Load<GameObject>("FishingV026/Items/"+item) ?? Resources.Load<GameObject>("FishingV023/Items/"+item);
            var filter=source==null?null:source.GetComponentInChildren<MeshFilter>();
            return filter==null?null:filter.sharedMesh;
        }
        public static Texture2D Icon(FieldItem item)
        {
            if(item==FieldItem.Axe || item==FieldItem.Shovel || item==FieldItem.Rake)return Resources.Load<Texture2D>("FishingFeedbackTools/"+item+"Thumbnail");
            var supplied=FishingUserTools.Icon(item);if(supplied!=null)return supplied;
            if(!Icons.TryGetValue(item,out var texture))
            {texture=(item==FieldItem.FieldGuide?Resources.Load<Texture2D>("FishingV031/FieldGuideThumbnail"):null) ?? Resources.Load<Texture2D>("FishingV029/"+item+"Thumbnail") ?? Resources.Load<Texture2D>("FishingV028/Coast/"+item+"Thumbnail") ?? Resources.Load<Texture2D>("FishingV026/Items/"+item+"Thumbnail") ?? Resources.Load<Texture2D>("FishingV023/Items/"+item+"Thumbnail");Icons.Add(item,texture);}
            return texture!=null?texture:FishingItemIcons.Get(item);
        }
    }
}
