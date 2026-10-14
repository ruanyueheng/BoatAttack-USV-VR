using System;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV026ArtValidation
    {
        private static void Check(bool valid,string label)
        {
            if(!valid)throw new InvalidOperationException(label);
            Debug.Log("[V026-ART] CHECK: "+label);
        }

        public static void CheckAssets(FishingPrototype c)
        {
            var items=new[]{FieldItem.Workbench,FieldItem.Furnace,FieldItem.Forge,FieldItem.Driftwood,FieldItem.Fiber,
                FieldItem.Stone,FieldItem.IronOre,FieldItem.Coal,FieldItem.Clay,FieldItem.Sand};
            foreach(var item in items)
            {
                var source=Resources.Load<GameObject>("FishingV026/Items/"+item);
                Check(source!=null,"V026 imported model exists / "+item);
                var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(mesh==FishingV023Art.ItemMesh(item),"Shared world/hand resolver uses V026 / "+item);
                Check(mesh.vertexCount>20 && mesh.colors32.Length==mesh.vertexCount && mesh.triangles.Length/3<=7500,
                    "Complete vertex palette within triangle budget / "+item);
                Check(mesh.bounds.min.y>=-.035f && mesh.bounds.min.y<=.035f,"Model sits on the ground origin / "+item);
                if(FishingRecipes.IsStation(item))
                    Check(mesh.bounds.size.x<=1.4f && mesh.bounds.size.z<=1.1f && mesh.bounds.max.y<=1.55f,
                        "Station preserves placement footprint / "+item);
                var icon=Resources.Load<Texture2D>("FishingV026/Items/"+item+"Thumbnail");
                Check(icon!=null && icon.width==256 && icon.height==256 && icon==FishingV023Art.Icon(item),
                    "Desktop and XR use matching model photograph / "+item);
            }
            foreach(var station in c.WorkshopWorld.State.Stations)
            {
                var root=c.WorkshopWorld.StationRoot(station.id);
                Check(root!=null && root.GetComponent<MeshRenderer>().sharedMaterial.shader==c.PaletteShader,
                    "Public and home stations retain runtime vertex shader / "+station.id);
            }
        }
    }
}
