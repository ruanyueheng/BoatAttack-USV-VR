using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public static class FishingVoyageArt
    {
        private static readonly Dictionary<FieldItem,Mesh> Meshes = new Dictionary<FieldItem,Mesh>();
        public static Mesh ItemMesh(FieldItem item)
        {
            if (!FishingBackpack.IsQuestItem(item)) return null;
            if (Meshes.TryGetValue(item, out var mesh) && mesh != null) return mesh;
            var b = new FishingMeshBuilder(); var brass = new Color(.75f,.5f,.19f); var ivory = new Color(.94f,.89f,.68f);
            if (item == FieldItem.WindRelic)
            {
                b.Arc(Vector3.up*.06f,.14f,.018f,0,360,brass,24);
                for (int i = 0; i < 4; i++) { float a = i*Mathf.PI*.5f; b.Ellipsoid(new Vector3(Mathf.Cos(a)*.14f,.06f+Mathf.Sin(a)*.14f,0),Vector3.one*.029f,ivory,8,4); }
            }
            else if (item == FieldItem.ForestRelic)
            {
                b.Ellipsoid(Vector3.up*.05f,new Vector3(.11f,.15f,.055f),new Color(.78f,.46f,.12f),8,5);
                b.Tube(new Vector3(-.045f,-.02f,-.06f),new Vector3(.04f,.13f,-.06f),.009f,.004f,new Color(.26f,.39f,.13f),6);
            }
            else if (item == FieldItem.TideRelic)
            {
                b.Box(Vector3.up*.05f,new Vector3(.20f,.15f,.09f),new Color(.19f,.32f,.33f));
                b.Arc(new Vector3(0,.05f,-.06f),.065f,.012f,0,360,brass,16);
                b.Fin(new Vector3(-.04f,.01f,-.075f),new Vector3(0,.105f,-.075f),new Vector3(.04f,.01f,-.075f),ivory);
            }
            else if (item == FieldItem.StarRelic)
            {
                b.Ellipsoid(Vector3.up*.05f,new Vector3(.08f,.18f,.07f),new Color(.47f,.69f,.85f),5,2);
                b.Arc(Vector3.up*.03f,.10f,.012f,0,360,brass,16);
            }
            else
            {
                b.Ellipsoid(Vector3.up*.04f,new Vector3(.17f,.17f,.035f),brass,24,4);
                b.Ellipsoid(new Vector3(0,.04f,-.033f),new Vector3(.14f,.14f,.012f),ivory,24,4);
                b.Fin(new Vector3(-.035f,-.05f,-.055f),new Vector3(0,.16f,-.055f),new Vector3(.035f,-.05f,-.055f),new Color(.69f,.18f,.14f));
                b.Arc(new Vector3(0,.25f,0),.035f,.009f,0,360,brass,12);
            }
            mesh = b.Finish(FishingBackpack.Name(item)); mesh.hideFlags = HideFlags.HideAndDontSave; Meshes[item] = mesh; return mesh;
        }
    }
}
